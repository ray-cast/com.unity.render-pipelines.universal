#ifndef UNIVERSAL_DEFERRED_LIT_PASS_INCLUDED
#define UNIVERSAL_DEFERRED_LIT_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Wind.hlsl"

float4 _TerrainSize;
TEXTURE2D(_TerrainHeightMap);      SAMPLER(sampler_TerrainHeightMap);

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float3 color        : COLOR;
    float2 texcoord     : TEXCOORD0;
    float2 lightmapUV   : TEXCOORD1;
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

    float3 color                    : TEXCOORD8;

    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct AttributesLean
{
    float4 position     : POSITION;
    float3 normalOS     : NORMAL;
    float3 color        : COLOR;
#ifdef _ALPHATEST_ON
    float2 texcoord     : TEXCOORD0;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VaryingsLean
{
    float4 positionCS : SV_POSITION;
#ifdef _ALPHATEST_ON
    float2 uv         : TEXCOORD0;
#endif
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;
    inputData.positionWS = input.positionWS;

    half3 viewDirWS = SafeNormalize(input.viewDirWS);
#ifdef _NORMALMAP 
    float sgn = input.tangentWS.w;      // should be either +1 or -1
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    inputData.normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz));
#else
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

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

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in universal (Physically Based) shader
Varyings LitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

#ifdef _WIND_ON
    Wind wind = GetMainWind(unity_ObjectToWorld._14_24_34 + input.positionOS.xyz, _WindStormWeight);
    wind.intensity *= input.color.r * _WindWeight;
    
    VertexPositionInputs vertexInput = GetWindVertexPositionInputs(wind, input.positionOS.xyz);
#else
    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
#endif

    // normalWS and tangentWS already normalize.
    // this is required to avoid skewing the direction during interpolation
    // also required for per-vertex lighting and SH evaluation
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    float3 viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;
    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

    // already normalized from normal transform to WS.
    output.normalWS = normalInput.normalWS;
    output.viewDirWS = viewDirWS;
#ifdef _NORMALMAP
    real sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif

    OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
    OUTPUT_SH(output.normalWS.xyz, output.vertexSH);
    
    output.color = input.color;
    output.screenPos = ComputeScreenPos(vertexInput.positionCS);
    output.positionWS = vertexInput.positionWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    output.positionCS = vertexInput.positionCS;

    return output;
}

float3 slerp(float3 start, float3 end, float percent)
{
     float angle = dot(start, end);
     angle = clamp(angle, -1.0f, 1.0f);
     float theta = acos(angle) * percent;
     float3 relativeVec = normalize(end - start * angle);
     return ((start * cos(theta)) + (relativeVec * sin(theta)));
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

#if defined(_VIRTUAL_BLEND_ON) && defined(_VIRTUAL_TEXTURE_HQ)
    real height = SampleVirtualHeight(input.positionWS);
    real heightDiff = abs(input.positionWS.y - height);
    real virtualTextureBlend = smoothstep(0, _VirtualBlendMaterial, heightDiff);
    real virtualNormalBlend = smoothstep(0, _VirtualBlendNormal, heightDiff);
    VirtualTexture virtualData = SampleVirtualTexture(input.positionWS - input.normalWS * heightDiff);
    surfaceData.albedo = lerp(virtualData.albedo, surfaceData.albedo, virtualTextureBlend);
    surfaceData.metallic = lerp(virtualData.metallic, surfaceData.metallic, virtualTextureBlend);
    surfaceData.smoothness = lerp(virtualData.smoothness, surfaceData.smoothness, virtualTextureBlend);
    inputData.bakedGI = lerp(virtualData.bakedGI, inputData.bakedGI, virtualTextureBlend);
    inputData.normalWS = lerp(virtualData.normal, inputData.normalWS, virtualNormalBlend);
    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
#endif

    BRDFData brdfData;
    InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, 1, brdfData);

#if defined(_VIRTUAL_BLEND_ON) && defined(_VIRTUAL_TEXTURE_HQ)
    half3 bakedGI = brdfData.diffuse * inputData.bakedGI;
    surfaceData.emission += lerp(bakedGI, GlobalIllumination(brdfData, inputData.bakedGI, surfaceData.occlusion, inputData.normalWS, inputData.viewDirectionWS), virtualTextureBlend);
#else
    surfaceData.emission += GlobalIllumination(brdfData, inputData.bakedGI, surfaceData.occlusion, inputData.normalWS, inputData.viewDirectionWS);
#endif

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
    data.occlusion = surfaceData.occlusion;
    data.translucency = _Translucency;

    return EncodeGbuffer(data);
}

float3 _LightDirection;

float4 GetShadowPositionHClip(AttributesLean input, float2 shadowBias)
{
#ifdef _WIND_ON
    Wind wind = GetMainWind(unity_ObjectToWorld._14_24_34 + input.position.xyz, _WindStormWeight);
    wind.intensity *= input.color.r * _WindWeight;
    
    float3 positionWS = TransformObjectToWindWorld(wind, input.position.xyz);
#else
    float3 positionWS = TransformObjectToWorld(input.position.xyz);
#endif
    
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection, shadowBias));

#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif

    return positionCS;
}

VaryingsLean ShadowBiasPassVertex(AttributesLean input)
{
    VaryingsLean output;
    UNITY_SETUP_INSTANCE_ID(input);
    float2 shadowBias = float2(_ShadowDepthBias, _ShadowNormalBias);

#if _ALPHATEST_ON
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
#endif
    output.positionCS = GetShadowPositionHClip(input, shadowBias);

    return output;
}

half4 ShadowPassFragment(VaryingsLean input) : SV_TARGET
{
#if _ALPHATEST_ON
    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
#endif
    return 0;
}

#endif