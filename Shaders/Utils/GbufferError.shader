Shader "Hidden/Universal Render Pipeline/GbufferError"
{
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"

            struct Attributes
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
                o.vertex = vertexInput.positionCS;
                return o;
            }

            FragmentOutput frag(Varyings i)
            {
                GbufferData data;
                data.albedo = 1;
                data.normalWS = 1;
                data.emission = 1;
                data.specular = 1;
                data.metallic = 1;
                data.smoothness = 1;
                data.occlusion = 1;

                return EncodeGbuffer(data);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
