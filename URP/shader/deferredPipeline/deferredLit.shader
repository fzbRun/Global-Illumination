Shader "Custom RP/deferredLit"
{
    
    SubShader
    {

        HLSLINCLUDE
        #include "../../shaderLibrary/common.hlsl"
        #include "../../shaderLibrary/LitInput.hlsl"
        ENDHLSL

        Pass{

                Tags{ "LightMode" = "CustomLit" }

                ZWrite On

                HLSLPROGRAM

                #pragma target 4.0
                #pragma multi_compile _ _FORWARDPIPELINE _DEFERREDPIPELINE
                #pragma shader_feature _RECEIVE_SHADOWS
                #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
                #pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7
                #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
                #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
                #pragma multi_compile _ _OnlyGI
                #pragma multi_compile _ LIGHTMAP_ON
                #pragma multi_compile _ _USEIBL
                #pragma multi_compile _ _RSM
                #pragma multi_compile _ _FastRSM
                #pragma multi_compile _ _LPV
                #pragma multi_compile _ _SSLPV
                #pragma multi_compile _ LOD_FADE_CROSSFADE
                #pragma multi_compile _ _LIGHTS_PER_OBJECT
                #pragma vertex vert
                #pragma fragment frag
                #include "deferredLitPass.hlsl"

                ENDHLSL

        }
    }
}
