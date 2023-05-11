#ifndef CUSTOM_GI_INCLUED
#define CUSTOM_GI_INCLUED

#if defined(LIGHTMAP_ON)
	#define GI_ATTRIBUTE_DATA float2 lightMapUV : TEXCOORD1;
	#define GI_VARYINGS_DATA float2 lightMapUV : VAR_LIGHT_MAP_UV;
	#define TRANSFER_GI_DATA(input, output) \
		output.lightMapUV = input.lightMapUV * \
		unity_LightmapST.xy + unity_LightmapST.zw;
	#define GI_FRAGMENT_DATA(input) input.lightMapUV
#else
	#define GI_ATTRIBUTE_DATA
	#define GI_VARYINGS_DATA
	#define TRANSFER_GI_DATA(input, output)
	#define GI_FRAGMENT_DATA(input) 0.0
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"

TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

TEXTURE2D(unity_ShadowMask);
SAMPLER(samplerunity_ShadowMask);

TEXTURE3D_FLOAT(unity_ProbeVolumeSH);	//记录Light Probe Group反射探针的漫反射光照
SAMPLER(samplerunity_ProbeVolumeSH);

TEXTURECUBE(unity_SpecCube0);	//记录Reflection反射探针高光的cubeMap
SAMPLER(samplerunity_SpecCube0);

TEXTURE2D(IBL_irradianceMap);
TEXTURE2D(IBL_lobeSpecularMap);
TEXTURE2D(IBL_BRDFMap);
SAMPLER(samplerIBL_irradianceMap);
float4 IBL_irradianceMap_TexelSize;
float4 IBL_lobeSpecularMap_TexelSize;
float4 IBL_BRDFMap_TexelSize;
float IBL_Intensity;

TEXTURE2D(_RSMTexture);
TEXTURE2D(_RSMNormalTexture);
SAMPLER(sampler_RSMTexture);
TEXTURE2D(_RSMDepthTexture);
SAMPLER(sampler_RSMDepthTexture);
float4 _RSMTexture_TexelSize;
float RSMSampleSize;
float RSMIntensity;

TEXTURE3D(_LPVTexture_R);
TEXTURE3D(_LPVTexture_G);
TEXTURE3D(_LPVTexture_B);
SAMPLER(sampler_LPVTexture_R);
float3 VoxelBoxStartPoint;
float3 VoxelBoxSize;
float VoxelSize;
float LPV_Intensity;

struct GI {
	float3 diffuse;
	float3 specular;
	ShadowMask shadowMask;
};

//采样光照贴图
float3 sampleLightMap(float2 lightMapUV) {
#if defined(LIGHTMAP_ON)
	return SampleSingleLightmap(TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap), lightMapUV, float4(1.0f, 1.0f, 0.0f, 0.0f),
#if defined(UNITY_LIGHTMAP_FULL_HDR)
	false,
#else 
	true,
#endif
	float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0f, 0.0f));
#else
	return 0.0f;
#endif
}

float4 sampleBakedShadows(float2 lightMapUV, Surface surface) {
#if defined(LIGHTMAP_ON)
	return SAMPLE_TEXTURE2D(unity_ShadowMask, samplerunity_ShadowMask, lightMapUV);
#else
	if (unity_ProbeVolumeParams.x) {
		return SampleProbeOcclusion(
			TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
			surface.position, unity_ProbeVolumeWorldToObject,
			unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
			unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
		);
	}
	else {
		return unity_ProbesOcclusion;
	}
	//return 1.0f;
#endif
}

//采样光照探针
float3 SampleLightProbe(Surface surface) {
#if defined(LIGHTMAP_ON)
	return 0.0f;
#else
	if (unity_ProbeVolumeParams.x) {
		return SampleProbeVolumeSH4(
			TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
			surface.position, surface.normal,
			unity_ProbeVolumeWorldToObject,
			unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
			unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
		);
	}
	else {
		float4 coefficients[7];
		coefficients[0] = unity_SHAr;
		coefficients[1] = unity_SHAg;
		coefficients[2] = unity_SHAb;
		coefficients[3] = unity_SHBr;
		coefficients[4] = unity_SHBg;
		coefficients[5] = unity_SHBb;
		coefficients[6] = unity_SHC;
		return max(0.0f, SampleSH9(coefficients, surface.normal));
	}
#endif
}

float3 sampleEnvironment(Surface surface, BRDF brdf) {

	float3 uvw = reflect(-surface.viewDir, surface.normal);
	float mip = PerceptualRoughnessToMipmapLevel(brdf.perceptualRoughness);
	float4 environment = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, uvw, mip);
	return DecodeHDREnvironment(environment, unity_SpecCube0_HDR);

}

float2 getOctahedralUVfromNormal(float3 normal) {

	float3 normal_abs = abs(normal);
	float2 uv = float2(normal_abs.x, normal_abs.y) * (1.0f / (normal_abs.x + normal_abs.y + normal_abs.z));
	if (normal.z < 0.0f) {
		uv = 1.0f - float2(uv.y, uv.x);
	}
	uv *= float2(sign(normal.x), sign(normal.y));
	uv = uv * 0.5f + 0.5f;
	return uv;

}

void sampleIBL(Surface surface, BRDF brdf, out float3 giDiffuse, out float3 giSpecualr) {

	//获得八面体映射的UV
	float3 normal = surface.normal;
	float3 normal_abs = abs(normal);
	float2 uv = getOctahedralUVfromNormal(normal);
	uv += 0.5f * IBL_irradianceMap_TexelSize.xy;

	giDiffuse = SAMPLE_TEXTURE2D(IBL_irradianceMap, samplerIBL_irradianceMap, uv) * surface.color * IBL_Intensity;

	float3 V = surface.viewDir;
	float3 R = reflect(-surface.viewDir, normal);
	float3 F0 = lerp(0.04f, surface.color, surface.metallic);
	float3 F = fresnelSchlickRoughness(saturate(dot(normal, V)), F0, brdf.roughness);
	const float MAX_REFLECTION_LOD = 4.0;
	
	uv = getOctahedralUVfromNormal(R);
	uv += 0.5f * IBL_lobeSpecularMap_TexelSize.xyz;
	float3 lobeSpecularColor = SAMPLE_TEXTURE2D_LOD(IBL_lobeSpecularMap, samplerIBL_irradianceMap, uv, brdf.roughness * MAX_REFLECTION_LOD);

	uv = float2(max(dot(normal, V), 0.0f), brdf.roughness);
	uv += 0.5f * IBL_BRDFMap_TexelSize;
	float2 brdf_sum = SAMPLE_TEXTURE2D(IBL_BRDFMap, samplerIBL_irradianceMap, uv);

	giSpecualr = lobeSpecularColor * (F * brdf_sum.x + brdf_sum.y) * IBL_Intensity;

}

float RadicalInverse_VdC(uint bits)
{
	bits = (bits << 16u) | (bits >> 16u);
	bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
	bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
	bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
	bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
	return float(bits) * 2.3283064365386963e-10; // / 0x100000000
}

float3 sampleRSM(Surface surface, BRDF brdf) {

	float3 worldPos = surface.position;
	float3 normal = surface.normal;

	float4 clipPos = mul(LightVPMatrix, float4(worldPos, 1.0f));
#if UNITY_UV_STARTS_AT_TOP
clipPos.y = -clipPos.y;
#endif
float3 ndcPos = clipPos.xyz / clipPos.w;
float2 uv = ndcPos.xy * 0.5f + 0.5f;

float3 diffuse = 0.0f;
//上面这个均匀样本分布好像要1024次才能遍历到所有情况，那么我们做一个缩放来快速较为平均的遍历
float sampleScale = 1024 / RSMSampleSize;
#if defined(_FastRSM)
#else

[unroll(RSMSampleSize)];
for (int i = 0; i < RSMSampleSize; i++) {

	float random1 = i / RSMSampleSize;
	float random2 = RadicalInverse_VdC(i * sampleScale);
	float2 random = float2(random1, random2) * 2.0f - 1.0f;
	random = random * random * random;
	random = random * random * random;
	float2 maxSclae = float2(min(uv.x, 1 - uv.x), min(uv.y, 1 - uv.y));
	float2 st = float2(uv.x + maxSclae.x * random.x * sin(2 * PI * random.y), uv.y + maxSclae.y * random.y * cos(2 * PI * random.x));

	float4 rsmColorAndDepth = SAMPLE_TEXTURE2D(_RSMTexture, sampler_RSMTexture, st);
	float3 rsmColor = rsmColorAndDepth.xyz;

	float rsmDepth = SAMPLE_TEXTURE2D(_RSMDepthTexture, sampler_RSMDepthTexture, st);

	float3 rsmNormal = SAMPLE_TEXTURE2D(_RSMNormalTexture, sampler_RSMTexture, st);
	rsmNormal = rsmNormal * 2.0f - 1.0f;

	float3 rsmWorldPos = GetWorldPositionFromLghtDepth(st, rsmDepth);
	float3 rsmDir = rsmWorldPos - worldPos;
	float distance = max(length(rsmDir), 0.01f);

	float3 rsmDiffuse = rsmColor * saturate(dot(rsmNormal, -rsmDir)) * saturate(dot(normal, rsmDir));
	rsmDiffuse /= distance * distance * distance * distance;

	diffuse += rsmDiffuse * abs(random.x * random.y) * RSMIntensity;

}
#endif

return diffuse * surface.color;

}

float4 getSHFunction(float3 normal)
{
	float x = normal.x;
	float y = normal.y;
	float z = normal.z;
	float4 SHFunction = float4(
		0.886226925f,
		-1.02332671f * y,
		1.02332671f * z,
		-1.02332671f * x
		/*
		sqrt(1.0f / PI) * 0.5f,

		-sqrt(3.0f / (4.0f * PI)) * y,
		sqrt(3.0f / (4.0f * PI)) * z,
		-sqrt(3.0f / (4.0f * PI)) * x

		sqrt(15.0f / PI) * 0.5f * x * y,
		sqrt(15.0f / PI) * 0.5f * y * z,
		sqrt(5.0f / PI) * 0.25f * (-x * x - y * y + 2 * z * z),
		sqrt(15.0f / PI) * 0.5f * z * x,
		sqrt(15.0f / PI) * 0.25f * (x * x - y * y),

		sqrt(35.0f / (2.0f * PI)) * 0.25f * (3 * x * x - y * y) * y,
		sqrt(105.0f / PI) * 0.5f * x * z * y,
		sqrt(21.0f / (2.0f * PI)) * 0.25f * y * (4 * z * z - x * x - y * y),
		sqrt(7.0f / PI) * 0.25f * z * (2 * z * z - 3 * x * x - 3 * y * y),
		sqrt(21.0f / (2.0f * PI)) * 0.25f * x * (4 * z * z - x * x - y * y),
		sqrt(105.0f / PI) * 0.25f * (x * x - y * y) * z,
		sqrt(35.0f / (2.0f * PI)) * 0.25f * (x * x - 3 * y * y) * x
		*/
		);
	return SHFunction;
}

float3 triSampleLPV(float3 worldPos, float3 normal) {

	float3 diffuse = 0.0f;
	float halfVoxelSize = 0.5f * VoxelBoxSize;

	float3 centerVoxelIndex = floor((worldPos - VoxelBoxStartPoint) / VoxelSize);
	float3 voxelCenterPos = centerVoxelIndex * VoxelBoxSize + VoxelBoxStartPoint;
	centerVoxelIndex += sign((worldPos - voxelCenterPos) / VoxelSize - 0.5f);
	voxelCenterPos = centerVoxelIndex * VoxelBoxSize + VoxelBoxStartPoint;

	for (int x = -1; x < 2; x += 2) {
		for (int y = -1; y < 2; y += 2) {
			for (int z = -1; z < 2; z += 2) {

				float3 offsetIndex = centerVoxelIndex + float3(x, y, z);
				float3 offsetVoxelCenterPos = offsetIndex * VoxelBoxSize + VoxelBoxStartPoint + halfVoxelSize;
				float3 uvw = offsetIndex / VoxelBoxSize;

				float4 SHCofe_R = SAMPLE_TEXTURE3D(_LPVTexture_R, sampler_LPVTexture_R, uvw);
				float4 SHCofe_G = SAMPLE_TEXTURE3D(_LPVTexture_G, sampler_LPVTexture_R, uvw);
				float4 SHCofe_B = SAMPLE_TEXTURE3D(_LPVTexture_B, sampler_LPVTexture_R, uvw);

				float3 sampleDir = normalize(offsetVoxelCenterPos - worldPos);
				float4 SHFunction = getSHFunction(sampleDir);
				float3 diffuseOffset = float3(dot(SHCofe_R, SHFunction), dot(SHCofe_G, SHFunction), dot(SHCofe_B, SHFunction));

				float3 disScale = abs(worldPos - offsetVoxelCenterPos) / VoxelBoxSize;
				float weight = disScale.x * disScale.y * disScale.z;
				diffuseOffset *= weight;

				float3 distanceOffset = abs(offsetVoxelCenterPos - worldPos);
				if (distanceOffset.x < halfVoxelSize || distanceOffset.y < halfVoxelSize || distanceOffset.z < halfVoxelSize){
					diffuse += diffuseOffset;
				}
				else if (dot(normal, sampleDir) > 0.0f) {
					diffuse += diffuseOffset;
				}

			}
		}
	}

	return saturate(diffuse);

}

float3 sampleLPV(Surface surface, BRDF brdf) {

	float4 SHCofe_R = 0.0f;
	float4 SHCofe_G = 0.0f;
	float4 SHCofe_B = 0.0f;

	float3 worldPos = surface.position;
	float3 normal = surface.normal;
#if defined(_LPV)

	float3 index = (worldPos - VoxelBoxStartPoint) / VoxelSize;
	float3 uvw = index / VoxelBoxSize;

	SHCofe_R = SAMPLE_TEXTURE3D(_LPVTexture_R, sampler_LPVTexture_R, uvw);
	SHCofe_G = SAMPLE_TEXTURE3D(_LPVTexture_G, sampler_LPVTexture_R, uvw);
	SHCofe_B = SAMPLE_TEXTURE3D(_LPVTexture_B, sampler_LPVTexture_R, uvw);

#elif defined(_SSLPV)

	float3 index = (worldPos - VoxelBoxStartPoint) / VoxelSize;
	float3 uvw = index / VoxelBoxSize;

	SHCofe_R = SAMPLE_TEXTURE3D(_LPVTexture_R, sampler_LPVTexture_R, uvw);
	SHCofe_G = SAMPLE_TEXTURE3D(_LPVTexture_G, sampler_LPVTexture_R, uvw);
	SHCofe_B = SAMPLE_TEXTURE3D(_LPVTexture_B, sampler_LPVTexture_R, uvw);

#endif

	float4 SHFunction = getSHFunction(normal);
	float3 diffuse = float3(dot(SHCofe_R, SHFunction), dot(SHCofe_G, SHFunction), dot(SHCofe_B, SHFunction));

	diffuse *= surface.color * LPV_Intensity;

	return saturate(diffuse);
	/*
	float3 pos = surface.position;
	normal = surface.normal;
	float4 clipPos = mul(LightVPMatrix, float4(pos, 1.0f));
#if UNITY_UV_STARTS_AT_TOP
	clipPos.y = -clipPos.y;
#endif
	float3 ndcPos = clipPos.xyz / clipPos.w;
	float2 uv = ndcPos.xy * 0.5f + 0.5f;

	float depth = SAMPLE_TEXTURE2D(_RSMDepthTexture, sampler_RSMDepthTexture, uv);
	pos = ComputeWorldSpacePosition(uv, depth, inverseLightViewProjectionMatrix);
	return float4(pos, 1.0f);
	*/
}

GI getGI(float2 lightMapUV, Surface surface, BRDF brdf) {
	GI gi;
#if defined(_FORWARDPIPELINE)
	gi.diffuse = sampleLightMap(lightMapUV) + SampleLightProbe(surface);
	gi.specular = sampleEnvironment(surface, brdf);
#else
	gi.diffuse = 0.0f;
	gi.specular = 0.0f;
#if defined(_USEIBL)
	sampleIBL(surface, brdf, gi.diffuse, gi.specular);
#endif

#if defined(_RSM)
	gi.diffuse += sampleRSM(surface, brdf);
#endif

#if defined(_LPV) || defined(_SSLPV)
	gi.diffuse += sampleLPV(surface, brdf);
#endif

#endif
	gi.shadowMask.always = false;
	gi.shadowMask.distance = false;
	gi.shadowMask.shadows = 1.0f;

#if defined(_SHADOW_MASK_ALWAYS)
	gi.shadowMask.always = true;
	gi.shadowMask.shadows = sampleBakedShadows(lightMapUV, surface);
#elif defined(_SHADOW_MASK_DISTANCE)
	gi.shadowMask.distance = true;
	gi.shadowMask.shadows = sampleBakedShadows(lightMapUV, surface);
#endif
	return gi;
}

#endif