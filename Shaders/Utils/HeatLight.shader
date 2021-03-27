Shader "Hidden/Universal Render Pipeline/HeatMap"
{
	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthAttachment.hlsl"

		half _MaxVisibleAdditionalLights;
		half4 _ScaleBiasRT;

		struct Attributes
		{
			float4 positionHCS : POSITION;
			float2 uv          : TEXCOORD0;
		};

		struct Varyings
		{
			float4 positionCS   : SV_POSITION;
			float2 uv           : TEXCOORD0;
		};

		half3 HeatMap(float greyValue)
		{
			half3 heat;
			heat.r = smoothstep(0.5, 0.8, greyValue);
			if(greyValue >= 0.90) {
				heat.r *= (1.1 - greyValue) * 5.0;
			}
			if(greyValue > 0.7) {
				heat.g = smoothstep(1.0, 0.7, greyValue);
			} else {
				heat.g = smoothstep(0.0, 0.7, greyValue);
			}
			heat.b = smoothstep(1.0, 0.0, greyValue);
			if(greyValue <= 0.3) {
				heat.b *= greyValue / 0.3;
			}
			return heat;
		}

		Varyings vert(uint id : SV_VERTEXID)
		{
			Varyings output = (Varyings)0;
			output.uv = float2(id / 2, id % 2) * 2;
			output.positionCS = float4(output.uv * 2 - 1, 0, 1);
			output.positionCS.y *= _ScaleBiasRT.x;
			return output;
		}

		float4 HeatLightFragment(Varyings input) : SV_Target
		{
			float depth = SampleDepthAttachment(input.uv.xy);
			float linearDepth = LinearEyeDepth(depth, _ZBufferParams);

			uint2 cullingLightIndex = GetCullingLightIndex(ComputeClusterClipSpacePosition(input.uv.xy), linearDepth);
			float weight = lerp(0.05, 1.0, saturate(half(cullingLightIndex.y) / GetPerClusterLightsLimit()));

			if (cullingLightIndex.y  <= GetPerClusterLightsLimit())
				return float4(HeatMap(weight), 0.5);
			else
				return float4(1, 0, 1, 0.5);
		}
	ENDHLSL

	SubShader
	{
		ZTest Off ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha
		Cull Off

		Pass
		{
			HLSLPROGRAM
				#pragma vertex vert
				#pragma fragment HeatLightFragment

				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
				#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
				#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
				#pragma multi_compile _ _SHADOWS_SOFT
				#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			ENDHLSL
		}
	}
}