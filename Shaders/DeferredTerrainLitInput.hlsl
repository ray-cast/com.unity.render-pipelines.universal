#ifndef UNIVERSAL_DEFERRED_TERRAIN_LIT_INPUT_INCLUDED
#define UNIVERSAL_DEFERRED_TERRAIN_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Terrain.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    half4 _BaseColor;
    half _Cutoff;
    float4 _Control_ST;
    float4 _Splat0_ST;
    float4 _Splat1_ST;
    float4 _Splat2_ST;
    float4 _Splat3_ST;
    half _BumpScale0;
    half _BumpScale1;
    half _BumpScale2;
    half _BumpScale3;
    half _Metallic0;
    half _Metallic1;
    half _Metallic2;
    half _Metallic3;
    half _Smoothness0;
    half _Smoothness1;
    half _Smoothness2;
    half _Smoothness3;
    half _specularAntiAliasingThreshold;
    half _ShadowDepthBias;
    half _ShadowNormalBias;
CBUFFER_END

float3 _LightDirection;
float4 _VTFeedbackParam;

TEXTURE2D(_Control); SAMPLER(sampler_Control);

TEXTURE2D(_Splat0); SAMPLER(sampler_Splat0);
TEXTURE2D(_Splat1);
TEXTURE2D(_Splat2);
TEXTURE2D(_Splat3);

TEXTURE2D(_Normal0); SAMPLER(sampler_Normal0);
TEXTURE2D(_Normal1);
TEXTURE2D(_Normal2);
TEXTURE2D(_Normal3);

TEXTURE2D(_WetnessMap0);
TEXTURE2D(_WetnessMap1);
TEXTURE2D(_WetnessMap2);
TEXTURE2D(_WetnessMap3);

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    float2 uv_Control = uv * _Control_ST.xy + _Control_ST.zw;
    float2 uv0_Splat0 = uv * _Splat0_ST.xy + _Splat0_ST.zw;
    float2 uv0_Splat1 = uv * _Splat1_ST.xy + _Splat1_ST.zw;
    float2 uv0_Splat2 = uv * _Splat2_ST.xy + _Splat2_ST.zw;
    float2 uv0_Splat3 = uv * _Splat3_ST.xy + _Splat3_ST.zw;

    float4 classify = SAMPLE_TEXTURE2D(_Control, sampler_Control, uv_Control);
    float classify_weight = 1 / dot(1, classify);

    float4 albedo =
        classify.x * SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, uv0_Splat0) + 
        classify.y * SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, uv0_Splat1) +
        classify.z * SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, uv0_Splat2) + 
        classify.w * SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, uv0_Splat3);

    albedo *= classify_weight;

    outSurfaceData.albedo = albedo.rgb;
    outSurfaceData.alpha = 1;

#if _NORMALMAP
    float3 normal = 
        classify.x * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal0, sampler_Normal0, uv0_Splat0), _BumpScale0) +
        classify.y * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal1, sampler_Normal0, uv0_Splat1), _BumpScale1) +
        classify.z * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal2, sampler_Normal0, uv0_Splat2), _BumpScale2) +
        classify.w * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal3, sampler_Normal0, uv0_Splat3), _BumpScale3);

    normal *= classify_weight;

    outSurfaceData.normalTS = normal;
#else
    outSurfaceData.normalTS = float3(0,0,1);
#endif

    outSurfaceData.smoothness = dot(classify , float4(_Smoothness0 , _Smoothness1 , _Smoothness2 , _Smoothness3));
    outSurfaceData.metallic = dot(classify , float4(_Metallic0 , _Metallic1 , _Metallic2 , _Metallic3));
    outSurfaceData.specular = 0.5f;
    outSurfaceData.emission = 0;
    outSurfaceData.occlusion = 1;

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