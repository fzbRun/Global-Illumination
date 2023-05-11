#ifndef CUSTOM_LIT_PASS_INCLUDE
#define CUSTOM_LIT_PASS_INCLUDE

#include "../shaderLibrary/Surface.hlsl"
#include "../shaderLibrary/Shadow.hlsl"
#include "../shaderLibrary/Light.hlsl"
#include "../shaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../shaderLibrary/Lighting.hlsl"

struct Attributes {
	float3 vertex : POSITION;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float2 texcoord : TEXCOORD0;
	GI_ATTRIBUTE_DATA
		UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 position : SV_POSITION;
	float3 worldPosition : VAR_POSITION;
	float3 normal : VAR_NORMAL;
#if defined(_NORMAL_MAP)
	float4 tangent : VAR_TANGENT;
#endif
#if defined(_HEIGHT_MAP)
	float3 dirView : VAR_DIRVIEW;
#endif
	float2 uv : VAR_BASE_UV;
	float2 detailUV : VAR_DETAIL_UV;
	GI_VARYINGS_DATA
		UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings vert(Attributes i) {

	Varyings o;

	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_TRANSFER_INSTANCE_ID(i, o);
	TRANSFER_GI_DATA(i, o);

	o.worldPosition = TransformObjectToWorld(i.vertex.xyz);
	o.position = TransformWorldToHClip(o.worldPosition);
	o.normal = TransformObjectToWorldNormal(i.normal);
	o.uv = TransformBaseUV(i.texcoord);

#if defined(_NORMAL_MAP)
	o.tangent = float4(TransformObjectToWorldDir(i.tangent.xyz), i.tangent.w);
#endif

#if defined(_HEIGHT_MAP)
	o.tangent = float4(TransformObjectToWorldDir(i.tangent.xyz), i.tangent.w);
	float3 bitangent = cross(o.normal, o.tangent.xyz) * o.tangent.w;
	float3x3 TBN_I = transpose(float3x3(o.tangent.xyz, bitangent, o.normal));
	o.dirView = mul(TBN_I, _WorldSpaceCameraPos.xyz - o.worldPosition.xyz);
#endif

#if defined(_DETAIL_MAP)
	o.detailUV = TransformDetailUV(i.texcoord);
#endif

#if UNITY_REVERSED_Z	//相机朝向负z轴,如果点在近平面外，则取近平面
	o.position.z = min(o.position.z, o.position.w * UNITY_NEAR_CLIP_VALUE);
#else
	o.position.z = max(o.position.z, o.position.w * UNITY_NEAR_CLIP_VALUE);
#endif

	return o;

}

#if defined(_FORWARDPIPELINE)
float4 frag(Varyings i) : SV_TARGET{

	UNITY_SETUP_INSTANCE_ID(i);

	ClipLOD(i.position, unity_LODFade.x);

	float3 normal = normalize(i.normal);

#if defined(_HEIGHT_MAP)
	i.uv = getHeightUV(i.uv, normalize(i.dirView));
	if (i.uv.x > 1.0 || i.uv.y > 1.0 || i.uv.x < 0.0 || i.uv.y < 0.0)
		discard;
#endif

	InputConfig c = GetInputConfig(i.uv);
#if defined(_MASK_MAP)
	c.useMask = true;
#endif
#if defined(_DETAIL_MAP)
	c.detailUV = i.detailUV;
	c.useDetail = true;
#endif

	float4 base = GetBase(c);

#if defined(_CLIPPING)
	clip(base.a - GetCutOff(c));
#endif

	Surface surface;
	surface.position = i.worldPosition;
#if defined(_NORMAL_MAP)
	surface.normal = NormalTangentToWorld(GetNormal(c), normal, normalize(i.tangent));
	surface.interpolatedNormal = i.normal;
#else
	surface.normal = normal;
	surface.interpolatedNormal = normal;
#endif

/*
#if defined(_SPECULAR_MAP)
	c.useSpecular = true;
#endif
*/
#if defined(_METALLIC_MAP)
	c.useMetallic = true;
#endif

#if defined(_ROUGHNESS_MAP)
	c.useRoughness = true;
#endif

#if defined(_OCCLUSION_MAP)
	c.useOcclusion = true;
#endif

	surface.uv = i.uv;
	surface.viewDir = normalize(_WorldSpaceCameraPos - i.worldPosition);
	//文章中说这里要用这个函数，但是我这里使用会出错，会有两个视角阴影消失，所以直接左乘。
	//surface.depth = -TransformWorldToView(i.position).z;
	surface.depth = mul(UNITY_MATRIX_V, i.worldPosition).z;
	surface.color = base.rgb;
	surface.alpha = base.a;
	//surface.specular = GetSpecular(c);
	surface.metallic = GetMetallic(c);
	float smoothness = GetSmoothness(c);
	surface.roughness = GetRoughness(c) * PerceptualSmoothnessToPerceptualRoughness(smoothness);
	surface.fresnel = GetFresnel(c);
	surface.occlusion = GetOcclusion(c);
	surface.dither = InterleavedGradientNoise(i.position.xy, 0);
	surface.renderingLayerMask = asuint(unity_RenderingLayer.x);

	BRDF brdf = getBRDF(surface);

	GI gi = getGI(GI_FRAGMENT_DATA(i), surface, brdf);

	float3 color = diffuse(surface, brdf, gi);
	color += 0.1f * base;
	color += getEmission(c);

	return float4(color, getFinalAlpha(surface.alpha));

}
#else
void frag(
	Varyings i,
	out float4 GT0 : SV_Target0,
	out float4 GT1 : SV_Target1,
	out float4 GT2 : SV_Target2,
	out float4 GT3 : SV_Target3)
{

	UNITY_SETUP_INSTANCE_ID(i);

	ClipLOD(i.position, unity_LODFade.x);

	float3 normal = normalize(i.normal);

#if defined(_HEIGHT_MAP)
	i.uv = getHeightUV(i.uv, normalize(i.dirView));
	if (i.uv.x > 1.0 || i.uv.y > 1.0 || i.uv.x < 0.0 || i.uv.y < 0.0)
		discard;
#endif

	InputConfig c = GetInputConfig(i.uv);

#if defined(_MASK_MAP)
	c.useMask = true;
#endif
#if defined(_DETAIL_MAP)
	c.detailUV = i.detailUV;
	c.useDetail = true;
#endif

#if defined(_NORMAL_MAP)
	normal = NormalTangentToWorld(GetNormal(c), normal, normalize(i.tangent));
#endif

	float4 base = GetBase(c);

#if defined(_CLIPPING)
	clip(base.a - GetCutOff(c));
#endif

/*
#if defined(_SPECULAR_MAP)
	c.useSpecular = true;
#endif
*/
#if defined(_METALLIC_MAP)
	c.useMetallic = true;
#endif

#if defined(_ROUGHNESS_MAP)
	c.useRoughness = true;
#endif

#if defined(_OCCLUSION_MAP)
	c.useOcclusion = true;
#endif

	//float specular = GetSpecular(c);
	float smoothness = GetSmoothness(c);
	float roguhness = GetRoughness(c) * PerceptualSmoothnessToPerceptualRoughness(smoothness);
	float metallic = GetMetallic(c);
	float fresnel = GetFresnel(c);
	float3 emission = getEmission(c);
	float occlusion = GetOcclusion(c);

	GT0 = base;
	GT1 = float4(normal * 0.5f + 0.5f, fresnel);
	GT2 = float4(1.0f, 1.0f, roguhness, metallic);
	GT3 = float4(emission, occlusion);

}
#endif

#endif