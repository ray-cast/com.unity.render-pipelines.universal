#ifndef UNIVERSAL_DECLARE_DEPTH_ATTACHMENT_INCLUDED
#define UNIVERSAL_DECLARE_DEPTH_ATTACHMENT_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

#if defined(_DEPTH_MSAA_2)
	#define MSAA_SAMPLES 2
#elif defined(_DEPTH_MSAA_4)
	#define MSAA_SAMPLES 4
#elif defined(_DEPTH_MSAA_8)
	#define MSAA_SAMPLES 8
#else
	#define MSAA_SAMPLES 1
#endif

#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
#	define TEXTURE_DEPTH_MS(name, samples) Texture2DMSArray<float, samples> name
#	define TEXTURE_DEPTH(name) TEXTURE2D_ARRAY_FLOAT(name)
#else
#	define TEXTURE_DEPTH_MS(name, samples) Texture2DMS<float, samples> name
#	define TEXTURE_DEPTH(name) TEXTURE2D_FLOAT(name)
#endif

#if MSAA_SAMPLES == 1
#	if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
#		define LOAD_TEXTURE2D_DEPTH(textureName, uv, sampleIndex) LOAD_TEXTURE2D_ARRAY(textureName, uv, unity_StereoEyeIndex).x
#		define SAMPLE_TEXTURE2D_DEPTH(textureName, samplerName, uv) SAMPLE_TEXTURE2D_ARRAY(textureName, samplerName, uv, unity_StereoEyeIndex).x
#	else
#		define LOAD_TEXTURE2D_DEPTH(textureName, uv, sampleIndex) LOAD_TEXTURE2D(textureName, uv).x
#		define SAMPLE_TEXTURE2D_DEPTH(textureName, samplerName, uv) SAMPLE_DEPTH_TEXTURE(textureName, samplerName, uv).x
#	endif
#else
#	if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
#		define LOAD_TEXTURE2D_DEPTH(textureName, uv, sampleIndex) LOAD_TEXTURE2D_ARRAY_MSAA(textureName, uv, unity_StereoEyeIndex, sampleIndex).x
#		define SAMPLE_TEXTURE2D_DEPTH(textureName, samplerName, uv) SAMPLE_TEXTURE2D_ARRAY(textureName, samplerName, uv, unity_StereoEyeIndex).x
#	else
#		define LOAD_TEXTURE2D_DEPTH(textureName, uv, sampleIndex) LOAD_TEXTURE2D_MSAA(textureName, uv, sampleIndex).x
#		define SAMPLE_TEXTURE2D_DEPTH(textureName, samplerName, uv) SAMPLE_DEPTH_TEXTURE(textureName, samplerName, uv).x
#	endif
#endif

#if MSAA_SAMPLES == 1
	TEXTURE_DEPTH(_CameraDepthAttachment);
	SAMPLER(sampler_CameraDepthAttachment);
#else
	TEXTURE_DEPTH_MS(_CameraDepthAttachment, MSAA_SAMPLES);
	float4 _CameraDepthAttachment_TexelSize;
#endif

#if UNITY_REVERSED_Z
	#define DEPTH_DEFAULT_VALUE 1.0
	#define DEPTH_OP min
#else
	#define DEPTH_DEFAULT_VALUE 0.0
	#define DEPTH_OP max
#endif

float SampleDepthAttachment(float2 uv)
{
#if MSAA_SAMPLES == 1
	return SAMPLE_TEXTURE2D_DEPTH(_CameraDepthAttachment, sampler_CameraDepthAttachment, uv);
#else
	int2 coord = int2(uv * _CameraDepthAttachment_TexelSize.zw);
	float outDepth = DEPTH_DEFAULT_VALUE;

	UNITY_UNROLL
	for (int i = 0; i < MSAA_SAMPLES; ++i)
		outDepth = DEPTH_OP(LOAD_TEXTURE2D_DEPTH(_CameraDepthAttachment, coord, i), outDepth);
	return outDepth;
#endif
}

float LoadDepthAttachment(uint2 uv)
{
#if MSAA_SAMPLES == 1
	return LOAD_TEXTURE2D_DEPTH(_CameraDepthAttachment, uv, 0);
#else
	int2 coord = int2(uv * _CameraDepthAttachment_TexelSize.zw);
	float outDepth = DEPTH_DEFAULT_VALUE;

	UNITY_UNROLL
	for (int i = 0; i < MSAA_SAMPLES; ++i)
		outDepth = DEPTH_OP(LOAD_TEXTURE2D_DEPTH(_CameraDepthAttachment, coord, i), outDepth);
	return outDepth;
#endif
}

#endif