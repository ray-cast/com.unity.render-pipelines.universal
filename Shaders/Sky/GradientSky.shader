Shader "Hidden/Universal Render Pipeline/Sky/GradientSky"
{
    HLSLINCLUDE

    #pragma vertex Vert

    #pragma editor_sync_compilation
    #pragma target 4.5
    #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

	float4 _GradientBottom;
    float4 _GradientMiddle;
    float4 _GradientTop;
    float _GradientDiffusion;
    float _SkyIntensity;

    struct Attributes
    {
        float4 positionOS   : POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float3 viewDirWS  : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output = (Varyings)0;

        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

        float3 viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;
        output.viewDirWS = viewDirWS;
        output.positionCS = vertexInput.positionCS;

        return output;
    }

    float4 FragRender(Varyings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float3 viewDirWS = normalize(input.viewDirWS);
        float verticalGradient = viewDirWS.y * _GradientDiffusion;
        float topLerpFactor = saturate(-verticalGradient);
        float bottomLerpFactor = saturate(verticalGradient);
        float3 color = lerp(_GradientMiddle.xyz, _GradientBottom.xyz, bottomLerpFactor);
        color = lerp(color, _GradientTop.xyz, topLerpFactor) * _SkyIntensity;

        return float4(color, 1.0);
    }

    ENDHLSL

    SubShader
    {
        Pass
        {
            ZWrite Off
            ZTest Less
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment FragRender
            ENDHLSL

        }
    }
    Fallback Off
}