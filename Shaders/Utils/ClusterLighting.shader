Shader "Hidden/Universal Render Pipeline/ClusterLighting"
{
	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthAttachment.hlsl"

		half4 _ScaleBiasRT;
		half4 _ScreenRect;

		struct Attributes
		{
			uint   id : SV_VERTEXID;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct Varyings
		{
			float4 positionCS   : SV_POSITION;
			float4 uv           : TEXCOORD0;
			float3 viewdir      : TEXCOORD1;
			UNITY_VERTEX_INPUT_INSTANCE_ID
			UNITY_VERTEX_OUTPUT_STEREO
		};

		Varyings ClusterLightingVertex(Attributes input)
		{
			Varyings output;
			UNITY_SETUP_INSTANCE_ID(input);
			UNITY_TRANSFER_INSTANCE_ID(input, output);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

			output.uv = float4(float2(input.id / 2, input.id % 2) * 2, 0, 1);
			output.positionCS = float4(output.uv.xy * 2 - 1, 0, 1);
			output.positionCS.y *= _ScaleBiasRT.x;
			
			float4 hpositionWS = mul(unity_MatrixInvVP, ComputeClipSpacePosition(output.uv, 1));
			hpositionWS /= hpositionWS.w;

			output.viewdir = GetCameraPositionWS() - hpositionWS.xyz;
			output.uv = UnityStereoTransformScreenSpaceTex(output.uv.xy).xyxy;

			return output;
		}

		float4 ClusterLightingFragment(Varyings input) : SV_Target
		{
			GbufferData surface = SampleGbufferTextures(input.uv.xy);

			BRDFData brdfData;
			InitializeBRDFData(surface.albedo, surface.metallic, surface.specular, surface.smoothness, 1, brdfData);

			float3 n = surface.normalWS;
			float3 v = normalize(input.viewdir);

			float deviceDepth = SampleDepthAttachment(input.uv.xy);
			float linearDepth = LinearEyeDepth(deviceDepth, _ZBufferParams);

#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
			deviceDepth = deviceDepth * 2 - 1;
#endif
			float3 worldPosition = ComputeWorldSpacePosition(input.uv.zw, deviceDepth, unity_MatrixInvVP);

			float3 lighting = surface.emission;

#if _MAIN_LIGHT_SHADOWS
			float4 shadowCoord = TransformWorldToShadowCoord(worldPosition);
			Light mainLight = GetMainLight(shadowCoord);
#else
			Light mainLight = GetMainLight();
#endif
			lighting += LightingPhysicallyBased(brdfData, mainLight, n, v);

			uint2 cullingLightIndex = GetCullingLightIndex(ComputeClusterClipSpacePosition(input.uv.xy), linearDepth);

			[loop]
			for (uint i = 0; i < cullingLightIndex.y; i++)
			{
				Light light = GetClusterAdditionalLight(cullingLightIndex.x + i, worldPosition);
				lighting += LightingPhysicallyBased(brdfData, light, n, v);
			}

			return float4(lighting, 1);
		}

		float4 ClusterAdditionalLightingFragment(Varyings input) : SV_Target
		{
			GbufferData surface = SampleGbufferTextures(input.uv.xy);

			BRDFData brdfData;
			InitializeBRDFData(surface.albedo, surface.metallic, surface.specular, surface.smoothness, 1, brdfData);

			float3 n = surface.normalWS;
			float3 v = normalize(input.viewdir);

			float deviceDepth = SampleDepthAttachment(input.uv.xy);
			float linearDepth = LinearEyeDepth(deviceDepth, _ZBufferParams);

#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
			deviceDepth = deviceDepth * 2 - 1;
#endif
			float3 worldPosition = ComputeWorldSpacePosition(input.uv.zw, deviceDepth, unity_MatrixInvVP);

			float3 lighting = surface.emission;

			uint2 cullingLightIndex = GetCullingLightIndex(ComputeClusterClipSpacePosition(input.uv.xy), linearDepth);

			[loop]
			for (uint i = 0; i < cullingLightIndex.y; i++)
			{
				Light light = GetClusterAdditionalLight(cullingLightIndex.x + i, worldPosition);
				lighting += LightingPhysicallyBased(brdfData, light, n, v);
			}

			return float4(lighting, 1);
		}
	ENDHLSL

	SubShader
	{
		ZTest Off ZWrite Off
		Cull Off

		Pass
		{
			HLSLPROGRAM
				#pragma vertex ClusterLightingVertex
				#pragma fragment ClusterLightingFragment

				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
				#pragma multi_compile _ _ADDITIONAL_LIGHTS
				#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
				#pragma multi_compile _ _SHADOWS_SOFT
				#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			ENDHLSL
		}
		Pass
		{
			HLSLPROGRAM
				#pragma vertex ClusterLightingVertex
				#pragma fragment ClusterAdditionalLightingFragment

				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
				#pragma multi_compile _ _ADDITIONAL_LIGHTS
				#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
				#pragma multi_compile _ _SHADOWS_SOFT
				#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			ENDHLSL
		}
	}
}