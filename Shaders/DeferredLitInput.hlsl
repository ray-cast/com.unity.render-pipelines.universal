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
half4 _SpecColor;
half4 _EmissionColor;
half _EmissionIntensity;
half _Cutoff;
half _StippleAlpha;
half _CameraRangeCutoff;
half _TargetRangeCutoff;
half3 _TargetPosition;
half _Smoothness;
half _WindWeight;
half _WindStormWeight;
half _Metallic;
half _Translucency;
half _BumpScale;
half _DetialBumpMapScale;
half _OcclusionStrength;
half _specularAntiAliasingThreshold;
half _ShadowDepthBias;
half _ShadowNormalBias;
CBUFFER_END

TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_WetnessMap);         SAMPLER(sampler_WetnessMap);
TEXTURE2D(_DetailMap);          SAMPLER(sampler_DetailMap);
TEXTURE2D(_DetailBumpMap);      SAMPLER(sampler_DetailBumpMap);

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;

#ifdef _METALLICSPECGLOSSMAP
    specGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);
    specGloss.a *= _Smoothness;
#else
    specGloss = half4(_Metallic.r, 1, 0, _Smoothness);
#endif

    return specGloss;
}

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
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoAlpha.a);
    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;

#if _DETAILMAP
    float2 uv_DetailMap = uv_OriginMap * _DetailMap_ST.xy + _DetailMap_ST.zw;
    half4 detailColor = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, uv_DetailMap);
    outSurfaceData.albedo.rgb = outSurfaceData.albedo.rgb * detailColor.rgb * 2;
#endif

    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.specular = half3(0.5h, 0.5h, 0.5h);
    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.occlusion = LerpWhiteTo(specGloss.g, _OcclusionStrength);

#if _DETAILBUMPMAP
    float2 uv_DetailBumpMap = uv_OriginMap * _DetailBumpMap_ST.xy + _DetailBumpMap_ST.zw;
    half3 detailBump = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailBumpMap, sampler_DetailBumpMap, uv_DetailBumpMap), _DetialBumpMapScale);
    detailBump.z = lerp(1, detailBump.z, saturate(_DetialBumpMapScale));
    outSurfaceData.normalTS = BlendNormal(outSurfaceData.normalTS, detailBump);
#endif

#if _EMISSIONMODE_COLOR
    outSurfaceData.emission = _EmissionColor * _EmissionIntensity;
#elif _EMISSIONMODE_ALBEDO
    outSurfaceData.emission = lerp(0, outSurfaceData.albedo * _EmissionColor * _EmissionIntensity, specGloss.b);
#elif _EMISSIONMODE_TEXTURE
    outSurfaceData.emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor * _EmissionIntensity;
#else
    outSurfaceData.emission = 0;
#endif

#ifdef _WETNESS_ON
    half wetness = SAMPLE_TEXTURE2D(_WetnessMap, sampler_WetnessMap, uv).a;
    outSurfaceData.albedo = lerp(outSurfaceData.albedo, outSurfaceData.albedo * outSurfaceData.albedo, clamp(wetness, 0.0, 0.35));
    outSurfaceData.smoothness = lerp(outSurfaceData.smoothness, 1.0, clamp(wetness, 0.2, 1.0));
    outSurfaceData.specular = lerp(outSurfaceData.specular, 0.25, clamp(wetness, 0.25, 0.5));
    outSurfaceData.occlusion = lerp(outSurfaceData.occlusion, 1.0, clamp(wetness, 0.45, 0.95));
    outSurfaceData.normalTS = lerp(outSurfaceData.normalTS, half3(0, 0, 1), clamp(wetness, 0.45, 0.95));
#endif
}

#endif // UNIVERSAL_INPUT_SURFACE_PBR_INCLUDED