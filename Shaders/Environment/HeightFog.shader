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

		float4 HeightFogFragment(Varyings i) : SV_Target
		{		
			float deviceDepth = SampleSceneDepth(i.uv);
			float linearDepth = Linear01Depth(deviceDepth, _ZBufferParams) * _ProjectionParams.z;

			if (deviceDepth == UNITY_RAW_FAR_CLIP_VALUE)
			{
				linearDepth = _MaxDistance;
			}

			float3 V = mul((float3x3)UNITY_MATRIX_I_V, normalize(-i.viewdir));
			float3 viewPos = i.viewdir * linearDepth;
    		float3 worldPosition = mul(UNITY_MATRIX_I_V, float4(viewPos, 1)).xyz;

			float cosZenith = -V.y;
			float startHeight = worldPosition.y;

            float odFallback = OpticalDepthHeightFog(_HeightFogBaseExtinction, _HeightFogBaseHeight, _HeightFogExponents, _HeightFogDensity, cosZenith, startHeight, linearDepth);
			float trFallback = TransmittanceFromOpticalDepth(odFallback);

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
			ENDHLSL
		}
	}
}