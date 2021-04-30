#ifndef UNIVERSAL_DEPTH_ONLY_PASS_INCLUDED
#define UNIVERSAL_DEPTH_ONLY_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct Attributes
{
    float4 position     : POSITION;
    float2 texcoord     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
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

Varyings DepthOnlyVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.position.xyz);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionCS = vertexInput.positionCS;

#ifdef _STIPPLETEST_ON
    output.screenPos = ComputeScreenPos(vertexInput.positionCS);
    output.positionWS = vertexInput.positionWS;
#endif

    return output;
}

half4 DepthOnlyFragment(Varyings input) : SV_TARGET
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

    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
    return 0;
}
#endif
