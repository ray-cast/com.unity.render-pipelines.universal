Shader "Hidden/Universal Render Pipeline/Lighting"
{
	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

		half4 _LightParams;
		half4 _ConeParams;
		half _AdditionalOccludersCount;
		float4 _AdditionalOccluderPosition[MAX_VISIBLE_LIGHTS];

        struct Attributes
        {
            float4 positionOS   : POSITION;
            float2 texcoord : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            half4  positionCS   : SV_POSITION;
            half2  uv           : TEXCOORD0;
            float3 viewdir      : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

		real SphericalCapsIntersection(real cosCap1, real cosCap2, real cap2, real cosDistance) {
			// Oat and Sander 2007, "Ambient Aperture Lighting"
			// Approximation mentioned by Jimenez et al. 2016
			real r1 = FastACosPos(cosCap1);
			real r2 = cap2;
			real d  = FastACos(cosDistance);

			if (min(r1, r2) <= max(r1, r2) - d) {
				return 1.0 - max(cosCap1, cosCap2);
			} else if (r1 + r2 <= d) {
				return 0.0;
			}

			real delta = abs(r1 - r2);
			real x = 1.0 - saturate((d - delta) / max(r1 + r2 - delta, 0.0001));
			real area = Smoothstep01(x);

			return area * (1.0 - max(cosCap1, cosCap2));
		}

		float DirectionalOcclusionSphere(float3 pos, float4 sphere, float4 cone)
		{
			float3 occluder = sphere.xyz - pos;
			float occluderLength2 = dot(occluder, occluder);
			float3 occluderDir = occluder * rsqrt(occluderLength2);

			float cosPhi = dot(occluderDir, cone.xyz);
			float cosTheta = sqrt(occluderLength2 / (sphere.w * sphere.w + occluderLength2));
			float cosCone = cos(cone.w);

			return 1.0 - SphericalCapsIntersection(cosTheta, cosCone, cone.w, cosPhi) / (1.0 - cosCone);
		}

		float DirectionalOcclusionCapsule(float3 pos, float3 capsuleA, float3 capsuleB, float capsuleRadius, float4 cone)
		{
			float3 Ld = capsuleB - capsuleA;
			float3 L0 = capsuleA - pos;
			float a = dot(cone.xyz, Ld);
			float t = saturate(dot(L0, a * cone.xyz - Ld) / (dot(Ld, Ld) - a * a));
			float3 posToRay = capsuleA + t * Ld;

			return DirectionalOcclusionSphere(pos, float4(posToRay, capsuleRadius), cone);
		}

        Varyings CapsuleShadowVertex(uint id : SV_VERTEXID)
        {
            Varyings output = (Varyings)0;
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.uv = GetFullScreenTriangleTexCoord(id);
            output.positionCS = GetFullScreenTriangleVertexPosition(id, UNITY_RAW_FAR_CLIP_VALUE);

            float4 screenPos = ComputeScreenPos(output.positionCS);
            float4 clipPos = float4(screenPos.xy / screenPos.w * 2 - 1, 1, 1);
            float4 clipVec = clipPos;
            output.viewdir = mul(unity_CameraInvProjection, clipVec).xyz;

            return output;
        }

		float4 CapsuleShadowFragment(Varyings input) : SV_Target
		{
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

			float3 n = lerp(SampleSceneGbufferNormal(input.uv.xy), _LightParams.xyz, _LightParams.w);
			float3 v = normalize(input.viewdir);

            float deviceDepth = SampleSceneDepth(input.uv.xy);
            float linearDepth = LinearEyeDepth(deviceDepth, _ZBufferParams);

            float3 worldPos = mul(UNITY_MATRIX_I_V, float4(input.viewdir * linearDepth, 1)).xyz;

			float ambientOcclusion = 1;

			for (uint i = 0; i < uint(_AdditionalOccludersCount); i++)
			{
				float4 capsuleA = _AdditionalOccluderPosition[i * 2];
				float4 capsuleB = _AdditionalOccluderPosition[i * 2 + 1];
				ambientOcclusion = min(ambientOcclusion, DirectionalOcclusionCapsule(worldPos, capsuleA.xyz, capsuleB.xyz, capsuleA.w, float4(n, _ConeParams.x)));
			}

			return LerpWhiteTo(ambientOcclusion, _ConeParams.y);
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

				#pragma vertex CapsuleShadowVertex
				#pragma fragment CapsuleShadowFragment
			ENDHLSL
		}
	}
}