#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
#define DIRECTIONAL_FILTER_SAMPLES 4
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
#define DIRECTIONAL_FILTER_SAMPLES 9
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
#define DIRECTIONAL_FILTER_SAMPLES 16
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#if defined(_OTHER_PCF3)
#define OTHER_FILTER_SAMPLES 4
#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
#define OTHER_FILTER_SAMPLES 9
#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
#define OTHER_FILTER_SAMPLES 16
#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_SHADOWED_OTHER_LIGHT_COUNT 16
#define MAX_CASCADE_COUNT 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
TEXTURE2D_SHADOW(_OtherShadowAtlas);
	#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	int _CascadeCount;
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4 _CascadeData[MAX_CASCADE_COUNT];
	float4x4 _DirectionalShadowMatrix[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
	float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
	float4 _OtherShadowTiles[MAX_SHADOWED_OTHER_LIGHT_COUNT];
	float4 _ShadowDistanceFade;
	float4 _ShadowAtlasSize;
CBUFFER_END

struct DirectionalShadowData {
	float strength;
	int tileIndex;
	float normalBias;
	int shadowMaskChannel;
};

struct OtherShadowData {
	bool isPoint;
	float strength;
	int tileIndex;
	int shadowMaskChannel;
	float3 lightPosition;
	float3 lightDirection;
	float3 spotDirection;
};


struct ShadowMask {
	bool always;
	bool distance;
	float4 shadows;	//采样得到的间接阴影
};

struct ShadowData {
	int cascadeIndex;
	float strength;
	float cascadeBlend;
	ShadowMask shadowMask;
};

float FadeShadowStrength(float distance, float scale, float fade) {
	return saturate((1.0f - distance * scale) * fade);
}

ShadowData getShadowData(Surface surface) {

	ShadowData shadowData;
	shadowData.cascadeBlend = 1.0f;
	shadowData.shadowMask.always = false;
	shadowData.shadowMask.distance = false;
	shadowData.shadowMask.shadows = 1.0f;
	
	int i;
	for (i = 0; i < _CascadeCount; i++) {
		float4 sphere = _CascadeCullingSpheres[i];
		float distanceSqr = DistanceSquared(surface.position, sphere.xyz);
		if (distanceSqr < sphere.w) {

			float fade = FadeShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z);

			//shadowData.strength = surface.depth < _ShadowDistance ? 1.0f : 0.0f;
			//因为我在动态设置联级比例时就取了摄像机farplane与maxDistance的较小值
			shadowData.strength = FadeShadowStrength(surface.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
			if (i + 1 == _CascadeCount) {
				//当物体逐渐最大距离时变淡
				shadowData.strength *= fade;
			}
			else {
				shadowData.cascadeBlend = fade;
			}

			break;
		}
	}

	//超出最大联级，并且联级大0（这样当没有平行光但有非平行光时，联级数即使为0，全局光照强度也不为0，非平行光阴影仍然可见），就不设置阴影。
	if (i == _CascadeCount && _CascadeCount > 0) {
		shadowData.strength = 0.0f;
	}
#if defined(_CASCADE_BLEND_DITHER)
	else if (shadowData.cascadeBlend < surface.dither) {
		i += 1;
	}
#endif

	//如果是硬阴影或是抖动，那么就不会有联级间的过度
	#if !defined(_CASCADE_BLEND_SOFT)
		shadowData.cascadeBlend = 1.0;
	#endif

	shadowData.cascadeIndex = i;

	return shadowData;
}

float SampleDirectionalShadowAtlas(float3 positionSTS) {
	return SAMPLE_TEXTURE2D_SHADOW(
		_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS
	);
}

float FilterDirectionalShadow(float3 positionSTS) {
#if defined(DIRECTIONAL_FILTER_SETUP)
	float weights[DIRECTIONAL_FILTER_SAMPLES];
	float2 positions[DIRECTIONAL_FILTER_SAMPLES];
	float4 size = _ShadowAtlasSize.yyxx;
	DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
	float shadow = 0;
	for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++) {
		shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i].xy, positionSTS.z));
	}
	return shadow;
#else
	return SampleDirectionalShadowAtlas(positionSTS);	//默认采样为线性插值2x2
#endif
}

float getCascadeShadow(DirectionalShadowData data, Surface surfaceWS, ShadowData global) {

	float3 normalBias = surfaceWS.interpolatedNormal * data.normalBias * _CascadeData[global.cascadeIndex].y;

	float3 positionSTS = mul(
		_DirectionalShadowMatrix[data.tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	).xyz;
	float shadow = FilterDirectionalShadow(positionSTS);

	//采样次一等的联级，将两个结果混合，使得两种联级的边界不割裂
	if (global.cascadeBlend < 1.0f) {
		normalBias = surfaceWS.interpolatedNormal * data.normalBias * _CascadeData[global.cascadeIndex + 1].y;
		positionSTS = mul(
			_DirectionalShadowMatrix[data.tileIndex + 1],
			float4(surfaceWS.position + normalBias, 1.0)
		).xyz;
		shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend);
	}

	return shadow;

}

float getBakedShadow(ShadowMask mask, int channel) {
	if (mask.always || mask.distance) {
		if (channel >= 0) {		//即使开启了阴影遮罩，但是如果光源的阴影设置为不使用阴影遮罩，那么就为-1
			return mask.shadows[channel];
		}
	}
	return 1.0f;
}

//当阴影强度为0或超出实时阴影范围时，尝试取得静态阴影。
float getBakedShadow(ShadowMask mask, float strength, int channel) {
	return lerp(1.0f, getBakedShadow(mask, channel), strength);	//受强度影响
}

float mixBakedAndRealTimeShadows(ShadowData global, float shadow, float strength, int maskChannel) {

	float baked = getBakedShadow(global.shadowMask, maskChannel);
	if (global.shadowMask.always) {
		shadow = lerp(1.0f, shadow, global.strength);	//远近影响,实时阴影
		shadow = min(shadow, baked);	//越小，说明阴影越强。采样得到的阴影如果强则应该rgb较小。
		return lerp(1.0f, shadow, strength);	//受强度影响
	}else if (global.shadowMask.distance) {
		shadow = lerp(baked, shadow, global.strength);	//远近影响
		return lerp(1.0f, shadow, strength);	//受强度影响
	}
	//将light.hlsl中的GetDirectionalShadowData函数的获得strength时判断远近移到这里。因为在上面if中需要需要不受远近影响的阴影强度对阴影进行插值。
	return lerp(1.0, shadow, strength * global.strength); 
}

//data.strength代表光照的阴影强度，global.strength代表远近影响的阴影强度。
float GetDirectionalShadowAttenuation(DirectionalShadowData data, Surface surfaceWS, ShadowData global) {

#if defined(_FORWARDPIPELINE)
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif
#endif

	if (data.strength * global.strength <= 0.0) {
		//没有实时阴影时，只调用静态阴影
		return getBakedShadow(global.shadowMask, data.strength, data.shadowMaskChannel);
	}

	//接受实时阴影时，混合实时阴影和静态阴影
	float shadow = getCascadeShadow(data, surfaceWS, global);
	return shadow;
	return mixBakedAndRealTimeShadows(global, shadow, data.strength, data.shadowMaskChannel);

}

float sampleOtherShadowAtlas(float3 positionSTS, float3 bounds) {
	positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);	//通过positionSTS来控制UV，使其不会超出纹理。
	return SAMPLE_TEXTURE2D_SHADOW(_OtherShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float FilterOtherShadow(float3 positionSTS, float3 bounds) {
#if defined(OTHER_FILTER_SETUP)
	real weights[OTHER_FILTER_SAMPLES];
	real2 positions[OTHER_FILTER_SAMPLES];
	float4 size = _ShadowAtlasSize.wwzz;
	OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);
	float shadow = 0;
	for (int i = 0; i < OTHER_FILTER_SAMPLES; i++) {
		shadow += weights[i] * sampleOtherShadowAtlas(float3(positions[i].xy, positionSTS.z), bounds);
	}
	return shadow;
#else
	return sampleOtherShadowAtlas(positionSTS, bounds);
#endif
}

static const float3 pointShadowPlanes[6] = {
	float3(-1.0, 0.0, 0.0),
	float3(1.0, 0.0, 0.0),
	float3(0.0, -1.0, 0.0),
	float3(0.0, 1.0, 0.0),
	float3(0.0, 0.0, -1.0),
	float3(0.0, 0.0, 1.0)
};

float getOtherShadow(OtherShadowData other, Surface surface, ShadowData global) {
	

	float tileIndex = other.tileIndex;
	float3 lightPlane = other.spotDirection;

	if (other.isPoint) {
		float faceOffset = CubeMapFaceID(-other.lightDirection);	//根据方向获得物体对应阴影贴图的相对索引
		tileIndex += faceOffset;
		lightPlane = pointShadowPlanes[faceOffset];
	}

	float4 tileData = _OtherShadowTiles[tileIndex];

	float3 surfaceToLight = other.lightPosition - surface.position;
	float distanceToLightPlane = dot(surfaceToLight, lightPlane);	//和光线方向进行点乘，计算单位长度下的像素大小的cos缩放,以此对normalBias进行缩放。

	float3 normalBias = surface.interpolatedNormal * distanceToLightPlane * tileData.w;
	float4 positionSTS = mul(
		_OtherShadowMatrices[tileIndex],
		float4(surface.position + normalBias, 1.0)	//增加normalBias后positionSTS可能会超出采样平面，所以在后面会进行clamp
	);
	return FilterOtherShadow(positionSTS.xyz / positionSTS.w, tileData.xyz);

}

float getOtherShadowAttenuation(OtherShadowData other, Surface surface, ShadowData global) {

#if defined(_FORWARDPIPELINE)
	#if !defined(_RECEIVE_SHADOWS)
	return 1.0;
	#endif
#endif

	float shadow;
	if (other.strength * global.strength <= 0.0f) {
		shadow = getBakedShadow(global.shadowMask, other.strength, other.shadowMaskChannel);
	}
	else {
		shadow = getOtherShadow(other, surface, global);
		shadow = mixBakedAndRealTimeShadows(global, shadow, other.strength, other.shadowMaskChannel);
	}

	return shadow;

}

#endif