#ifndef CUSTOM_DEFERRED_LIT_PASS_INCLUDE
#define CUSTOM_DEFERRED_LIT_PASS_INCLUDE

#include "../../shaderLibrary/Surface.hlsl"
#include "../../shaderLibrary/Shadow.hlsl"
#include "../../shaderLibrary/Light.hlsl"
#include "../../shaderLibrary/BRDF.hlsl"
#include "../../ShaderLibrary/GI.hlsl"
#include "../../shaderLibrary/Lighting.hlsl"

TEXTURE2D(_AlbedoTexture);
TEXTURE2D(_NormalTexture);
TEXTURE2D(_MVRMTexture);
TEXTURE2D(_EmissionAndOcclusionTexture);
SAMPLER(sampler_AlbedoTexture);
SAMPLER(sampler_NormalTexture);

struct Varyings {
	float4 vertex : SV_POSITION;
	float2 uv : VAR_SCREEN_UV;
};

Varyings vert(uint vertexID : SV_VertexID) {

	Varyings o;

	o.vertex = float4(
		vertexID <= 1 ? -1.0f : 3.0f,
		vertexID == 0 ? 3.0f : -1.0f,
		0.0f, 1.0f
		);
	o.uv = float2(
		vertexID <= 1 ? 0.0f : 2.0f,
		vertexID == 0 ? 2.0f : 0.0f
		);

	if (_ProjectionParams.x < 0.0f) {
		o.uv.y = 1.0f - o.uv.y;
	}

	return o;
}

/*
float3 GetWorldPositionFromDepth(float2 uv, float depth) {

	//得用unity的函数才能正确的深度重建，八成是和平台性相关
	//return ComputeWorldSpacePosition(uv, depth, inverseViewProjectionMatrix);

/*
//对于DirectX
#if UNITY_REVERSED_Z
	depth = 1.0f - depth;
#else
	depth = depth * 2.0f - 1.0f;
#endif	//Opengl

	float2 xy = uv * 2.0f - 1.0f;
	float3 ndcPos = float3(xy, depth);
	float4 worldPos = mul(inverseViewProjectionMatrix, float4(ndcPos, 1.0f));
	worldPos = worldPos.x / worldPos.w;
	return worldPos;

}
*/

float4 frag(Varyings i, out float depthOut : SV_DEPTH) : SV_TARGET{

	float4 albedo = SAMPLE_TEXTURE2D(_AlbedoTexture, sampler_AlbedoTexture, i.uv);

	float4 normalAndFresnel = SAMPLE_TEXTURE2D(_NormalTexture, sampler_NormalTexture, i.uv);
	float3 normal = normalAndFresnel.rgb * 2.0f - 1.0f;
	float fresnel = normalAndFresnel.a;

	float4 MVRM = SAMPLE_TEXTURE2D(_MVRMTexture, sampler_NormalTexture, i.uv);
	float2 motionVector = MVRM.rg;
	float roughness = MVRM.b;
	float metallic = MVRM.a;

	float4 EmissionAndOcclusion = SAMPLE_TEXTURE2D(_EmissionAndOcclusionTexture, sampler_NormalTexture, i.uv);
	float3 emission = EmissionAndOcclusion.rgb;
	float occlusion = EmissionAndOcclusion.a;

	float depth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_NormalTexture, i.uv);

	float3 worldPosition = GetWorldPositionFromDepth(i.uv, depth);

	Surface surface;
	surface.uv = i.uv;
	surface.position = worldPosition;
	surface.normal = normal;
	surface.interpolatedNormal = normal;
	surface.viewDir = normalize(_WorldSpaceCameraPos - worldPosition);
	surface.depth = mul(UNITY_MATRIX_V, worldPosition).z;
	surface.color = albedo.rgb;
	surface.alpha = albedo.a;
	surface.metallic = metallic;
	surface.roughness = roughness;
	surface.fresnel = fresnel;
	surface.occlusion = occlusion;
	surface.dither = InterleavedGradientNoise(i.uv, 0);
	surface.renderingLayerMask = asuint(unity_RenderingLayer.x);

	BRDF brdf = getBRDF(surface);

	GI gi = getGI(GI_FRAGMENT_DATA(i), surface, brdf);

	float3 color = diffuse(surface, brdf, gi);
	color += 0.1f * albedo;
	color += emission;

	depthOut = depth;
#if defined(_OnlyGI)
	/*
	float3 camerPos = ComputeWorldSpacePosition(i.uv, depth, inverseProjectionMatrix);
	float3 k = camerPos.z > -8.0f ? 1.0f : 0.0f;
	return float4(color * 0.01f + k, 1.0f);
	*/
	return float4(color * 0.001f + gi.diffuse, getFinalAlpha(surface.alpha));
#else
	return float4(color, getFinalAlpha(surface.alpha));
#endif

}

#endif