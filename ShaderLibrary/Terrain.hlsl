#ifndef UNITY_TERRAIN_INCLUDED
#define UNITY_TERRAIN_INCLUDED

struct TerrainPatch
{
    float4 rect;
    int mipmap;
    int neighbor;
    int padding1;
    int padding2;
};

struct TerrainPositionInputs
{
	float2 texcoord;
    float3 positionWS; // World space position
    float3 positionVS; // View space position
    float4 positionCS; // Homogeneous clip space position
    float4 positionNDC;// Homogeneous normalized device coordinates
};

float4 _TerrainSize;
float4 _TerrainHeightMap_TexelSize;

float4x4 _PivotMatrixWS;

TEXTURE2D(_TerrainLightMap); SAMPLER(sampler_TerrainLightMap);
TEXTURE2D(_TerrainHeightMap); SAMPLER(sampler_TerrainHeightMap);
TEXTURE2D(_TerrainNormalMap); SAMPLER(sampler_TerrainNormalMap);

StructuredBuffer<TerrainPatch> _AllInstancesPatchBuffer;
StructuredBuffer<uint> _VisibleInstancesIndexBuffer;
StructuredBuffer<uint> _VisibleShadowIndexBuffer;

void SetupTerrainInstancing()
{	
}

#ifdef PROCEDURAL_INSTANCING_ON

TerrainPositionInputs GetTerrainPositionInputs(float3 positionOS, float4 color)
{
    TerrainPatch infoData = _AllInstancesPatchBuffer[_VisibleInstancesIndexBuffer[unity_InstanceID]];

    float4 rect = infoData.rect;
    float2 diff = 0;
    
    int neighbor = infoData.neighbor;
    if (neighbor & 1)
    {
        diff.x = -color.r;
    }
    if (neighbor & 2)
    {
        diff.x = -color.g;
    }
    if (neighbor & 4)
    {
        diff.y = -color.b;
    }
    if (neighbor & 8)
    {
        diff.y = -color.a;
    }

    float2 position2D = rect.xy + rect.zw * (positionOS.xz + diff) * 0.25;
    int2 pixelCoord = saturate(position2D / _TerrainSize.xz) * (_TerrainHeightMap_TexelSize.zw - 1);

    positionOS = position2D.xyy;
    positionOS.y = UnpackHeightmap(_TerrainHeightMap.Load(int3(pixelCoord, 0)).r) * _TerrainSize.y * 2;

    UNITY_MATRIX_M = _PivotMatrixWS;

    TerrainPositionInputs input;
    input.texcoord = pixelCoord * _TerrainHeightMap_TexelSize.xy;

    input.positionWS = TransformObjectToWorld(positionOS);
    input.positionVS = TransformWorldToView(input.positionWS);
    input.positionCS = TransformWorldToHClip(input.positionWS);

    float4 ndc = input.positionCS * 0.5f;
    input.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    input.positionNDC.zw = input.positionCS.zw;

    return input;
}

TerrainPositionInputs GetTerrainShadowPositionInputs(float3 positionOS)
{
    TerrainPatch infoData = _AllInstancesPatchBuffer[_VisibleShadowIndexBuffer[unity_InstanceID]];

    float4 rect = infoData.rect;

    float2 position2D = rect.xy + rect.zw * positionOS.xz * 0.25;
    int2 pixelCoord = saturate(position2D / _TerrainSize.xz) * (_TerrainHeightMap_TexelSize.zw - 1);

    positionOS = position2D.xyy;
    positionOS.y = UnpackHeightmap(_TerrainHeightMap.Load(int3(pixelCoord, 0)).r) * _TerrainSize.y * 2;

    UNITY_MATRIX_M = _PivotMatrixWS;

    TerrainPositionInputs input;
    input.texcoord = pixelCoord * _TerrainHeightMap_TexelSize.xy;

    input.positionWS = TransformObjectToWorld(positionOS);
    input.positionVS = TransformWorldToView(input.positionWS);
    input.positionCS = TransformWorldToHClip(input.positionWS);

    float4 ndc = input.positionCS * 0.5f;
    input.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    input.positionNDC.zw = input.positionCS.zw;

    return input;
}

#endif

#endif