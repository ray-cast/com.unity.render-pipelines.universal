Shader "Hidden/Universal Render Pipeline/Sky/GradientSky"
{
    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

    TEXTURE2D(_SceneLut);
    TEXTURE2D(_CameraColorTexture);

    float3 _userLutParams;

    Varyings VertRender(uint id : SV_VERTEXID)
    {
        Varyings o;
        o.uv = GetFullScreenTriangleTexCoord(id);
        o.positionCS = GetFullScreenTriangleVertexPosition(id);

		return o;
    }

    float4 FragRender(Varyings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float3 color = SAMPLE_TEXTURE2D_X(_SceneLut, sampler_PointClamp, input.uv.xy);
        //color = ApplyLut2D(TEXTURE2D_ARGS(_SceneLut, sampler_LinearClamp), float3(input.uv.xy, 0), _userLutParams);
        //float3 viewDirWS = normalize(input.viewdir.xyz / input.viewdir.w);

        return float4(color, 1.0);
    }

    ENDHLSL

    SubShader
    {
        Pass
        {
            ZTest Off ZWrite Off
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