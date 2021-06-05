Shader "Hidden/Universal Render Pipeline/ClusterLighting"
{
	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/CloudShadow.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

		struct Varyings
		{
			float4 positionCS   : SV_POSITION;
			float4 uv           : TEXCOORD0;
			float3 viewdir      : TEXCOORD1;
			UNITY_VERTEX_INPUT_INSTANCE_ID
			UNITY_VERTEX_OUTPUT_STEREO
		};

		Varyings ClusterLightingVertex(uint id : SV_VERTEXID)
		{
			Varyings output = (Varyings)0;
			output.uv = GetFullScreenTriangleTexCoord(id).xyxy;
			output.positionCS = GetFullScreenTriangleVertexPosition(id, UNITY_RAW_FAR_CLIP_VALUE);

			float4 hpositionWS = mul(unity_MatrixInvVP, ComputeClipSpacePosition(output.uv.xy, 1));
			hpositionWS /= hpositionWS.w;
			
			output.viewdir = GetCameraPositionWS() - hpositionWS.xyz;

			return output;
		}

		float4 ClusterLightingFragment(Varyings input) : SV_Target
		{
			GbufferData surface = SampleGbufferTextures(input.uv.xy);

			BRDFData brdfData;
			InitializeBRDFData(surface.albedo, surface.metallic, surface.specular, surface.smoothness, 1, brdfData);

			float3 n = surface.normalWS;
			float3 v = normalize(input.viewdir);

			float deviceDepth = SampleSceneDepth(input.uv.xy);
			float linearDepth = LinearEyeDepth(deviceDepth, _ZBufferParams);

#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
			deviceDepth = deviceDepth * 2 - 1;
#endif
			float3 worldPosition = ComputeWorldSpacePosition(input.uv.zw, deviceDepth, unity_MatrixInvVP);

			Light mainLight = GetMainLight();
			mainLight.distanceAttenuation = 1;
			mainLight.shadowAttenuation = SampleScreenSpaceShadowMap(input.uv.xy);

			float3 lighting = surface.emission;
#ifdef _CAPSULE_SHADOWS
			lighting *= SampleScreenSpaceOcclusionMap(input.uv.xy);
#endif
			lighting += LightingWrappedPhysicallyBased(brdfData, mainLight, n, v, surface.translucency);

			uint2 cullingLightIndex = GetCullingLightIndex(ComputeClusterClipSpacePosition(input.uv.xy), linearDepth);

			UNITY_LOOP
			for (uint i = 0; i < cullingLightIndex.y; i++)
			{
				Light light = GetClusterAdditionalLight(cullingLightIndex.x + i, worldPosition);
				lighting += LightingWrappedPhysicallyBased(brdfData, light, n, v, surface.translucency);
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

			float deviceDepth = SampleSceneDepth(input.uv.xy);
			float linearDepth = LinearEyeDepth(deviceDepth, _ZBufferParams);

#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
			deviceDepth = deviceDepth * 2 - 1;
#endif
			float3 worldPosition = ComputeWorldSpacePosition(input.uv.zw, deviceDepth, unity_MatrixInvVP);

			float3 lighting = surface.emission;
#ifdef _CAPSULE_SHADOWS
			lighting *= SampleScreenSpaceOcclusionMap(input.uv.xy);
#endif
			uint2 cullingLightIndex = GetCullingLightIndex(ComputeClusterClipSpacePosition(input.uv.xy), linearDepth);

			UNITY_LOOP
			for (uint i = 0; i < cullingLightIndex.y; i++)
			{
				Light light = GetClusterAdditionalLight(cullingLightIndex.x + i, worldPosition);
				lighting += LightingWrappedPhysicallyBased(brdfData, light, n, v, surface.translucency);
			}

			return float4(lighting, 1);
		}
	ENDHLSL

	SubShader
	{
		ZTest Greater ZWrite Off
		Blend Off
		Cull Off

		Pass
		{
			HLSLPROGRAM
                #pragma target 3.5
                #pragma prefer_hlslcc gles
                #pragma exclude_renderers d3d11_9x
                #pragma editor_sync_compilation

				#pragma vertex ClusterLightingVertex
				#pragma fragment ClusterLightingFragment

				#pragma multi_compile _ _CAPSULE_SHADOWS
				#pragma multi_compile _ _ADDITIONAL_LIGHTS
				#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
				#pragma multi_compile _ _SHADOWS_SOFT
				#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			ENDHLSL
		}
		Pass
		{
			HLSLPROGRAM
                #pragma target 3.5
                #pragma prefer_hlslcc gles
                #pragma exclude_renderers d3d11_9x
                #pragma editor_sync_compilation

				#pragma vertex ClusterLightingVertex
				#pragma fragment ClusterAdditionalLightingFragment

				#pragma multi_compile _ _CAPSULE_SHADOWS
				#pragma multi_compile _ _ADDITIONAL_LIGHTS
				#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
				#pragma multi_compile _ _SHADOWS_SOFT
				#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			ENDHLSL
		}
	}
}