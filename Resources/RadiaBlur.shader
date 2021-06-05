Shader "Hidden/Blur/RadiaBlur"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_BlurSize("BlurSize", Float) = 1
		_Params("Params", Vector) = (0.5, 0.5, -25, 0.45)
	}

	HLSLINCLUDE

		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

		struct appdata
		{
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
		};

		struct v2f
		{
			float2 uv : TEXCOORD0;
			float4 vertex : SV_POSITION;
		};

		TEXTURE2D_X(_MainTex);

		float4 _MainTex_ST;
		float4 _MainTex_TexelSize;
		float4 _Params;

		v2f vert(appdata v)
		{
			v2f o;
			VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
			o.vertex = vertexInput.positionCS;
			o.uv = TRANSFORM_TEX(v.uv, _MainTex);
			return o;
		}

		float4 frag(v2f i) : SV_Target
		{
			float4 color = float4(0,0,0,1);
			half2 step = i.uv - _Params.xy;
			half length = distance(i.uv, _Params.xy) / distance(float2(1, 1), _Params.xy);
			half scale = 1;
			half scaleDelta = _Params.z * _MainTex_TexelSize.x * pow(length, _Params.w);

			[unroll]
			for (int j = 0; j < 10; j++)
			{
				color += SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, _Params.xy + step * scale, 0);
				scale += scaleDelta;
			}
			
			return color * (1.0f / 10);
		}

	ENDHLSL

	SubShader
	{
		ZTest Always ZWrite Off Cull Off
		Tags {"RenderPipeline" = "UniversalPipeline"}

		Pass
		{
			HLSLPROGRAM
				#pragma vertex vert
				#pragma fragment frag
			ENDHLSL
		}
	}
}