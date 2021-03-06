#ifndef UNIVERSAL_INPUT_INCLUDED
#define UNIVERSAL_INPUT_INCLUDED

#define MAX_VISIBLE_LIGHTS_UBO  128
#define MAX_VISIBLE_LIGHTS_SSBO 512
#define USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA 0

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderTypes.cs.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"

#if defined(SHADER_API_MOBILE) || (defined(SHADER_API_GLCORE) && !defined(SHADER_API_SWITCH)) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) // Workaround for bug on Nintendo Switch where SHADER_API_GLCORE is mistakenly defined
    #define MAX_VISIBLE_LIGHTS 128
#else
    #define MAX_VISIBLE_LIGHTS 512
#endif

struct InputData
{
    float3  positionWS;
    half3   normalWS;
    half3   viewDirectionWS;
    float4  shadowCoord;
    half    fogCoord;
    half3   vertexLighting;
    half3   bakedGI;
};

///////////////////////////////////////////////////////////////////////////////
//                      Constant Buffers                                     //
///////////////////////////////////////////////////////////////////////////////

half4 _GlossyEnvironmentColor;
half4 _SubtractiveShadowColor;

#define _InvCameraViewProj unity_MatrixInvVP
float4 _ScaledScreenParams;

float4 _MainLightPosition;
half4 _MainLightColor;

half4 _AdditionalLightsCount;

float4 _ClusterDimParams;
float4 _ClusterSizeParams;
float4 _ClusterLightParams;
float4 _ClusterProjectionParams;
float4 _ClusterScreenDimensionParams;
float4 _ClusterDimensionParams;

#define _ClusterNear _ClusterProjectionParams.x
#define _ClusterFar _ClusterProjectionParams.y
#define _ClusterNearK _ClusterProjectionParams.z
#define _ClusterLogGridDimY _ClusterProjectionParams.w

StructuredBuffer<uint> _ClusterLightIndexBuffer;
StructuredBuffer<uint2> _ClusterLightGridBuffer;
StructuredBuffer<LightData> _ClusterLightBuffer;

#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
	StructuredBuffer<LightData> _AdditionalLightsBuffer;
	StructuredBuffer<int> _AdditionalLightsIndices;
#else
	// GLES3 causes a performance regression in some devices when using CBUFFER.
	#ifndef SHADER_API_GLES3
		CBUFFER_START(AdditionalLights)
	#endif

	float4 _AdditionalLightsPosition[MAX_VISIBLE_LIGHTS];
	half4 _AdditionalLightsColor[MAX_VISIBLE_LIGHTS];
	half4 _AdditionalLightsAttenuation[MAX_VISIBLE_LIGHTS];
	half4 _AdditionalLightsSpotDir[MAX_VISIBLE_LIGHTS];
	half4 _AdditionalLightsOcclusionProbes[MAX_VISIBLE_LIGHTS];

	#ifndef SHADER_API_GLES3
		CBUFFER_END
	#endif
#endif

#define UNITY_MATRIX_M     unity_ObjectToWorld
#define UNITY_MATRIX_I_M   unity_WorldToObject
#define UNITY_MATRIX_V     unity_MatrixV
#define UNITY_MATRIX_I_V   unity_MatrixInvV
#define UNITY_MATRIX_P     OptimizeProjectionMatrix(glstate_matrix_projection)
#define UNITY_MATRIX_I_P   ERROR_UNITY_MATRIX_I_P_IS_NOT_DEFINED
#define UNITY_MATRIX_VP    unity_MatrixVP
#define UNITY_MATRIX_I_VP  unity_MatrixInvVP
#define UNITY_MATRIX_MV    mul(UNITY_MATRIX_V, UNITY_MATRIX_M)
#define UNITY_MATRIX_T_MV  transpose(UNITY_MATRIX_MV)
#define UNITY_MATRIX_IT_MV transpose(mul(UNITY_MATRIX_I_M, UNITY_MATRIX_I_V))
#define UNITY_MATRIX_MVP   mul(UNITY_MATRIX_VP, UNITY_MATRIX_M)

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

#endif