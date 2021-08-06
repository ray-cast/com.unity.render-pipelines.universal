Shader "Hidden/Universal Render Pipeline/ScreenSpaceShadows"
{
	SubShader
	{
		HLSLINCLUDE

		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/PerObjectShadows.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/CloudShadow.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

		#define SHADOW_BLUR_COUNT 3

		TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

		float2 _Offset;

		struct Attributes
		{
			float4 positionOS   : POSITION;
			float2 texcoord : TEXCOORD0;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct Varyings
		{
			float4 positionCS   : SV_POSITION;
			float2 uv           : TEXCOORD0;
			float3 viewdir      : TEXCOORD1;
			UNITY_VERTEX_OUTPUT_STEREO
		};

		Varyings MainShadowVertex(uint id : SV_VERTEXID)
		{
			Varyings output = (Varyings)0;
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

			output.uv = GetFullScreenTriangleTexCoord(id);
			output.positionCS = GetFullScreenTriangleVertexPosition(id, UNITY_RAW_FAR_CLIP_VALUE);

			float4 screenPos = ComputeScreenPos(output.positionCS);
			float4 clipPos = float4(screenPos.xy / screenPos.w * 2 - 1, 1, 1);
			output.viewdir = mul(unity_CameraInvProjection, clipPos).xyz;

			return output;
		}

		half4 MainShadowFragment(Varyings input) : SV_Target
		{
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

			float deviceDepth = SampleSceneDepth(input.uv);
			float linearDepth = LinearEyeDepth(deviceDepth, _ZBufferParams);

		#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
			float3 worldPos = ComputeWorldSpacePosition(input.uv, deviceDepth * 2 - 1, unity_MatrixInvVP);
		#else
			float3 worldPos = mul(UNITY_MATRIX_I_V, float4(input.viewdir * linearDepth, 1)).xyz;
		#endif

			float4 weights;
			float4 anotherWeights;
			float4 distances2 = ComputeCascadeWeightsSplit(worldPos, weights, anotherWeights);

			float shadowRandom = InterleavedGradientNoise(input.positionCS.xy, 0);
			float shadowAttenuation = MainLightRealtimeShadow(TransformWorldToShadowCoord(worldPos, weights), shadowRandom);

		#ifdef _MAIN_LIGHT_SHADOWS_CASCADE
			UNITY_BRANCH
			if (anotherWeights.y != 0)
			{
				float blendFactor = saturate(distances2.x / _CascadeShadowSplitSphereRadii.x * 2 - 1);
				float4 shadowCoord2 = TransformWorldToShadowCoord(worldPos, anotherWeights);
				shadowAttenuation = lerp(shadowAttenuation, MainLightRealtimeShadow(shadowCoord2, shadowRandom), blendFactor);
			}
		#endif

		#ifdef _MAIN_LIGHT_PER_OBJECT_SHADOWS
			uint objectShadowCount = GetPerObjectShadowsCount();
			for (uint shadowIndex = 0u; shadowIndex < objectShadowCount; ++shadowIndex)
			{
				shadowAttenuation *= PerObjectRealtimeShadow(shadowIndex, worldPos, 0);
			}
		#endif

		#ifdef _MAIN_LIGHT_CLOUD_SHADOWS
			float random = InterleavedGradientNoise(input.positionCS.xy, _Frame.x);
			shadowAttenuation *= GetCloudShadow(worldPos);
			shadowAttenuation += lerp(-0.5, 0.5, random) / 255.f;
			shadowAttenuation = saturate(shadowAttenuation);
		#endif

			return shadowAttenuation;
		}

		real BilateralWeight(real r, float depth, float center_d, real sigma, real sharpness)
		{
			const real blurSigma = sigma * 0.5f;
			const real blurFalloff = 1.0f / (2.0f * blurSigma * blurSigma);
			real ddiff = (depth - center_d) * sharpness;
			return exp(-r * r * blurFalloff - ddiff * ddiff);
		}

		Varyings BlurVertex(uint id : SV_VERTEXID)
		{
			Varyings output = (Varyings)0;
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

			output.uv = GetFullScreenTriangleTexCoord(id);
			output.positionCS = GetFullScreenTriangleVertexPosition(id, UNITY_RAW_FAR_CLIP_VALUE);

			return output;
		}

		half4 BlurFragment(Varyings input) : SV_Target
		{
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

			real totalWeight = 1;
			real totalColor = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv).r;

			real2 offset1 = input.uv + _Offset;
			real2 offset2 = input.uv - _Offset;

			float center_d = LinearEyeDepth(SampleSceneDepth(input.uv.xy), _ZBufferParams);

			UNITY_UNROLL
			for(int r = 1; r < SHADOW_BLUR_COUNT; r++)
			{
				float depth1 = LinearEyeDepth(SampleSceneDepth(offset1).r, _ZBufferParams);
				float depth2 = LinearEyeDepth(SampleSceneDepth(offset2).r, _ZBufferParams);

				real shadow1 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, offset1).r;
				real shadow2 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, offset2).r;

				real bilateralWeight1 = BilateralWeight(r, depth1, center_d, SHADOW_BLUR_COUNT, 10);
				real bilateralWeight2 = BilateralWeight(r, depth2, center_d, SHADOW_BLUR_COUNT, 10);

				totalColor += shadow1 * bilateralWeight1;
				totalColor += shadow2 * bilateralWeight2;

				totalWeight += bilateralWeight1;
				totalWeight += bilateralWeight2;

				offset1 += _Offset;
				offset2 -= _Offset;
			}

			return totalColor / totalWeight + lerp(-0.5, 0.5, InterleavedGradientNoise(input.positionCS.xy, _Frame.x)) / 255.f;
		}

		ENDHLSL

		Pass
		{
			Name "ScreenSpaceShadows"

			ZTest Greater ZWrite Off
			Blend Off
			Cull Off

			HLSLPROGRAM
				#pragma target 3.5
				#pragma prefer_hlslcc gles
				#pragma exclude_renderers d3d11_9x
				#pragma editor_sync_compilation

				#pragma vertex   MainShadowVertex
				#pragma fragment MainShadowFragment

				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
				#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
				#pragma multi_compile _ _MAIN_LIGHT_CLOUD_SHADOWS
				#pragma multi_compile _ _MAIN_LIGHT_PER_OBJECT_SHADOWS
				#pragma multi_compile _ _SHADOWS_SOFT
			ENDHLSL
		}

		Pass
		{
			Name "Bilateral Filtering"

			ZTest Greater ZWrite Off
			Blend Off
			Cull Off

			HLSLPROGRAM
				#pragma target 3.5
				#pragma prefer_hlslcc gles
				#pragma exclude_renderers d3d11_9x
				#pragma editor_sync_compilation

				#pragma vertex   BlurVertex
				#pragma fragment BlurFragment
			ENDHLSL
		}
	}
}
