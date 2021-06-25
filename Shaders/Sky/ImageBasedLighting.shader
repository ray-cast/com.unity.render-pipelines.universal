Shader "Hidden/Universal Render Pipeline/Sky/BakeSky"
{
    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ImageBasedLighting.hlsl"

    TEXTURECUBE(_Cubemap);
    SAMPLER(sampler_Cubemap);
    float4 _Cubemap_TexelSize;

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

    float4 FragBaker(Varyings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float3 N = ComputeSphereNormal(input.uv);

        return float4(ImportanceSampleSpecularIBL(
                                TEXTURE2D_ARGS(_Cubemap, sampler_Cubemap), 
                                N,
                                N,
                                1,
                                0.5,
                                GenerateHashedRandomFloat(input.uv * 1024),
                                _Cubemap_TexelSize.z), 1.0);
    }

    ENDHLSL

    SubShader
    {
        Pass
        {
            ZTest Always ZWrite Off
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma target 3.5
                #pragma prefer_hlslcc gles
                #pragma exclude_renderers d3d11_9x
                #pragma editor_sync_compilation

                #pragma vertex VertRender
                #pragma fragment FragBaker
            ENDHLSL
        }
    }
    Fallback Off
}