﻿Shader "Universal Render Pipeline/Grass Lit"
{
    Properties
    {
        [MainColor] _MainColor("基本颜色", Color) = (1,1,1,1)
        [TexToggle(_ALBEDOMAP)][MainTexture] _MainTex("基本贴图", 2D) = "white" {}

        [Space(20)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("启用透明度剔除", Float) = 0
        _Cutoff("透明剔除", Range(0.0, 1.0)) = 0.5

        [Space(20)]
        [TexToggle(_ROOTMAP)][NoScaleOffset]_RootTex("根部贴图", 2D) = "white" {}
        _RootSize("根部区域大小", Vector) = (0,0,1)
        _RootCenter("根部区域中心", Vector) = (0,0,0)
        _RootStrength("根部混合权重", Range(0, 1)) = 0
        _RootBlendHeight("根部高度混合", Range(0, 1)) = 0.1

        [Space(20)]
        _HeadColor("尖部颜色", Color) = (1,1,1,1)
        [TexToggle(_HEADMAP)][NoScaleOffset]_HeadTex("尖部贴图", 2D) = "white" {}
        _HeadSize("尖部区域大小", Vector) = (0,0,1)
        _HeadCenter("尖部区域中心", Vector) = (0,0,0)
        _HeadStrength("尖部混合权重", Range(0, 1)) = 0
        _HeadBlendHeight("尖部高度混合", Range(0, 1)) = 1

        [Space(20)]
        _WindDirection("风的朝向", Vector) = (1.0, 0.0, 0.0)
        _WindAIntensity("风的强度", Float) = 1
        _WindAFrequency("风的频率", Float) = 1
        _WindRange("风场运动范围", Float) = 20
        _WindATiling("风的持续", Vector) = (0.1,0.1,0)
        _WindAWrap("风产生的弯曲", Vector) = (0.5,0.5,0)

        _WindScatter("风场扩散范围", Vector) = (20, 20, 1, 1)
        _WindHightlightSpeed("风场高光扰动速率", Float) = 1
        _WindHightlightIntensity("风场高光扰动强度", Float) = 2

        [NoScaleOffset]_WindNoiseMap("风场扰动贴图(示例:gradient_beam_007)", 2D) = "black" {}

        _RandomNormal("法线扰动", Range(0, 1)) = 0.1

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
            float2 uv : TEXCOORD0;
        #if _INSTANCING_RENDERING_ON
            uint instanceID : SV_InstanceID;
        #endif
        };

        struct Varyings
        {
            float4 positionCS  : SV_POSITION;//草顶点的裁剪空间坐标
            float4 positionWS  : TEXCOORD1;
            float4 normalWS    : TEXCOORD2;
            float3 bakeGI      : TEXCOORD3;
            float2 uv          : TEXCOORD4;
            float4 color       : TEXCOORD5;
        };

        struct AttributesLean
        {
            float4 positionOS   : POSITION;
        #if _INSTANCING_RENDERING_ON
            uint instanceID : SV_InstanceID;
        #endif
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct VaryingsLean
        {
            float4 positionCS  : SV_POSITION;

            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct MeshData
        {
            float4 position;
            float4 scale;
        };

        CBUFFER_START(UnityPerMaterial)
            half3 _MainColor;
            half _Cutoff;

            float3 _PivotPosWS;
            float3 _PivotScaleWS;

            float _RootBlendHeight;
            float _RootStrength;
            float3 _RootCenter;
            float3 _RootSize;

            float _HeadBlendHeight;
            float _HeadStrength;
            float3 _HeadCenter;
            float3 _HeadSize;
            float4 _HeadColor;

            float _RandomNormal;

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
        TEXTURE2D(_RootTex);       SAMPLER(sampler_RootTex);
        TEXTURE2D(_HeadTex);       SAMPLER(sampler_HeadTex);
        TEXTURE2D(_WindNoiseMap);  SAMPLER(sampler_WindNoiseMap);

        StructuredBuffer<MeshData> _AllInstancesTransformBuffer;
        StructuredBuffer<uint> _AllVisibleInstancesIndexBuffer;

        float2 BoundsToWorldUV(in float3 wPos, in half4 b)
        {
            return (wPos.xz * b.z) - (b.xy * b.z);
        }

        float2 GetColorMapUV(in float3 wPos, half3 center, half3 size)
        {
            float3 offset = center - size * 0.5;
            return BoundsToWorldUV(wPos, half4(offset.xz, 1.f / size.x, 0));
        }

        float ObjectPosRand01(float3 wPos) {
            return frac(dot(wPos, 1));
        }

        Varyings LitPassVertex(Attributes input)
        {
            Varyings output;

        #ifdef _INSTANCING_RENDERING_ON
            uint index = _AllVisibleInstancesIndexBuffer[input.instanceID];
            float3 pivotPositionWS = _AllInstancesTransformBuffer[index].position;
            float3 pivotScaleWS = _AllInstancesTransformBuffer[index].scale;
        #else
            float3 pivotPositionWS = float3(UNITY_MATRIX_M[0][3], UNITY_MATRIX_M[1][3], UNITY_MATRIX_M[2][3]);
        #endif

            half3 direction = SafeNormalize(_WindDirection);
            half2 windTexcoord = (pivotPositionWS + input.positionOS).xz / _WindScatter;
            half2 windWindTexcoord = windTexcoord - direction.xz * _WindHightlightSpeed * _Time.x;
            half wind = SAMPLE_TEXTURE2D_LOD(_WindNoiseMap, sampler_WindNoiseMap, windWindTexcoord, 0).r;

            float distanceThreadhold = 1 - saturate(distance(pivotPositionWS, GetCameraPositionWS()) / _WindRange);

            float4 positionOS = input.positionOS;
            positionOS.xyz = ApplyRotationAndScale(positionOS.xyz, pivotPositionWS, 1, 1, 1);
            positionOS.xyz = ApplyBending(positionOS.xyz, pivotPositionWS, _BendStrength);
            positionOS.xyz = ApplyWind(positionOS.xyz, pivotPositionWS, direction, _WindAFrequency, _WindATiling, _WindAWrap, (_WindAIntensity + wind * _WindHightlightIntensity) * distanceThreadhold);

            float3 cameraTransformRightWS = UNITY_MATRIX_V[0].xyz;
            float3 cameraTransformForwardWS = -UNITY_MATRIX_V[2].xyz;
            half3 randomAddToN = (_RandomNormal * sin(pivotPositionWS.x * 82.32523 + pivotPositionWS.z)) * cameraTransformRightWS;

            half3 N = normalize(half3(0, 1, 0) + randomAddToN);

            float rootBlend = lerp(1, smoothstep(_RootBlendHeight, _RootBlendHeight + (1 - _HeadBlendHeight), sqrt(input.positionOS.y)), _RootStrength);
            float headBlend = lerp(0, lerp(1, ObjectPosRand01(pivotPositionWS), _HeadColor.a), _HeadStrength);

        #ifdef _INSTANCING_RENDERING_ON
            output.positionWS = float4(positionOS * pivotScaleWS + pivotPositionWS, rootBlend);
        #else
            output.positionWS = float4(TransformObjectToWorld(positionOS), rootBlend);
        #endif
            output.normalWS = float4(N, headBlend);
            output.color = float4(_MainColor, wind * saturate(input.positionOS.y / 0.12));
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.uv = input.uv;
            output.bakeGI = SampleSH(N);

            return output;
        }

        FragmentOutput LitPassFragment(Varyings input)
        {
        #if _ALBEDOMAP
            half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            albedo.rgb *= input.color.rgb;
        #else
            half4 albedo = half4(input.color.rgb, 1);
        #endif

        #if _ALPHATEST_ON
            clip(albedo.a - _Cutoff);
        #endif

        #if _ROOTMAP
            half3 rootColor = SAMPLE_TEXTURE2D_LOD(_RootTex, sampler_RootTex, GetColorMapUV(input.positionWS.xyz, _RootCenter, _RootSize), 0);
        #else
            half3 rootColor = albedo.xyz;
        #endif

        #if _HEADMAP
            half3 headColor = SAMPLE_TEXTURE2D_LOD(_HeadTex, sampler_HeadTex, GetColorMapUV(input.positionWS.xyz, _HeadCenter, _HeadSize), 0);
            headColor = lerp(albedo.xyz, headColor * _HeadColor.rgb, input.normalWS.a);
        #else
            half3 headColor = lerp(albedo.xyz, _HeadColor.rgb, input.normalWS.a);
        #endif

            GbufferData data;
            data.albedo = lerp(rootColor, headColor, input.positionWS.a) * (1 + input.color.a);
            data.normalWS = input.normalWS.xyz;
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
            float3 pivotPositionWS = _AllInstancesTransformBuffer[index].position;
            float3 pivotScaleWS = _AllInstancesTransformBuffer[index].scale;
        #else
            float3 pivotPositionWS = float3(UNITY_MATRIX_M[0][3], UNITY_MATRIX_M[1][3], UNITY_MATRIX_M[2][3]);
        #endif

            half3 direction = SafeNormalize(_WindDirection);
            half2 windTexcoord = (pivotPositionWS + input.positionOS).xz / _WindScatter;
            half2 windWindTexcoord = windTexcoord - direction.xz * _WindHightlightSpeed * _Time.x;
            half wind = SAMPLE_TEXTURE2D_LOD(_WindNoiseMap, sampler_WindNoiseMap, windWindTexcoord, 0).r;

            float distanceThreadhold = 1 - saturate(distance(pivotPositionWS, GetCameraPositionWS()) / _WindRange);

            float4 positionOS = input.positionOS;
            positionOS.xyz = ApplyRotationAndScale(positionOS.xyz, pivotPositionWS, 1, 1, 1);
            positionOS.xyz = ApplyBending(positionOS.xyz, pivotPositionWS, _BendStrength);
            positionOS.xyz = ApplyWind(positionOS.xyz, pivotPositionWS, direction, _WindAFrequency, _WindATiling, _WindAWrap, (_WindAIntensity + wind * _WindHightlightIntensity) * distanceThreadhold);
            
        #ifdef _INSTANCING_RENDERING_ON
            float4 positionWS = float4(positionOS.xyz * pivotScaleWS + pivotPositionWS, 1);
        #else
            float4 positionWS = float4(TransformObjectToWorld(positionOS), 1);
        #endif

            output.positionCS = TransformWorldToHClip(positionWS);

            return output;
        }

        half4 DepthOnlyFragment(VaryingsLean input) : SV_TARGET
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
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

                #pragma shader_feature _ALPHATEST_ON
                #pragma shader_feature_local _ROOTMAP
                #pragma shader_feature_local _HEADMAP
                #pragma shader_feature_local _ALBEDOMAP

                #pragma multi_compile_local _ _INSTANCING_RENDERING_ON

                #pragma vertex LitPassVertex
                #pragma fragment LitPassFragment
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

                #pragma shader_feature _ALPHATEST_ON
                #pragma shader_feature_local _ALBEDOMAP

                #pragma multi_compile_local _ _INSTANCING_RENDERING_ON

                #pragma vertex DepthOnlyVertex
                #pragma fragment DepthOnlyFragment
            ENDHLSL
        }
    }
}