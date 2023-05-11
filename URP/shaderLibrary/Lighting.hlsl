#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

#include "../ShaderLibrary/GI.hlsl"

float3 diffuseLight(Surface surface, Light light, BRDF brdf) {
	return saturate(dot(surface.normal, light.direction)) * light.color * light.attenuation * DirectBRDF(surface, brdf, light);
}

bool RenderingLayersOverlap(Surface surface, Light light) {
	return (surface.renderingLayerMask & light.renderingLayerMask) != 0;
}

float3 diffuse(Surface surface, BRDF brdf, GI gi) {
	float3 color = IndirectBRDF(surface, brdf, gi.diffuse, gi.specular);
	ShadowData shadowData = getShadowData(surface);
	shadowData.shadowMask = gi.shadowMask;

	for (int i = 0; i < _DirectionalLightCount; i++) {
		Light light = getDirectionLight(i, surface, shadowData);
#if defined(_FORWARDPIPELINE)
		if (RenderingLayersOverlap(surface, light)) {
			color += diffuseLight(surface, light, brdf);
		}
#else
		color += diffuseLight(surface, light, brdf);
#endif
	}

#if defined(_LIGHTS_PER_OBJECT)
	for (int i = 0; i < min(unity_LightData.y, 8); i++) {
		int lightIndex = unity_LightIndices[(uint)i / 4][(uint)i % 4];
		Light light = getOtherLight(lightIndex, surface, shadowData);
#if defined(_FORWARDPIPELINE)
		if (RenderingLayersOverlap(surface, light)) {
			color += diffuseLight(surface, light, brdf);
		}
#else
		color += diffuseLight(surface, light, brdf);
#endif
	}
#else
	for (int i = 0; i < _OtherLightCount; i++) {
		Light light = getOtherLight(i, surface, shadowData);
#if defined(_FORWARDPIPELINE)
		if (RenderingLayersOverlap(surface, light)) {
			color += diffuseLight(surface, light, brdf);
		}
#else
		color += diffuseLight(surface, light, brdf);
#endif
	}
#endif

	return color;
}

#endif