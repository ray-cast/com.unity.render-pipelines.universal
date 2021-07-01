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
float4 _VirtualPageRegion_Params;

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
TEXTURE2D(_VirtualAlbedoTexture); SAMPLER(sampler_VirtualAlbedoTexture);
TEXTURE2D(_VirtualNormalTexture); SAMPLER(sampler_VirtualNormalTexture);

struct VirtualTexture
{
	float3 albedo;
	float3 normal;
};

float2 ComputePageTexcoord(float3 worldPos)
{
	return (worldPos.xz - _VirtualPageRegion_Params.xy) * _VirtualPageRegion_Params.zw;
}

float4 ComputePageMipLevel(float2 texcoord)
{
    float2 page = floor(texcoord * _VirtualFeedback_Params.x);

    float2 uvInt = texcoord * _VirtualFeedback_Params.y;
    float2 dx = ddx(uvInt);
    float2 dy = ddy(uvInt);
    int mip = clamp(int(0.5 * log2(max(dot(dx, dx), dot(dy, dy))) + 0.5 + _VirtualFeedback_Params.w), 0, _VirtualFeedback_Params.z);

    return float4(float3(page, mip) / 255.0, 1);
}

float4 SampleLookupPage(float2 uv)
{
	float2 uvInt = uv - frac(uv * _VirtualPage_Params.x) * _VirtualPage_Params.y;
	float4 page = SAMPLE_TEXTURE2D_LOD(_VirtualLookupTexture, sampler_VirtualLookupTexture, uvInt, 0) * 255;
	return page;
}

VirtualTexture SampleVirtualTexture(float3 worldPos)
{
	float2 uv = ComputePageTexcoord(worldPos);
	float4 page = SampleLookupPage(uv);
	float2 inPageOffset = frac(uv * exp2(_VirtualPage_Params.z - page.b));
	uv = (page.rg * (_VirtualTile_Params.y + _VirtualTile_Params.x * 2) + inPageOffset * _VirtualTile_Params.y + _VirtualTile_Params.x) / _VirtualTile_Params.zw;

	VirtualTexture data;
	data.albedo = SAMPLE_TEXTURE2D_LOD(_VirtualAlbedoTexture, sampler_VirtualAlbedoTexture, uv, 0).rgb;
	data.normal = UnpackNormalMaxComponent(SAMPLE_TEXTURE2D_LOD(_VirtualNormalTexture, sampler_VirtualNormalTexture, uv, 0).xyz);

	return data;
}

#endif