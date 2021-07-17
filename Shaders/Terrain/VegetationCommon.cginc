//#ifndef VEGETATION_COMMON_INCLUDED
//#define VEGETATION_COMMON_INCLUDED

// Terrain engine shader helpers

#define BEND_COUNT 20
uniform float _BendCount;
uniform float4 _BendData[BEND_COUNT];

//传入的各分量是角度值
inline float4x4 RotationByDegree(float3 rotation)
{
	float radX = radians(rotation.x);
	float radY = radians(rotation.y);
	float radZ = radians(rotation.z);

	float sinX = sin(radX);
	float cosX = cos(radX);
	float sinY = sin(radY);
	float cosY = cos(radY);
	float sinZ = sin(radZ);
	float cosZ = cos(radZ);

	return float4x4(
		cosY * cosZ, -cosY * sinZ, sinY, 0.0,
		cosX * sinZ + sinX * sinY * cosZ, cosX * cosZ - sinX * sinY * sinZ, -sinX * cosY, 0.0,
		sinX * sinZ - cosX * sinY * cosZ, sinX * cosZ + cosX * sinY * sinZ, cosX * cosY, 0.0,
		0.0, 0.0, 0.0, 1.0
		);
}
inline float4x4 RotationByRadian(float3 rotation)
{
	float radX = rotation.x;
	float radY = rotation.y;
	float radZ = rotation.z;

	float sinX = sin(radX);
	float cosX = cos(radX);
	float sinY = sin(radY);
	float cosY = cos(radY);
	float sinZ = sin(radZ);
	float cosZ = cos(radZ);

	return float4x4(
		cosY * cosZ, -cosY * sinZ, sinY, 0.0,
		cosX * sinZ + sinX * sinY * cosZ, cosX * cosZ - sinX * sinY * sinZ, -sinX * cosY, 0.0,
		sinX * sinZ - cosX * sinY * cosZ, sinX * cosZ + cosX * sinY * sinZ, cosX * cosY, 0.0,
		0.0, 0.0, 0.0, 1.0
		);
}
//传入的是弧度值
inline float3x3 RotationY(float rotateYRadian)
{
	return float3x3(
		cos(rotateYRadian), 0.0, sin(rotateYRadian),
		0.0, 1.0, 0.0,
		-sin(rotateYRadian), 0.0, cos(rotateYRadian)
		);
}
inline float3 RotationByVector(float3 position, float3 vec)
{
	float3 vecInYZ = float3(0, vec.y, vec.z);
	float3 vecInXY = float3(vec.x, vec.y, 0);
	float radX = sign(vecInYZ.z) * acos(dot(float3(0, 1, 0), normalize(vecInYZ)));//[0,pi]
	float radZ = sign(-vecInXY.x) * acos(dot(float3(0, 1, 0), normalize(vecInXY)));//[0,pi]
	return mul(RotationByRadian(float3(radX, 0, radZ)), position);
}
inline float3 ApplyBending(float3 positionOS, float3 perGrassPivotPosWS, float bendStrength)
{
	float3 oldPos = positionOS;
	//return mul(RotationY(radZ), positionOS);
	//return mul(RotationByRadian(float3(-PI  / 2, 0, -PI / 2)), positionOS);
	//return float3(0.1, 0, 0.1) * _BendData[0].w * saturate(positionOS.y);
	// Bending
	float3 vertexWorldPos = positionOS + perGrassPivotPosWS;
	for (int i = 0; i < uint(_BendCount); i++)
	{
		float bendRadius = _BendData[i].w;
		float3 benderWorldPos = _BendData[i].xyz;
		//float bendRadius = _InteractionTouchBendedInstances.w;
		//float3 benderWorldPos = _InteractionTouchBendedInstances.xyz;
		float distToBender = distance(perGrassPivotPosWS, benderWorldPos);
		if (bendRadius > 0 && distToBender < bendRadius)
		{
			/*float distToBender = distance(float3(vertexWorldPos.x, 0, vertexWorldPos.z), float3(benderWorldPos.x, 0, benderWorldPos.z));
			float bendPower = (bendRadius - min(bendRadius, distToBender)) / (bendRadius + 0.001);
			float3 bendDir = normalize(vertexWorldPos - benderWorldPos);
			float2 vertexOffset = bendDir.xz * bendPower * bendStrength;
			offset.xz += lerp(float2(0, 0), vertexOffset, saturate(bendRadius * positionOS.y));*/

			/*float distToBender = distance(float3(vertexWorldPos.x, 0, vertexWorldPos.z), float3(benderWorldPos.x, 0, benderWorldPos.z));
			float bendPower = 1 - saturate(distToBender / bendRadius);
			bendPower *= saturate(positionOS.y);
			float3 bendDir = vertexWorldPos - benderWorldPos;
			offset += bendDir * bendPower * bendStrength;*/

			//float distToBender = distance(float3(vertexWorldPos.x, 0, vertexWorldPos.z), float3(benderWorldPos.x, 0, benderWorldPos.z));
			//float bendPower = (bendRadius - min(bendRadius, distToBender)) / (bendRadius + 0.001);
			//float3 bendDir = normalize(vertexWorldPos - benderWorldPos);
			//float2 vertexOffset = bendDir.xz * bendPower ;
			//offset.xz += lerp(float2(0, 0), vertexOffset, saturate(bendRadius ));
			//offset.xz += vertexOffset;
			
			
			float disRate = 1;
			float edgeDis = bendRadius -distToBender;
			float bendEdge = 0.3;
			if (edgeDis<bendEdge)
			{
				disRate = edgeDis/bendEdge;
			}

			

			bendStrength = bendStrength*disRate;

			float3 bendDir = perGrassPivotPosWS - benderWorldPos;
			bendDir.y =0;
			bendDir = normalize(bendDir)*1.4f;
			float radX = bendDir.z * PI / 2 * saturate(bendStrength);
			float radZ = -bendDir.x * PI / 2 * saturate(bendStrength);
			positionOS = mul(RotationByRadian(float3(radX, 0, radZ)), positionOS);
			//break;

			/*float WMDistance = 1 - clamp(distance(vertexWorldPos.xyz, benderWorldPos.xyz) / bendRadius, 0, 1);
			float3 posDifferences = normalize(vertexWorldPos.xyz - benderWorldPos.xyz);
			float3 strengthedDifferences = posDifferences * (bendStrength + bendStrength);
			float3 resultXZ = WMDistance * strengthedDifferences;
			vertexWorldPos.xz += resultXZ.xz;
			vertexWorldPos.y -= WMDistance * bendStrength;*/
		}
	}
	return positionOS;
}
inline float3 ApplyRotationAndScale(float3 positionOS, float3 perGrassPivotPosWS, float xScale, float yScale, float zScale, float minScale = 0.5, float maxScale = 1.0f)
{
	//float3 cameraTransformRightWS = UNITY_MATRIX_V[0].xyz;//UNITY_MATRIX_V[0].xyz == world space camera Right unit vector
	//float3 cameraTransformUpWS = UNITY_MATRIX_V[1].xyz;//UNITY_MATRIX_V[1].xyz == world space camera Up unit vector
	//float3 cameraTransformForwardWS = -UNITY_MATRIX_V[2].xyz;//UNITY_MATRIX_V[2].xyz == -1 * world space camera Forward unit vector
	//草随机旋转
	float radian = (sin(perGrassPivotPosWS.x*95.4643 + perGrassPivotPosWS.z) + 1) * PI;
	positionOS = mul(RotationY(radian), positionOS);
	//草缩放，Y根据位置随机缩放，xz直接缩放指定值
	float perGrassHeight = lerp(minScale, maxScale, (sin(perGrassPivotPosWS.x*23.4643 + perGrassPivotPosWS.z) * 0.45 + 0.55)) * yScale;
	positionOS *= float3(xScale, perGrassHeight, zScale);
	//move grass posOS -> posWS
	return positionOS;
}
//void FastSinCos(float4 val, out float4 s, out float4 c)
//{
//	val = val * 6.408849 - 3.1415927;
//	float4 r5 = val * val;
//	float4 r6 = r5 * r5;
//	float4 r7 = r6 * r5;
//	float4 r8 = r6 * r5;
//	float4 r1 = r5 * val;
//	float4 r2 = r1 * r5;
//	float4 r3 = r2 * r5;
//	float4 sin7 = { 1, -0.16161616, 0.0083333, -0.00019841 };
//	float4 cos8 = { -0.5, 0.041666666, -0.0013888889, 0.000024801587 };
//	s = val + r1 * sin7.y + r2 * sin7.z + r3 * sin7.w;
//	c = 1 + r5 * cos8.x + r6 * cos8.y + r7 * cos8.z + r8 * cos8.w;
//}
//float3 ApplyFastWind(float3 vertex, float texCoordY)
//{
//	float _WindSpeed = 1;
//	float _WindBending = 1;
//	if (_WindSpeed == 0) return vertex;
//
//	float speed = _WindSpeed;
//
//	float4 _waveXmove = float4 (0.024, 0.04, -0.12, 0.096);
//	float4 _waveZmove = float4 (0.006, .02, -0.02, 0.1);
//
//	const float4 waveSpeed = float4 (1.2, 2, 1.6, 4.8);
//
//	float4 waves;
//	waves = vertex.x * _WindBending;
//	waves += vertex.z * _WindBending;
//
//	waves += _Time.x * (1 - 0.4) * waveSpeed * speed;
//
//	float4 s, c;
//	waves = frac(waves);
//	FastSinCos(waves, s, c);
//
//	float waveAmount = texCoordY * (1 + 0.4);
//	s *= waveAmount;
//
//	s *= normalize(waveSpeed);
//
//	s = s * s;
//	float fade = dot(s, 1.3);
//	s = s * s;
//
//	float3 waveMove = float3 (0, 0, 0);
//	waveMove.x = dot(s, _waveXmove);
//	waveMove.z = dot(s, _waveZmove);
//
//	vertex.xz -= waveMove.xz;
//
//	//vertex -= mul(_World2Object, float3(_WindSpeed, 0, _WindSpeed)).x * _WindBending * _SinTime;
//
//	return vertex;
//}
inline float3 ApplyWind(float3 vertex, float3 root, float3 direction, float frequency, float2 tiling, float2 wrap, float intensity)
{
	float wind = 0;
	wind += sin(_Time.y * frequency + dot(1, root.xz * tiling.xy)) * wrap.x + wrap.y;
	wind *= vertex.y;
	wind *= intensity;

	return vertex + direction * wind;
}

half3 ApplyGrassSpecular(half3 lightDir, half3 normal, half3 viewDir, half3 specular, half smoothness)
{
    float3 halfVec = SafeNormalize(float3(lightDir) + float3(viewDir));
    half NdotH = saturate(dot(normal, halfVec));
    half modifier = pow(NdotH, smoothness);
    half3 specularReflection = specular * modifier;
    return specularReflection;
}

half3 ApplySingleDirectLight(Light light, half3 N, half3 V, half3 albedo, half3 specular, half positionOSY)
{
	half3 enery = albedo * (dot(N, light.direction) * 0.5 + 0.5);
	enery += ApplyGrassSpecular(light.direction, float3(0, 1, 0), V, specular, 20.0) * positionOSY;
	enery *= light.color * light.shadowAttenuation * light.distanceAttenuation;

	return enery;
}

inline float3 CalculateTouchBending(float3 vertex, float4 interactionTouchBendedInstances, float touchBendingStrength)
{
	float3 sphereCenter = interactionTouchBendedInstances;
	float radius = interactionTouchBendedInstances.z;
	if (distance(vertex.xyz, sphereCenter.xyz) < radius)
	{
		float3 posDifferences = normalize(vertex.xyz - sphereCenter.xyz);
		float3 strengthedDifferences = posDifferences * (touchBendingStrength + touchBendingStrength);
		return vertex.xyz += strengthedDifferences;

		float WMDistance = 1 - clamp(distance(vertex.xyz, sphereCenter.xyz) / radius, 0, 1);
		float3 resultXZ = WMDistance * strengthedDifferences;
		vertex.xz += resultXZ.xz;
		vertex.y -= WMDistance * touchBendingStrength;
		return vertex;
	}

	return vertex;
}