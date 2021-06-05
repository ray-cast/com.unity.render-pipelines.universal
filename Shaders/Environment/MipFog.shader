Shader "Hidden/Universal Render Pipeline/Fog/MipFog"
{
	Properties
	{
		_MipFogMap("Texture", 2D) = "white" {}
		_MipFogParams("MipFogParams", Vector) = (1, 1, 1, 0)
		_MipFogFactorParams("MipFogFactorParams", Vector) = (0, 0, 0, 0)
	}

	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

		struct Varyings
		{
			float2 uv      : TEXCOORD0;
			float3 viewdir : TEXCOORD1;
			float4 vertex  : SV_POSITION;
		};

#if	_MIPFOG_MAP
		TEXTURE2D(_MipFogMap);
		SAMPLER(sampler_MipFogMap);
#endif

		half4 _MipFogParams;
		half4 _MipFogFactorParams;

		half3 rotate(half3 normal, half theta)
		{
			half c = cos(theta);
			half s = sin(theta);

			half3x3 m;
			m[0] = half3(c, 0, -s);
			m[1] = half3(0, 1, 0);
			m[2] = half3(s, 0, c);

			return mul(normal, m);
		}

		float2 SampleLatlong(float3 normal)
		{
			static float InvPIE = 0.318309886142f;
			normal = clamp(normal, -1.0, 1.0);
			float2 coord = float2((atan2(normal.x, normal.z) * InvPIE * 0.5f + 0.5f), 1.0f - acos(normal.y) * InvPIE);
			return coord;
		}

		real ComputeMipFogFactor(float z)
		{
		#if defined(_FOG_LINEAR)
		    // factor = (end-z)/(end-start) = z * (-1/(end-start)) + (end/(end-start))
		    float fogFactor = saturate(z * _MipFogFactorParams.z + _MipFogFactorParams.w);
		    return real(fogFactor);
		#elif defined(_FOG_EXP) || defined(_FOG_EXP2)
		    // factor = exp(-(density*z)^2)
		    // -density * z computed at vertex
		    return real(z * _MipFogFactorParams.x);
		#else
		    return 0.0h;
		#endif
		}

		real ComputeMipFogIntensity(real fogFactor)
		{
		    real fogIntensity = 0.0h;
		#if defined(_FOG_LINEAR) || defined(_FOG_EXP) || defined(_FOG_EXP2)
		#if defined(_FOG_EXP)
		    fogIntensity = saturate(exp2(-fogFactor));
		#elif defined(_FOG_EXP2)
		    fogIntensity = saturate(exp2(-fogFactor * fogFactor));
		#elif defined(_FOG_LINEAR)
		    fogIntensity = fogFactor;
		#endif
		#endif
		    return fogIntensity;
		}

		Varyings MipFogVertex(uint id : SV_VERTEXID)
		{
			Varyings o;
			o.uv = GetFullScreenTriangleTexCoord(id);
			o.vertex = GetFullScreenTriangleVertexPosition(id);

			float4 hpositionWS = mul(unity_MatrixInvVP, ComputeClipSpacePosition(o.uv, 1));
			hpositionWS /= hpositionWS.w;
			
			o.viewdir = GetCameraPositionWS() - hpositionWS.xyz;

			return o;
		}

		float4 MipFogFragment(Varyings i) : SV_Target
		{		
			float deviceDepth = SampleSceneDepth(i.uv);
			float linearDepth = LinearEyeDepth(deviceDepth, _ZBufferParams);

#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
			deviceDepth = deviceDepth * 2 - 1;
#endif
			float3 worldPosition = ComputeWorldSpacePosition(i.uv, deviceDepth, unity_MatrixInvVP);

			real fogFactor = ComputeMipFogFactor(linearDepth);
			real fogIntensity = ComputeMipFogIntensity(fogFactor);

			real mipFogFactor = 1.0h - fogIntensity;

			UNITY_BRANCH
			if (deviceDepth == UNITY_RAW_FAR_CLIP_VALUE)
				mipFogFactor = 1.0h - lerp(fogIntensity, 1.0h, _MipFogFactorParams.y);

			real3 mipFogColor = _MipFogParams.xyz;

#	if _MIPFOG_MAP
			real3 normal = rotate(normalize(worldPosition), _MipFogParams.w);
			real mipLevel = (1 - saturate((linearDepth - _ProjectionParams.y) / (_ProjectionParams.z - _ProjectionParams.y))) * 6;
			mipFogColor *= pow(SAMPLE_TEXTURE2D_LOD(_MipFogMap, sampler_MipFogMap, SampleLatlong(normal), mipLevel).xyz, 1.0h / 2.2h);
#	endif

			return float4(mipFogColor, mipFogFactor);
		}
	ENDHLSL

	SubShader
	{
		Tags {"RenderPipeline" = "UniversalPipeline"}

		Pass
		{
			ZTest Off ZWrite Off
			Cull Off
			Blend SrcAlpha OneMinusSrcAlpha, Zero One

			HLSLPROGRAM
                #pragma target 3.5
                #pragma prefer_hlslcc gles
                #pragma exclude_renderers d3d11_9x
                #pragma editor_sync_compilation

				#pragma vertex MipFogVertex
				#pragma fragment MipFogFragment

				#pragma multi_compile_local _ _FOG_LINEAR _FOG_EXP _FOG_EXP2
				#pragma multi_compile_local _ _MIPFOG_MAP
			ENDHLSL
		}
	}
}