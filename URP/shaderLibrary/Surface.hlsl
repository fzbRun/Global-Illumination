#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

struct Surface {
	float3 position;
	float3 normal;
	float2 uv;
	float3 interpolatedNormal;
	float3 viewDir;
	float depth;
	float3 color;
	//float3 specular;
	float alpha;
	float metallic;
	float roughness;
	//float smoothness;
	float fresnel;
	float occlusion;
	float dither;
	uint renderingLayerMask;
};


#endif