#ifndef FRAGMENT_INCLUDED
#define FRAGMENT_INCLUDED

TEXTURE2D(_CameraColorTexture);
SAMPLER(sampler_CameraColorTexture);
TEXTURE2D(_CameraDepthTexture);

float4 _CameraDepthTexture_TexelSize;
float4 _CameraBufferSize;	//当使用渲染缩放时，_ScreenParams不在匹配当前分辨率，会出错，所以自己传入一个

struct Fragment {
	float2 position;
	float2 screenUV;
	float depth;
	float bufferDepth;
};

Fragment GetFragment(float4 position) {
	Fragment f;
	f.position = position.xy;
	//f.screenUV = f.position / _ScreenParams.xy;
	f.screenUV = f.position * _CameraBufferSize.xy;
	f.depth = isOrthographicCamera() ? orthographicDepthBufferToLinear(position.z) : position.w;	//粒子相机空间深度
	f.bufferDepth = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, sampler_point_clamp, f.screenUV, 0);
	//原来天空盒和非透明物体的相机空间的深度
	f.bufferDepth = isOrthographicCamera() ? orthographicDepthBufferToLinear(f.bufferDepth) : LinearEyeDepth(f.bufferDepth, _ZBufferParams);
	return f;
}

float4 getBufferColor(Fragment fragment, float2 uvOffset = float2(0.0, 0.0)) {
	float2 uv = fragment.screenUV + uvOffset;
	return SAMPLE_TEXTURE2D_LOD(_CameraColorTexture, sampler_CameraColorTexture, uv, 0);
}

#endif