Shader "Hidden/Universal Render Pipeline/Fog/MipFog"
{
	Properties
	{
		_MipFogMap("Texture", 2D) = "white" {}
		_MipFogParams("MipFogParams", Vector) = (1, 1, 1, 0.01)
		_MipFogParams2("MipFogParams2", Vector) = (0, 0, 0, 0)
	}

	HLSLINCLUDE
		#define InvPIE 0.318309886142f

		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

		struct Varyings
		{
			float2 uv : TEXCOORD0;
			float3 viewVec : TEXCOORD1;
			float4 vertex : SV_POSITION;
		};

#if	_MIPFOG_MAP
		TEXTURE2D(_MipFogMap);
		SAMPLER(sampler_MipFogMap);
#endif

		half4 _MipFogParams;
		half2 _MipFogParams2;
		half4 _HeightFogDeepColor;
		half4 _HeightFogShallowColor;
		half3 _HeightFogParams;

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
			normal = clamp(normal, -1.0, 1.0);
			float2 coord = float2((atan2(normal.x, normal.z) * InvPIE * 0.5f + 0.5f), 1.0f - acos(normal.y) * InvPIE);
			return coord;
		}

		Varyings FogVertex(uint id : SV_VERTEXID)
		{
			Varyings o;
			o.uv = float2(id / 2, id % 2) * 2;
			o.vertex = float4(o.uv * 2 - 1, 0, 1);

			#if UNITY_UV_STARTS_AT_TOP
				o.uv.y = 1 - o.uv.y;
				o.viewVec = mul(unity_CameraInvProjection, float4(o.vertex.x, -o.vertex.y, 0, 1)).xyz;
			#else
				o.viewVec = mul(unity_CameraInvProjection, float4(o.vertex.x, o.vertex.y, 0, 1)).xyz;
			#endif

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

#if _MIPFOG
			half mipLevel = (1 - saturate((linearDepth - _ProjectionParams.y) / (_ProjectionParams.z - _ProjectionParams.y))) * 6;
			half mipFogFactor = 1.0h - lerp(saturate(exp2(-linearDepth * _MipFogParams.w)), 1, step(mipLevel, 0.1h) * _MipFogParams2.y);
			half3 mipFogColor = _MipFogParams.xyz;

#	if _MIPFOG_MAP
			half3 normal = rotate(normalize(worldPosition), _MipFogParams2.x);
			mipFogColor *= pow(SAMPLE_TEXTURE2D_LOD(_MipFogMap, sampler_MipFogMap, SampleLatlong(normal), mipLevel).xyz, 1.0f / 2.2f);
#	endif
#endif

#if _HEIGHTFOG
#	if _HEIGHTFOG_CAMERA_HEIGHT
			float height = max(0, GetCameraPositionWS().y +  _HeightFogParams.z - worldPosition.y);
#	else
			float height = max(0, _HeightFogParams.z - worldPosition.y);
#	endif
			float heightFogFactor = saturate((1 - exp(-_HeightFogParams.y * height)) * _HeightFogParams.x);
			float3 heightFogColor = lerp(_HeightFogDeepColor.xyz, _HeightFogShallowColor, heightFogFactor);
#endif

#if _HEIGHTFOG & _MIPFOG
			float3 fogColor = lerp(mipFogColor, heightFogColor, heightFogFactor);
			return float4(fogColor, max(mipFogFactor, heightFogFactor));
#elif _HEIGHTFOG
			return float4(heightFogColor, heightFogFactor);
#elif _MIPFOG
			return float4(mipFogColor, mipFogFactor);
#else
			return 0;
#endif
		}
	ENDHLSL

	SubShader
	{
		Tags {"RenderPipeline" = "UniversalPipeline"}

		Pass
		{
			ZTest Off
			ZWrite Off
			Cull Off
			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
				#pragma vertex FogVertex
				#pragma fragment MipFogFragment
				#pragma multi_compile_local _ _MIPFOG
				#pragma multi_compile_local _ _MIPFOG_MAP
				#pragma multi_compile_local _ _HEIGHTFOG
				#pragma multi_compile_local _ _HEIGHTFOG_CAMERA_HEIGHT
			ENDHLSL
		}
	}
}