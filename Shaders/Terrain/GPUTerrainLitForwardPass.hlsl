#ifndef UNIVERSAL_GPUTERRAIN_FORWARD_LIT_PASS_INCLUDED
#define UNIVERSAL_GPUTERRAIN_FORWARD_LIT_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Terrain.hlsl"

struct Attributes
{
    float3 positionOS   : POSITION;
    float2 texcoord     : TEXCOORD0;
    half4 color         : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 uvMainAndLM : TEXCOORD0; // xy: control, zw: lightmap
#ifndef TERRAIN_SPLAT_BASEPASS
    float4 uvSplat01 : TEXCOORD1; // xy: splat0, zw: splat1
    float4 uvSplat23 : TEXCOORD2; // xy: splat2, zw: splat3
#endif

    float2 texcoord : TEXCOORD3;
    float3 viewDir  : TEXCOORD4;
    half3  vertexSH : TEXCOORD5; // SH

    float3 positionWS : TEXCOORD7;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD8;
#endif
    float4 clipPos : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    half3 viewDirWS = input.viewDir;
    
#if SHADER_HINT_NICE_QUALITY
    viewDirWS = SafeNormalize(viewDirWS);
#endif
    
    float3 dx = ddx(input.positionWS);
    float3 dy = ddy(input.positionWS);
    float3 normal = -normalize(cross(dx, dy));

    half3 normalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalMap, sampler_TerrainNormalMap, input.texcoord).rgb * 2 - 1));
    half3 tangentWS = cross(GetObjectToWorldMatrix()._13_23_33, normalWS);

    inputData.normalWS = TransformTangentToWorld(normalTS, half3x3(-tangentWS, cross(normalWS, tangentWS), normalWS));   
    inputData.normalWS = NormalizeNormalPerPixel(normalWS);

    inputData.viewDirectionWS = viewDirWS;

    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
    inputData.bakedGI = SAMPLE_GI(input.uvMainAndLM.zw, SampleSH(inputData.normalWS.xyz), inputData.normalWS);
}

Varyings LitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

#ifdef PROCEDURAL_INSTANCING_ON
    TerrainPositionInputs vertexInput = GetTerrainPositionInputs(input.positionOS, input.color);
#else
    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS);
#endif

    output.positionWS = vertexInput.positionWS;
    output.texcoord = input.texcoord;
    output.viewDir = GetCameraPositionWS() - vertexInput.positionWS;
    output.vertexSH = SampleSH(float3(0,0,0));
    output.clipPos = vertexInput.positionCS;
    
    return output;
}

// Used in Standard (Physically Based) shader
half4 LitPassFragment(Varyings input) : SV_Target
{
    InputData inputData;
    InitializeInputData(input, half3(0, 0, 1), inputData);

    half3 albedo = 1;
    float metallic = 0;
    float smoothness = 0.5;
    float occlusion = 1;
    float alpha = 1;
    half4 color = UniversalFragmentPBR(inputData, albedo, metallic, half3(0.5h, 0.5h, 0.5h), smoothness, occlusion, /* emission */half3(0, 0, 0), alpha);

    return color;
}

float3 _LightDirection;

struct AttributesLean
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
#ifdef _ALPHATEST_ON
	float2 texcoord     : TEXCOORD0;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VaryingsLean
{
#ifdef _ALPHATEST_ON
    float2 texcoord    : TEXCOORD0;
#endif
    float4 positionCS  : SV_POSITION;

    UNITY_VERTEX_OUTPUT_STEREO
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

VaryingsLean ShadowPassVertex(AttributesLean input, uint instanceID : SV_InstanceID)
{
    VaryingsLean o = (VaryingsLean) 0;

#ifdef PROCEDURAL_INSTANCING_ON
    TerrainPositionInputs vertexInput = GetTerrainShadowPositionInputs(input.positionOS);
#else
    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS);
#endif

    float3 normalWS = _TerrainNormalMap.Load(int3(vertexInput.positionWS.xz, 0)).rgb * 2 - 1;
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(vertexInput.positionWS, normalWS, _LightDirection));

#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif

    o.positionCS = positionCS;

#ifdef _ALPHATEST_ON
	o.texcoord = input.texcoord;
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

#endif
