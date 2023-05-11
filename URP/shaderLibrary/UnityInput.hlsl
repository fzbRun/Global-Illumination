#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	float4x4 unity_WorldToObject;
	float4 unity_LODFade;
	real4 unity_WorldTransformParams;

	//物体的渲染层级
	float4 unity_RenderingLayer;

	real4 unity_LightData;
	real4 unity_LightIndices[2];	//每个对象最多8盏灯影响

	float4 unity_LightmapST;	//这两个ST顺序必须这样，否则会破环SRP Batching
	float4 unity_DynamicLightmapST;

	float4 unity_ProbesOcclusion;

	float4 unity_SpecCube0_HDR;

	//三阶球谐系数
	float4 unity_SHAr;
	float4 unity_SHAg;
	float4 unity_SHAb;
	float4 unity_SHBr;
	float4 unity_SHBg;
	float4 unity_SHBb;
	float4 unity_SHC;

	float4 unity_ProbeVolumeParams;
	float4x4 unity_ProbeVolumeWorldToObject;
	float4 unity_ProbeVolumeSizeInv;
	float4 unity_ProbeVolumeMin;
CBUFFER_END

float _Time;

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 UNITY_MATRIX_P;
//float4x4 UNITY_MATRIX_I_P;
float4x4 unity_CameraToWorld;

float4x4 ViewProjectionMatrix;
float4x4 ViewMatrix;
float4x4 ProjectionMatrix;
float4x4 inverseViewMatrix;
float4x4 inverseProjectionMatrix;
float4x4 inverseViewProjectionMatrix;

float4x4 LightVPMatrix;
float4x4 LightViewMatrix;
float4x4 LightProjectionMatrix;
float4x4 inverseLightViewMatrix;
float4x4 inverseLightProjectionMatrix;
float4x4 inverseLightViewProjectionMatrix;
float4x4 sampleLightVPMatrix;
float4x4 inversesampleLightVPMatrix;

float4x4 glstate_matrix_projection;
float3 _WorldSpaceCameraPos;

float4 unity_OrthoParams;
float4 _ProjectionParams;	//用来反转图像，由于不同图形API导致坐标原点不同
float4 _ScreenParams;
float4 _ZBufferParams;
#endif