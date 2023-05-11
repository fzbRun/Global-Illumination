Shader "Custom RP/Lit"
{

    Properties{

        [Foldout(1, 3)]
        _Foldout_Texture("Texture_Foldout", float) = 1

        _BaseMap("BaseColorMap", 2D) = "white" {}
        _BaseColor("Color", Color) = (0.5, 0.5, 0.5, 1.0)

        [Toggle(_NORMAL_MAP)] _NormalMapToggle("Normal Map", Float) = 0
        [NoScaleOffset] _NormalMap("Normals", 2D) = "bump" {}
        _NormalScale("Normal Scale", Range(0, 1)) = 1

        [Toggle(_HEIGHT_MAP)] _HeightMapToggle("Height Map", Float) = 0
        [NoScaleOffset]_HeightMap("Height", 2D) = "white"{}
        _HeightScale("Height Scale", Range(0, 1)) = 1

        [Toggle(_MASK_MAP)] _MaskMapToggle("Mask Map", Float) = 0
        [NoScaleOffset]_MaskMap("Mask(MODS)", 2D) = "white"{}   //r项为金属值， g项为遮挡， b项为细节， a项为光滑度

        //[Toggle(_SPECULAR_MAP)]_SpecularToggle("Specular Map", Float) = 0
        //[NoScaleOffset]_SpecularMap("Specular", 2D) = "white"{}

        [Toggle(_METALLIC_MAP)]_MetallicToggle("Metallic Map", Float) = 0
        [NoScaleOffset]_MetallicMap("Metallic", 2D) = "white"{}
        _Metallic("Metallic", Range(0, 1)) = 0

        [Toggle(_ROUGHNESS_MAP)]_RoughnessToggle("Roughness Map", Float) = 0
        [NoScaleOffset]_RoughnessMap("Roughness", 2D) = "white"{}
        _Roughness("Roughness", Range(0, 1)) = 0

        _Smoothness("Smoothness", Range(0, 1)) = 0.5

        [Toggle(_OCCLUSION_MAP)]_OcclusionToggle("Occlusion Map", Float) = 0
        [NoScaleOffset]_OcclusionMap("Occlusion", 2D) = "white"{}
        _Occlusion("Occlusion", Range(0, 1)) = 1

        _Fresnel("Fresnel", Range(0, 1)) = 1

        [NoScaleOffset] _EmissionMap("Emission", 2D) = "white" {}
        [HDR] _EmissionColor("EmissionColor", Color) = (0.0, 0.0, 0.0, 0.0)

        _DetailMap("Details", 2D) = "linearGrey"{}
        [Toggle(_DETAIL_MAP)] _DetailMapToggle("Detail Maps", Float) = 0
        [NoScaleOffset] _DetailNormalMap("Detail Normals", 2D) = "bump" {}
        _DetailAlbedo("Detail Albedo", Range(0, 1)) = 1
        _DetailSmoothness("Detail Smoothness", Range(0, 1)) = 1
        _DetailNormalScale("Detail Normal Scale", Range(0, 1)) = 1

        [Foldout_Out(1)]
        _Foldout_Texture_Out("TextureOut_Foldout", float) = 1

        [Foldout(1, 3)]
        _Foldout_RenderSetting("RenderSetting_Foldout", float) = 1

        [Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst blend", Float) = 1

        [Enum(Off, 0, On, 1)]_ZWrite("Z write", Float) = 1

        [Foldout_Out(1)]
        _Foldout_RenderSetting_Out("RenderSetting_Foldout", float) = 1

        [Foldout(1, 3)]
        _Foldout_Illumination("Illumination_Foldout", float) = 1

        _CutOff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Toggle(_CLIPPING)] _Clipping("Alpha Clipping", Float) = 0

        [KeywordEnum(On, Clip, Dither, Off)] _Shadows("Shadows", Float) = 0
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows("Receive Shadows", Float) = 1

        [Foldout_Out(1)]
        _Foldout_Illumination_Out("IlluminationOut_Foldout", float) = 1

        [HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {} //用于创建大量相同物体时支持实例化
        [HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)    //用于创建大量相同物体时支持实例化
    }

        SubShader{

            HLSLINCLUDE
            #include "../shaderLibrary/common.hlsl"
            #include "../shaderLibrary/LitInput.hlsl"
            ENDHLSL

            Pass{

                Tags{ "LightMode" = "CustomLit" }

                //用于多屏幕混合
                //Blend[_SrcBlend][_DstBlend], One OneMinusSrcAlpha
                Blend[_SrcBlend][_DstBlend]
                ZWrite[_ZWrite]

                HLSLPROGRAM

                #pragma target 4.0
                #pragma shader_feature _CLIPPING
                #pragma multi_compile _ _FORWARDPIPELINE _DEFERREDPIPELINE
                //#pragma shader_feature _PREMULTIPLY_ALPHA
                #pragma shader_feature _RECEIVE_SHADOWS
                #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
                #pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7
                #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
                #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
                #pragma multi_compile _ LIGHTMAP_ON
                #pragma multi_compile _ _RSM
                #pragma multi_compile _ _FastRSM
                #pragma multi_compile _ _LPV
                #pragma multi_compile _ _SSLPV
                #pragma multi_compile _ LOD_FADE_CROSSFADE
                #pragma shader_feature _NORMAL_MAP
                #pragma shader_feature _HEIGHT_MAP
                //#pragma shader_feature _SPECULAR_MAP
                #pragma shader_feature _METALLIC_MAP
                #pragma shader_feature _ROUGHNESS_MAP
                #pragma shader_feature _OCCLUSION_MAP
                #pragma shader_feature _MASK_MAP
                #pragma shader_feature _DETAIL_MAP
                #pragma multi_compile _ _LIGHTS_PER_OBJECT
                #pragma multi_compile_instancing
                #pragma vertex vert
                #pragma fragment frag
                #include "LitPass.hlsl"

                ENDHLSL

            }

            Pass{

                Tags{ "LightMode" = "ShadowCaster" }

                ColorMask 0
                Cull Front

                HLSLPROGRAM
                #pragma target 3.5
                #pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
                #pragma multi_compile _ LOD_FADE_CROSSFADE
                #pragma multi_compile_instancing
                #pragma vertex ShadowCasterPassVertex
                #pragma fragment ShadowCasterPassFragment
                #include "ShadowPass.hlsl"
                ENDHLSL

            }

            Pass{

                Tags{ "LightMode" = "Meta" }

                Cull Off

                HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex MetaPassVertex
                #pragma fragment MetaPassFragment
                #include "MetaPass.hlsl"
                ENDHLSL

            }

            Pass
            {

                Tags{ "LightMode" = "RSMPass" }

                ZWrite On

                HLSLPROGRAM

                #pragma target 3.5
                //#pragma shader_feature _CLIPPING //只考虑非透明的物体
                #pragma shader_feature _NORMAL_MAP
                #pragma multi_compile_instancing
                #pragma vertex vert
                #pragma fragment frag
                #include "GI/RSM/RSMPass.hlsl"

                ENDHLSL
            }

        }

        CustomEditor "Scarecrow.SimpleShaderGUI"

}