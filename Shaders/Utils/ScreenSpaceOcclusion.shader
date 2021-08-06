Shader "Hidden/Universal Render Pipeline/Lighting"
{
	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

		#define SSDO_SAMPLER_COUNT 16
		#define SSDO_SAMPLE_BLUR_COUNT 3

		TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

		half4 _SSDO_BlurParams;
		half4 _SSDO_SampleParams;
		half4 _SSDO_SampleParams2;

		struct Attributes
		{
			float4 positionOS  : POSITION;
			float2 texcoord    : TEXCOORD0;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct Varyings
		{
			half4  positionCS   : SV_POSITION;
			half2  uv           : TEXCOORD0;
			float3 viewdir      : TEXCOORD1;
			UNITY_VERTEX_OUTPUT_STEREO
		};

		float GetJitterOffset(float2 uv)
		{
			return dot(frac(uv * 0.25), float2(1.0, 0.25));
		}

		Varyings ScreenSpaceOcclusionVertex(uint id : SV_VERTEXID)
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

		float4 ScreenSpaceOcclusionFragment(Varyings input) : SV_Target
		{
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

			float deviceDepth = SampleSceneDepth(input.uv.xy);
			float linearDepth = LinearEyeDepth(deviceDepth, _ZBufferParams);
			
		#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
			float3 worldPos = ComputeWorldSpacePosition(input.uv.xy, deviceDepth * 2 - 1, unity_MatrixInvVP);
		#else
			float3 worldPos = mul(UNITY_MATRIX_I_V, float4(input.viewdir * linearDepth, 1)).xyz;
		#endif

			float3 worldNormal = SampleSceneGbufferNormal(input.uv.xy);
			if (dot(worldNormal, 1) < 0.001f)
				worldNormal = normalize(cross(ddy(worldPos),ddx(worldPos)));

			real2 maxRadius = clamp(_SSDO_SampleParams.z / linearDepth, _SSDO_SampleParams.xy, _SSDO_SampleParams.xy * 100);
			if (floor(fmod(input.positionCS.x, 2)) > 0) maxRadius *= 0.5;
			if (floor(fmod(input.positionCS.y, 2)) > 0) maxRadius *= 0.5;

			real2 sampleRotate[SSDO_SAMPLER_COUNT];
			real2 sampleRadius = maxRadius;
			real4 sampleOcclustion = 0.0f;

			real2 sampleSinCos = 0;
			sincos(GetJitterOffset(input.positionCS) * PI * 2, sampleSinCos.y, sampleSinCos.x);

			real2x2 sampleRotMat = { sampleSinCos.y, sampleSinCos.x, -sampleSinCos.x, sampleSinCos.y };

			UNITY_UNROLL
			for (int i = 0; i < SSDO_SAMPLER_COUNT; i+=4)
			{
				sampleRotate[i + 0] = mul(PoissonDisk[i + 0], sampleRotMat) * sampleRadius;
				sampleRotate[i + 1] = mul(PoissonDisk[i + 1], sampleRotMat) * sampleRadius;
				sampleRotate[i + 2] = mul(PoissonDisk[i + 2], sampleRotMat) * sampleRadius;
				sampleRotate[i + 3] = mul(PoissonDisk[i + 3], sampleRotMat) * sampleRadius;
			}

			UNITY_UNROLL
			for (int j = 0; j < SSDO_SAMPLER_COUNT; j+=4)
			{
				real2 sampleOffset[4];
				sampleOffset[0] = input.uv.xy + sampleRotate[j + 0];
				sampleOffset[1] = input.uv.xy + sampleRotate[j + 1];
				sampleOffset[2] = input.uv.xy + sampleRotate[j + 2];
				sampleOffset[3] = input.uv.xy + sampleRotate[j + 3];

				float4 sampleDepths;
				sampleDepths[0] = SampleSceneDepth(sampleOffset[0]);
				sampleDepths[1] = SampleSceneDepth(sampleOffset[1]);
				sampleDepths[2] = SampleSceneDepth(sampleOffset[2]);
				sampleDepths[3] = SampleSceneDepth(sampleOffset[3]);

			#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
				sampleDepths = sampleDepths * 2 - 1;
			#endif

				real3 samplePosition[4];
				samplePosition[0] = ComputeWorldSpacePosition(sampleOffset[0], sampleDepths[0], unity_MatrixInvVP);
				samplePosition[1] = ComputeWorldSpacePosition(sampleOffset[1], sampleDepths[1], unity_MatrixInvVP);
				samplePosition[2] = ComputeWorldSpacePosition(sampleOffset[2], sampleDepths[2], unity_MatrixInvVP);
				samplePosition[3] = ComputeWorldSpacePosition(sampleOffset[3], sampleDepths[3], unity_MatrixInvVP);

				real3 sampleDirection[4];
				sampleDirection[0] = samplePosition[0] - worldPos;
				sampleDirection[1] = samplePosition[1] - worldPos;
				sampleDirection[2] = samplePosition[2] - worldPos;
				sampleDirection[3] = samplePosition[3] - worldPos;

				real4 sampleLength2 = real4(
					dot(sampleDirection[0], sampleDirection[0]),
					dot(sampleDirection[1], sampleDirection[1]),
					dot(sampleDirection[2], sampleDirection[2]),
					dot(sampleDirection[3], sampleDirection[3])
				);

				real4 sampleLengthInv = rsqrt(sampleLength2);

				sampleDirection[0] *= sampleLengthInv[0];
				sampleDirection[1] *= sampleLengthInv[1];
				sampleDirection[2] *= sampleLengthInv[2];
				sampleDirection[3] *= sampleLengthInv[3];

				real4 sampleAngle = real4(
					dot(sampleDirection[0], worldNormal),
					dot(sampleDirection[1], worldNormal),
					dot(sampleDirection[2], worldNormal),
					dot(sampleDirection[3], worldNormal));

				real emitterScale = 2.5;
				real emitterRadius = sampleRadius.x * linearDepth * 2;
				real emitterArea = (emitterScale * emitterRadius * emitterRadius) * PI * 0.25;

				real4 sh = emitterArea * saturate(sampleAngle - _SSDO_SampleParams2.x) / (sampleLength2 + emitterArea);

				sampleOcclustion += dot(sh, 1);
			}

			sampleOcclustion /= SSDO_SAMPLER_COUNT;

			return pow(1 - sampleOcclustion.w, _SSDO_SampleParams.w * 2.2);
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

			real2 offset1 = input.uv + _SSDO_BlurParams.xy;
			real2 offset2 = input.uv - _SSDO_BlurParams.xy;

			float center_d = LinearEyeDepth(SampleSceneDepth(input.uv.xy), _ZBufferParams);

			UNITY_UNROLL
			for(int r = 1; r < SSDO_SAMPLE_BLUR_COUNT; r++)
			{
				float depth1 = LinearEyeDepth(SampleSceneDepth(offset1).r, _ZBufferParams);
				float depth2 = LinearEyeDepth(SampleSceneDepth(offset2).r, _ZBufferParams);

				float shadow1 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, offset1).r;
				float shadow2 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, offset2).r;

				real bilateralWeight1 = BilateralWeight(r, depth1, center_d, SSDO_SAMPLE_BLUR_COUNT, _SSDO_BlurParams.z);
				real bilateralWeight2 = BilateralWeight(r, depth2, center_d, SSDO_SAMPLE_BLUR_COUNT, _SSDO_BlurParams.z);

				totalColor += shadow1 * bilateralWeight1;
				totalColor += shadow2 * bilateralWeight2;

				totalWeight += bilateralWeight1;
				totalWeight += bilateralWeight2;

				offset1 += _SSDO_BlurParams.xy;
				offset2 -= _SSDO_BlurParams.xy;
			}

			return totalColor / totalWeight;
		}
	ENDHLSL

	SubShader
	{
		Pass
		{
			ZTest Greater ZWrite Off
			Blend Off
			Cull Off

			HLSLPROGRAM
				#pragma target 3.5
				#pragma prefer_hlslcc gles
				#pragma exclude_renderers d3d11_9x
				#pragma editor_sync_compilation

				#pragma vertex ScreenSpaceOcclusionVertex
				#pragma fragment ScreenSpaceOcclusionFragment
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