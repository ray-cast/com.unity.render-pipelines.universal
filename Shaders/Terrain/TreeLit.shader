Shader "Universal Render Pipeline/Tree Lit"
{
    Properties
    {
		[MainColor] _MainColor("基本颜色", Color) = (1,1,1,1)
        [TexToggle(_ALBEDOMAP)][MainTexture] _MainTex("基本贴图", 2D) = "white" {}

        [Space(20)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("启用透明度剔除", Float) = 0
		_Cutoff("透明剔除", Range(0.0, 1.0)) = 0.5

        [Space(10)]
        _Width("宽度缩放", Float) = 1
        _Height("高度缩放", Float) = 1

        [Space(10)]
        _WindDirection("风的朝向", Vector) = (1.0, 0.0, 0.0)
        _WindAIntensity("风的强度", Float) = 1
        _WindAFrequency("风的频率", Float) = 1
        _WindRange("风场运动范围", Float) = 20
        _WindATiling("风的持续", Vector) = (0.1,0.1,0)
        _WindAWrap("风产生的弯曲", Vector) = (0.5,0.5,0)

		_WindScatter("风场高光扩散范围", Vector) = (20, 20, 1, 1)
		_WindHightlightSpeed("风场高光扰动速率", Float) = 1
		_WindHightlightIntensity("风场高光扰动速率", Float) = 2

        [NoScaleOffset]_WindNoiseMap("风场扰动贴图(示例:gradient_beam_007)", 2D) = "black" {}

        [Space(20)]
        [Toggle(_INSTANCING_RENDERING_ON)]_InstancingRendering("启用实例批次渲染", int) = 0

		[HideInInspector]_BendStrength("_BendStrength", Float) = 0.2

        //make SRP batcher happy
        [HideInInspector]_PivotPosWS("_PivotPosWS", Vector) = (0,0,0,0)
        [HideInInspector]_PivotScaleWS("_PivotScaleWS", Vector) = (1,1,0)
    }
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"
        #include "VegetationCommon.cginc"

        struct Attributes
        {
            float4 positionOS : POSITION;//草顶点的模型空间坐标
            float3 normal     : NORMAL;
            float2 uv         : TEXCOORD0;
        #if _INSTANCING_RENDERING_ON
            uint instanceID : SV_InstanceID;
        #endif
        };

        struct Varyings
        {
            float4 positionCS  : SV_POSITION;//草顶点的裁剪空间坐标
            float2 uv          : TEXCOORD0;
            float3 bakeGI      : TEXCOORD1;
            float3 normal      : TEXCOORD2;
        };

        struct AttributesLean
        {
            float4 positionOS   : POSITION;
            float2 uv : TEXCOORD0;
        #if _INSTANCING_RENDERING_ON
            uint instanceID : SV_InstanceID;
        #endif
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct VaryingsLean
        {
            float4 positionCS  : SV_POSITION;
            float2 uv : TEXCOORD0;

            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct BatchData
        {
            float4 position;
            float4 scale;
        };

        CBUFFER_START(UnityPerMaterial)
            half4 _MainColor;
            half _Cutoff;

            float3 _PivotPosWS;
            float3 _PivotScaleWS;
            float4x4 _PivotMatrixWS;

            float _Width;
            float _Height;

            float _WindAIntensity;
            float _WindAFrequency;
            float _WindRange;
            float2 _WindATiling;
            float2 _WindAWrap;
            float3 _WindDirection;

            float2 _WindScatter;
            float _WindHightlightSpeed;
            float _WindHightlightIntensity;

            float _BendStrength;//按压弯曲程度
        CBUFFER_END

        TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);
        TEXTURE2D(_WindNoiseMap);  SAMPLER(sampler_WindNoiseMap);

        StructuredBuffer<BatchData> _AllInstancesTransformBuffer;
        StructuredBuffer<uint> _AllVisibleInstancesIndexBuffer;

        Varyings LitPassVertex(Attributes input)
        {
            Varyings output;

        #ifdef _INSTANCING_RENDERING_ON
            uint index = _AllVisibleInstancesIndexBuffer[input.instanceID];
            float3 positionWS = _AllInstancesTransformBuffer[index].position.xyz;
            float3 scale = _AllInstancesTransformBuffer[index].scale.xyz;
            UNITY_MATRIX_M = float4x4(
                    scale.x,0,0, positionWS.x,
                    0,scale.y,0, positionWS.y,
                    0,0,scale.z, positionWS.z,
                    0,0,0,1
                );
            UNITY_MATRIX_M = mul(_PivotMatrixWS, UNITY_MATRIX_M);
        #endif

            half3 direction = SafeNormalize(_WindDirection);
            half3 pivotPositionWS = half3(UNITY_MATRIX_M[0][3], UNITY_MATRIX_M[1][3], UNITY_MATRIX_M[2][3]);
            half2 windTexcoord = (pivotPositionWS.xz + input.positionOS.xz) / _WindScatter;
            half2 windWindTexcoord = windTexcoord - direction.xz * _WindHightlightSpeed * _Time.x;
            half wind = SAMPLE_TEXTURE2D_LOD(_WindNoiseMap, sampler_WindNoiseMap, windWindTexcoord, 0).r;

            float distanceThreadhold = 1 - saturate(distance(pivotPositionWS, GetCameraPositionWS()) / _WindRange);

            float3 positionOS = input.positionOS.xyz;
            positionOS = ApplyRotationAndScale(positionOS, pivotPositionWS, _Width, _Height, _Width);
            positionOS = ApplyBending(positionOS.xyz, pivotPositionWS, _BendStrength);
            positionOS = ApplyWind(positionOS, pivotPositionWS, direction, _WindAFrequency, _WindATiling, _WindAWrap, (_WindAIntensity + wind * _WindHightlightIntensity) * distanceThreadhold);
            
            output.positionCS = TransformWorldToHClip(TransformObjectToWorld(positionOS));
            output.uv = input.uv;
            output.normal = input.normal;
            output.bakeGI = SampleSH(input.normal);

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
    
            GbufferData data;
            data.albedo = albedo.xyz;
            data.normalWS = input.normal;
            data.specular = 0.5;
            data.metallic = 0;
            data.smoothness = 0.25;
            data.occlusion = 1;
            data.emission = input.bakeGI * data.albedo;

            return EncodeGbuffer(data);
        }

        VaryingsLean DepthOnlyVertex(AttributesLean input)
        {
            VaryingsLean output = (VaryingsLean)0;

            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        #ifdef _INSTANCING_RENDERING_ON
            uint index = _AllVisibleInstancesIndexBuffer[input.instanceID];
            float3 positionWS = _AllInstancesTransformBuffer[index].position.xyz;
            float3 scale = _AllInstancesTransformBuffer[index].scale.xyz;
            UNITY_MATRIX_M = float4x4(
                    scale.x,0,0, positionWS.x,
                    0,scale.y,0, positionWS.y,
                    0,0,scale.z, positionWS.z,
                    0,0,0,1
                );
            UNITY_MATRIX_M = mul(_PivotMatrixWS, UNITY_MATRIX_M);
        #endif

            half3 direction = SafeNormalize(_WindDirection);
            half3 pivotPositionWS = half3(UNITY_MATRIX_M[0][3], UNITY_MATRIX_M[1][3], UNITY_MATRIX_M[2][3]);
            half2 windTexcoord = (pivotPositionWS.xz + input.positionOS.xz) / _WindScatter;
            half2 windWindTexcoord = windTexcoord - direction.xz * _WindHightlightSpeed * _Time.x;
            half wind = SAMPLE_TEXTURE2D_LOD(_WindNoiseMap, sampler_WindNoiseMap, windWindTexcoord, 0).r;

            float distanceThreadhold = 1 - saturate(distance(pivotPositionWS, GetCameraPositionWS()) / _WindRange);

            float3 positionOS = input.positionOS.xyz;
            positionOS = ApplyRotationAndScale(positionOS, pivotPositionWS, 1, 1, 1);
            positionOS = ApplyBending(positionOS, pivotPositionWS, _BendStrength);
            positionOS = ApplyWind(positionOS, pivotPositionWS, direction, _WindAFrequency, _WindATiling, _WindAWrap, (_WindAIntensity + wind * _WindHightlightIntensity) * distanceThreadhold);           

            output.uv = input.uv;
            output.positionCS = TransformWorldToHClip(TransformObjectToWorld(positionOS));

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
                #pragma shader_feature_local _ALBEDOMAP

                #pragma multi_compile_local _ _INSTANCING_RENDERING_ON
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

                #pragma multi_compile_local _ _INSTANCING_RENDERING_ON
            ENDHLSL
        }
    }
}