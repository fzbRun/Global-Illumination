#ifndef CUSTOM_PARTICLE_UNLIT_PASS_INCLUDE
#define CUSTOM_PARTICLE_UNLIT_PASS_INCLUDE

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

#include "../shaderLibrary/common.hlsl"

struct Attributes {
	float3 vertex : POSITION;
	float4 color : COLOR;
#if defined(_FLIPBOOK_BLENDING)
	float4 texcoord : TEXCOORD0;
	float flipbookBlend : TEXCOORD1;
#else
	float2 texcoord : TEXCOORD0;
#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 position : SV_POSITION;

#if defined(_VERTEX_COLOR)
	float4 color : VAR_COLOR;
#endif

	float2 uv : VAR_BASE_UV;

#if defined(_FLIPBOOK_BLENDING)
	float3 flipbookUVB : VAR_FLIPBOOK;
#endif

	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings vert(Attributes i) {

	Varyings o;

	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_TRANSFER_INSTANCE_ID(i, o);

	o.position = TransformWorldToHClip(TransformObjectToWorld(i.vertex));
#if defined(_VERTEX_COLOR)
	o.color = i.color;
#endif
	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	o.uv = i.texcoord.xy * baseST.xy + baseST.zw;
#if defined(_FLIPBOOK_BLENDING)
	o.flipbookUVB.xy = TransformBaseUV(i.texcoord.zw);
	o.flipbookUVB.z = i.flipbookBlend;
#endif
	return o;

}

float4 frag(Varyings i) : SV_TARGET{

	UNITY_SETUP_INSTANCE_ID(i);
	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
#if defined(_FLIPBOOK_BLENDING)
	float4 baseMap2 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.flipbookUVB.xy);
	baseMap = lerp(baseMap, baseMap2, i.flipbookUVB.z);
#endif

	Fragment fragment = GetFragment(i.position);

#if defined(_NEAR_FADE)
	float nearAttenuation = (fragment.depth - INPUT_PROP(_NearFadeDistance)) / INPUT_PROP(_NearFadeRange);
	baseMap.a *= saturate(nearAttenuation);
#endif

#if defined(_SOFT_PARTICLES)
	float depthDetla = fragment.bufferDepth - fragment.depth;
	float nearAttenuation2 = (depthDetla - INPUT_PROP(_SoftParticlesDistance)) / INPUT_PROP(_SoftParticlesRange);
	baseMap.a *= saturate(nearAttenuation2);
#endif

	float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);

#if defined(_VERTEX_COLOR)
	float4 color = i.color;
#else
	float4 color = 1;
#endif

	float4 base = baseMap * baseColor * color;

#if defined(_CLIPPING)
	clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _CutOff));
#endif

#if defined(_DISTORTION)
	float4 rawMap = SAMPLE_TEXTURE2D(_DistortionMap, sampler_BaseMap, i.uv);
	float4 rawMap2 = SAMPLE_TEXTURE2D(_DistortionMap, sampler_BaseMap, i.flipbookUVB.xy);
	rawMap = lerp(rawMap, rawMap2, i.flipbookUVB.z);
	float2 offset = DecodeNormal(rawMap, INPUT_PROP(_DistortionStrength)).xy;
	offset *= base.a * 0.1f;
	base.rgb = lerp(getBufferColor(fragment, offset).rgb, base.rgb, saturate(base.a - INPUT_PROP(_DistortionBlend)));
#endif

	return base;

}
#endif