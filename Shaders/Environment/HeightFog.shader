Shader "Hidden/Universal Render Pipeline/Fog/HeightFog"
{
	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VolumeRendering.hlsl"

		struct Varyings
		{
			float2 uv      : TEXCOORD0;
			float3 viewdir : TEXCOORD1;
			float4 vertex  : SV_POSITION;
		};

		half4 _HeightFogColor;
		half4 _HeightFogParams;

		#define _HeightFogDensity _HeightFogColor.w
		#define _HeightFogExponents _HeightFogParams.xy
		#define _HeightFogBaseExtinction _HeightFogParams.z
		#define _HeightFogBaseHeight _HeightFogParams.w

		static float _MaxDistance = 5000;

#if	_MIPFOG_MAP
		float4 _SkyMipParams;
		TEXTURE2D(_SkyMipTexture);
		SAMPLER(sampler_SkyMipTexture);
#endif

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

		float3 GetCurrentViewPosition()
		{
		    return UNITY_MATRIX_I_V._14_24_34;
		}

		Varyings HeightFogVertex(uint id : SV_VERTEXID)
		{
			Varyings o;
			o.uv = GetFullScreenTriangleTexCoord(id);
			o.vertex = GetFullScreenTriangleVertexPosition(id);

		    float4 screenPos = ComputeScreenPos(o.vertex);
		    float4 ndcPos = (screenPos / screenPos.w) * 2 - 1;
		    o.viewdir = mul(unity_CameraInvProjection, float3(ndcPos.xy, 1.0).xyzz).xyz;

			return o;
		}

		float4 HeightFogFragment(Varyings input) : SV_Target
		{		
			float deviceDepth = SampleSceneDepth(input.uv);
			float linearDepth = Linear01Depth(deviceDepth, _ZBufferParams) * _ProjectionParams.z;

			float3 V = mul((float3x3)UNITY_MATRIX_I_V, normalize(-input.viewdir));
			float3 viewPos = input.viewdir * (deviceDepth == UNITY_RAW_FAR_CLIP_VALUE ? _MaxDistance : linearDepth);
    		float3 worldPosition = mul(UNITY_MATRIX_I_V, float4(viewPos, 1)).xyz;

			float cosZenith = -V.y;
			float startHeight = worldPosition.y;

            float odFallback = OpticalDepthHeightFog(_HeightFogBaseExtinction, _HeightFogBaseHeight, _HeightFogExponents, _HeightFogDensity, cosZenith, startHeight, linearDepth);
			float trFallback = TransmittanceFromOpticalDepth(odFallback);

#if defined(_MIPFOG_MAP)
			real3 normal = rotate(normalize(worldPosition), _SkyMipParams.w);
			real mipLevel = (1 - saturate((linearDepth - _ProjectionParams.y) / (_ProjectionParams.z - _ProjectionParams.y))) * 6;
			_HeightFogColor.xyz *= _SkyMipParams.xyz;
			_HeightFogColor.xyz *= SAMPLE_TEXTURE2D_LOD(_SkyMipTexture, sampler_SkyMipTexture, SampleLatlong(normal), mipLevel).xyz;
			_HeightFogColor.xyz += lerp(-0.5, 0.5, InterleavedGradientNoise(input.vertex.xy, _Frame.x)) / 255.f;
#endif

			return float4(_HeightFogColor.xyz, (1 - trFallback));
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

				#pragma vertex HeightFogVertex
				#pragma fragment HeightFogFragment

				#pragma multi_compile_local _ _MIPFOG_MAP
			ENDHLSL
		}
	}
}