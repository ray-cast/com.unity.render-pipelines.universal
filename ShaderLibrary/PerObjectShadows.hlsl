#ifndef UNIVERSAL_PER_OBJECT_SHADOWS_INCLUDED
#define UNIVERSAL_PER_OBJECT_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

#if defined(SHADER_API_GLES)
    #define MAX_VISIBLE_PER_OBJECT_SHADOWS 4
#else
    #define MAX_VISIBLE_PER_OBJECT_SHADOWS 8
#endif

TEXTURE2D(_PerObjectShadowTexture);
SAMPLER(sampler_PerObjectShadowTexture);

#ifndef SHADER_API_GLES3
CBUFFER_START(PerObjectShadow)
#endif
float4x4    _PerObjectWorldToShadow[MAX_VISIBLE_PER_OBJECT_SHADOWS];
float4      _PerObjectShadowsClip[MAX_VISIBLE_PER_OBJECT_SHADOWS];
float4      _PerObjectShadowsCount;
half4       _PerObjectShadowOffset0;
half4       _PerObjectShadowOffset1;
half4       _PerObjectShadowOffset2;
half4       _PerObjectShadowOffset3;
float4      _PerObjectShadowmapSize; // (xy: 1/width and 1/height, zw: width and height)
#ifndef SHADER_API_GLES3
CBUFFER_END
#endif

ShadowSamplingData GetPerObjectShadowSamplingData()
{
    ShadowSamplingData shadowSamplingData;
    shadowSamplingData.shadowOffset0 = _PerObjectShadowOffset0;
    shadowSamplingData.shadowOffset1 = _PerObjectShadowOffset1;
    shadowSamplingData.shadowOffset2 = _PerObjectShadowOffset2;
    shadowSamplingData.shadowOffset3 = _PerObjectShadowOffset3;
    shadowSamplingData.shadowmapSize = _PerObjectShadowmapSize;
    return shadowSamplingData;
}

int GetPerObjectShadowsCount()
{
    return _PerObjectShadowsCount.x;
}

static const float2 PoissonDisk2[16] = 
{
    float2(-0.7071,  0.7071),
    float2(-0.0000, -0.8750),
    float2( 0.5303,  0.5303),
    float2(-0.6250, -0.0000),
    float2( 0.3536, -0.3536),
    float2(-0.0000,  0.3750),
    float2(-0.1768, -0.1768),
    float2( 0.1250,  0.0000),
    float2(-0.7070, -0.7071),
    float2( 0.8750, -0.0000),
    float2(-0.5303,  0.5303),
    float2(-0.0000, -0.6250),
    float2(-0.3536,  0.3536),
    float2( 0.3750, -0.0000),
    float2(-0.1768, -0.1768),
    float2( 0.0000,  0.1250),
};

#define PER_OBJECT_SHADOW_SAMPLER_COUNT 8

real SamplePerObjectShadowMapFiltered(TEXTURE2D_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, ShadowSamplingData samplingData, half random)
{
    real2 sampleRotate[PER_OBJECT_SHADOW_SAMPLER_COUNT];
    real2 sampleRadius = 1.0;

    real2 sampleSinCos = 0;
    sincos(random * PI * 2, sampleSinCos.y, sampleSinCos.x);

    real2x2 sampleRotMat = { sampleSinCos.y, sampleSinCos.x, -sampleSinCos.x, sampleSinCos.y };

    UNITY_UNROLL
    for (int i = 0; i < PER_OBJECT_SHADOW_SAMPLER_COUNT; i+=4)
    {
        sampleRotate[i + 0] = mul(PoissonDisk2[i + 0], sampleRotMat) * sampleRadius;
        sampleRotate[i + 1] = mul(PoissonDisk2[i + 1], sampleRotMat) * sampleRadius;
        sampleRotate[i + 2] = mul(PoissonDisk2[i + 2], sampleRotMat) * sampleRadius;
        sampleRotate[i + 3] = mul(PoissonDisk2[i + 3], sampleRotMat) * sampleRadius;
    }

    real attenuation = 0;

    for (int i = 0; i < PER_OBJECT_SHADOW_SAMPLER_COUNT; i++)
    {
        float2 shadow = SAMPLE_TEXTURE2D_X(ShadowMap, sampler_ShadowMap, shadowCoord.xy + sampleRotate[i] * samplingData.shadowmapSize).x;
#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
        attenuation += shadow > shadowCoord.z;
#else
        attenuation += shadow < shadowCoord.z;
        //attenuation += exp(-max(1e-5, shadow - shadowCoord.z) * 50);
#endif
    }

    return attenuation / PER_OBJECT_SHADOW_SAMPLER_COUNT;
}

real SamplePerObjectShadowMap(TEXTURE2D_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, ShadowSamplingData samplingData, half4 shadowParams, half random)
{
    real attenuation;
    attenuation = SamplePerObjectShadowMapFiltered(TEXTURE2D_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingData, random);
    attenuation = LerpWhiteTo(attenuation, shadowParams.x);

    return attenuation;
}

half PerObjectRealtimeShadow(int lightIndex, float3 positionWS, half random)
{
    ShadowSamplingData shadowSamplingData = GetPerObjectShadowSamplingData();
    float4 shadowCoord = mul(_PerObjectWorldToShadow[lightIndex], float4(positionWS, 1.0));
    float4 shadowClip = _PerObjectShadowsClip[lightIndex];

    shadowCoord.xyz /= shadowCoord.w;

    UNITY_BRANCH
    if (shadowCoord.x < shadowClip.x || shadowCoord.y < shadowClip.y || shadowCoord.x > shadowClip.z || shadowCoord.y > shadowClip.w)
        return 1;

    half4 shadowParams = GetMainLightShadowParams();
    return SamplePerObjectShadowMap(TEXTURE2D_ARGS(_PerObjectShadowTexture, sampler_PerObjectShadowTexture), shadowCoord, shadowSamplingData, shadowParams, random);
}

#endif