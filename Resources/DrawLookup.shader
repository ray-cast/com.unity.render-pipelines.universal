Shader "VirtualTexture/VTDrawLookup"
{
	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

		struct Attributes
		{
			float4 positionOS   : POSITION;
			float2 uv           : TEXCOORD0;

			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct Varyings
		{
			float4 color           : TEXCOORD0;
			float4 positionHCS	   : SV_POSITION;
		};

		float4 _tempInfo;

		UNITY_INSTANCING_BUFFER_START(InstanceProp)
		UNITY_DEFINE_INSTANCED_PROP(float4, _PageInfo)
		UNITY_DEFINE_INSTANCED_PROP(float4x4, _ImageMVP)
		UNITY_INSTANCING_BUFFER_END(InstanceProp)

		Varyings vert(Attributes IN)
		{
			Varyings output;

			UNITY_SETUP_INSTANCE_ID(IN);
			float4x4 mat = UNITY_MATRIX_M;
			mat = UNITY_ACCESS_INSTANCED_PROP(InstanceProp, _ImageMVP);
			float2 pos = saturate(mul(mat, IN.positionOS).xy);
			pos.y = 1 - pos.y;
			output.positionHCS = float4(2.0 * pos - 1,0.5,1);
			output.color = UNITY_ACCESS_INSTANCED_PROP(InstanceProp, _PageInfo);

			return output;
		}

		half4 frag(Varyings input) : SV_Target
		{
			return input.color;
		}
	ENDHLSL
	SubShader
	{
		Pass
		{
			ZTest Always
			Cull Front

			HLSLPROGRAM

            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

			#pragma vertex vert
			#pragma fragment frag
			
			#pragma multi_compile_instancing

			ENDHLSL
		}
	}
}
