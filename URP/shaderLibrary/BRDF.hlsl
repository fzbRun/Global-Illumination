#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

#define MIN_REFLECTIVITY 0.04

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

struct BRDF {
	float3 diffuse;
	float3 specular;
	float roughness;
	float perceptualRoughness;
	float fresnel;
};

float OnMinusReflectivity(float metallic) {
	float range = 1.0 - MIN_REFLECTIVITY;
	return range - metallic * range;
}

//看不懂这个公式，好像直接将所有项统一了，就当brdf算好了。
float SpecularStrength(Surface surface, BRDF brdf, Light light) {
	float3 h = normalize(surface.viewDir + light.direction);
	float nh2 = Square(saturate(dot(surface.normal, h)));
	float lh2 = Square(saturate(dot(light.direction, h)));
	float r2 = Square(brdf.roughness);
	float d2 = Square(nh2 * (r2 - 1) + 1.001f);
	float normalization = 4.0f * brdf.roughness + 2.0f;
	return r2 / (d2 * max(0.1f, nh2) * normalization);
}

/*
float3 DirectBRDF(Surface surface, BRDF brdf, Light light) {
	return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}
*/

//菲涅尔项
float3 fresnelSchlick(float cosTheta, float3 F0) {
	return F0 + (1.0f - F0) * pow(1.0f - cosTheta, 5.0f);
}

float3 fresnelSchlickRoughness(float cosTheta, float3 F0, float roughness)
{
	return F0 + (max(1.0 - roughness, F0) - F0) * pow(saturate(1.0 - cosTheta), 5.0);
}

//法线分布函数
float DistributionGGX(float3 N, float3 H, float roughness)
{
	float a = roughness * roughness;
	float a2 = a * a;
	float NdotH = max(dot(N, H), 0.0);
	float NdotH2 = NdotH * NdotH;

	float nom = a2;
	float denom = (NdotH2 * (a2 - 1.0) + 1.0);
	denom = PI * denom * denom;

	return nom / denom;
}

//几何屏蔽项
float GeometrySchlickGGX(float NdotV, float roughness)
{
	float r = (roughness + 1.0);
	float k = (r * r) / 8.0;

	float nom = NdotV;
	float denom = NdotV * (1.0 - k) + k;

	return nom / denom;
}
float GeometrySmith(float3 N, float3 V, float3 L, float roughness)
{
	float NdotV = max(dot(N, V), 0.0);
	float NdotL = max(dot(N, L), 0.0);
	float ggx2 = GeometrySchlickGGX(NdotV, roughness);
	float ggx1 = GeometrySchlickGGX(NdotL, roughness);

	return ggx1 * ggx2;
}

float3 DirectBRDF(Surface surface, BRDF brdf, Light light) {

	float3 N = normalize(surface.normal);
	float3 L = normalize(light.direction);
	float3 V = normalize(surface.viewDir);
	float3 H = normalize(L + V);

	float F0 = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);
	float FS = fresnelSchlick(saturate(dot(H, V)), F0);
	float NDF = DistributionGGX(N, H, brdf.roughness);
	float G = GeometrySmith(N, V, L, brdf.roughness);

	float FD = 1.0f - FS;	//金属值的影响提前在算brdf时以及考虑

	float3 nominator = FS * NDF * G;
	float denominator = 4.0f * saturate(dot(N, L)) * saturate(dot(N, V)) + 0.001f;
	float specular = nominator / denominator;

	float NL = saturate(dot(N, L));

	return (specular * brdf.specular + FD * brdf.diffuse / PI) * NL;
}

//diffuse表示漫反射光照，brdf.diffuse表示材质对漫反射光照的反射率。
float3 IndirectBRDF(Surface surface, BRDF brdf, float3 diffuse, float3 specular) {

	float fresnelStrength = surface.fresnel * Pow4(1.0f - saturate(dot(surface.normal, surface.viewDir)));
	float3 reflection = specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);

	reflection /= brdf.roughness * brdf.roughness + 1.0f;

	return (diffuse * brdf.diffuse + reflection) * surface.occlusion;

}

BRDF getBRDF(Surface surface) {

	float oneMinusReflectivity = OnMinusReflectivity(surface.metallic);	//计算漫反射率

	BRDF brdf;
	brdf.diffuse = surface.color * oneMinusReflectivity * surface.alpha;
	//brdf.specular = surface.specular * lerp(MIN_REFLECTIVITY * surface.color, surface.color, surface.metallic);	//根据金属值得到夹角为90度时的镜面反射率
	brdf.specular = lerp(Luminance(surface.color), surface.color, surface.metallic);	//根据金属值得到夹角为90度时的镜面反射率
	brdf.perceptualRoughness = RoughnessToPerceptualRoughness(surface.roughness);	//通过光滑度获得粗糙度
	//brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness) * surface.roughness;
	brdf.roughness = surface.roughness;
	float smoothness = RoughnessToPerceptualSmoothness(surface.roughness);
	brdf.fresnel = saturate(smoothness + 1.0f - oneMinusReflectivity) * surface.fresnel;
	return brdf;

}

#endif