Shader "VirtualTexture/DrawTexture"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VirtualTexture.hlsl"

        float4 _Control_ST;
        float4 _Splat0_ST;
        float4 _Splat1_ST;
        float4 _Splat2_ST;
        float4 _Splat3_ST;
        float4 _TerrainHeight;

        half _BumpScale0;
        half _BumpScale1;
        half _BumpScale2;
        half _BumpScale3;

        half _Metallic0;
        half _Metallic1;
        half _Metallic2;
        half _Metallic3;

        half _Smoothness0;
        half _Smoothness1;
        half _Smoothness2;
        half _Smoothness3;

        float4x4 _ImageMVP;

        TEXTURE2D(_Control); SAMPLER(sampler_Control);

        TEXTURE2D(_Splat0); SAMPLER(sampler_Splat0);
        TEXTURE2D(_Splat1);
        TEXTURE2D(_Splat2);
        TEXTURE2D(_Splat3);

        TEXTURE2D(_Normal); SAMPLER(sampler_Normal);
        TEXTURE2D(_Height); SAMPLER(sampler_Height);
        TEXTURE2D(_LightMap); SAMPLER(sampler_LightMap);

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
            float2 uv_Splat0 = input.uv * _Splat0_ST.xy + _Splat0_ST.zw;
            float2 uv_Splat1 = input.uv * _Splat1_ST.xy + _Splat1_ST.zw;
            float2 uv_Splat2 = input.uv * _Splat2_ST.xy + _Splat2_ST.zw;
            float2 uv_Splat3 = input.uv * _Splat3_ST.xy + _Splat3_ST.zw;

            half4 blend = SAMPLE_TEXTURE2D_LOD(_Control, sampler_Control, uv_Control, 0);
            blend *= rcp(max(1, dot(1, blend)));

            half4 albedo =
                blend.x * SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, uv_Splat0) + 
                blend.y * SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, uv_Splat1) +
                blend.z * SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, uv_Splat2) + 
                blend.w * SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, uv_Splat3);

            float3 normalWS = UnpackNormalMaxComponent(SAMPLE_TEXTURE2D_LOD(_Normal, sampler_Normal, uv_Control, 0).xyz);
            float height = UnpackHeightmap(SAMPLE_TEXTURE2D(_Height, sampler_Height, uv_Control));
            float3 bakedGI = SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, uv_Control);

        #ifdef _NORMALMAP
            float3 normalTS = 
                blend.x * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal0, sampler_Normal0, uv_Splat0), _BumpScale0) +
                blend.y * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal1, sampler_Normal0, uv_Splat1), _BumpScale1) +
                blend.z * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal2, sampler_Normal0, uv_Splat2), _BumpScale2) +
                blend.w * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal3, sampler_Normal0, uv_Splat3), _BumpScale3);

            float3 tangentWS = float3(0, 0, 1);
            float3 bitangent = cross(normalWS.xyz, tangentWS.xyz);

            normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(tangentWS.xyz, bitangent.xyz, normalWS.xyz)));
        #endif

            VirtualTexture output;
            output.albedo = albedo.xyz;
            output.normal = normalWS;
            output.smoothness = dot(blend, half4(_Smoothness0 , _Smoothness1 , _Smoothness2 , _Smoothness3));
            output.metallic = dot(blend, half4(_Metallic0 , _Metallic1 , _Metallic2 , _Metallic3));
            output.height = height * _TerrainHeight.x * 2 + _TerrainHeight.y;
            output.bakedGI = bakedGI;

            return EncodeVirtualBuffer(output);
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