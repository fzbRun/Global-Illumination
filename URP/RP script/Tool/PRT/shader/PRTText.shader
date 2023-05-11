Shader "GI/PRTText"
{
    SubShader
    {

        HLSLINCLUDE
        #include "../../../../shaderLibrary/common.hlsl"
        ENDHLSL

         Pass{

            Tags{ "LightMode" = "CustomLit" }

            HLSLPROGRAM

            TEXTURE2D(_PRT_SH0);
            TEXTURE2D(_PRT_SH1);
            TEXTURE2D(_PRT_SH2);
            TEXTURE2D(_PRT_SH3);
            SAMPLER(sampler_PRT_SH0);

            float LCofe[16];
            float TCofe[4];

            struct Attributes {
                float3 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 position : SV_POSITION;
                float3 worldPos : VAR_POSITION;
                float3 normal : VAR_NORMAL;
                float2 uv : VAR_BASE_UV;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes i) {

                Varyings o;

                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);

                o.worldPos = TransformObjectToWorld(i.vertex);
                o.position = TransformWorldToHClip(o.worldPos);
                o.normal = TransformObjectToWorldNormal(i.normal);
                o.uv = i.texcoord;

                #if UNITY_REVERSED_Z	//相机朝向负z轴,如果点在近平面外，则取近平面
                o.position.z = min(o.position.z, o.position.w * UNITY_NEAR_CLIP_VALUE);
                #else
                o.position.z = max(o.position.z, o.position.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return o;

            }

            float2 getOctahedralUVfromNormal(float3 normal) {

                float3 normal_abs = abs(normal);
                float2 uv = float2(normal_abs.x, normal_abs.y) * (1.0f / (normal_abs.x + normal_abs.y + normal_abs.z));
                if (normal.z < 0.0f) {
                    uv = 1.0f - float2(uv.y, uv.x);
                }
                uv *= float2(sign(normal.x), sign(normal.y));
                uv = uv * 0.5f + 0.5f;
                return uv;

            }

            float getLightFromSH(float3 normal, float4 SHCofe0, float4 SHCofe1, float4 SHCofe2, float4 SHCofe3) {

                float x = normal.x;
                float y = normal.y;
                float z = normal.z;
                float SHFunction_normal[16] =
                {
                    1.0f,

                    y,
                    z,
                    x,

                    x * y,
                    y * z,
                    -x * x - y * y + 2 * z * z, //这里是3z^2 - 1，而x^2  +y^2 + z^2 = 1
                    z * x,
                    x * x - y * y,

                    y * (3 * x * x - y * y),
                    x * y * z,
                    y * (4 * z * z - x * x - y * y),
                    z * (2 * z * z - 3 * x * x - 3 * y * y),
                    x * (4 * z * z - x * x - y * y),
                    z * (x * x - y * y),
                    x * (x * x - 3 * y * y)

                };

                float SHFunction16[16] =
                {
                    0.2821,

                    0.4886,
                    0.4886,
                    0.4886,

                    1.09255,
                    1.09255,
                    0.3154,
                    1.09255,
                    0.546275,

                    0.59,
                    2.8906,
                    0.4570458,
                    0.3732,
                    0.4570458,
                    1.4453,
                    0.59
                };

                /*
                float SHCofes[16] = { SHCofe0.x, SHCofe0.y, SHCofe0.z, SHCofe0.w,
                     SHCofe1.x, SHCofe1.y, SHCofe1.z, SHCofe1.w,
                     SHCofe2.x, SHCofe2.y, SHCofe2.z, SHCofe2.w,
                     SHCofe3.x, SHCofe3.y, SHCofe3.z, SHCofe3.w };

                float Lo = PI * SHFunction16[0] * SHFunction_normal[0] * SHCofes[0];
                Lo += 0.66667f * PI * SHFunction16[1] * SHFunction_normal[1] * SHCofes[1];
                Lo += 0.66667f * PI * SHFunction16[2] * SHFunction_normal[2] * SHCofes[2];
                Lo += 0.66667f * PI * SHFunction16[3] * SHFunction_normal[3] * SHCofes[3];
                Lo += 0.25f * PI * SHFunction16[4] * SHFunction_normal[4] * SHCofes[4];
                Lo += 0.25f * PI * SHFunction16[5] * SHFunction_normal[5] * SHCofes[5];
                Lo += 0.25f * PI * SHFunction16[6] * SHFunction_normal[6] * SHCofes[6];
                Lo += 0.25f * PI * SHFunction16[7] * SHFunction_normal[7] * SHCofes[7];
                Lo += 0.25f * PI * SHFunction16[8] * SHFunction_normal[8] * SHCofes[8];
                */
                float Lo = 0.0f;
                float LoArray[16] = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                for (int i = 0; i < 16; i++) {
                    LoArray[i] = LCofe[i] * SHFunction_normal[i] * SHFunction16[i];
                }

                int index[4] = { 0, 1, 4, 9 };
                for (int i = 0; i < 4; i++) {
                    for (int j = 0; j < 2 * i + 1; j++) {
                        LoArray[index[i] + j] *= TCofe[i] * sqrt(4 * PI / (2 * i + 1));
                    }
                   
                }

                for (int i = 0; i < 16; i++) {
                    Lo += LoArray[i];
                }

                return Lo;

            }

            float4 frag(Varyings i) : SV_TARGET{

                UNITY_SETUP_INSTANCE_ID(i);

                float3 normal = normalize(i.normal);
                float2 uv = getOctahedralUVfromNormal(normal);

                float4 SHCofe0 = SAMPLE_TEXTURE2D(_PRT_SH0, sampler_PRT_SH0, uv);
                float4 SHCofe1 = SAMPLE_TEXTURE2D(_PRT_SH1, sampler_PRT_SH0, uv);
                float4 SHCofe2 = SAMPLE_TEXTURE2D(_PRT_SH2, sampler_PRT_SH0, uv);
                float4 SHCofe3 = SAMPLE_TEXTURE2D(_PRT_SH3, sampler_PRT_SH0, uv);

                float Lo = getLightFromSH(normal, SHCofe0, SHCofe1, SHCofe2, SHCofe3);
                return Lo / PI;

            }

            #pragma vertex vert
            #pragma fragment frag

            ENDHLSL

         }

    }
}
