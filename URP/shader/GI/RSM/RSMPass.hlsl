#ifndef RSM_PASS_INCLUDE
#define RSM_PASS_INCLUDE

float3 RSMLightDir;
float3 RSMLightColor;

bool _ShadowPancaking;

struct Attributes {
	float3 vertex : POSITION;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float2 texcoord : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 position : SV_POSITION;
	float3 worldPosition : VAR_POSITION;
	float3 normal : VAR_NORMAL;
#if defined(_NORMAL_MAP)
	float4 tangent : VAR_TANGENT;
#endif
	float2 uv : VAR_BASE_UV;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings vert(Attributes i) {

	Varyings o;

	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_TRANSFER_INSTANCE_ID(i, o);

	o.worldPosition = TransformObjectToWorld(i.vertex.xyz);
	o.position = mul(LightVPMatrix, float4(o.worldPosition, 1.0f));
	//o.position = TransformWorldToHClip(o.worldPosition);

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

	o.normal = TransformObjectToWorldNormal(i.normal);
	o.uv = TransformBaseUV(i.texcoord);

#if defined(_NORMAL_MAP)
	o.tangent = float4(TransformObjectToWorldDir(i.tangent.xyz), i.tangent.w);
#endif

	return o;

}

void frag(Varyings i, 
	out float4 ColorAndDepth : SV_Target0,
	out float4 Normal : SV_Target1){

	UNITY_SETUP_INSTANCE_ID(i);

	InputConfig c = GetInputConfig(i.uv);

	float4 base = GetBase(c);
	float3 normal = normalize(i.normal);
#if defined(_NORMAL_MAP)
	normal = NormalTangentToWorld(GetNormal(c), normal, normalize(i.tangent));
#endif

	float3 color = saturate(dot(normal, -RSMLightDir)) * base * RSMLightColor;
	//float4 clipPos = mul(LightVPMatrix, float4(i.worldPosition, 1.0f));
	float4 clipPos = TransformWorldToHClip(i.worldPosition);;
	float depth = clipPos.z / clipPos.w;
	depth = depth * 0.5f + 0.5f;
	ColorAndDepth = float4(color, depth);
	/*
	float4 x = mul(unity_MatrixV, float4(1.0f, 1.0f, 1.0f, 1.0f));
	float4 y = mul(LightViewMatrix, float4(1.0f, 1.0f, 1.0f, 1.0f));

	float4 z = mul(UNITY_MATRIX_P, float4(1.0f, 1.0f, 1.0f, 1.0f));
	float4 w = mul(LightProjectionMatrix, float4(1.0f, 1.0f, 1.0f, 1.0f));
	*/
	normal = normal * 0.5f + 0.5f;
	Normal = float4(normal, 1.0f);// +(z + w) * 0.01f;// +(x + y + z + w) * 0.01f;

}

#endif