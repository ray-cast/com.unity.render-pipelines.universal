#ifndef UNITY_VIRTUAL_TEXTURE_INCLUDED
#define UNITY_VIRTUAL_TEXTURE_INCLUDED

float4 _VTRealRect;
float4 _VTPageParam;
float4 _VTTileParam;

TEXTURE2D(_VTLookupTex); SAMPLER(sampler_VTLookupTex);
TEXTURE2D(_VTDiffuse); SAMPLER(sampler_VTDiffuse);
TEXTURE2D(_VTNormal); SAMPLER(sampler_VTNormal);

struct VirtualTexture
{
	float4 diffuse;
	float3 normal;
};

float4 SampleLookupPage(float2 uv)
{
	float2 uvInt = uv - frac(uv * _VTPageParam.x) * _VTPageParam.y;
	float4 page = SAMPLE_TEXTURE2D_LOD(_VTLookupTex, sampler_VTLookupTex, uvInt, 0) * 255;
	return page;
}

VirtualTexture SampleVirtualTexture(float2 uv)
{
	float4 page = SampleLookupPage(uv);
	float2 inPageOffset = frac(uv * exp2(_VTPageParam.z - page.b));
	uv = (page.rg * (_VTTileParam.y + _VTTileParam.x * 2) + inPageOffset * _VTTileParam.y + _VTTileParam.x) / _VTTileParam.zw;

	VirtualTexture data;
	data.diffuse = SAMPLE_TEXTURE2D_LOD(_VTDiffuse, sampler_VTDiffuse, uv, 0);
	data.normal = UnpackNormalScale(SAMPLE_TEXTURE2D_LOD(_VTNormal, sampler_VTDiffuse, uv, 0), 1);

	return data;
}

#endif