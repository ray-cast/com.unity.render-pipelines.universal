#ifndef UNITY_WIND_INCLUDED
#define UNITY_WIND_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

float4 _WindParams1;
float4 _WindParams2;
float4 _WindParams3;

TEXTURE2D(_WindNoiseMap);  SAMPLER(sampler_WindNoiseMap);

struct Wind
{
    half3 direction;
    half  intensity;
    half  speed;
    half  bending;
    half  frequency;
    half  range;
    half2 random;
    half2 tiling;
    half  storm;
    half distanceAttenuation;
};

float3 GetWindDirection()
{
	return _WindParams1.xyz;
}

float GetWindIntensity()
{
	return _WindParams1.w;
}

float2 GetWindRandom()
{
	return _WindParams2.xy;
}

float GetWindBending()
{
	return _WindParams2.z;
}

float GetWindFrequency()
{
	return _WindParams2.w;
}

float GetWindRange()
{
	return _WindParams3.z;
}

float GetWindSpeed()
{
	return _WindParams3.w;
}

float2 GetWindTiling()
{
	return _WindParams3.xy;
}

float GetStormStrength(Wind wind, float3 position, half weight)
{
    half strength = (1 - weight);
    half2 texcoord = position.xz / wind.tiling - wind.direction.xz * wind.speed * _Time.x;
    return SAMPLE_TEXTURE2D_LOD(_WindNoiseMap, sampler_WindNoiseMap, texcoord, strength * 7).r * weight;
}

Wind GetMainWind()
{
    Wind wind;
    wind.direction = _WindParams1.xyz;
    wind.intensity = _WindParams1.w;
    wind.random = _WindParams2.xy;
    wind.bending = _WindParams2.z;
    wind.frequency = _WindParams2.w;
    wind.tiling = _WindParams3.xy;
    wind.range = _WindParams3.z;
    wind.speed = _WindParams3.w;
    wind.storm = 0;
    wind.distanceAttenuation = 1;

    return wind;
}

Wind GetMainWind(float3 position, half weight)
{
    Wind wind;
    wind.direction = _WindParams1.xyz;
    wind.intensity = _WindParams1.w;
    wind.random = _WindParams2.xy;
    wind.bending = _WindParams2.z;
    wind.frequency = _WindParams2.w;
    wind.tiling = _WindParams3.xy;
    wind.range = _WindParams3.z;
    wind.speed = _WindParams3.w;
    wind.distanceAttenuation = 1 - saturate(distance(position, GetCameraPositionWS()) / wind.range);

    half2 texcoord = position.xz / wind.tiling - wind.direction.xz * wind.speed * _Time.x;
    wind.storm = SAMPLE_TEXTURE2D_LOD(_WindNoiseMap, sampler_WindNoiseMap, texcoord, (1 - weight) * 7).r * weight;

    return wind;
}

float3 ApplyWind(Wind wind, float3 position)
{
	float small = sin(_Time.y * wind.frequency + dot(1, position.xz * wind.random)) * wind.bending;

    float windStrength = 0;
    windStrength += small;
    windStrength += wind.storm;
    windStrength *= wind.distanceAttenuation;
    windStrength *= wind.intensity;
    windStrength = clamp(windStrength, -1, 1);

    float rad = windStrength * PI / 2;
    float3 grassUpWS = float3(0, 1, 0);
    float3 windDir = wind.direction;
    windDir = windDir - dot(windDir, grassUpWS);

    float x, y;
    sincos(rad, x, y);

    float3 windedPos = x * windDir + y * grassUpWS;

    return windedPos - grassUpWS;
}

float3 TransformObjectToWindWorld(Wind wind, float3 positionOS)
{
    float3 positionWS = TransformObjectToWorld(positionOS);
    return positionWS + ApplyWind(wind, positionWS);
}

VertexPositionInputs GetWindVertexPositionInputs(Wind wind, float3 positionOS)
{
    VertexPositionInputs input;
    input.positionWS = TransformObjectToWindWorld(wind, positionOS);
    input.positionVS = TransformWorldToView(input.positionWS);
    input.positionCS = TransformWorldToHClip(input.positionWS);

    float4 ndc = input.positionCS * 0.5f;
    input.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    input.positionNDC.zw = input.positionCS.zw;

    return input;
}

#endif