#ifndef CUSTOM_CAMERA_RENDERER_PASSES_INCLUDED
#define CUSTOM_CAMERA_RENDERER_PASSES_INCLUDED

TEXTURE2D(_SourceTexture);

struct Varyings { 
	float4 vertex : SV_POSITION;
	float2 uv : VAR_SCREEN_UV;
};

Varyings DefaultPassVertex(uint vertexID : SV_VertexID) { 
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

float4 CopyPassFragment(Varyings input) : SV_TARGET{
	return SAMPLE_TEXTURE2D_LOD(_SourceTexture, sampler_linear_clamp, input.uv, 0);
}

float CopyDepthPassFragment(Varyings i) : SV_TARGET{
	return SAMPLE_DEPTH_TEXTURE_LOD(_SourceTexture, sampler_point_clamp, i.uv, 0);
}

#endif