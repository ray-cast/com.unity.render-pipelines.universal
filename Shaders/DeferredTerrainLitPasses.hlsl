#ifndef UNIVERSAL_DEFERRED_TERRAIN_LIT_PASS_INCLUDED
#define UNIVERSAL_DEFERRED_TERRAIN_LIT_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Terrain.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VirtualTexture.hlsl"
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

TEXTURE2D(_Control); SAMPLER(sampler_Control);

TEXTURE2D(_Splat0); SAMPLER(sampler_Splat0);
TEXTURE2D(_Splat1);
TEXTURE2D(_Splat2);
TEXTURE2D(_Splat3);

TEXTURE2D(_Normal0); SAMPLER(sampler_Normal0);
TEXTURE2D(_Normal1);
TEXTURE2D(_Normal2);
TEXTURE2D(_Normal3);

TEXTURE2D(_WetnessMap0); SAMPLER(sampler_WetnessMap0);
TEXTURE2D(_WetnessMap1);
TEXTURE2D(_WetnessMap2);
TEXTURE2D(_WetnessMap3);

struct Attributes
{
    float4 positionOS   : POSITION;
    float4 color        : COLOR;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 lightmapUV   : TEXCOORD1;
    float2 uv2          : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;
    DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 1);

    float3 positionWS               : TEXCOORD2;
    float3 normalWS                 : TEXCOORD3;
#ifdef _NORMALMAP
    float4 tangentWS                : TEXCOORD4;    // xyz: tangent, w: sign
#endif
    float3 viewDirWS                : TEXCOORD5;

    float4 positionCS               : SV_POSITION;

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct AttributesLean
{
    float4 position     : POSITION;
    float3 normalOS     : NORMAL;
    float4 color        : COLOR;
#ifdef _ALPHATEST_ON
    float2 texcoord     : TEXCOORD0;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VaryingsLean
{
    float4 positionCS   : SV_POSITION;
#ifdef _ALPHATEST_ON
    float2 texcoord     : TEXCOORD0;
#endif
    UNITY_VERTEX_OUTPUT_STEREO
};

struct FeedbackAttributes
{
    float3 positionOS   : POSITION;
    float4 color        : COLOR;
    float2 texcoord     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct FeedbackVaryings
{
    float3 positionWS               : TEXCOORD0;
    float2 texcoord                 : TEXCOORD1;
    float4 positionCS               : SV_POSITION;

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct PhysicalMaterial
{
    half3 albedo;
    half3 specular;
    half  metallic;
    half  smoothness;
    half3 emission;
    half  occlusion;
    half  alpha;
#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3  positionWS;
#endif
    half3   normalWS;
    half3   viewDirectionWS;
    half3   bakedGI;
};

inline void InitializeStandardLitSurfaceData(Varyings input, out PhysicalMaterial physicalMaterial)
{
    float2 uv_Control = input.uv * _Control_ST.xy + _Control_ST.zw;
    float2 uv0_Splat0 = input.uv * _Splat0_ST.xy + _Splat0_ST.zw;
    float2 uv0_Splat1 = input.uv * _Splat1_ST.xy + _Splat1_ST.zw;
    float2 uv0_Splat2 = input.uv * _Splat2_ST.xy + _Splat2_ST.zw;
    float2 uv0_Splat3 = input.uv * _Splat3_ST.xy + _Splat3_ST.zw;

    physicalMaterial.alpha = 1;
    physicalMaterial.specular = 0.5f;
    physicalMaterial.emission = 0;
    physicalMaterial.occlusion = 1;
    physicalMaterial.viewDirectionWS = SafeNormalize(input.viewDirWS);

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    physicalMaterial.positionWS = input.positionWS;
#endif

#ifdef _USE_VIRTUAL_TEXTURE
    VirtualTexture virtualData = SampleVirtualTexture(input.positionWS);
    physicalMaterial.albedo = virtualData.albedo;
    physicalMaterial.normalWS = virtualData.normal;
    physicalMaterial.smoothness = virtualData.smoothness;
    physicalMaterial.metallic = virtualData.metallic;
#else
    float4 classify = SAMPLE_TEXTURE2D(_Control, sampler_Control, uv_Control);
    classify *= rcp(max(1, dot(1, classify)));

    float4 albedo =
        classify.x * SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, uv0_Splat0) + 
        classify.y * SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, uv0_Splat1) +
        classify.z * SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, uv0_Splat2) + 
        classify.w * SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, uv0_Splat3);

    physicalMaterial.albedo = albedo;
    physicalMaterial.smoothness = dot(classify , float4(_Smoothness0 , _Smoothness1 , _Smoothness2 , _Smoothness3));
    physicalMaterial.metallic = dot(classify , float4(_Metallic0 , _Metallic1 , _Metallic2 , _Metallic3));

#   if _NORMALMAP
    float3 normalTS = 
        classify.x * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal0, sampler_Normal0, uv0_Splat0), _BumpScale0) +
        classify.y * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal1, sampler_Normal0, uv0_Splat1), _BumpScale1) +
        classify.z * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal2, sampler_Normal0, uv0_Splat2), _BumpScale2) +
        classify.w * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal3, sampler_Normal0, uv0_Splat3), _BumpScale3);
#   else
    float3 normalTS = float3(0,0,1);
#   endif

#ifdef _WETNESS_ON
    half wetness = SAMPLE_TEXTURE2D(_WetnessMap, sampler_WetnessMap, uv).a;
    physicalMaterial.albedo = lerp(physicalMaterial.albedo, physicalMaterial.albedo * physicalMaterial.albedo, clamp(wetness, 0.0, 0.35));
    physicalMaterial.smoothness = lerp(physicalMaterial.smoothness, 1.0, clamp(wetness, 0.2, 1.0));
    physicalMaterial.specular = lerp(physicalMaterial.specular, 0.25, clamp(wetness, 0.25, 0.5));
    physicalMaterial.occlusion = lerp(physicalMaterial.occlusion, 1.0, clamp(wetness, 0.45, 0.95));
    normalTS = lerp(normalTS, half3(0, 0, 1), clamp(wetness, 0.45, 0.95));
#endif

#   ifdef PROCEDURAL_INSTANCING_ON
    input.normalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalMap, sampler_TerrainNormalMap, input.uv).rgb * 2 - 1));
#   endif

#   ifdef _NORMALMAP 
    float3 tangentWS = float3(0, 0, 1);
    float3 bitangent = cross(input.normalWS.xyz, tangentWS.xyz);
    physicalMaterial.normalWS = TransformTangentToWorld(normalTS, half3x3(tangentWS.xyz, bitangent.xyz, input.normalWS.xyz));
#   else
    physicalMaterial.normalWS = input.normalWS;
#   endif

    physicalMaterial.normalWS = NormalizeNormalPerPixel(physicalMaterial.normalWS);
#endif

#ifdef PROCEDURAL_INSTANCING_ON
    physicalMaterial.bakedGI = SAMPLE_TEXTURE2D(_TerrainLightMap, sampler_TerrainLightMap, input.uv).rgb;
#else
    physicalMaterial.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, physicalMaterial.normalWS);
#endif
}

Varyings LitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

#ifdef PROCEDURAL_INSTANCING_ON
    TerrainPositionInputs vertexInput = GetTerrainPositionInputs(input.positionOS.xyz, input.color);
    output.uv = vertexInput.texcoord;
#else
    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    output.uv = input.texcoord;
#endif
    
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.normalWS = normalInput.normalWS;
    output.viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;

#ifdef _NORMALMAP
    real sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif

    OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
    OUTPUT_SH(output.normalWS.xyz, output.vertexSH);
    
    output.positionWS = vertexInput.positionWS;
    output.positionCS = vertexInput.positionCS;

    return output;
}

FragmentOutput LitPassFragment(Varyings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    PhysicalMaterial physicalMaterial;
    InitializeStandardLitSurfaceData(input, physicalMaterial);

    BRDFData brdfData;
    InitializeBRDFData(physicalMaterial.albedo, physicalMaterial.metallic, physicalMaterial.specular, physicalMaterial.smoothness, 1, brdfData);

#ifdef PROCEDURAL_INSTANCING_ON
    physicalMaterial.emission += brdfData.diffuse * physicalMaterial.bakedGI;
#else
    physicalMaterial.emission += GlobalIllumination(brdfData, physicalMaterial.bakedGI, physicalMaterial.occlusion, physicalMaterial.normalWS, physicalMaterial.viewDirectionWS);
#endif

#ifdef _SPECULAR_ANTIALIASING
    physicalMaterial.smoothness = GeometricNormalFiltering(physicalMaterial.smoothness, physicalMaterial.normalWS, _specularAntiAliasingThreshold, 2);
#endif

    GbufferData data = (GbufferData)0;
    data.albedo = physicalMaterial.albedo;
    data.normalWS = physicalMaterial.normalWS;
    data.emission = physicalMaterial.emission;
    data.specular = physicalMaterial.specular;
    data.metallic = physicalMaterial.metallic;
    data.smoothness = physicalMaterial.smoothness;
    data.occlusion = physicalMaterial.occlusion;
    data.translucency = 0;

    return EncodeGbuffer(data);
}

VaryingsLean ShadowPassVertex(AttributesLean input)
{
    VaryingsLean o = (VaryingsLean)0;
    UNITY_SETUP_INSTANCE_ID(input);

#ifdef PROCEDURAL_INSTANCING_ON
    TerrainPositionInputs vertexInput = GetTerrainShadowPositionInputs(input.position.xyz);
#else
    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.position.xyz);
#endif

    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float2 shadowBias = float2(_ShadowDepthBias, _ShadowNormalBias);
    float4 clipPos = TransformWorldToHClip(ApplyShadowBias(vertexInput.positionWS, normalWS, _LightDirection, shadowBias));

#if UNITY_REVERSED_Z
    clipPos.z = min(clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE);
#else
    clipPos.z = max(clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE);
#endif

    o.positionCS = clipPos;

#ifdef _ALPHATEST_ON
#   ifdef PROCEDURAL_INSTANCING_ON
    o.texcoord = vertexInput.texcoord;
#   else
    o.texcoord = input.texcoord;
#   endif
#endif

    return o;
}

half4 ShadowPassFragment(VaryingsLean IN) : SV_TARGET
{
#ifdef _ALPHATEST_ON
    ClipHoles(IN.texcoord);
#endif
    return 0;
}

VaryingsLean DepthOnlyVertex(AttributesLean input)
{
    VaryingsLean output = (VaryingsLean)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

#ifdef PROCEDURAL_INSTANCING_ON
    TerrainPositionInputs vertexInput = GetTerrainPositionInputs(input.position.xyz, input.color);
#else
    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.position.xyz);
#endif

    output.positionCS = vertexInput.positionCS;

#ifdef _ALPHATEST_ON
    output.texcoord = v.texcoord;
#endif

    return output;
}

half4 DepthOnlyFragment(VaryingsLean input) : SV_TARGET
{
#ifdef _ALPHATEST_ON
    ClipHoles(input.texcoord);
#endif
#ifdef SCENESELECTIONPASS
    // We use depth prepass for scene selection in the editor, this code allow to output the outline correctly
    return half4(_ObjectId, _PassValue, 1.0, 1.0);
#endif
    return 0;
}

Varyings UniversalVertexMeta(Attributes input)
{
    Varyings output;
    output.positionCS = MetaVertexPosition(input.positionOS, input.lightmapUV, input.uv2, unity_LightmapST, unity_DynamicLightmapST);
    output.uv = input.texcoord;
    return output;
}

half4 UniversalFragmentMeta(Varyings input) : SV_Target
{
    PhysicalMaterial surfaceData;
    InitializeStandardLitSurfaceData(input, surfaceData);

    BRDFData brdfData;
    InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, surfaceData.alpha, brdfData);

    MetaInput metaInput;
    metaInput.Albedo = brdfData.diffuse + brdfData.specular * brdfData.roughness * 0.5;
    metaInput.SpecularColor = surfaceData.specular;
    metaInput.Emission = surfaceData.emission;

    return MetaFragment(metaInput);
}

FeedbackVaryings FeedbackVertex(FeedbackAttributes input)
{
    FeedbackVaryings output = (FeedbackVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

#ifdef PROCEDURAL_INSTANCING_ON
    TerrainPositionInputs vertexInput = GetTerrainPositionInputs(input.positionOS.xyz, input.color);
    output.texcoord = vertexInput.texcoord;
#else
    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    output.texcoord = input.texcoord;
#endif

    output.positionWS = vertexInput.positionWS;
    output.positionCS = vertexInput.positionCS;

    return output;
}

float4 FeedbackFragment(FeedbackVaryings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float2 texcoord = ComputePageTexcoord(input.positionWS);
    return ComputePageMipLevel(texcoord);
}

#endif