Shader "Unlit/ConvertRSM"
{

    SubShader
    {

        HLSLINCLUDE
        #include "../../../shaderLibrary/common.hlsl"
        #include "../../../shaderLibrary/LitInput.hlsl"
        ENDHLSL

        Pass
        {

            HLSLPROGRAM

            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "RSMLUTPass.hlsl"

            ENDHLSL

        }
    }
}
