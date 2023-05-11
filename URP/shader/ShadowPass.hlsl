#ifndef CUSTOM_SHADOW_PASS_INCLUDE
#define CUSTOM_SHADOW_PASS_INCLUDE

#include "../shaderLibrary/Surface.hlsl"
#include "../shaderLibrary/Light.hlsl"
#include "../shaderLibrary/BRDF.hlsl"
#include "../shaderLibrary/Lighting.hlsl"

struct Attributes {
	float3 vertex : POSITION;
	float2 texcoord : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 position : SV_POSITION;
	float2 uv : VAR_BASE_UV;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

bool _ShadowPancaking;

Varyings ShadowCasterPassVertex(Attributes i) {

	Varyings o;

	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_TRANSFER_INSTANCE_ID(i, o);

	float3 worldPosition = TransformObjectToWorld(i.vertex.xyz);
	o.position = TransformWorldToHClip(worldPosition);

	if (_ShadowPancaking) {
#if UNITY_REVERSED_Z
		o.position.z = min(
			o.position.z, o.position.w * UNITY_NEAR_CLIP_VALUE
		);
#else
		o.position.z = max(
			o.position.z, o.position.w * UNITY_NEAR_CLIP_VALUE
		);
#endif
	}

	o.uv = TransformBaseUV(i.texcoord);
	return o;

}

void ShadowCasterPassFragment(Varyings i){

	UNITY_SETUP_INSTANCE_ID(i);
	ClipLOD(i.position, unity_LODFade.x);

	InputConfig c = GetInputConfig(i.uv, 0.0f);
	float4 base = GetBase(c);

#if defined(_SHADOWS_CLIP)
	clip(base.a - GetCutOff(c));
#elif defined(_SHADOWS_DITHER)
	float dither = InterleavedGradientNoise(i.position.xy, 0);
	clip(base.a - dither);
#endif

}
#endif