#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 64

#include "../shaderLibrary/Shadow.hlsl"

CBUFFER_START(_CustomLight)
	int _DirectionalLightCount;
	float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightDirectionsAndMask[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];

	int _OtherLightCount;
	float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightDirectionsAndMask[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];
CBUFFER_END

struct Light {
	float3 color;
	float3 direction;
	float attenuation;
	uint renderingLayerMask;
};

DirectionalShadowData GetDirectionalShadowData(int lightIndex, ShadowData shadowData) {
	DirectionalShadowData data;
	data.strength = abs(_DirectionalLightShadowData[lightIndex].x);	// *shadowData.strength;不在这里判断远近对阴影强度的变化
	data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;	//第几个光的第几个联级
	data.normalBias = _DirectionalLightShadowData[lightIndex].z;
	data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w;
	return data;
}

OtherShadowData getOtherShadowData(int lightIndex) {
	OtherShadowData data;
	data.strength = abs(_OtherLightShadowData[lightIndex].x);
	data.tileIndex = _OtherLightShadowData[lightIndex].y;
	data.isPoint = _OtherLightShadowData[lightIndex].z == 1;
	data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
	data.lightPosition = 0.0;
	data.lightDirection = 0.0;
	data.spotDirection = 0.0;
	return data;
}

Light getDirectionLight(int index, Surface surface, ShadowData shadowData) {
	Light light;
	light.color = _DirectionalLightColors[index].rgb;
	light.direction = _DirectionalLightDirectionsAndMask[index].xyz;
	light.renderingLayerMask = asuint(_DirectionalLightDirectionsAndMask[index].w);
	DirectionalShadowData data = GetDirectionalShadowData(index, shadowData);
	light.attenuation = GetDirectionalShadowAttenuation(data, surface, shadowData);
	return light;
}

Light getOtherLight(int index, Surface surface, ShadowData shadowData) {
	Light light;
	light.color = _OtherLightColors[index].rgb;
	float3 position = _OtherLightPositions[index].xyz;
	float3 ray = position - surface.position;
	light.direction = normalize(ray);

	//聚光内外光圈衰减
	float3 spotDirection = _OtherLightDirectionsAndMask[index].xyz;
	light.renderingLayerMask = asuint(_OtherLightDirectionsAndMask[index].w);
	float4 spotAngles = _OtherLightSpotAngles[index];
	float spotAttenuation = dot(spotDirection, light.direction);
	spotAttenuation = Square(saturate(spotAttenuation * spotAngles.x + spotAngles.y));

	//阴影
	OtherShadowData otherShadowData = getOtherShadowData(index);
	otherShadowData.lightPosition = position;
	otherShadowData.lightDirection = light.direction;
	otherShadowData.spotDirection = spotDirection;

	float shadowAttenuation = getOtherShadowAttenuation(otherShadowData, surface, shadowData);

	//距离衰减
	float distanceSqr = max(dot(ray, ray), 0.0001f);
	float distanceAttenuation = Square(saturate(1.0f - Square(distanceSqr * _OtherLightPositions[index].w))) / distanceSqr;

	light.attenuation = distanceAttenuation * spotAttenuation * shadowAttenuation;

	return light;
}

#endif