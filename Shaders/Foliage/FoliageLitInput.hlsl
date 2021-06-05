#ifndef UNIVERSAL_DEFERRED_LIT_INPUT_INCLUDED
#define UNIVERSAL_DEFERRED_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _DetailMap_ST;
float4 _DetailBumpMap_ST;
half4 _BaseColor;
half3 _StemColor;
half4 _EmissionColor;
half3 _TranslucencyColor;
half _EmissionIntensity;
half _Cutoff;
half _CameraRangeCutoff;
half _TargetRangeCutoff;
half3 _TargetPosition;
half _Smoothness;
half _Metallic;
half _WindWeight;
half _WindStormWeight;
half _BumpScale;
half _DetialBumpMapScale;
half _OcclusionStrength;
half _specularAntiAliasingThreshold;
half _ShadowDepthBias;
half _ShadowNormalBias;
half _TranslucencyDistortion;
half _TranslucencyPower;
half _TranslucencyScale;
half _TranslucencyAmbient;
half _Translucency;
CBUFFER_END

TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);

half SampleOcclusion(float2 uv)
{
#ifdef _OCCLUSIONMAP
// TODO: Controls things like these by exposing SHADER_QUALITY levels (low, medium, high)
#if defined(SHADER_API_GLES)
    return SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
#else
    half occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
    return LerpWhiteTo(occ, _OcclusionStrength);
#endif
#else
    return 1.0;
#endif
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    float2 uv_OriginMap = (uv - _BaseMap_ST.zw) / _BaseMap_ST.xy;
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    outSurfaceData.alpha = Alpha(albedoAlpha.b, _BaseColor, _Cutoff);

    outSurfaceData.albedo = lerp(_BaseColor.rgb, _StemColor.rgb, albedoAlpha.g);

    outSurfaceData.metallic = 0;
    outSurfaceData.specular = half3(0.5h, 0.5h, 0.5h);
    outSurfaceData.smoothness = _Smoothness;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.occlusion = LerpWhiteTo(albedoAlpha.r, _OcclusionStrength);

#if _EMISSIONMODE_COLOR
    outSurfaceData.emission = _EmissionColor * _EmissionIntensity;
#elif _EMISSIONMODE_ALBEDO
    outSurfaceData.emission = outSurfaceData.albedo * _EmissionColor * _EmissionIntensity;
#elif _EMISSIONMODE_TEXTURE
    outSurfaceData.emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor * _EmissionIntensity;
#else
    outSurfaceData.emission = 0;
#endif
}

#endif // UNIVERSAL_INPUT_SURFACE_PBR_INCLUDED