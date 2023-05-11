Shader "Custom RP/Unlit"
{

	Properties{
		_BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_CutOff("Alpha CutOff", Range(0.0, 1.0)) = 0.5
		_BaseMap("Texture", 2D) = "white"{}
		[Enum(UnityEngine.Rendering.BlendMode)] _ScrBlend("Scr Blend", float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite("Z Write", float) = 1
		[Toggle(_CLIPPING)] _Clipping("Alpha Clipping", float) = 0
	}

	SubShader{
	
		Pass{

			Blend [_ScrBlend] [_DstBlend]
			ZWrite [_ZWrite]

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma shader_feature _CLIPPING
			#pragma multi_compile_instancing
			#include "UnlitPass.hlsl"

			ENDHLSL
			
		}

	}

}