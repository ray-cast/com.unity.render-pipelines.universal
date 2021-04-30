Shader "Universal Render Pipeline/Flower Lit Deferred"
{
    Properties
    {
		[MainTexture] _MainTex("Albedo", 2D) = "white" {}
		[MainColor] _MainColor("Color", Color) = (1,1,1,1)
		_Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        [Header(Grass Shape)]
        _GrassWidth("_GrassWidth", Float) = 0.2
        _GrassHeight("_GrassHeight", Float) = 1

        [Header(Wind)]
        _WindAIntensity("_WindAIntensity", Float) = 1.77
        _WindAFrequency("_WindAFrequency", Float) = 4
        _WindATiling("_WindATiling", Vector) = (0.1,0.1,0)
        _WindAWrap("_WindAWrap", Vector) = (0.5,0.5,0)
        _WindDirection("WindDirection", Vector) = (1.0, 0.0, 0.0)

		_WindScatter("风场扩散范围", Vector) = (20, 20, 1, 1)
		_WindHightlightSpeed("风场高光扰动速率", Float) = 1
		_WindHightlightIntensity("风场高光扰动速率", Float) = 2

        [NoScaleOffset]_WindNoiseMap("风场扰动贴图", 2D) = "black" {}

		[Header(TouchBend)]
		_BendStrength("_BendStrength", Float) = 0.2

        //make SRP batcher happy
        [HideInInspector]_PivotPosWS("_PivotPosWS", Vector) = (0,0,0,0)
        [HideInInspector]_BoundSize("_BoundSize", Vector) = (1,1,0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline"}

        Pass
        {
            Name "DeferredLit"
            Tags{"LightMode" = "Deferred"}

			Cull Off
			ZTest Less

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"
			#include "VegetationCommon.cginc"

            struct Attributes
            {
                float4 positionOS : POSITION;//草顶点的模型空间坐标
				float2 uv : TEXCOORD0;
				uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;//草顶点的裁剪空间坐标
				float2 uv : TEXCOORD0;
                float3 bakeGI : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
				sampler2D _MainTex;
				half4 _MainColor;
				half _Cutoff;

                float3 _PivotPosWS;
                float2 _BoundSize;

                float _GrassWidth;
                float _GrassHeight;

                float _WindAIntensity;
                float _WindAFrequency;
                float2 _WindATiling;
                float2 _WindAWrap;
                float3 _WindDirection;

				float2 _WindScatter;
				float _WindHightlightSpeed;
				float _WindHightlightIntensity;

				float _BendStrength;//按压弯曲程度

                StructuredBuffer<float3> _AllInstancesTransformBuffer;
                StructuredBuffer<uint> _AllVisibleInstancesIndexBuffer;
            CBUFFER_END

			SAMPLER(sampler_LinearClamp);
			SAMPLER(sampler_LinearRepeat);
			SAMPLER(sampler_TrilinearClamp);
			SAMPLER(sampler_TrilinearRepeat);
			SAMPLER(sampler_PointClamp);
			SAMPLER(sampler_PointRepeat);

			TEXTURE2D(_WindNoiseMap);

            Varyings vert(Attributes input)
            {
                Varyings output;

				uint index = _AllVisibleInstancesIndexBuffer[input.instanceID];
				float3 pivotPositionWS = _AllInstancesTransformBuffer[index];

				half3 direction = normalize(_WindDirection);
				half2 windTexcoord = (pivotPositionWS + input.positionOS).xz / _WindScatter;
				half2 windWindTexcoord = windTexcoord - direction.xz * _WindHightlightSpeed * _Time.x;
				half wind = SAMPLE_TEXTURE2D_LOD(_WindNoiseMap, sampler_LinearRepeat, windWindTexcoord, 0).r;

				float3 positionOS = input.positionOS;
				positionOS = ApplyRotationAndScale(positionOS, pivotPositionWS, _GrassWidth, _GrassHeight, _GrassWidth);
				positionOS = ApplyBending(positionOS.xyz, pivotPositionWS, _BendStrength);
				positionOS = ApplyWind(positionOS, pivotPositionWS, direction, _WindAFrequency, _WindATiling, _WindAWrap, _WindAIntensity + wind * _WindHightlightIntensity);
				
				output.positionCS = TransformWorldToHClip(positionOS + pivotPositionWS);
				output.uv = input.uv;
                output.bakeGI = SampleSH(float3(0,1,0));

				return output;
            }

			FragmentOutput frag(Varyings input)
			{
				half4 albedo = tex2D(_MainTex, input.uv);
				clip(albedo.a - _Cutoff);

				GbufferData data;
				data.albedo = albedo.xyz * _MainColor.xyz;
				data.normalWS = float3(0, 1, 0);
				data.specular = 0.5;
				data.metallic = 0;
				data.smoothness = 0.25;
				data.occlusion = 1;
				data.emission = input.bakeGI * data.albedo;

				return EncodeGbuffer(data);
            }
            ENDHLSL
        }
    }
}