#ifndef UNITY_VIRTUAL_TEXTURE_INCLUDED
#define UNITY_VIRTUAL_TEXTURE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// x = page size;
// y = 1.0f / page size
// z = max mip level
// w = 0
float4 _VirtualPage_Params;

// x = region.x
// y = region.y
// z = 1.0f / region.width
// w = 1.0f / region.height
float4 _VirtualRegion_Params;

// x = tile.paddingSize
// y = tile.tileSize
// z = tile.width
// w = tile.height
float4 _VirtualTile_Params;

// x = page size
// y = page size * tile size
// z = max mip level - 1
// w = mipmap bias
float4 _VirtualFeedback_Params;

TEXTURE2D(_VirtualLookupTexture); SAMPLER(sampler_VirtualLookupTexture);
TEXTURE2D(_VirtualBufferTexture0); SAMPLER(sampler_VirtualBufferTexture0);
TEXTURE2D(_VirtualBufferTexture1); SAMPLER(sampler_VirtualBufferTexture1);
TEXTURE2D(_VirtualBufferTexture2); SAMPLER(sampler_VirtualBufferTexture2);

struct VirtualTexture
{
	float3 albedo;
	float3 normal;
	float3 bakedGI;
	float smoothness;
	float metallic;
	float height;
};

struct VirtualOutput
{
    float4 color0 : SV_TARGET0;
    float4 color1 : SV_TARGET1;
    float4 color2 : SV_TARGET2;
};

float4 EncodeFloatRGBA(float v)
{
   float4 enc = float4(1.0f, 255.0f, 65025.0f, 16581375.0f) * v;
   enc = frac(enc);
   enc -= enc.yzww * float4(1 / 255.0f, 1 / 255.0f, 1 / 255.0f, 0);
   return enc;
}

float DecodeFloatRGBA(float4 rgba)
{
    return dot(rgba, float4(1, 1 / 255.0f, 1 / 65025.0f, 1 / 16581375.0f));
}

VirtualOutput EncodeVirtualBuffer(VirtualTexture data)
{
	VirtualOutput output = (VirtualOutput)0;
	output.color0 = float4(data.albedo, data.metallic);
	output.color1 = float4(data.normal.xz * 0.5 + 0.5, PackFloatToR8G8(saturate(data.height / 128)));
	output.color2 = float4(data.bakedGI, data.smoothness);

	return output;
}

float2 ComputePageTexcoord(float3 worldPos)
{
	return (worldPos.xz - _VirtualRegion_Params.xy) * _VirtualRegion_Params.zw;
}

float4 ComputePageMipLevel(float2 texcoord)
{
    float2 page = floor(texcoord * _VirtualFeedback_Params.x);

    float2 uvInt = texcoord * _VirtualFeedback_Params.y;
    float2 dx = ddx(uvInt);
    float2 dy = ddy(uvInt);
    int mip = clamp(int(0.5 * log2(max(dot(dx, dx), dot(dy, dy))) + _VirtualFeedback_Params.w + 0.5), 0, _VirtualFeedback_Params.z);

    return float4(float3(page, mip) / 255.0, 1);
}

float4 SampleLookupPage(float2 uv)
{
	float2 uvInt = uv - frac(uv * _VirtualPage_Params.x) * _VirtualPage_Params.y;
	float4 page = SAMPLE_TEXTURE2D_LOD(_VirtualLookupTexture, sampler_VirtualLookupTexture, uvInt, 0) * 255;
	return page;
}

float2 ComputeTileTexcoord(float3 worldPos)
{
	float2 uv = ComputePageTexcoord(worldPos);
	float4 page = SampleLookupPage(uv);
	float2 pageOffset = frac(uv * exp2(_VirtualPage_Params.z - page.b));
	float2 tileUV = (page.rg * (_VirtualTile_Params.y + _VirtualTile_Params.x * 2) + pageOffset * _VirtualTile_Params.y + _VirtualTile_Params.x) / _VirtualTile_Params.zw;
	return tileUV;
}

float SampleVirtualHeight(float3 worldPos)
{
	float2 uv = ComputeTileTexcoord(worldPos);
	return UnpackFloatFromR8G8(SAMPLE_TEXTURE2D_LOD(_VirtualBufferTexture1, sampler_VirtualBufferTexture2, uv, 0).zw) * 128;
}

VirtualTexture SampleVirtualTexture(float3 worldPos)
{
	float2 uv = ComputeTileTexcoord(worldPos);

	float4 color0 = SAMPLE_TEXTURE2D_LOD(_VirtualBufferTexture0, sampler_VirtualBufferTexture0, uv, 0);
	float4 color1 = SAMPLE_TEXTURE2D_LOD(_VirtualBufferTexture1, sampler_VirtualBufferTexture1, uv, 0);
	float4 color2 = SAMPLE_TEXTURE2D_LOD(_VirtualBufferTexture2, sampler_VirtualBufferTexture2, uv, 0);

	VirtualTexture data;
	data.albedo = color0.rgb;
	data.metallic = color0.a;
	data.normal.xz = color1.xy * 2 - 1;
	data.normal.y = sqrt(1 - dot(data.normal.xz, data.normal.xz));
	data.normal = normalize(data.normal);
	data.height = UnpackFloatFromR8G8(color1.zw) * 128;
	data.bakedGI = color2.rgb;
	data.smoothness = color2.a;

	return data;
}

#endif