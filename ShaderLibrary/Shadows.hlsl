#ifndef UNIVERSAL_SHADOWS_INCLUDED
#define UNIVERSAL_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShadowSamplingTent.hlsl"

#define SHADOWS_SCREEN 0
#define SHADOWS_SAMPLE_COUNT 8
#define MAX_SHADOW_CASCADES 4

static const float2 PoissonDisk[16] = 
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

#if !defined(_RECEIVE_SHADOWS_OFF)
    #if defined(_MAIN_LIGHT_SHADOWS)
        #define MAIN_LIGHT_CALCULATE_SHADOWS

        #if !defined(_MAIN_LIGHT_SHADOWS_CASCADE)
            #define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
        #endif
    #endif

    #if defined(_ADDITIONAL_LIGHT_SHADOWS)
        #define ADDITIONAL_LIGHT_CALCULATE_SHADOWS
    #endif
#endif

#if defined(_ADDITIONAL_LIGHTS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
    #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
#endif

TEXTURE2D(_MainLightShadowmapTexture);
SAMPLER(sampler_MainLightShadowmapTexture);

TEXTURE2D(_AdditionalLightsShadowmapTexture);
SAMPLER(sampler_AdditionalLightsShadowmapTexture);

SCREENSPACE_TEXTURE(_ScreenSpaceShadowmapTexture);
SAMPLER(sampler_ScreenSpaceShadowmapTexture);

SCREENSPACE_TEXTURE(_ScreenSpaceOcclusionTexture);
SAMPLER(sampler_ScreenSpaceOcclusionTexture);

SCREENSPACE_TEXTURE(_CapsuleShadowTexture);
SAMPLER(sampler_CapsuleShadowTexture);

// GLES3 causes a performance regression in some devices when using CBUFFER.
#ifndef SHADER_API_GLES3
CBUFFER_START(MainLightShadows)
#endif
// Last cascade is initialized with a no-op matrix. It always transforms
// shadow coord to half3(0, 0, NEAR_PLANE). We use this trick to avoid
// branching since ComputeCascadeIndex can return cascade index = MAX_SHADOW_CASCADES
float4x4    _MainLightWorldToShadow[MAX_SHADOW_CASCADES + 1];
float4      _CascadeShadowSplitSpheres0;
float4      _CascadeShadowSplitSpheres1;
float4      _CascadeShadowSplitSpheres2;
float4      _CascadeShadowSplitSpheres3;
float4      _CascadeShadowSplitSphereRadii;
half4       _MainLightShadowOffset0;
half4       _MainLightShadowOffset1;
half4       _MainLightShadowOffset2;
half4       _MainLightShadowOffset3;
half4       _MainLightShadowParams;  // (x: shadowStrength, y: 1.0 if soft shadows, 0.0 otherwise)
half4       _MainLightShadowCascadesSplit;
float4      _MainLightShadowmapSize; // (xy: 1/width and 1/height, zw: width and height)
#ifndef SHADER_API_GLES3
CBUFFER_END
#endif

#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
StructuredBuffer<ShadowData> _AdditionalShadowsBuffer;
StructuredBuffer<int> _AdditionalShadowsIndices;
half4       _AdditionalShadowOffset0;
half4       _AdditionalShadowOffset1;
half4       _AdditionalShadowOffset2;
half4       _AdditionalShadowOffset3;
float4      _AdditionalShadowmapSize; // (xy: 1/width and 1/height, zw: width and height)
#else
// GLES3 causes a performance regression in some devices when using CBUFFER.
#ifndef SHADER_API_GLES3
CBUFFER_START(AdditionalLightShadows)
#endif
float4x4    _AdditionalLightsWorldToShadow[MAX_VISIBLE_LIGHTS];
half4       _AdditionalShadowParams[MAX_VISIBLE_LIGHTS];
half4       _AdditionalShadowOffset0;
half4       _AdditionalShadowOffset1;
half4       _AdditionalShadowOffset2;
half4       _AdditionalShadowOffset3;
float4      _AdditionalShadowmapSize; // (xy: 1/width and 1/height, zw: width and height)
#ifndef SHADER_API_GLES3
CBUFFER_END
#endif
#endif

float4 _ShadowBias; // x: depth bias, y: normal bias

#define BEYOND_SHADOW_FAR(shadowCoord) shadowCoord.z <= 0.0 || shadowCoord.z >= 1.0

struct ShadowSamplingData
{
    half4 shadowOffset0;
    half4 shadowOffset1;
    half4 shadowOffset2;
    half4 shadowOffset3;
    float4 shadowmapSize;
};

ShadowSamplingData GetMainLightShadowSamplingData()
{
    ShadowSamplingData shadowSamplingData;
    shadowSamplingData.shadowOffset0 = _MainLightShadowOffset0;
    shadowSamplingData.shadowOffset1 = _MainLightShadowOffset1;
    shadowSamplingData.shadowOffset2 = _MainLightShadowOffset2;
    shadowSamplingData.shadowOffset3 = _MainLightShadowOffset3;
    shadowSamplingData.shadowmapSize = _MainLightShadowmapSize;
    return shadowSamplingData;
}

ShadowSamplingData GetAdditionalLightShadowSamplingData()
{
    ShadowSamplingData shadowSamplingData;
    shadowSamplingData.shadowOffset0 = _AdditionalShadowOffset0;
    shadowSamplingData.shadowOffset1 = _AdditionalShadowOffset1;
    shadowSamplingData.shadowOffset2 = _AdditionalShadowOffset2;
    shadowSamplingData.shadowOffset3 = _AdditionalShadowOffset3;
    shadowSamplingData.shadowmapSize = _AdditionalShadowmapSize;
    return shadowSamplingData;
}

// ShadowParams
// x: ShadowStrength
// y: 1.0 if shadow is soft, 0.0 otherwise
half4 GetMainLightShadowParams()
{
    return _MainLightShadowParams;
}


// ShadowParams
// x: ShadowStrength
// y: 1.0 if shadow is soft, 0.0 otherwise
half4 GetAdditionalLightShadowParams(int lightIndex)
{
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    return _AdditionalShadowsBuffer[lightIndex].shadowParams;
#else
    return _AdditionalShadowParams[lightIndex];
#endif
}

half SampleScreenSpaceShadowMap(float2 shadowCoord)
{
    // The stereo transform has to happen after the manual perspective divide
    shadowCoord.xy = UnityStereoTransformScreenSpaceTex(shadowCoord.xy);

#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    half attenuation = SAMPLE_TEXTURE2D_ARRAY(_ScreenSpaceShadowmapTexture, sampler_ScreenSpaceShadowmapTexture, shadowCoord.xy, unity_StereoEyeIndex).x;
#else
    half attenuation = SAMPLE_TEXTURE2D(_ScreenSpaceShadowmapTexture, sampler_ScreenSpaceShadowmapTexture, shadowCoord.xy).x;
#endif

    return attenuation;
}

half SampleScreenSpaceOcclusionMap(float2 shadowCoord)
{
    // The stereo transform has to happen after the manual perspective divide
    shadowCoord.xy = UnityStereoTransformScreenSpaceTex(shadowCoord.xy);

#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    half attenuation = SAMPLE_TEXTURE2D_ARRAY(_ScreenSpaceOcclusionTexture, sampler_ScreenSpaceOcclusionTexture, shadowCoord.xy, unity_StereoEyeIndex).x;
#else
    half attenuation = SAMPLE_TEXTURE2D(_ScreenSpaceOcclusionTexture, sampler_ScreenSpaceOcclusionTexture, shadowCoord.xy).x;
#endif

    return attenuation;
}

half SampleCapsuleShadowMap(float2 shadowCoord)
{
    // The stereo transform has to happen after the manual perspective divide
    shadowCoord.xy = UnityStereoTransformScreenSpaceTex(shadowCoord.xy);

#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    half attenuation = SAMPLE_TEXTURE2D_ARRAY(_CapsuleShadowTexture, sampler_CapsuleShadowTexture, shadowCoord.xy, unity_StereoEyeIndex).x;
#else
    half attenuation = SAMPLE_TEXTURE2D(_CapsuleShadowTexture, sampler_CapsuleShadowTexture, shadowCoord.xy).x;
#endif

    return attenuation;
}

real SampleShadowmapFiltered(TEXTURE2D_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, ShadowSamplingData samplingData, half random)
{
    real blurSize = 1.0;

    UNITY_BRANCH
    if (shadowCoord.w > 0)
    {
        real size = 1 - pow(_MainLightShadowCascadesSplit[uint(shadowCoord.w) - 1], 0.5);
        blurSize *= rcp(1 + shadowCoord.w);
        blurSize *= size;
    }

    real2 sampleRotate[SHADOWS_SAMPLE_COUNT];

    real2 sampleSinCos = 0;
    sincos(random * PI * 2, sampleSinCos.y, sampleSinCos.x);

    real2x2 sampleRotMat = { sampleSinCos.y, sampleSinCos.x, -sampleSinCos.x, sampleSinCos.y };

    UNITY_UNROLL
    for (int i = 0; i < SHADOWS_SAMPLE_COUNT; i += 4)
    {
        sampleRotate[i + 0] = mul(PoissonDisk[i + 0], sampleRotMat) * blurSize;
        sampleRotate[i + 1] = mul(PoissonDisk[i + 1], sampleRotMat) * blurSize;
        sampleRotate[i + 2] = mul(PoissonDisk[i + 2], sampleRotMat) * blurSize;
        sampleRotate[i + 3] = mul(PoissonDisk[i + 3], sampleRotMat) * blurSize;
    }

    real attenuation = 0;

    for (int i = 0; i < SHADOWS_SAMPLE_COUNT; i++)
    {
        float2 shadow = SAMPLE_TEXTURE2D_X(ShadowMap, sampler_ShadowMap, shadowCoord.xy + sampleRotate[i] * samplingData.shadowmapSize).x;
#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
        attenuation += shadow > shadowCoord.z;
#else
        attenuation += shadow < shadowCoord.z;
#endif
    }

    return attenuation / SHADOWS_SAMPLE_COUNT;
}

real SampleShadowmap(TEXTURE2D_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, ShadowSamplingData samplingData, half4 shadowParams, bool isPerspectiveProjection = true, half random = 0)
{
    real attenuation;
    real shadowStrength = shadowParams.x;

#if _MAIN_LIGHT_EXPONENTIAL_SHADOWS
    real shadow = SAMPLE_TEXTURE2D_X(ShadowMap, sampler_ShadowMap, shadowCoord.xy).x;
    attenuation = exp(-max(1e-5, shadow - shadowCoord.z - 0.05) * 100);
#else
    #ifdef _SHADOWS_SOFT
        attenuation = SampleShadowmapFiltered(TEXTURE2D_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingData, random);
    #else
        real shadow = SAMPLE_TEXTURE2D_X(ShadowMap, sampler_ShadowMap, shadowCoord.xy).x;
        #if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
            attenuation = shadow > shadowCoord.z;
        #else
            attenuation = shadow < shadowCoord.z;
        #endif
    #endif
#endif

    attenuation = LerpWhiteTo(attenuation, shadowStrength);

    // Shadow coords that fall out of the light frustum volume must always return attenuation 1.0
    // TODO: We could use branch here to save some perf on some platforms.
    return BEYOND_SHADOW_FAR(shadowCoord) ? 1.0 : attenuation;
}

half ComputeCascadeIndex(float3 positionWS)
{
    float3 fromCenter0 = positionWS - _CascadeShadowSplitSpheres0.xyz;
    float3 fromCenter1 = positionWS - _CascadeShadowSplitSpheres1.xyz;
    float3 fromCenter2 = positionWS - _CascadeShadowSplitSpheres2.xyz;
    float3 fromCenter3 = positionWS - _CascadeShadowSplitSpheres3.xyz;
    float4 distances2 = float4(dot(fromCenter0, fromCenter0), dot(fromCenter1, fromCenter1), dot(fromCenter2, fromCenter2), dot(fromCenter3, fromCenter3));

    half4 weights = half4(distances2 < _CascadeShadowSplitSphereRadii);
    weights.yzw = saturate(weights.yzw - weights.xyz);

    return 4 - dot(weights, half4(4, 3, 2, 1));
}

float4 ComputeCascadeWeightsSplit(float3 positionWS, out half4 weights, out half4 anotherWeights)
{
    float3 fromCenter0 = positionWS - _CascadeShadowSplitSpheres0.xyz;
    float3 fromCenter1 = positionWS - _CascadeShadowSplitSpheres1.xyz;
    float3 fromCenter2 = positionWS - _CascadeShadowSplitSpheres2.xyz;
    float3 fromCenter3 = positionWS - _CascadeShadowSplitSpheres3.xyz;
    float4 distances2 = float4(dot(fromCenter0, fromCenter0), dot(fromCenter1, fromCenter1), dot(fromCenter2, fromCenter2), dot(fromCenter3, fromCenter3));

    weights = float4(distances2 < _CascadeShadowSplitSphereRadii);
    anotherWeights = weights;
    weights.yzw = saturate(weights.yzw - weights.xyz);
    anotherWeights -= weights;

    return distances2;
}

float4 TransformWorldToShadowCoord(float3 positionWS)
{
#ifdef _MAIN_LIGHT_SHADOWS_CASCADE
    half cascadeIndex = ComputeCascadeIndex(positionWS);
#else
    half cascadeIndex = 0;
#endif

    float4 shadowCoord = mul(_MainLightWorldToShadow[cascadeIndex], float4(positionWS, 1.0));
    shadowCoord.xyz /= shadowCoord.w;
    shadowCoord.w = cascadeIndex;

    return shadowCoord;
}

float4 TransformWorldToShadowCoord(float3 positionWS, float4 weights)
{
#ifdef _MAIN_LIGHT_SHADOWS_CASCADE
    half cascadeIndex = 4 - dot(weights, half4(4, 3, 2, 1));
#else
    half cascadeIndex = 0;
#endif

    float4 shadowCoord = mul(_MainLightWorldToShadow[cascadeIndex], float4(positionWS, 1.0));
    shadowCoord.xyz /= shadowCoord.w;
    shadowCoord.w = cascadeIndex;

    return shadowCoord;
}

half MainLightRealtimeShadow(float4 shadowCoord, real random = 0)
{
#if !defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    return 1.0h;
#endif

    ShadowSamplingData shadowSamplingData = GetMainLightShadowSamplingData();
    half4 shadowParams = GetMainLightShadowParams();
    return SampleShadowmap(TEXTURE2D_ARGS(_MainLightShadowmapTexture, sampler_MainLightShadowmapTexture), shadowCoord, shadowSamplingData, shadowParams, random);
}

half AdditionalLightRealtimeShadow(int lightIndex, float3 positionWS)
{
#if !defined(ADDITIONAL_LIGHT_CALCULATE_SHADOWS)
    return 1.0h;
#endif

    ShadowSamplingData shadowSamplingData = GetAdditionalLightShadowSamplingData();

#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    lightIndex = _AdditionalShadowsIndices[lightIndex];

    // We have to branch here as otherwise we would sample buffer with lightIndex == -1.
    // However this should be ok for platforms that store light in SSBO.
    UNITY_BRANCH
    if (lightIndex < 0)
        return 1.0;

    float4 shadowCoord = mul(_AdditionalShadowsBuffer[lightIndex].worldToShadowMatrix, float4(positionWS, 1.0));
#else
    float4 shadowCoord = mul(_AdditionalLightsWorldToShadow[lightIndex], float4(positionWS, 1.0));
#endif

    shadowCoord /= shadowCoord.w;

    half4 shadowParams = GetAdditionalLightShadowParams(lightIndex);
    return SampleShadowmap(TEXTURE2D_ARGS(_AdditionalLightsShadowmapTexture, sampler_AdditionalLightsShadowmapTexture), shadowCoord, shadowSamplingData, shadowParams);
}

float4 GetShadowCoord(VertexPositionInputs vertexInput)
{
    return TransformWorldToShadowCoord(vertexInput.positionWS);
}

float3 ApplyShadowBias(float3 positionWS, float3 normalWS, float3 lightDirection)
{
    float invNdotL = 1.0 - saturate(dot(lightDirection, normalWS));
    float scale = invNdotL * _ShadowBias.y;

    // normal bias is negative since we want to apply an inset normal offset
    positionWS = lightDirection * _ShadowBias.xxx + positionWS;
    positionWS = normalWS * scale.xxx + positionWS;
    return positionWS;
}

float3 ApplyShadowBias(float3 positionWS, float3 normalWS, float3 lightDirection, float2 shadowBias)
{
    float invNdotL = 1.0 - saturate(dot(lightDirection, normalWS));
    float scale = invNdotL * _ShadowBias.y * shadowBias.y;

    // normal bias is negative since we want to apply an inset normal offset
    positionWS = lightDirection * _ShadowBias.xxx * shadowBias.xxx + positionWS;
    positionWS = normalWS * scale.xxx + positionWS;
    return positionWS;
}

///////////////////////////////////////////////////////////////////////////////
// Deprecated                                                                 /
///////////////////////////////////////////////////////////////////////////////

// Renamed -> _MainLightShadowParams
#define _MainLightShadowData _MainLightShadowParams

// Deprecated: Use GetMainLightShadowParams instead.
half GetMainLightShadowStrength()
{
    return _MainLightShadowData.x;
}

// Deprecated: Use GetAdditionalLightShadowParams instead.
half GetAdditionalLightShadowStrenth(int lightIndex)
{
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    return _AdditionalShadowsBuffer[lightIndex].shadowParams.x;
#else
    return _AdditionalShadowParams[lightIndex].x;
#endif
}

#endif
