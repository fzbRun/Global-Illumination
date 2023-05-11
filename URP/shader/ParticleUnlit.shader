Shader "Custom RP/ParticleUnlit"
{

	Properties{
		_BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_CutOff("Alpha CutOff", Range(0.0, 1.0)) = 0.5
		_BaseMap("Texture", 2D) = "white"{}
		[Enum(UnityEngine.Rendering.BlendMode)] _ScrBlend("Scr Blend", float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite("Z Write", float) = 1
		[Toggle(_CLIPPING)] _Clipping("Alpha Clipping", float) = 0
		[Toggle(_VERTEX_COLOR)] _VertexColor("Vertex Color", Float) = 0
		[Toggle(_FLIPBOOK_BLENDING)] _FlipbookBlending("Flipbook Blending", Float) = 0
		[Toggle(_NEAR_FADE)] _NearFade("Near Fade", Float) = 0
		_NearFadeDistance("Near Fade Distance", Range(0.0, 10.0)) = 1
		_NearFadeRange("Near Fade Range", Range(0.01, 10.0)) = 1
		[Toggle(_SOFT_PARTICLES)]_SoftParticles("Soft Particles", Float) = 0
		_SoftParticlesDistance("Soft Particles Distance", Range(0.0, 10.0)) = 0
		_SoftParticlesRange("Soft Particles Range", Range(0.01, 10.0)) = 1
		[Toggle(_DISTORTION)]_Distortion("Distortion", Float) = 0
		[NoScaleOffset] _DistortionMap("Distortion Vectors", 2D) = "bump"{}
		_DistortionStrength("Distortion Strength", Range(0.0, 0.2)) = 0.1
		_DistortionBlend("Distortion Blend", Range(0.0, 1.0)) = 1
	}

		SubShader{

			HLSLINCLUDE
			#include "../shaderLibrary/common.hlsl"
			#include "../shaderLibrary/LitInput.hlsl"
			ENDHLSL

			Pass{

				Blend[_ScrBlend][_DstBlend]
				ZWrite[_ZWrite]

				HLSLPROGRAM

				#pragma vertex vert
				#pragma fragment frag
				#pragma shader_feature _CLIPPING
				#pragma shader_feature _VERTEX_COLOR
				#pragma shader_feature _FLIPBOOK_BLENDING
				#pragma shader_feature _NEAR_FADE
				#pragma shader_feature _SOFT_PARTICLES
				#pragma shader_feature _DISTORTION
				#pragma multi_compile_instancing
				#include "ParticleUnlitPass.hlsl"

				ENDHLSL

			}

			 Pass{

				Tags{ "LightMode" = "ShadowCaster" }

				ColorMask 0
				Cull Front

				HLSLPROGRAM
				#pragma target 3.5
				#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
				#pragma multi_compile_instancing
				#pragma vertex ShadowCasterPassVertex
				#pragma fragment ShadowCasterPassFragment
				#include "ShadowPass.hlsl"
				ENDHLSL

			}

		}

}