#ifndef UNIVERSAL_COPY_DEPTH_PASS_INCLUDED
#define UNIVERSAL_COPY_DEPTH_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthAttachment.hlsl"

half4 _ScaleBiasRT;

struct Attributes
{
    float4 positionHCS   : POSITION;
    float2 uv           : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS   : SV_POSITION;
    float2 uv           : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings vert(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.uv = UnityStereoTransformScreenSpaceTex(input.uv);

    // Note: CopyDepth pass is setup with a mesh already in CS
    // Therefore, we can just output vertex position

    // We need to handle y-flip in a way that all existing shaders using _ProjectionParams.x work. 
    // Otherwise we get flipping issues like this one (case https://issuetracker.unity3d.com/issues/lwrp-depth-texture-flipy)

    // Unity flips projection matrix in non-OpenGL platforms and when rendering to a render texture.
    // If URP is rendering to RT:
    //  - Source Depth is upside down. We need to copy depth by using a shader that has flipped matrix as well so we have same orientaiton for source and copy depth.
    //  - This also guarantess to be universal across if we are using a depth prepass.
    //  - When shaders (including shader graph) render objects that sample depth they adjust uv sign with  _ProjectionParams.x. (https://docs.unity3d.com/Manual/SL-PlatformDifferences.html)
    //  - All good.
    // If URP is NOT rendering to RT neither rendering with OpenGL:
    //  - Source Depth is NOT fliped. We CANNOT flip when copying depth and don't flip when sampling. (ProjectionParams.x == 1)
    output.positionCS = float4(input.positionHCS.xyz, 1.0);
    output.positionCS.y *= _ScaleBiasRT.x;
    return output;
}

float frag(Varyings input) : SV_Depth
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    UNITY_SETUP_INSTANCE_ID(input);
    return SampleDepthAttachment(input.uv);
}

#endif
