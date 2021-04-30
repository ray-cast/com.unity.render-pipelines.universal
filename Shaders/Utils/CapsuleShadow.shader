Shader "Hidden/Universal Render Pipeline/Lighting"
{
	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

		half4 _LightParams;
		half4 _ScaleBiasRT;

		half4 _ConeParams;
		half _AdditionalOccludersCount;
		float4 _AdditionalOccluderPosition[MAX_VISIBLE_LIGHTS];

		float Pow2(float x) {
			return x * x;
		}

		float acosFast(float x) {
			// Lagarde 2014, "Inverse trigonometric functions GPU optimization for AMD GCN architecture"
			// This is the approximation of degree 1, with a max absolute error of 9.0x10^-3
			float y = abs(x);
			float p = -0.1565827 * y + 1.570796;
			p *= sqrt(1.0 - y);
			return x >= 0.0 ? p : PI - p;
		}

		float acosFastPositive(float x) {
			// Lagarde 2014, "Inverse trigonometric functions GPU optimization for AMD GCN architecture"
			float p = -0.1565827 * x + 1.570796;
			return p * sqrt(1.0 - x);
		}

		float sphericalCapsIntersection(float cosCap1, float cosCap2, float cap2, float cosDistance) {
			// Oat and Sander 2007, "Ambient Aperture Lighting"
			// Approximation mentioned by Jimenez et al. 2016
			float r1 = acosFastPositive(cosCap1);
			float r2 = cap2;
			float d  = acosFast(cosDistance);

			// We work with cosine angles, replace the original paper's use of
			// cos(min(r1, r2)_ with max(cosCap1, cosCap2)
			// We also remove a multiplication by 2 * PI to simplify the computation
			// since we divide by 2 * PI at the call site

			if (min(r1, r2) <= max(r1, r2) - d) {
				return 1.0 - max(cosCap1, cosCap2);
			} else if (r1 + r2 <= d) {
				return 0.0;
			}

			float delta = abs(r1 - r2);
			float x = 1.0 - saturate((d - delta) / max(r1 + r2 - delta, 0.0001));
			// simplified smoothstep()
			float area = Pow2(x) * (-2.0 * x + 3.0);
			return area * (1.0 - max(cosCap1, cosCap2));
		}

		float directionalOcclusionSphere(float3 pos, float4 sphere, float4 cone) {
			float3 occluder = sphere.xyz - pos;
			float occluderLength2 = dot(occluder, occluder);
			float3 occluderDir = occluder * rsqrt(occluderLength2);

			float cosPhi = dot(occluderDir, cone.xyz);
			float cosTheta = sqrt(occluderLength2 / (Pow2(sphere.w) + occluderLength2));
			float cosCone = cos(cone.w);

			return 1.0 - sphericalCapsIntersection(cosTheta, cosCone, cone.w, cosPhi) / (1.0 - cosCone);
		}

		float directionalOcclusionCapsule(float3 pos, float3 capsuleA, float3 capsuleB, float capsuleRadius, float4 cone) {
			float3 Ld = capsuleB - capsuleA;
			float3 L0 = capsuleA - pos;
			float a = dot(cone.xyz, Ld);
			float t = saturate(dot(L0, a * cone.xyz - Ld) / (dot(Ld, Ld) - a * a));
			float3 posToRay = capsuleA + t * Ld;

			return directionalOcclusionSphere(pos, float4(posToRay, capsuleRadius), cone);
		}

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
			output.uv = float4(float2(id / 2, id % 2) * 2, 0, 1);
			output.positionCS = float4(output.uv.xy * 2 - 1, 0, 1);
			output.positionCS.y *= _ScaleBiasRT.x;

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

#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
			return float4(surface.emission, any(1 - deviceDepth));
#else
			return float4(surface.emission, any(deviceDepth));
#endif
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

#if _MAIN_LIGHT_SHADOWS
			float4 shadowCoord = TransformWorldToShadowCoord(worldPosition);
			Light mainLight = GetMainLight(shadowCoord);
#else
			Light mainLight = GetMainLight();
#endif

			float3 ambientOcclusion = 1;
			float3 lightOcclusion = 1;

			for (uint i = 0; i < uint(_AdditionalOccludersCount); i++)
				ambientOcclusion *= directionalOcclusionSphere(worldPosition, _AdditionalOccluderPosition[i], float4(n, _ConeParams.x));

			for (uint i = 0; i < uint(_AdditionalOccludersCount); i++)
				lightOcclusion *= directionalOcclusionSphere(worldPosition, _AdditionalOccluderPosition[i], float4(mainLight.direction, _ConeParams.y));

			float3 lighting = surface.emission * ambientOcclusion;
			lighting += LightingPhysicallyBased(brdfData, mainLight, n, v) * lightOcclusion;

#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
			return float4(lighting, any(1 - deviceDepth));
#else
			return float4(lighting, any(deviceDepth));
#endif
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
	}
}