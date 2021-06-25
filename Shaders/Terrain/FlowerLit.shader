Shader "Universal Render Pipeline/Flower Lit"
{
    Properties
    {
		[MainColor] _MainColor("基本颜色", Color) = (1,1,1,1)
        [TexToggle(_ALBEDOMAP)][MainTexture] _MainTex("基本贴图", 2D) = "white" {}

        [Space(20)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("启用透明度剔除", Float) = 0
		_Cutoff("透明剔除", Range(0.0, 1.0)) = 0.5

        [Space(20)]
        [ToggleOff(_VERTEX_NORMAL_OFF)]_UseVertexNormalOff("启用顶点法线", int) = 1
        [TexToggle(_NORMALMAP)][NoScaleOffset]_BumpMap("法线贴图", 2D) = "bump" {}
        _BumpScale("法线强度", Range(-10, 10)) = 1.0

        [Space(20)]
        _Smoothness("光滑度", Range(0.0, 1.0)) = 0.0
        _Specular("镜面反射系数", Range(0.0, 1.0)) = 0.5

        [Space(20)]
        _WindWeight("风场影响程度", Range(0.0, 1.0)) = 1.0
        _WindStormWeight("风浪影响程度", Range(0.0, 1.0)) = 1.0

        [Space(20)]
        [Toggle(_INSTANCING_RENDERING_ON)]_InstancingRendering("启用实例批次渲染", int) = 0

		[HideInInspector]_BendStrength("_BendStrength", Float) = 0.2

        [Space(10)]
        [HideInInspector]_Width("宽度缩放", Float) = 1
        [HideInInspector]_Height("高度缩放", Float) = 1
    }
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Wind.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/InstancingRendering.hlsl"
        #include "VegetationCommon.cginc"

        struct Attributes
        {
            float4 positionOS : POSITION;//草顶点的模型空间坐标
            float3 normalOS   : NORMAL;
            float4 tangentOS  : TANGENT;
            float2 uv         : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS  : SV_POSITION;//草顶点的裁剪空间坐标
            float2 uv          : TEXCOORD0;
            float3 bakeGI      : TEXCOORD1;
            float3 normalWS    : TEXCOORD2;
            float4 tangentWS   : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct AttributesLean
        {
            float4 positionOS   : POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct VaryingsLean
        {
            float4 positionCS  : SV_POSITION;
            float2 uv : TEXCOORD0;

            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        CBUFFER_START(UnityPerMaterial)
            half4 _MainColor;
            half _Cutoff;

            float4x4 _PivotMatrixWS;

            float _Specular;
            float _Smoothness;
            float _BumpScale;

            float _Width;
            float _Height;

            float _WindWeight;
            float _WindStormWeight;

            float _BendStrength;//按压弯曲程度
        CBUFFER_END

        TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);

        Varyings LitPassVertex(Attributes input)
        {
            Varyings output;

            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            UNITY_MATRIX_M = mul(_PivotMatrixWS, UNITY_MATRIX_M);
        #endif

            VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

            half3 pivotPositionWS = half3(UNITY_MATRIX_M[0][3], UNITY_MATRIX_M[1][3], UNITY_MATRIX_M[2][3]);

            float3 positionOS = input.positionOS.xyz;
            positionOS = ApplyRotationAndScale(positionOS, pivotPositionWS, _Width, _Height, _Width);
            positionOS = ApplyBending(positionOS.xyz, pivotPositionWS, _BendStrength);

            Wind wind = GetMainWind(pivotPositionWS, _WindStormWeight);
            wind.intensity *= positionOS.y * _WindWeight;
            float3 positionWS = TransformObjectToWindWorld(wind, positionOS);
            
            output.positionCS = TransformWorldToHClip(positionWS);
            output.normalWS = normalInput.normalWS;
            output.tangentWS = half4(normalInput.tangentWS.xyz, input.tangentOS.w * GetOddNegativeScale());
            output.uv = input.uv;
        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            output.bakeGI = unity_InstanceBakeGI;
        #else
            output.bakeGI = SampleSH(normalInput.normalWS);
        #endif

            return output;
        }

        FragmentOutput LitPassFragment(Varyings input)
        {
        #if defined(_ALPHATEST_ON) && defined(_ALBEDOMAP)
            half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            clip(albedo.a - _Cutoff);
            albedo.rgb *= _MainColor.rgb;
        #elif  defined(_ALBEDOMAP)
            half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            albedo.rgb *= _MainColor.rgb;
        #else
            half4 albedo = float4(_MainColor.rgb, 1);
        #endif
    
        #if defined(_NORMALMAP) && !defined(_VERTEX_NORMAL_OFF)
            float3 normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
            float sgn = input.tangentWS.w;      // should be either +1 or -1
            float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
            input.normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz));
        #endif

        #if defined(_VERTEX_NORMAL_OFF)
            input.normalWS = float3(0, 1, 0);
        #endif

            GbufferData data = (GbufferData)0;
            data.albedo = albedo.xyz;
            data.normalWS = input.normalWS;
            data.specular = _Specular;
            data.metallic = 0;
            data.smoothness = _Smoothness;
            data.occlusion = 1;
            data.translucency = 0;
            data.emission = input.bakeGI * data.albedo;

            return EncodeGbuffer(data);
        }

        VaryingsLean DepthOnlyVertex(AttributesLean input)
        {
            VaryingsLean output = (VaryingsLean)0;

            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            UNITY_MATRIX_M = mul(_PivotMatrixWS, UNITY_MATRIX_M);
        #endif

            half3 pivotPositionWS = half3(UNITY_MATRIX_M[0][3], UNITY_MATRIX_M[1][3], UNITY_MATRIX_M[2][3]);

            float3 positionOS = input.positionOS.xyz;
            positionOS = ApplyRotationAndScale(positionOS, pivotPositionWS, 1, 1, 1);
            positionOS = ApplyBending(positionOS, pivotPositionWS, _BendStrength);

            Wind wind = GetMainWind(pivotPositionWS, _WindStormWeight);
            wind.intensity *= positionOS.y * _WindWeight;
            float3 positionWS = TransformObjectToWindWorld(wind, positionOS);

            output.uv = input.uv;
            output.positionCS = TransformWorldToHClip(positionWS);

            return output;
        }

        half4 DepthOnlyFragment(VaryingsLean input) : SV_TARGET
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        #if defined(_ALPHATEST_ON) && defined(_ALBEDOMAP)
            half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            clip(albedo.a - _Cutoff);
        #endif

            return 0;
        }
    ENDHLSL
    SubShader
    {
        Tags{"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
        LOD 300

        Pass
        {
            Name "DeferredLit"
            Tags{"LightMode" = "Deferred"}

            ZTest Less ZWrite On
            Cull Off

            HLSLPROGRAM
                #pragma prefer_hlslcc gles
                #pragma exclude_renderers d3d11_9x

                #pragma vertex LitPassVertex
                #pragma fragment LitPassFragment

                #pragma shader_feature _ALPHATEST_ON
                #pragma shader_feature _NORMALMAP

                #pragma shader_feature_local _ALBEDOMAP
                #pragma shader_feature_local _VERTEX_NORMAL_OFF

                #pragma multi_compile _ PROCEDURAL_INSTANCING_ON
                #pragma instancing_options procedural:SetupInstancing
                #pragma instancing_options assumeuniformscaling
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZTest Less ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
                #pragma prefer_hlslcc gles
                #pragma exclude_renderers d3d11_9x

                #pragma vertex DepthOnlyVertex
                #pragma fragment DepthOnlyFragment

                #pragma shader_feature _ALPHATEST_ON
                #pragma shader_feature_local _ALBEDOMAP

                #pragma multi_compile _ PROCEDURAL_INSTANCING_ON
                #pragma instancing_options procedural:SetupInstancing
                #pragma instancing_options assumeuniformscaling
            ENDHLSL
        }
    }
}