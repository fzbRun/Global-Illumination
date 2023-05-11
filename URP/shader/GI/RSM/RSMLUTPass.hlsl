#ifndef RSM_LUT_PASS_INCLUDE
#define RSM_LUT_PASS_INCLUDE

TEXTURE2D(_RSMTexture);
SAMPLER(sampler_RSMTexture);

TEXTURE2D(_RSMDepthTexture);
SAMPLER(sampler_RSMDepthTexture);

//float4x4 inverseLightViewProjectionMatrix;

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

//float3 GetWorldPositionFromDepth(float2 uv, float depth) {
	//return ComputeWorldSpacePosition(uv, depth, inverseLightViewProjectionMatrix);
//}

float4 frag(Varyings i) : SV_TARGET{

	float4 colorAndDepth = SAMPLE_TEXTURE2D(_RSMTexture, sampler_RSMTexture, i.uv);
	float depth = SAMPLE_TEXTURE2D(_RSMDepthTexture, sampler_RSMDepthTexture, i.uv);
	float3 worldPosition = GetWorldPositionFromLghtDepth(i.uv, depth);

	return float4(worldPosition, 1.0f);

}

#endif