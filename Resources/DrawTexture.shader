Shader "VirtualTexture/DrawTexture"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        float4 _Control_ST;
        float4 _Splat0_ST;
        float4 _Splat1_ST;
        float4 _Splat2_ST;
        float4 _Splat3_ST;

        float4x4 _ImageMVP;

        TEXTURE2D(_Control); SAMPLER(sampler_Control);

        TEXTURE2D(_Splat0); SAMPLER(sampler_Splat0);
        TEXTURE2D(_Splat1);
        TEXTURE2D(_Splat2);
        TEXTURE2D(_Splat3);

        TEXTURE2D(_Normal0); SAMPLER(sampler_Normal0);
        TEXTURE2D(_Normal1);
        TEXTURE2D(_Normal2);
        TEXTURE2D(_Normal3);

        struct Attributes
        {
            float4 positionOS   : POSITION;
            float2 uv           : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS    : SV_POSITION;
            float2 uv            : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct VirtualOutput
        {
            float3 albedo : SV_TARGET0;
            float3 normal : SV_TARGET1;
        };

        Varyings BakeVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = mul(_ImageMVP, input.positionOS);
            output.uv = input.uv;
            return output;
        }

        VirtualOutput BakeFragment(Varyings input)
        {
            float2 uv_Control = input.uv * _Control_ST.xy + _Control_ST.zw;
            float2 uv0_Splat0 = input.uv * _Splat0_ST.xy + _Splat0_ST.zw;
            float2 uv0_Splat1 = input.uv * _Splat1_ST.xy + _Splat1_ST.zw;
            float2 uv0_Splat2 = input.uv * _Splat2_ST.xy + _Splat2_ST.zw;
            float2 uv0_Splat3 = input.uv * _Splat3_ST.xy + _Splat3_ST.zw;

            float4 classify = SAMPLE_TEXTURE2D(_Control, sampler_Control, uv_Control);
            float classify_weight = 1 / dot(1, classify);

            VirtualOutput output;

            float4 albedo =
                classify.x * SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, uv0_Splat0) + 
                classify.y * SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, uv0_Splat1) +
                classify.z * SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, uv0_Splat2) + 
                classify.w * SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, uv0_Splat3);

            output.albedo = albedo.xyz * classify_weight;

        #ifdef _NORMALMAP
            float3 normal = 
                classify.x * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal0, sampler_Normal0, uv0_Splat0), _BumpScale0) +
                classify.y * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal1, sampler_Normal0, uv0_Splat1), _BumpScale1) +
                classify.z * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal2, sampler_Normal0, uv0_Splat2), _BumpScale2) +
                classify.w * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal3, sampler_Normal0, uv0_Splat3), _BumpScale3);

            normal *= classify_weight;

            output.normal = normal;
        #else
            output.normal = 0;
        #endif

            return output;
        }

    ENDHLSL
    SubShader
    {
        Pass
        {
            Name "DrawTexture"

            ZTest Always ZWrite Off

            HLSLPROGRAM

            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex BakeVertex
            #pragma fragment BakeFragment

            #pragma shader_feature_local _NORMALMAP

            ENDHLSL
        }
    }
}