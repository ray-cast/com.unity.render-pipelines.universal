#ifndef UNIVERSAL_GBUFFER_INCLUDED
#define UNIVERSAL_GBUFFER_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_CameraGBufferTexture0);
TEXTURE2D(_CameraGBufferTexture1);
TEXTURE2D(_CameraGBufferTexture2);
TEXTURE2D(_CameraGBufferTexture3);

SAMPLER(sampler_CameraGBufferTexture0);
SAMPLER(sampler_CameraGBufferTexture1);
SAMPLER(sampler_CameraGBufferTexture2);
SAMPLER(sampler_CameraGBufferTexture3);

struct GbufferData
{
    half3 albedo;
    half3 normalWS;
    half3 emission;
    half3 specular;
    half  metallic;
    half  smoothness;
    half  occlusion;
    half  translucency;
};

struct FragmentOutput
{
	float4 color0 : SV_TARGET0;
	float4 color1 : SV_TARGET1;
	float4 color2 : SV_TARGET2;
	float4 color3 : SV_TARGET3;
};

float3 EncodeNormal(float3 normal)
{
	normal = NormalizeNormalPerPixel(normal);
	float p = sqrt(-normal.z * 8 + 8);
	float2 enc = normal.xy / p + 0.5f;
	float2 enc255 = enc * 255;
	float2 residual = floor(frac(enc255) * 16);
	return float3(floor(enc255), residual.x * 16 + residual.y) / 255;
}

float3 DecodeNormal(float3 enc)
{
	float nz = floor(enc.z * 255) / 16;
	enc.xy += float2(floor(nz) / 16, frac(nz)) / 255;
	float2 fenc = enc.xy * 4 - 2;
	float f = dot(fenc, fenc);
	float g = sqrt(1 - f / 4);
	float3 normal;
	normal.xy = fenc * g;
	normal.z = f / 2 - 1;
	return NormalizeNormalPerPixel(normal);
}

float4 EncodeRGBT(float3 rgb, float range = 1024)
{
	float limits = 0;
	limits = max(max(rgb.r, rgb.g), max(rgb.b, 1e-6f));
	limits = min(limits, range);

	float4 encode;
	encode.a = (range + 1) / range *  limits / (1 + limits);
	encode.a = ceil(encode.a * 255.0) / 255.0;
	encode.xyz = rgb.rgb * rcp(encode.a / (1.0 + 1.0 / range - encode.a));

	return encode;
}

float3 DecodeRGBT(float4 rgbt, float range = 1024)
{
	rgbt.a *= rcp(1 + 1 / range - rgbt.a);
	return rgbt.rgb * rgbt.a;
}

FragmentOutput EncodeGbuffer(GbufferData data)
{
	FragmentOutput output = (FragmentOutput)0;
	output.color0 = float4(data.albedo, data.occlusion);
	output.color1 = float4(data.metallic, dot(data.specular, half3(0.25, 0.5, 0.25)), data.translucency, data.smoothness);
	output.color2 = float4(PackNormalMaxComponent(data.normalWS), 0);
	output.color3 = EncodeRGBT(data.emission);

	return output;
}

GbufferData DecodeGbuffer(FragmentOutput fragment)
{
	GbufferData data = (GbufferData)0;
	data.albedo = fragment.color0.rgb;
	data.occlusion = fragment.color0.a;
	data.metallic = fragment.color1.r;
	data.specular = fragment.color1.g;
	data.translucency = fragment.color1.b;
	data.smoothness = fragment.color1.a;
	data.normalWS = UnpackNormalMaxComponent(fragment.color2.rgb);
	data.emission = DecodeRGBT(fragment.color3);
	return data;
}

GbufferData SampleGbufferTextures(float2 uv)
{
	FragmentOutput fragment;
	fragment.color0 = SAMPLE_TEXTURE2D_LOD(_CameraGBufferTexture0, sampler_CameraGBufferTexture0, uv, 0);
	fragment.color1 = SAMPLE_TEXTURE2D_LOD(_CameraGBufferTexture1, sampler_CameraGBufferTexture1, uv, 0);
	fragment.color2 = SAMPLE_TEXTURE2D_LOD(_CameraGBufferTexture2, sampler_CameraGBufferTexture2, uv, 0);
	fragment.color3 = SAMPLE_TEXTURE2D_LOD(_CameraGBufferTexture3, sampler_CameraGBufferTexture3, uv, 0);

	return DecodeGbuffer(fragment);
}

GbufferData LoadGbufferTextures(uint2 uv)
{
	FragmentOutput fragment;
	fragment.color0 = LOAD_TEXTURE2D_LOD(_CameraGBufferTexture0, uv, 0);
	fragment.color1 = LOAD_TEXTURE2D_LOD(_CameraGBufferTexture1, uv, 0);
	fragment.color2 = LOAD_TEXTURE2D_LOD(_CameraGBufferTexture2, uv, 0);
	fragment.color3 = LOAD_TEXTURE2D_LOD(_CameraGBufferTexture3, uv, 0);

	return DecodeGbuffer(fragment);
}

float3 SampleSceneGbufferNormal(float2 uv)
{
    return UnpackNormalMaxComponent(SAMPLE_TEXTURE2D_LOD(_CameraGBufferTexture2, sampler_CameraGBufferTexture2, uv, 0).rgb);
}

float3 LoadSceneGbufferNormal(uint2 uv)
{
    return UnpackNormalMaxComponent(LOAD_TEXTURE2D_X(_CameraGBufferTexture2, uv).rgb);
}

#endif