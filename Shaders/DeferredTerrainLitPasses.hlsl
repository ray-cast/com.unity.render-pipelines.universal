#ifndef UNIVERSAL_DEFERRED_TERRAIN_LIT_PASS_INCLUDED
#define UNIVERSAL_DEFERRED_TERRAIN_LIT_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

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

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    inputData.positionWS = input.positionWS;
#endif

    half3 viewDirWS = SafeNormalize(input.viewDirWS);

#ifdef PROCEDURAL_INSTANCING_ON
    input.normalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalMap, sampler_TerrainNormalMap, input.uv).rgb * 2 - 1));
#endif

#ifdef _NORMALMAP 
    float sgn = input.tangentWS.w;      // should be either +1 or -1
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    inputData.normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz));
#else
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

#ifdef PROCEDURAL_INSTANCING_ON
    inputData.bakedGI = SAMPLE_TEXTURE2D(_TerrainLightMap, sampler_TerrainLightMap, input.uv).rgb;
#else
    inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
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

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);

    BRDFData brdfData;
    InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, 1, brdfData);

#ifdef PROCEDURAL_INSTANCING_ON
    surfaceData.emission += inputData.bakedGI * surfaceData.albedo;
#else
    surfaceData.emission += GlobalIllumination(brdfData, inputData.bakedGI, surfaceData.occlusion, inputData.normalWS, inputData.viewDirectionWS);
#endif

#ifdef _SPECULAR_ANTIALIASING
    surfaceData.smoothness = GeometricNormalFiltering(surfaceData.smoothness, inputData.normalWS, _specularAntiAliasingThreshold, 2);
#endif

    GbufferData data = (GbufferData)0;
    data.albedo = surfaceData.albedo;
    data.normalWS = inputData.normalWS;
    data.emission = surfaceData.emission;
    data.specular = surfaceData.specular;
    data.metallic = surfaceData.metallic;
    data.smoothness = surfaceData.smoothness;
    data.occlusion = surfaceData.occlusion;
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

#ifdef UNITY_REVERSED_Z
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
    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

    BRDFData brdfData;
    InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, surfaceData.alpha, brdfData);

    MetaInput metaInput;
    metaInput.Albedo = brdfData.diffuse + brdfData.specular * brdfData.roughness * 0.5;
    metaInput.SpecularColor = surfaceData.specular;
    metaInput.Emission = surfaceData.emission;

    return MetaFragment(metaInput);
}

#endif