Shader "Hidden/Universal Render Pipeline/Sky/GradientSky"
{
    HLSLINCLUDE

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
        float4 viewdir    : TEXCOORD0;
        float2 uv         : TEXCOORD1;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings VertRender(uint id : SV_VERTEXID)
    {
        Varyings o;
        o.uv = GetFullScreenTriangleTexCoord(id);
        o.positionCS = GetFullScreenTriangleVertexPosition(id, UNITY_RAW_FAR_CLIP_VALUE);
        o.viewdir = mul(unity_MatrixInvVP, ComputeClipSpacePosition(GetFullScreenTriangleTexCoord(id), 1));

        return o;
    }

    float4 FragRender(Varyings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float3 viewDirWS = normalize(GetCameraPositionWS() - input.viewdir.xyz / input.viewdir.w);
        float verticalGradient = viewDirWS.y * _GradientDiffusion;
        float topLerpFactor = saturate(-verticalGradient);
        float bottomLerpFactor = saturate(verticalGradient);
        float3 color = lerp(_GradientMiddle.xyz, _GradientBottom.xyz, bottomLerpFactor);
        color = lerp(color, _GradientTop.xyz, topLerpFactor) * _SkyIntensity;

        return float4(color, 1.0);
    }

    float4 FragCubeRender(Varyings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float3 viewDirWS = normalize(input.viewdir.xyz / input.viewdir.w);
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
            ZTest LEqual ZWrite Off
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma target 3.5
                #pragma editor_sync_compilation

                #pragma vertex VertRender
                #pragma fragment FragRender
            ENDHLSL

        }
        Pass
        {
            ZTest Always ZWrite Off
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma target 3.5
                #pragma editor_sync_compilation

                #pragma vertex VertRender
                #pragma fragment FragCubeRender
            ENDHLSL

        }
    }
    Fallback Off
}