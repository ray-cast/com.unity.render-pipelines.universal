Shader "Hidden/Universal Render Pipeline/Lighting"
{
	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

		half4 _LightParams;

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

		Varyings TiledLightingVertex(uint id : SV_VERTEXID)
		{
			Varyings output = (Varyings)0;
			output.uv = float4(GetFullScreenTriangleTexCoord(id), 0, 1);
			output.positionCS = GetFullScreenTriangleVertexPosition(id, UNITY_RAW_FAR_CLIP_VALUE);

			float4 hpositionWS = mul(unity_MatrixInvVP, ComputeClipSpacePosition(output.uv.xy, 1));
			hpositionWS /= hpositionWS.w;
			
			output.viewdir = GetCameraPositionWS() - hpositionWS.xyz;

			return output;
		}

		Varyings VolumeLightingVertex(Attributes input)
		{
			Varyings output;
			
			VertexPositionInputs vertexInput = GetVertexPositionInputs(input.position.xyz * _LightParams.y);
			output.positionCS = vertexInput.positionCS;
			output.uv = ComputeScreenPos(vertexInput.positionCS);
			output.viewdir = GetCameraPositionWS() - vertexInput.positionWS.xyz;

			return output;
		}

		float4 EmissionLightingFragment(Varyings input) : SV_Target
		{
			GbufferData surface = SampleGbufferTextures(input.uv.xy);

			BRDFData brdfData;
			InitializeBRDFData(surface.albedo, surface.metallic, surface.specular, surface.smoothness, 1, brdfData);

			float3 n = surface.normalWS;
			float3 v = normalize(input.viewdir);

			float deviceDepth = SampleSceneDepth(input.uv.xy);
#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
			float3 worldPosition = ComputeWorldSpacePosition(input.uv.xy, deviceDepth * 2 - 1, unity_MatrixInvVP);
#else
			float3 worldPosition = ComputeWorldSpacePosition(input.uv.xy, deviceDepth, unity_MatrixInvVP);
#endif

#ifdef _ENVIRONMENT_OCCLUSION
			surface.emission *= GTAOMultiBounce(SampleScreenSpaceOcclusionMap(input.uv.xy), surface.albedo);
#endif
#ifdef _CAPSULE_SHADOWS
			surface.emission *= SampleCapsuleShadowMap(input.uv.xy);
#endif

			return float4(surface.emission, 1);
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
			float3 worldPosition = ComputeWorldSpacePosition(input.uv.xy, deviceDepth * 2 - 1, unity_MatrixInvVP);
#else
			float3 worldPosition = ComputeWorldSpacePosition(input.uv.xy, deviceDepth, unity_MatrixInvVP);
#endif

			Light mainLight = GetMainLight();
			mainLight.distanceAttenuation = 1;
			mainLight.shadowAttenuation = SampleScreenSpaceShadowMap(input.uv.xy);

			float3 lighting = surface.emission;
#ifdef _CAPSULE_SHADOWS
			lighting *= SampleCapsuleShadowMap(input.uv.xy);
#endif
#ifdef _ENVIRONMENT_OCCLUSION
			float occlusion = SampleScreenSpaceOcclusionMap(input.uv.xy);
			lighting *= GTAOMultiBounce(occlusion, surface.albedo);
			mainLight.shadowAttenuation *= ComputeMicroShadowing(occlusion, abs(dot(mainLight.direction, n)), 0.5);
#endif
			lighting += LightingWrappedPhysicallyBased(brdfData, mainLight, n, v, surface.translucency);

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
			float3 lighting = LightingWrappedPhysicallyBased(brdfData, light, n, v, surface.translucency);

			return float4(lighting, 0);
		}
	ENDHLSL

	SubShader
	{
		Pass
		{
			// Emission Only
			ZTest Greater ZWrite Off
			Blend Off
			Cull Off

			HLSLPROGRAM
                #pragma target 3.5
                #pragma prefer_hlslcc gles
                #pragma exclude_renderers d3d11_9x
                #pragma editor_sync_compilation

				#pragma vertex TiledLightingVertex
				#pragma fragment EmissionLightingFragment

				#pragma multi_compile _ _CAPSULE_SHADOWS
				#pragma multi_compile _ _ENVIRONMENT_OCCLUSION
			ENDHLSL
		}
		Pass
		{
			// Main Lighting
			ZTest Greater ZWrite Off
			Blend Off
			Cull Off

			HLSLPROGRAM
                #pragma target 3.5
                #pragma prefer_hlslcc gles
                #pragma exclude_renderers d3d11_9x
                #pragma editor_sync_compilation

				#pragma vertex TiledLightingVertex
				#pragma fragment MainLightingFragment

				#pragma multi_compile _ _CAPSULE_SHADOWS
				#pragma multi_compile _ _ENVIRONMENT_OCCLUSION
				#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			ENDHLSL
		}
		Pass
		{
			// Additional Directional Light
			ZTest Greater ZWrite Off
			Cull Off
			Blend One One

			HLSLPROGRAM
                #pragma target 3.5
                #pragma prefer_hlslcc gles
                #pragma exclude_renderers d3d11_9x
                #pragma editor_sync_compilation

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
			// Inside Volume Lighting
			ZTest Greater ZWrite Off
			Cull Front
			Blend One One

			HLSLPROGRAM
                #pragma target 3.5
                #pragma prefer_hlslcc gles
                #pragma exclude_renderers d3d11_9x
                #pragma editor_sync_compilation

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
			// Outside Volume Lighting
			ZTest Lequal ZWrite Off
			Cull Back
			Blend One One

			HLSLPROGRAM
                #pragma target 3.5
                #pragma prefer_hlslcc gles
                #pragma exclude_renderers d3d11_9x
                #pragma editor_sync_compilation

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