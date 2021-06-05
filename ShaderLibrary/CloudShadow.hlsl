#ifndef UNITY_CLOUD_SHADOW_INCLUDED
#define UNITY_CLOUD_SHADOW_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Wind.hlsl"

float4 _CloudParams1;
TEXTURE2D(_CloudShadowMap);  SAMPLER(sampler_CloudShadowMap);

float GetCloudShadow(float3 positionWS)
{
#ifdef _MAIN_LIGHT_CLOUD_SHADOWS
	Wind wind = GetMainWind();
    half2 texcoord = positionWS.xz / _CloudParams1.xy - wind.direction.xz * wind.speed * _Time.x * _CloudParams1.z;
    half weight = SAMPLE_TEXTURE2D_BIAS(_CloudShadowMap, sampler_CloudShadowMap, texcoord, -8).r;
    return 1 - saturate(weight * _CloudParams1.w);
#else
	return 1;
#endif
}

#endif