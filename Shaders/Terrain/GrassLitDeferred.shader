Shader "Universal Render Pipeline/Grass Lit Deferred"
{
	Properties
	{
		[Header(Wind)]
		_WindAIntensity("_WindAIntensity", Float) = 1.77
		_WindAFrequency("_WindAFrequency", Float) = 4
		_WindATiling("_WindATiling", Vector) = (0.1,0.1,0)
		_WindAWrap("_WindAWrap", Vector) = (0.5,0.5,0)
		_WindDirection("WindDirection", Vector) = (1.0, 0.0, 0.0)

		_WindScatter("风场扩散范围", Vector) = (20, 20, 1, 1)
		_WindRange("风场运动范围", Float) = 20
		_WindHightlightSpeed("风场高光扰动速率", Float) = 1
		_WindHightlightIntensity("风场高光扰动速率", Float) = 2
		[NoScaleOffset]_WindNoiseMap("风场扰动贴图", 2D) = "black" {}

		[Header(TouchBend)]
		_BendStrength("_BendStrength", Float) = 0.2

		_RandomNormal("_RandomNormal", Range(0, 1)) = 0.15

		//make SRP batcher happy
		[HideInInspector]_PivotPosWS("_PivotPosWS", Vector) = (0,0,0,0)
		[HideInInspector]_BoundSize("_BoundSize", Vector) = (1,1,0)
	}

	HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Gbuffer.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

		#include "VegetationCommon.cginc"

		#define MAX_COLOR_BUFFER 16
		#define MAX_SCALE_BUFFER 16

		half4 _AllScalesBuffer[MAX_SCALE_BUFFER];
		half4 _AllColorsBuffer[MAX_COLOR_BUFFER * 2];

		StructuredBuffer<float3> _AllInstancesTransformBuffer;
		StructuredBuffer<uint3> _AllInstancesIndexBuffer;
		StructuredBuffer<uint> _AllVisibleInstancesIndexBuffer;

		CBUFFER_START(UnityPerMaterial)
			float3 _PivotPosWS;
			float2 _BoundSize;

			float _WindRange;
			float _WindAIntensity;
			float _WindAFrequency;
			float2 _WindATiling;
			float2 _WindAWrap;
			float3 _WindDirection;

			float2 _WindScatter;
			float _WindHightlightSpeed;
			float _WindHightlightIntensity;

			half _RandomNormal;
			float _BendStrength;
		CBUFFER_END

		TEXTURE2D(_WindNoiseMap);
		SAMPLER(sampler_WindNoiseMap);

		struct Attributes
		{
			float4 positionOS   : POSITION;
			float4 normalOS     : NORMAL;
			float2 lightmapUV   : TEXCOORD1;
			uint instanceID : SV_InstanceID;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct Varyings
		{
			float4 positionCS  : SV_POSITION;//草顶点的裁剪空间坐标
			float4 color       : TEXCOORD0;
			DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 1);

			float3 normalWS    : TEXCOORD2;
			float3 viewDirWS   : TEXCOORD3;

			UNITY_VERTEX_INPUT_INSTANCE_ID
			UNITY_VERTEX_OUTPUT_STEREO
		};

		half2 hash( half2 x )  // replace this by something better
		{
			const half2 k = half2( 0.3183099, 0.3678794 );
			x = x*k + k.yx;
			return -1.0 + 2.0*frac( 16.0 * k*frac( x.x*x.y*(x.x+x.y)) );
		}

		float noise( in half2 p )
		{
			half2 i = floor( p );
			half2 f = frac( p );

			half2 u = f*f*(3.0-2.0*f);

			return lerp(lerp(dot( hash( i + half2(0.0,0.0) ), f - half2(0.0,0.0) ), 
							 dot( hash( i + half2(1.0,0.0) ), f - half2(1.0,0.0) ), u.x),
						lerp(dot( hash( i + half2(0.0,1.0) ), f - half2(0.0,1.0) ), 
							 dot( hash( i + half2(1.0,1.0) ), f - half2(1.0,1.0) ), u.x), u.y);
		}

		Varyings LitPassVertex(Attributes input)
		{
			Varyings output = (Varyings)0;

			UNITY_SETUP_INSTANCE_ID(input);
			UNITY_TRANSFER_INSTANCE_ID(input, output);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

			uint index = _AllVisibleInstancesIndexBuffer[input.instanceID];
			uint3 perGrassIndex = _AllInstancesIndexBuffer[index];
			float3 pivotPositionWS = _AllInstancesTransformBuffer[index];
			float3 scale = _AllScalesBuffer[perGrassIndex.z].xyz;
			half4 dryColor = _AllColorsBuffer[perGrassIndex.x];
			half4 healthyColor = _AllColorsBuffer[perGrassIndex.y];

			half3 direction = normalize(_WindDirection);
			half2 windTexcoord = (pivotPositionWS + input.positionOS).xz / _WindScatter;
			half2 windWindTexcoord = windTexcoord - direction.xz * _WindHightlightSpeed * _Time.x;
			half wind = SAMPLE_TEXTURE2D_LOD(_WindNoiseMap, sampler_WindNoiseMap, windWindTexcoord, 0).r;

			float distanceThreadhold = 1 - saturate(distance(pivotPositionWS, GetCameraPositionWS()) / _WindRange);

			float3 positionWS = input.positionOS;
			positionWS = ApplyRotationAndScale(positionWS, pivotPositionWS, scale.x, scale.y, scale.z);
			positionWS = ApplyBending(positionWS.xyz, pivotPositionWS, _BendStrength);
			positionWS = ApplyWind(positionWS, pivotPositionWS, direction, _WindAFrequency, _WindATiling, _WindAWrap, (_WindAIntensity + wind * _WindHightlightIntensity) * distanceThreadhold);
			positionWS += pivotPositionWS;

			half occlusion = (noise(pivotPositionWS.xz) + 0.25);

			dryColor = lerp(pow(dryColor, 1.4), dryColor, lerp(occlusion, 1, dryColor.a));
			healthyColor = lerp(pow(healthyColor, 1.4), healthyColor, lerp(occlusion, 1, healthyColor.a));

			half3 albedo = lerp(healthyColor, dryColor, saturate(input.positionOS.y));
			albedo = lerp(albedo, dryColor, wind * saturate(input.positionOS.y / 0.12));

			float3 cameraTransformRightWS = UNITY_MATRIX_V[0].xyz;
			float3 cameraTransformForwardWS = -UNITY_MATRIX_V[2].xyz;
			half3 randomAddToN = (_RandomNormal * sin(pivotPositionWS.x * 82.32523 + pivotPositionWS.z)) * cameraTransformRightWS;

			half3 N = normalize(half3(0, 1, 0) + randomAddToN);
			half3 V = normalize(_WorldSpaceCameraPos - positionWS);

			output.positionCS = TransformWorldToHClip(positionWS);
			output.color = float4(albedo, occlusion);
			output.normalWS = N;
			output.viewDirWS = GetCameraPositionWS() - positionWS;

			OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
			OUTPUT_SH(output.normalWS.xyz, output.vertexSH);

			return output;
		}

		FragmentOutput LitPassFragment(Varyings input)
		{
			SurfaceData surfaceData;
			surfaceData.albedo = input.color.xyz;
			surfaceData.specular = 0.5;
			surfaceData.metallic = 0;
			surfaceData.smoothness = 0.25;
			surfaceData.normalTS = float3(0, 0, 1);
			surfaceData.occlusion = input.color.a;
			surfaceData.emission = SampleSH(0) * surfaceData.albedo * surfaceData.occlusion;
			surfaceData.alpha = 1;

			GbufferData data;
			data.albedo = surfaceData.albedo;
			data.normalWS = input.normalWS;
			data.emission = surfaceData.emission;
			data.specular = surfaceData.specular;
			data.metallic = surfaceData.metallic;
			data.smoothness = surfaceData.smoothness;
			data.occlusion = surfaceData.occlusion;

			return EncodeGbuffer(data);
		}

		struct AttributesLean
		{
			float4 positionOS   : POSITION;
			uint instanceID : SV_InstanceID;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct VaryingsLean
		{
			float4 positionCS  : SV_POSITION;

			UNITY_VERTEX_INPUT_INSTANCE_ID
			UNITY_VERTEX_OUTPUT_STEREO
		};

		VaryingsLean DepthOnlyVertex(AttributesLean input)
		{
			VaryingsLean output = (VaryingsLean)0;

			UNITY_SETUP_INSTANCE_ID(input);
			UNITY_TRANSFER_INSTANCE_ID(input, output);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

			uint index = _AllVisibleInstancesIndexBuffer[input.instanceID];
			uint3 perGrassIndex = _AllInstancesIndexBuffer[index];
			float3 pivotPositionWS = _AllInstancesTransformBuffer[index];
			float3 scale = _AllScalesBuffer[perGrassIndex.z].xyz;

			half3 direction = normalize(_WindDirection);
			half2 windTexcoord = (pivotPositionWS + input.positionOS).xz / _WindScatter;
			half2 windWindTexcoord = windTexcoord - direction.xz * _WindHightlightSpeed * _Time.x;
			half wind = SAMPLE_TEXTURE2D_LOD(_WindNoiseMap, sampler_WindNoiseMap, windWindTexcoord, 0).r;

			float distanceThreadhold = 1 - saturate(distance(pivotPositionWS, GetCameraPositionWS()) / _WindRange);

			float3 positionWS = input.positionOS;
			positionWS = ApplyRotationAndScale(positionWS, pivotPositionWS, scale.x, scale.y, scale.z);
			positionWS = ApplyBending(positionWS.xyz, pivotPositionWS, _BendStrength);
			positionWS = ApplyWind(positionWS, pivotPositionWS, direction, _WindAFrequency, _WindATiling, _WindAWrap, (_WindAIntensity + wind * _WindHightlightIntensity) * distanceThreadhold);
			positionWS += pivotPositionWS;

			output.positionCS = TransformWorldToHClip(positionWS);

			return output;
		}

		half4 DepthOnlyFragment(VaryingsLean input) : SV_TARGET
		{
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
			return 0;
		}
	ENDHLSL

	SubShader
	{
        // Universal Pipeline tag is required. If Universal render pipeline is not set in the graphics settings
        // this Subshader will fail. One can add a subshader below or fallback to Standard built-in to make this
        // material work with both Universal Render Pipeline and Builtin Unity Pipeline
        Tags{"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
        LOD 300

		Pass
		{
            Name "DeferredLit"
            Tags{"LightMode" = "Deferred"}

			ZTest Less ZWrite On
			Cull Off

			HLSLPROGRAM
				#pragma prefer_hlslcc gles
				#pragma exclude_renderers d3d11_9x

				#pragma vertex LitPassVertex
				#pragma fragment LitPassFragment
			ENDHLSL
		}
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZTest Less ZWrite On
            ColorMask 0
            Cull Off

			HLSLPROGRAM
				#pragma prefer_hlslcc gles
				#pragma exclude_renderers d3d11_9x

				#pragma vertex DepthOnlyVertex
				#pragma fragment DepthOnlyFragment
			ENDHLSL
        }
	}
}