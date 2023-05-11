#ifndef CUSTOM_META_PASS_INCLUDED
#define CUSTOM_META_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadow.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"

CBUFFER_START(UnityMetaPass)
	float unity_OneOverOutputBoost;
	float unity_MaxOutputValue;
	bool4 unity_MetaFragmentControl;
CBUFFER_END

struct Attributes {
	float3 vertex : POSITION;
	float2 texcoord : TEXCOORD0;
	float2 lightMapUV : TEXCOORD1;
};

struct Varyings {
	float4 position : SV_POSITION;
	float2 uv : VAR_BASE_UV;
	float2 detailUV : VAR_DETAIL_UV;
};

Varyings MetaPassVertex(Attributes i) {
	Varyings o;
	i.vertex.xy = i.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
	i.vertex.z = i.vertex.z > 0.0f ? FLT_MIN : 0.0f;
	//o.position = mul(UNITY_MATRIX_VP, i.vertex);
	o.position = TransformWorldToHClip(i.vertex);
	o.uv = TransformBaseUV(i.texcoord);
	o.detailUV = TransformDetailUV(i.texcoord);
	return o;
}

float4 MetaPassFragment(Varyings i) : SV_TARGET{

	InputConfig c = GetInputConfig(i.uv);
#if defined(_MASK_MAP)
	c.useMask = true;
#endif
#if defined(_DETAIL_MAP)
	c.detailUV = i.detailUV;
	c.useDetail = true;
#endif
	float4 base = GetBase(c);

#if defined(_METALLIC_MAP)
	c.useMetallic = true;
#endif

#if defined(_ROUGHNESS_MAP)
	c.useRoughness = true;
#endif

#if defined(_OCCLUSION_MAP)
	c.useOcclusion = true;
#endif

	Surface surface;
	ZERO_INITIALIZE(Surface, surface);
	surface.color = base.rgb;
	surface.alpha = 1.0f;
	surface.metallic = GetMetallic(c);
	float smoothness = GetSmoothness(c);
	surface.roughness = GetRoughness(c) * PerceptualSmoothnessToPerceptualRoughness(smoothness);
	BRDF brdf = getBRDF(surface);

	float4 meta;
	if (unity_MetaFragmentControl.x) {
		meta = float4(brdf.diffuse + brdf.specular * brdf.roughness * 0.5f, 1.0f);
		//meta = float4(1.0f, 1.0f, 1.0f, 1.0f);
		meta.rgb = min(PositivePow(meta.rgb, unity_OneOverOutputBoost), unity_MaxOutputValue);
	}
	else if(unity_MetaFragmentControl.y ){
		meta = float4(getEmission(c), 1.0f);
	}

	return meta;
	//return float4(1.0f, 1.0f, 1.0f, 1.0f);
}
#endif