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
        float3 viewdir  : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings VertRender(uint id : SV_VERTEXID)
    {
		float4 hpositionWS = mul(unity_MatrixInvVP, ComputeClipSpacePosition(GetFullScreenTriangleTexCoord(id), 1));
		hpositionWS /= hpositionWS.w;

		Varyings o;
		o.positionCS = GetFullScreenTriangleVertexPosition(id, UNITY_RAW_FAR_CLIP_VALUE);
		o.viewdir = GetCameraPositionWS() - hpositionWS.xyz;

		return o;
    }

    float4 FragRender(Varyings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float3 viewDirWS = normalize(input.viewdir);
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
    }
    Fallback Off
}