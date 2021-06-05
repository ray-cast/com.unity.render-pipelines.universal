#ifndef UNIVERSAL_DEFERRED_LIT_PASS_INCLUDED
#define UNIVERSAL_DEFERRED_LIT_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Wind.hlsl"

struct Attributes
{
    float3 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 lightmapUV   : TEXCOORD1;
    float3 color        : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct AttributesLean
{
    float3 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float3 color        : COLOR;
    float2 texcoord     : TEXCOORD0;
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
    float4 screenPos                : TEXCOORD6;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD7;
#endif

    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct VaryingsLean
{
    float2 uv           : TEXCOORD0;
#ifdef _STIPPLETEST_ON
    float3 positionWS   : TEXCOORD1;
    float4 screenPos    : TEXCOORD2;
#endif
    float4 positionCS   : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;
    inputData.positionWS = input.positionWS;

#ifdef _NORMALMAP 
    float sgn = input.tangentWS.w;      // should be either +1 or -1
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    inputData.normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz));
#else
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = NormalizeNormalPerPixel(input.viewDirWS);

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif

    inputData.fogCoord = 0;
    inputData.vertexLighting = 0;
    inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
}

Varyings LitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    Wind wind = GetMainWind(unity_ObjectToWorld._14_24_34 + input.positionOS.xyz, _WindStormWeight);
    wind.intensity *= input.color.r * _WindWeight;
    
    VertexPositionInputs vertexInput = GetWindVertexPositionInputs(wind, input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.normalWS = normalInput.normalWS;
    output.viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;

#ifdef _NORMALMAP
    real sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif

    OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
    OUTPUT_SH(output.normalWS.xyz, output.vertexSH);
    
    output.screenPos = ComputeScreenPos(vertexInput.positionCS);
    output.positionWS = vertexInput.positionWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    output.positionCS = vertexInput.positionCS;

    return output;
}

FragmentOutput LitPassFragment(Varyings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#ifdef _STIPPLETEST_ON
    input.screenPos /= input.screenPos.w;
    input.screenPos.xy *= _ScreenParams.xy;

    float alpha = 1;
    alpha *= saturate(distance(input.positionWS, _TargetPosition) / _TargetRangeCutoff);
    alpha *= saturate(distance(input.positionWS, GetCameraPositionWS()) / _CameraRangeCutoff);

    StippleAlpha(alpha, input.screenPos);
#endif

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);

    BRDFData brdfData;
    InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, 1, brdfData);

    Light mainLight = GetMainLight();

    half lambert = saturate(dot(inputData.normalWS, mainLight.direction));
    half3 transLightDir = mainLight.direction + inputData.normalWS * _TranslucencyDistortion;
    half transDot = pow(saturate(dot(inputData.viewDirectionWS, -transLightDir)), _TranslucencyPower) * _TranslucencyScale;
    half translucencyTerm = (1 - lambert) * transDot;
    translucencyTerm += abs(1 - dot(inputData.normalWS, inputData.viewDirectionWS)) * _TranslucencyAmbient;
    translucencyTerm *= _Translucency * SampleSH(0);

    surfaceData.emission += surfaceData.albedo * translucencyTerm * _TranslucencyColor * surfaceData.occlusion;
    surfaceData.emission += GlobalIllumination(brdfData, inputData.bakedGI, 1, inputData.normalWS, inputData.viewDirectionWS);

#if _SPECULAR_ANTIALIASING
    surfaceData.smoothness = GeometricNormalFiltering(surfaceData.smoothness, inputData.normalWS, _specularAntiAliasingThreshold, 2);
#endif

    GbufferData data = (GbufferData)0;
    data.albedo = surfaceData.albedo;
    data.normalWS = inputData.normalWS;
    data.emission = surfaceData.emission;
    data.specular = surfaceData.specular;
    data.metallic = surfaceData.metallic;
    data.smoothness = surfaceData.smoothness;
    data.occlusion = 1;
    data.translucency = 0;

    return EncodeGbuffer(data);
}

float3 _LightDirection;

float4 GetShadowPositionHClip(AttributesLean input, float2 shadowBias)
{
    Wind wind = GetMainWind(unity_ObjectToWorld._14_24_34 + input.positionOS.xyz, _WindStormWeight);
    wind.intensity *= input.color.r * _WindWeight;
    
    VertexPositionInputs vertexInput = GetWindVertexPositionInputs(wind, input.positionOS.xyz);

    float3 positionWS = vertexInput.positionWS;
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(vertexInput.positionWS, normalWS, _LightDirection, shadowBias));

#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif

    return positionCS;
}

VaryingsLean ShadowBiasPassVertex(AttributesLean input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float2 shadowBias = float2(_ShadowDepthBias, _ShadowNormalBias);

    VaryingsLean output = (VaryingsLean)0;
    output.positionCS = GetShadowPositionHClip(input, shadowBias);

#ifdef _ALPHATEST_ON
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
#endif

    return output;
}

half4 ShadowPassFragment(VaryingsLean input) : SV_TARGET
{
#ifdef _ALPHATEST_ON
    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).b, _BaseColor, _Cutoff);
#endif
    return 0;
}

VaryingsLean DepthOnlyVertex(AttributesLean input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    
    Wind wind = GetMainWind(unity_ObjectToWorld._14_24_34 + input.positionOS.xyz, _WindStormWeight);
    wind.intensity *= input.color.r * _WindWeight;
    
    VertexPositionInputs vertexInput = GetWindVertexPositionInputs(wind, input.positionOS.xyz);

    VaryingsLean output = (VaryingsLean)0;
    output.positionCS = vertexInput.positionCS;

#ifdef _ALPHATEST_ON
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
#endif

#ifdef _STIPPLETEST_ON
    output.screenPos = ComputeScreenPos(vertexInput.positionCS);
    output.positionWS = vertexInput.positionWS;
#endif

    return output;
}

half4 DepthOnlyFragment(VaryingsLean input) : SV_TARGET
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#ifdef _STIPPLETEST_ON
    input.screenPos /= input.screenPos.w;
    input.screenPos.xy *= _ScreenParams.xy;

    float alpha = 1;
    alpha *= saturate(distance(input.positionWS, _TargetPosition) / _TargetRangeCutoff);
    alpha *= saturate(distance(input.positionWS, GetCameraPositionWS()) / _CameraRangeCutoff);

    StippleAlpha(alpha, input.screenPos);
#endif

#if _ALPHATEST_ON
    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).b, _BaseColor, _Cutoff);
#endif
    return 0;
}

#endif