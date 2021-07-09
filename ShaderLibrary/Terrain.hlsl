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
    float3 positionOS; // Object space position
    float3 positionWS; // World space position
    float3 positionVS; // View space position
    float4 positionCS; // Homogeneous clip space position
    float4 positionNDC;// Homogeneous normalized device coordinates
};

float4 _TerrainSize;
float4 _TerrainHeightMap_TexelSize;

float4x4 _PivotMatrixWS;

StructuredBuffer<uint> _VisiblePatchIndexBuffer;
StructuredBuffer<uint> _VisibleShadowIndexBuffer;

StructuredBuffer<TerrainPatch> _TerrainPatchBuffer;

TEXTURE2D(_TerrainLightMap); SAMPLER(sampler_TerrainLightMap);
TEXTURE2D(_TerrainHeightMap); SAMPLER(sampler_TerrainHeightMap);
TEXTURE2D(_TerrainNormalMap); SAMPLER(sampler_TerrainNormalMap);

void SetupTerrainInstancing()
{
}

#ifdef PROCEDURAL_INSTANCING_ON

TerrainPositionInputs GetTerrainPositionInputs(float3 positionOS, float4 color)
{
    TerrainPatch infoData = _TerrainPatchBuffer[_VisiblePatchIndexBuffer[unity_InstanceID]];

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
    float2 pixelCoord = position2D / _TerrainSize.xz * _TerrainHeightMap_TexelSize.zw;

    positionOS = position2D.xyy;
    positionOS.y = UnpackHeightmap(LOAD_TEXTURE2D(_TerrainHeightMap, pixelCoord)) * _TerrainSize.y * 2;

    UNITY_MATRIX_M = _PivotMatrixWS;

    TerrainPositionInputs input;
    input.texcoord = pixelCoord * _TerrainHeightMap_TexelSize.xy;

    input.positionOS = positionOS;
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
    TerrainPatch infoData = _TerrainPatchBuffer[_VisibleShadowIndexBuffer[unity_InstanceID]];

    float4 rect = infoData.rect;

    float2 position2D = rect.xy + rect.zw * positionOS.xz * 0.25;
    float2 pixelCoord = position2D / _TerrainSize.xz * _TerrainHeightMap_TexelSize.zw;

    positionOS = position2D.xyy;
    positionOS.y = UnpackHeightmap(LOAD_TEXTURE2D(_TerrainHeightMap, pixelCoord)) * _TerrainSize.y * 2;

    UNITY_MATRIX_M = _PivotMatrixWS;

    TerrainPositionInputs input;
    input.texcoord = pixelCoord * _TerrainHeightMap_TexelSize.xy;

    input.positionOS = positionOS;
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