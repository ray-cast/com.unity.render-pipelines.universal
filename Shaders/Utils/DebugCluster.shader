Shader "Hidden/Universal Render Pipeline/DebugCluster"
{
	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

		struct VertexShaderOutput
		{
			float3 Min          : AABB_MIN;
			float3 Max          : AABB_MAX;
			float4 Color        : COLOR;
		};

		struct Varyings
		{
			float4 Color        : COLOR;
			float4 position     : SV_POSITION;
		};

		struct AABB
		{
			float4 Min;
			float4 Max;
		};

		StructuredBuffer<float4> _ClusterBoxBuffer;

		bool CMin(float3 a, float3 b)
		{
			if (a.x < b.x && a.y < b.y && a.z < b.z)
				return true;
			else
				return false;
		}

		bool CMax(float3 a, float3 b)
		{
			return (a.x > b.x && a.y > b.y && a.z > b.z) ? true : false;
		}

		half3 HeatMap(float greyValue)
		{
			half3 heat;
			heat.r = smoothstep(0.5, 0.8, greyValue);
			if(greyValue >= 0.90) {
				heat.r *= (1.1 - greyValue) * 5.0;
			}
			if(greyValue > 0.7) {
				heat.g = smoothstep(1.0, 0.7, greyValue);
			} else {
				heat.g = smoothstep(0.0, 0.7, greyValue);
			}
			heat.b = smoothstep(1.0, 0.0, greyValue);
			if(greyValue <= 0.3) {
				heat.b *= greyValue / 0.3;
			}
			return heat;
		}

		VertexShaderOutput vert(uint clusterID : SV_VertexID)
		{
			float4 aabb = _ClusterBoxBuffer[clusterID];
			float count = _ClusterLightGridBuffer[clusterID].y;
			float4 factor = aabb.w * (count > 0.5 ? 0.2 : 0.05);

			VertexShaderOutput output = (VertexShaderOutput)0;
			output.Min = aabb.xyz - aabb.w / 20;
			output.Max = aabb.xyz + aabb.w / 20;
			
			if (count <= GetPerClusterLightsLimit())
				output.Color = float4(lerp(float3(1,1,1), HeatMap(saturate(count / GetPerClusterLightsLimit())), saturate(count)), 1);
			else
				output.Color = float4(1, 0, 1, 1);

			return output;
		}

		[maxvertexcount(16)]
		void main_GS(point VertexShaderOutput IN[1], inout TriangleStream<Varyings> OutputStream)
		{
			float3 min = IN[0].Min;
			float3 max = IN[0].Max;

			const float4 Pos[8] = {
				float4(min.x, min.y, min.z, 1.0f),
				float4(min.x, min.y, max.z, 1.0f),
				float4(min.x, max.y, min.z, 1.0f),
				float4(min.x, max.y, max.z, 1.0f),
				float4(max.x, min.y, min.z, 1.0f),
				float4(max.x, min.y, max.z, 1.0f),
				float4(max.x, max.y, min.z, 1.0f),
				float4(max.x, max.y, max.z, 1.0f)
			};

			const uint indices[18] = { 0, 1, 2, 3, 6, 7, 4, 5, -1, 2, 6, 0, 4, 1, 5, 3, 7, -1 };

			[unroll]
			for (uint i = 0; i < 18; ++i)
			{
				if (indices[i] == (uint) - 1)
				{
					OutputStream.RestartStrip();
				}
				else
				{
					Varyings output = (Varyings)0;
					VertexPositionInputs vertexInput = GetVertexPositionInputs(Pos[indices[i]].xyz);
					output.position = vertexInput.positionCS;
					output.Color = IN[0].Color;
					OutputStream.Append(output);
				}
			}
		}

		float4 frag(Varyings input) : SV_Target
		{
			return input.Color;
		}
	ENDHLSL

	SubShader
	{
		ZTest LEqual ZWrite On
		Cull Back

		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}

		Pass
		{
			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma geometry main_GS
			#pragma exclude_renderers d3d11_9x metal gles
			#pragma target 5.0
			#pragma enable_d3d11_debug_symbols

			ENDHLSL
		}
	}
}