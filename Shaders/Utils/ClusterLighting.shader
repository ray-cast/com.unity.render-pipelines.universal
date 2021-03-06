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
			float4 positionHCS : POSITION;
			float2 uv          : TEXCOORD0;
		};

		struct Varyings
		{
			float4 positionCS   : SV_POSITION;
			float4 uv           : TEXCOORD0;
			float3 viewdir      : TEXCOORD1;
		};

		Varyings vert(Attributes input)
		{
			Varyings output;

			output.uv = UnityStereoTransformScreenSpaceTex(input.uv).xyxy;
			
    		output.positionCS = float4(input.positionHCS.xyz, 1.0);
    		output.positionCS.y *= _ScaleBiasRT.x;

			float4 hpositionWS = mul(unity_MatrixInvVP, ComputeClipSpacePosition(input.uv, 1));
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
				#pragma vertex vert
				#pragma fragment ClusterLightingFragment

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