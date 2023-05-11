#ifndef CUSTOM_LPV_INJECT_PASS_INCLUDE
#define CUSTOM_LPV_INJECT_PASS_INCLUDE

sampler2D _RSMTexture;
sampler2D _RSMNormalTexture;
sampler2D _RSMDepthTexture;
SAMPLER(sampler_RSMTexture);
float RSMMapSize;
float3 VoxelBoxStartPoint;
float3 VoxelBoxSize;
float VoxelSize;
int radiateSize;
/*
struct Varyings {
	float4 vertex : SV_POSITION;
	int3 voxelIndex : VAR_VOXELINDEX;
	float3 flux : VAR_FLUX;
	float3 normal : VAR_NORMAL;
	float2 uv : VAR_SCREEN_UV;
};

Varyings vert(uint vertexID : SV_VertexID) {

	Varyings o;

	o.uv = (float)vertexID / RSMMapSize;

	if (_ProjectionParams.x < 0.0f) {
		o.uv.y = 1.0f - o.uv.y;
	}

	float halfVoxelSize = VoxelSize * 0.5f;

	float depth = tex2D(_RSMDepthTexture, o.uv);
	float3 worldPos = GetWorldPositionFromLghtDepth(o.uv, depth);
	o.normal = tex2D(_RSMNormalTexture, o.uv).xyz * 2.0f - 1.0f;
	o.flux = tex2D(_RSMTexture, o.uv);
	o.voxelIndex = floor(worldPos + 0.5f * o.normal - VoxelBoxStartPoint) / VoxelSize;

	o.vertex = float4((o.voxelIndex.xy + 0.5f) / VoxelBoxSize.xy * 2.0f - 1.0f, 0.0f, 1.0f);

	return o;
}

struct g2f
{
	float3 normal : TEXCOORD0;
	float3 flux : TEXCOORD1;
	float4 vertex : SV_POSITION;
#if UNITY_UV_STARTS_AT_TOP
	float rtIndex : SV_RenderTargetArrayIndex;
#else
	float rtIndex : SV_RenderTargetArrayIndex_REVERSE;
#endif
};

[maxvertexcount(1)]
g2f geom(point Varyings input[1], inout PointStream<g2f> outStream) {

	g2f o;

	o.vertex = input[0].vertex;
	o.normal = input[0].normal;
	o.flux = input[0].flux;
	o.rtIndex = input[0].voxelIndex.z;
	outStream.Append(o);

}

float4 getSHFunction(float3 normal)
{
	float x = normal.x;
	float y = normal.y;
	float z = normal.z;
	float4 SHFunction = float4(
		0.282094792f,
		-0.488602512f * y,
		0.488602512f * z,
		-0.488602512f * x
		);
	return SHFunction;
}

void frag(
	g2f i,
	out float4 GT0 : SV_Target0,
	out float4 GT1 : SV_Target1,
	out float4 GT2 : SV_Target2)
{

	float3 flux = i.flux;
	float3 normal = normalize(i.normal);
	float4 SHFunction = getSHFunction(normal);

	GT0 = flux.r / PI * SHFunction;
	GT1 = flux.g / PI * SHFunction;
	GT2 = flux.b / PI * SHFunction;

}
*/
#endif