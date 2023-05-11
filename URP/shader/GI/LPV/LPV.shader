Shader "GI/LPVInject"
{

    SubShader
    {
        
        HLSLINCLUDE
        #include "../../../shaderLibrary/common.hlsl"
        #include "../../../shaderLibrary/LitInput.hlsl"
        ENDHLSL

        Pass{

            Tags{ "LightMode" = "CustomLit" }

            Blend One One
            ZWrite Off

            HLSLPROGRAM

            #pragma target 4.0
            //#pragma vertex vert
            //#pragma geometry geom
            //#pragma fragment frag
            #include "LPVInject.hlsl"

            ENDHLSL

        }

    }
}
