struct AABB
{
	float4 Min;
	float4 Max;
};

struct Plane
{
	float3 N;
	float  d;
};

struct Sphere
{
	float3 center;
	float  radius;
};

struct Cone
{
	float3 center;
	float3 direction;
	float range;
	float angle;
};

float4x4 _TransformToViewMatrix;
float4x4 _InverseViewMatrix;
float4 _ClusterZBufferParams;

StructuredBuffer<AABB> _ClusterBoxBuffer;
StructuredBuffer<uint>	_ClusterUniqueBuffer;
StructuredBuffer<uint> _ClusterFlagBuffer;
ByteAddressBuffer _ClusterUniqueCounterBuffer;

RWStructuredBuffer<AABB> _RWClusterBoxBuffer;
RWStructuredBuffer<uint> _RWClusterFlagBuffer;
RWStructuredBuffer<uint> _RWClusterUniqueBuffer;
RWStructuredBuffer<uint> _RWClusterLightIndexCounterBuffer;
RWStructuredBuffer<uint2> _RWClusterLightGridBuffer;
RWStructuredBuffer<uint3> _RWClusterIndirectArgumentsBuffer;
RWStructuredBuffer<uint> _RWClusterLightIndexListBuffer;
RWByteAddressBuffer _RWClustersIndirectArgumentBuffer;

groupshared AABB gs_ClusterAABB;
groupshared uint gs_ClusterIndex1D;
groupshared uint gs_LightCount;
groupshared uint gs_LightStartOffset;
groupshared uint gs_LightList[MAX_WORKGROUP_SIZE_X];

bool ComputeSphereBoxIntersection(Sphere sphere, AABB aabb)
{
	float3 x = max(aabb.Min.xyz, min(sphere.center, aabb.Max.xyz));
	float3 v = x - sphere.center;
	return dot(v, v) <= sphere.radius * sphere.radius;
}

bool ComputeSphereConeIntersection(Sphere sphere, Cone cone)
{
	float3 v = sphere.center - cone.center;

	float lenSqr = dot(v, v);
	float angle  = dot(v, cone.direction);
	float distanceClosestPoint = cos(cone.angle) * sqrt(lenSqr - angle * angle) - angle * sin(cone.angle);

	bool angleCull = distanceClosestPoint < sphere.radius;
	bool frontCull = angle <  sphere.radius + cone.range;
	bool backCull  = angle > -sphere.radius;

	return angleCull & frontCull & backCull;
}

bool ComputeConeBoxIntersection(Cone cone, AABB aabb)
{
	float3 extents = (aabb.Max.xyz - aabb.Min.xyz) * 0.5h;
	float3 center = aabb.Min.xyz + extents;
	float radius = length(extents);

	Sphere sphere = { center, radius };

	return ComputeSphereConeIntersection(sphere, cone);
}

bool ComputeRayPlaneInterction(float3 a, float3 b, Plane p, out float3 q)
{
	float3 ab = b - a;
	float t = (p.d - dot(p.N, a)) / dot(p.N, ab);

	bool intersect = (t >= 0.0h && t <= 1.0h);

	q = float3(0, 0, 0);
	if (intersect)
	{
		q = a + t * ab;
	}

	return intersect;
}

uint ComputeClusterIndex1D(uint3 clusterIndex3D)
{
	return clusterIndex3D.x + (_ClusterDimensionParams.x * (clusterIndex3D.y + _ClusterDimensionParams.y * clusterIndex3D.z));
}

uint3 ComputeClusterIndex3D(uint clusterIndex1D)
{
	uint i = clusterIndex1D % uint(_ClusterDimensionParams.x);
	uint j = clusterIndex1D % uint(_ClusterDimensionParams.x * _ClusterDimensionParams.y) / uint(_ClusterDimensionParams.x);
	uint k = clusterIndex1D / uint(_ClusterDimensionParams.x * _ClusterDimensionParams.y);

	return uint3(i, j, k);
}

uint3 ComputeClusterIndex3D(float2 screenPos, float viewZ)
{
#if UNITY_UV_STARTS_AT_TOP
	screenPos.y = _ClusterScreenDimensionParams.y - screenPos.y;
#endif

	uint i = screenPos.x / _ClusterSizeParams.x;
	uint j = screenPos.y / _ClusterSizeParams.y;
	uint k = log(viewZ / _ClusterNear) * _ClusterLogGridDimY;

	return uint3(i, j, k);
}

float4 ScreenToView(float4 screen)
{
	float2 uv = screen.xy * _ClusterScreenDimensionParams.zw;
	float4 clipPosition = ComputeClipSpacePosition(uv, screen.z);
	float4 hpositionWS = mul(_TransformToViewMatrix, clipPosition);
	hpositionWS = hpositionWS / hpositionWS.w;
	return hpositionWS;
}

struct ComputeShaderInput
{
	uint3 GroupID           : SV_GroupID;
	uint3 GroupThreadID     : SV_GroupThreadID;
	uint3 DispatchThreadID  : SV_DispatchThreadID;
	uint  GroupIndex        : SV_GroupIndex;
};

[numthreads(MAX_WORKGROUP_SIZE_X, 1, 1)]
void ClearClusterBuffers(ComputeShaderInput input)
{
	_RWClusterFlagBuffer[input.DispatchThreadID.x] = 0;	
	_RWClusterUniqueBuffer[input.DispatchThreadID.x] = 0;
	_RWClusterLightGridBuffer[input.DispatchThreadID.x] = uint2(0, 0);
}

[numthreads(1, 1, 1)]
void ClearLightIndexCounter(ComputeShaderInput IN)
{
	_RWClusterLightIndexCounterBuffer[0] = 0;
}

[numthreads(MAX_WORKGROUP_SIZE_X, 1, 1)]
void ClearLightIndexList(ComputeShaderInput IN)
{
	_RWClusterLightIndexListBuffer[IN.DispatchThreadID.x] = 0;
}

[numthreads(MAX_WORKGROUP_SIZE_X, 1, 1)]
void SetupClusterAABBs(ComputeShaderInput cs_IDs)
{
	uint clusterIndex1D = cs_IDs.DispatchThreadID.x;
	uint3 clusterIndex3D = ComputeClusterIndex3D(clusterIndex1D);

	Plane nearPlane = { 0.0f, 0.0f, 1.0f, _ClusterNear * pow(abs(_ClusterNearK), clusterIndex3D.z) };
	Plane farPlane = { 0.0f, 0.0f, 1.0f, _ClusterNear * pow(abs(_ClusterNearK), clusterIndex3D.z + 1) };

	float4 planeMin = float4(clusterIndex3D.xy * _ClusterSizeParams.xy, 0.0f, 1.0f);
	float4 planeMax = float4((clusterIndex3D.xy + 1) * _ClusterSizeParams.xy, 0.0f, 1.0f);

#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
	planeMin.z = -(planeMin.z * 2 - 1);
	planeMax.z = -(planeMax.z * 2 - 1);
#endif

	planeMin = ScreenToView(planeMin);
	planeMax = ScreenToView(planeMax);

	planeMin.z = -planeMin.z;
	planeMax.z = -planeMax.z;

	float3 nearMin, nearMax, farMin, farMax;
	float3 eye = float3(0, 0, 0);
	ComputeRayPlaneInterction(eye, (float3)planeMin, nearPlane, nearMin);
	ComputeRayPlaneInterction(eye, (float3)planeMax, nearPlane, nearMax);
	ComputeRayPlaneInterction(eye, (float3)planeMin, farPlane, farMin);
	ComputeRayPlaneInterction(eye, (float3)planeMax, farPlane, farMax);

	float3 aabbMin = min(nearMin, min(nearMax, min(farMin, farMax)));
	float3 aabbMax = max(nearMin, max(nearMax, max(farMin, farMax)));

	AABB aabb = { float4(aabbMin, 1.0f), float4(aabbMax, 1.0f) };
	
	_RWClusterBoxBuffer[clusterIndex1D] = aabb;
}

[numthreads(MAX_WORKGROUP_UV_X, MAX_WORKGROUP_UV_Y, 1)]
void SetupClusterFlags(uint3 id : SV_DispatchThreadID)
{
	uint2 texCoord = id.xy;

	float depth = LoadDepthAttachment(texCoord).x;
	uint3 clusterIndex3D = ComputeClusterIndex3D(texCoord, LinearEyeDepth(depth, _ClusterZBufferParams));
	uint clusterIndex1D = ComputeClusterIndex1D(clusterIndex3D);

	_RWClusterFlagBuffer[clusterIndex1D] = 1;
}

[numthreads(MAX_WORKGROUP_SIZE_X, 1, 1)]
void ComputeClusterCount(uint3 id : SV_DispatchThreadID)
{
	uint clusterID = id.x;

	if (_ClusterFlagBuffer[clusterID] > 0)
	{
		uint i = _RWClusterUniqueBuffer.IncrementCounter();
		_RWClusterUniqueBuffer[i] = clusterID;
	}
}

[numthreads(1,1,1)]
void UpdateIndirectArgumentBuffers(uint3 id : SV_DispatchThreadID)
{
	uint clusterCount = _ClusterUniqueCounterBuffer.Load(0);
	_RWClustersIndirectArgumentBuffer.Store3(0, uint3(clusterCount, 1, 1));
}

void AssignLightsToClusterBuffer(uint clusterID)
{
	uint lightCount = 0;
	uint lightStartOffset = 0;

	InterlockedAdd(_RWClusterLightIndexCounterBuffer[0], _ClusterLightParams.y, lightStartOffset);

	AABB aabb = _ClusterBoxBuffer[clusterID];

	for (uint i = 0, count = 0; i < uint(_ClusterLightParams.x) && lightCount < uint(_ClusterLightParams.y); i++)
	{
		float4 lightPositionWS = _ClusterLightBuffer[i].position;
		if (lightPositionWS.w > 0)
		{
			float4 color = _ClusterLightBuffer[i].color;
			float4 spotDirection = _ClusterLightBuffer[i].spotDirection;
			float3 lightPosView = mul(_InverseViewMatrix, float4(lightPositionWS.xyz, 1)).xyz;

			if (spotDirection.w > 0)
			{
				Cone cone = (Cone)0;
				cone.center = lightPosView;
				cone.direction = mul((float3x3)_InverseViewMatrix, -spotDirection.xyz).xyz;
				cone.angle = spotDirection.w;
				cone.range = color.w;

				if (ComputeConeBoxIntersection(cone, aabb))
				{
					_RWClusterLightIndexListBuffer[lightStartOffset + lightCount] = i;
					lightCount++;
				}
			}
			else
			{
				Sphere sphere = { lightPosView, color.w };

				if (ComputeSphereBoxIntersection(sphere, aabb))
				{
					_RWClusterLightIndexListBuffer[lightStartOffset + lightCount] = i;
					lightCount++;
				}
			}
		}
		else
		{
			_RWClusterLightIndexListBuffer[lightStartOffset + lightCount] = i;
			lightCount++;
		}
	}

	_RWClusterLightGridBuffer[clusterID] = uint2(lightStartOffset, lightCount);
}

[numthreads(MAX_WORKGROUP_SIZE_X, 1, 1)]
void AssignLightsToClusters(uint3 id : SV_DispatchThreadID)
{	
	AssignLightsToClusterBuffer(id.x);
}

[numthreads(MAX_WORKGROUP_SIZE_X, 1, 1)]
void AssignLightsToClustersClamped(uint3 id : SV_DispatchThreadID)
{	
	uint clusterCount = _ClusterUniqueCounterBuffer.Load(0);
	if (id.x < clusterCount)
	{
		uint clusterID = _ClusterUniqueBuffer[id.x];
		AssignLightsToClusterBuffer(clusterID);
	}
}

[numthreads(MAX_WORKGROUP_SIZE_X, 1, 1)]
void AssignLightsToClustersIndirect(uint3 id : SV_DispatchThreadID)
{	
	uint clusterID = _ClusterUniqueBuffer[id.x];
	AssignLightsToClusterBuffer(clusterID);
}

[numthreads(MAX_WORKGROUP_SIZE_X, 1, 1)]
void AssignLightsToClustersIndirectNoLimit(ComputeShaderInput IN)
{	
	uint i, index;

	if (IN.GroupIndex == 0)
	{
		gs_LightCount = 0;
		gs_ClusterIndex1D = _ClusterUniqueBuffer[IN.GroupID.x];
		gs_ClusterAABB = _ClusterBoxBuffer[gs_ClusterIndex1D];
	}

	GroupMemoryBarrierWithGroupSync();

	for (i = IN.GroupIndex; i < uint(_ClusterLightParams.x); i += MAX_WORKGROUP_SIZE_X)
	{
		float4 lightPositionWS = _ClusterLightBuffer[i].position;
		if (lightPositionWS.w > 0)
		{
			float4 color = _ClusterLightBuffer[i].color;
			float4 spotDirection = _ClusterLightBuffer[i].spotDirection;
			float3 lightPosView = mul(_InverseViewMatrix, float4(lightPositionWS.xyz, 1)).xyz;

			if (spotDirection.w > 0)
			{
				Cone cone = (Cone)0;
				cone.center = lightPosView;
				cone.direction = mul((float3x3)_InverseViewMatrix, -spotDirection.xyz).xyz;
				cone.angle = spotDirection.w;
				cone.range = color.w;

				if (ComputeConeBoxIntersection(cone, gs_ClusterAABB))
				{
					InterlockedAdd(gs_LightCount, 1, index);
					if (index < MAX_WORKGROUP_SIZE_X)
					{
						gs_LightList[index] = i;
					}
				}
			}
			else
			{
				Sphere sphere = { lightPosView, color.w };

				if (ComputeSphereBoxIntersection(sphere, gs_ClusterAABB))
				{
					InterlockedAdd(gs_LightCount, 1, index);
					if (index < MAX_WORKGROUP_SIZE_X)
					{
						gs_LightList[index] = i;
					}
				}
			}
		}
		else
		{
			InterlockedAdd(gs_LightCount, 1, index);
			if (index < MAX_WORKGROUP_SIZE_X)
			{
				gs_LightList[index] = i;
			}
		}
	}

	GroupMemoryBarrierWithGroupSync();

	if (IN.GroupIndex == 0)
	{
		InterlockedAdd(_RWClusterLightIndexCounterBuffer[0], gs_LightCount, gs_LightStartOffset);
		_RWClusterLightGridBuffer[gs_ClusterIndex1D] = uint2(gs_LightStartOffset, gs_LightCount);
	}

	GroupMemoryBarrierWithGroupSync();

	for (i = IN.GroupIndex; i < gs_LightCount; i += MAX_WORKGROUP_SIZE_X)
	{
		_RWClusterLightIndexListBuffer[gs_LightStartOffset + i] = gs_LightList[i];
	}
}