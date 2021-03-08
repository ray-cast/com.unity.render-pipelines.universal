Shader "Hidden/Universal Render Pipeline/Lighting"
{
	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

		half4 _LightParams;
		half4 _ScaleBiasRT;

		struct Attributes
		{
			float4 position : POSITION;
			float2 uv       : TEXCOORD0;
		};

		struct Varyings
		{
			float4 positionCS   : SV_POSITION;
			float4 uv           : TEXCOORD0;
			float3 viewdir      : TEXCOORD1;
		};

		Varyings TiledLightingVertex(Attributes input)
		{
			Varyings output;

			float4 hpositionWS = mul(unity_MatrixInvVP, ComputeClipSpacePosition(input.uv, 1));
			hpositionWS /= hpositionWS.w;

			output.uv = float4(UnityStereoTransformScreenSpaceTex(input.uv).xy, 0, 1);
			
			output.positionCS = float4(input.uv * 2 - 1, 0.0, 1.0);
			output.positionCS.y *= _ScaleBiasRT.x;

			output.viewdir = GetCameraPositionWS() - hpositionWS.xyz;

			return output;
		}

		Varyings VolumeLightingVertex(Attributes input)
		{
			Varyings output;
			
			VertexPositionInputs vertexInput = GetVertexPositionInputs(input.position * _LightParams.y);
			output.positionCS = vertexInput.positionCS;
			output.uv = ComputeScreenPos(vertexInput.positionCS);
			output.viewdir = GetCameraPositionWS() - vertexInput.positionWS.xyz;

			return output;
		}

		float4 MainLightingFragment(Varyings input) : SV_Target
		{
			GbufferData surface = SampleGbufferTextures(input.uv.xy);

			BRDFData brdfData;
			InitializeBRDFData(surface.albedo, surface.metallic, surface.specular, surface.smoothness, 1, brdfData);

			float3 n = surface.normalWS;
			float3 v = normalize(input.viewdir);

			float deviceDepth = SampleSceneDepth(input.uv.xy);
#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
			deviceDepth = 2 * deviceDepth - 1;
#endif
			float3 worldPosition = ComputeWorldSpacePosition(input.uv.xy, deviceDepth, unity_MatrixInvVP);

			float3 lighting = surface.emission;

#if _MAIN_LIGHT_SHADOWS
			float4 shadowCoord = TransformWorldToShadowCoord(worldPosition);
			Light mainLight = GetMainLight(shadowCoord);
#else
			Light mainLight = GetMainLight();
#endif
			lighting += LightingPhysicallyBased(brdfData, mainLight, n, v);

			return float4(lighting, 1);
		}

		float4 AdditionalLightingFragment(Varyings input) : SV_Target
		{
			input.uv = input.uv / input.uv.w;

			GbufferData surface = SampleGbufferTextures(input.uv.xy);

			BRDFData brdfData;
			InitializeBRDFData(surface.albedo, surface.metallic, surface.specular, surface.smoothness, 1, brdfData);

			float3 n = surface.normalWS;
			float3 v = normalize(input.viewdir);

			float deviceDepth = SampleSceneDepth(input.uv.xy);
#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
            deviceDepth = 2 * deviceDepth - 1;
#endif
			float3 worldPosition = ComputeWorldSpacePosition(input.uv.xy, deviceDepth, unity_MatrixInvVP);

			Light light = GetAdditionalPerObjectLight(uint(_LightParams.x), worldPosition);
			float3 lighting = LightingPhysicallyBased(brdfData, light, n, v);

			return float4(lighting, 0);
		}
	ENDHLSL

	SubShader
	{
		Pass
		{
			ZTest Off ZWrite Off
			Cull Off

			HLSLPROGRAM
				#pragma vertex TiledLightingVertex
				#pragma fragment MainLightingFragment

				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
				#pragma multi_compile _ _SHADOWS_SOFT
				#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			ENDHLSL
		}
		Pass
		{
			ZTest Off ZWrite Off
			Cull Off
			Blend One One

			HLSLPROGRAM
				#pragma vertex TiledLightingVertex
				#pragma fragment AdditionalLightingFragment

				#pragma multi_compile _ _SHADOWS_SOFT
				#pragma multi_compile _ _ADDITIONAL_LIGHTS
				#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
				#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			ENDHLSL
		}
		Pass
		{
			// inside volume
			ZTest Greater ZWrite Off
			Cull Front
			Blend One One

			HLSLPROGRAM
				#pragma vertex VolumeLightingVertex
				#pragma fragment AdditionalLightingFragment

				#pragma multi_compile _ _SHADOWS_SOFT
				#pragma multi_compile _ _ADDITIONAL_LIGHTS
				#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
				#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			ENDHLSL
		}
		Pass
		{
			// outside volume
			ZTest Lequal ZWrite Off
			Cull Back
			Blend One One

			HLSLPROGRAM
				#pragma vertex VolumeLightingVertex
				#pragma fragment AdditionalLightingFragment

				#pragma multi_compile _ _SHADOWS_SOFT
				#pragma multi_compile _ _ADDITIONAL_LIGHTS
				#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
				#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			ENDHLSL
		}
	}
}