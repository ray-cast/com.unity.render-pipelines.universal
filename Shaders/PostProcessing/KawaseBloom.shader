Shader "Hidden/Universal Render Pipeline/KawaseBloom"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

        TEXTURE2D_X(_MainTex);
        TEXTURE2D_X(_MainTexLowMip);
        TEXTURE2D_X(_CameraGlowTexture);

        float4 _MainTex_TexelSize;
        float4 _MainTexLowMip_TexelSize;

        float4 _Params; // x: scatter, y: clamp, z: threshold (linear), w: threshold knee
        float _FilterRadius;

        #define Scatter             _Params.x
        #define ClampMax            _Params.y
        #define Threshold           _Params.z
        #define ThresholdKnee       _Params.w

        struct DownsampleVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv         : TEXCOORD0;
            float4 uv01       : TEXCOORD1;
            float4 uv23       : TEXCOORD2;
        };

        struct UpsampleVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv         : TEXCOORD0;
            float4 uv01       : TEXCOORD1;
            float4 uv23       : TEXCOORD2;
            float4 uv45       : TEXCOORD3;
            float4 uv67       : TEXCOORD4;
        };

        half4 EncodeHDR(half3 color)
        {
        #if _USE_RGBM
            half4 outColor = EncodeRGBM(color);
        #else
            half4 outColor = half4(color, 1.0);
        #endif

        #if UNITY_COLORSPACE_GAMMA
            return half4(sqrt(outColor.xyz), outColor.w); // linear to γ
        #else
            return outColor;
        #endif
        }

        half3 DecodeHDR(half4 color)
        {
        #if UNITY_COLORSPACE_GAMMA
            color.xyz *= color.xyz; // γ to linear
        #endif

        #if _USE_RGBM
            return DecodeRGBM(color);
        #else
            return color.xyz;
        #endif
        }

        Varyings KawasePrefilter(uint id : SV_VERTEXID)
        {
            Varyings output;
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.uv = float2(id / 2, id % 2) * 2;
            output.positionCS = float4(output.uv * 2 - 1, 0, 1);

            #if UNITY_UV_STARTS_AT_TOP
                output.uv.y = 1 - output.uv.y;
            #endif

            return output;
        }

        DownsampleVaryings KawaseDownsample(uint id : SV_VERTEXID)
        {
            DownsampleVaryings output;
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float2 o =  _FilterRadius * _MainTex_TexelSize.xy * 0.5;

            output.uv = float2(id / 2, id % 2) * 2;
            output.positionCS = float4(output.uv * 2 - 1, 0, 1);

            #if UNITY_UV_STARTS_AT_TOP
                output.uv.y = 1 - output.uv.y;
            #endif

            output.uv01.xy = output.uv + float2(-o.x, -o.y);
            output.uv01.zw = output.uv + float2(-o.x,  o.y);
            output.uv23.xy = output.uv + float2( o.x,  o.y);
            output.uv23.zw = output.uv + float2( o.x, -o.y);

            return output;
        }

        UpsampleVaryings KawaseUpsample(uint id : SV_VERTEXID)
        {
            UpsampleVaryings output;
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float2 o2 = _FilterRadius * _MainTex_TexelSize;
            float2 o = o2 * 0.5f;

            output.uv = float2(id / 2, id % 2) * 2;
            output.positionCS = float4(output.uv * 2 - 1, 0, 1);

            #if UNITY_UV_STARTS_AT_TOP
                output.uv.y = 1 - output.uv.y;
            #endif

            output.uv01.xy = output.uv + float2(-o2.x, 0);
            output.uv01.zw = output.uv + float2(-o.x, o.y);
            output.uv23.xy = output.uv + float2(0, o2.y);
            output.uv23.zw = output.uv + o;
            output.uv45.xy = output.uv + float2(o2.x, 0);
            output.uv45.zw = output.uv + float2(o.x, -o.y);
            output.uv67.xy = output.uv + float2(0, -o2.y);
            output.uv67.zw = output.uv - o;

            return output;
        }

        half4 FragPrefilter(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);

        #if _BLOOM_HQ
            half4 red = GATHER_RED_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv);
            half4 green = GATHER_GREEN_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv);
            half4 blue = GATHER_BLUE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv);
            half3 c1 = half3(red.x, green.x, blue.x);
            half3 c2 = half3(red.y, green.y, blue.y);
            half3 c3 = half3(red.z, green.z, blue.z);
            half3 c4 = half3(red.w, green.w, blue.w);
            half3 color = min(c1, min(c2, min(c3, c4)));
        #else
            half3 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv).xyz;
        #endif

            // User controlled clamp to limit crazy high broken spec
            color = min(color, ClampMax);

            // Thresholding
            half brightness = Max3(color.r, color.g, color.b);
            half softness = clamp(brightness - Threshold + ThresholdKnee, 0.0, 2.0 * ThresholdKnee);
            softness = (softness * softness) / (4.0 * ThresholdKnee + 1e-4);
            half multiplier = max(brightness - Threshold, softness) / max(brightness, 1e-4);
            color *= multiplier;

        #if _BLOOM_GLOW
            color += DecodeRGBM(SAMPLE_TEXTURE2D_X(_CameraGlowTexture, sampler_PointClamp, uv));
        #endif

            return EncodeHDR(color);
        }

        half4 KawaseBlur(DownsampleVaryings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            half3 s;
            s =  DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv)) * 4;
            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv01.xy));
            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv01.zw));
            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv23.xy));
            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv23.zw));

            return EncodeHDR(s * 0.125);
        }

        half4 FragUpsample(UpsampleVaryings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            half3 highMip = DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv));

            half3 lowMip;
            lowMip = DecodeHDR(SAMPLE_TEXTURE2D(_MainTexLowMip, sampler_LinearClamp, input.uv01.xy));
            lowMip += DecodeHDR(SAMPLE_TEXTURE2D(_MainTexLowMip, sampler_LinearClamp, input.uv01.zw)) * 2;
            lowMip += DecodeHDR(SAMPLE_TEXTURE2D(_MainTexLowMip, sampler_LinearClamp, input.uv23.xy));
            lowMip += DecodeHDR(SAMPLE_TEXTURE2D(_MainTexLowMip, sampler_LinearClamp, input.uv23.zw)) * 2;
            lowMip += DecodeHDR(SAMPLE_TEXTURE2D(_MainTexLowMip, sampler_LinearClamp, input.uv45.xy));
            lowMip += DecodeHDR(SAMPLE_TEXTURE2D(_MainTexLowMip, sampler_LinearClamp, input.uv45.zw)) * 2;
            lowMip += DecodeHDR(SAMPLE_TEXTURE2D(_MainTexLowMip, sampler_LinearClamp, input.uv67.xy));
            lowMip += DecodeHDR(SAMPLE_TEXTURE2D(_MainTexLowMip, sampler_LinearClamp, input.uv67.zw)) * 2;
            lowMip *= 0.08333;

            return EncodeHDR(lerp(highMip, lowMip, Scatter));
        }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Off ZWrite Off Cull Off

        Pass
        {
            Name "Bloom Prefilter"

            HLSLPROGRAM
                #pragma vertex KawasePrefilter
                #pragma fragment FragPrefilter
                #pragma target 5.0
                #pragma multi_compile_local _ _BLOOM_HQ
                #pragma multi_compile_local _ _BLOOM_GLOW
                #pragma multi_compile_local _ _USE_RGBM
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Kawase Blur"

            HLSLPROGRAM
                #pragma vertex KawaseDownsample
                #pragma fragment KawaseBlur
                #pragma multi_compile_local _ _USE_RGBM
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Upsample"

            HLSLPROGRAM
                #pragma vertex KawaseUpsample
                #pragma fragment FragUpsample
                #pragma multi_compile_local _ _USE_RGBM
            ENDHLSL
        }
    }
}
