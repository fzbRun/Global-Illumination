using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

/*
 * 这里的PRT只对单个probe进行测试，所以只需要texture2D来存储球谐系数（八面体映射）
 * 后面如果需要将之改为DDGI，再修复为3Dtexture
*/

[RequireComponent(typeof(LightProbeGroup))]
[ExecuteInEditMode]
public class PRTWithCS : MonoBehaviour
{

    public ComputeShader PRTCS;
    public ComputeBuffer SHFunction;
    public int mapSize = 256;

    //测试只用白光，所以3阶9个系数
    float[] SHCoef;
    public RenderTexture PRT_SH0_CS;
    public RenderTexture PRT_SH1_CS;
    public RenderTexture PRT_SH2_CS;
    public RenderTexture PRT_SH3_CS;
    public Texture2D PRT_SH0;
    public Texture2D PRT_SH1;
    public Texture2D PRT_SH2;
    public Texture2D PRT_SH3;

    float[] L;
    float[] T;

    /*
    //没有遮挡项，只考虑cos，所以所有probe共用一组系数
    //PRT后面实际不需要系数了

    public RenderTexture VisibilityVolume_SH0;
    public RenderTexture VisibilityVolume_SH1;
    public RenderTexture VisibilityVolume_SH2;
    public RenderTexture VisibilityVolume_SH3;
    */

    protected const float PI = Mathf.PI;
    protected float[] SHFunction16 =
    {
        Mathf.Sqrt(1.0f / Mathf.PI) * 0.5f, //0.2821

        Mathf.Sqrt(3.0f / (4.0f * PI)),   //* y 0.4886
        Mathf.Sqrt(3.0f / (4.0f * PI)),   //* z
        Mathf.Sqrt(3.0f / (4.0f * PI)),   //* x

        Mathf.Sqrt(15.0f / PI) * 0.5f,    //* xy 1.09255
        Mathf.Sqrt(15.0f / PI) * 0.5f,    //* yz
        Mathf.Sqrt(5.0f / PI) * 0.25f,    //* 3 * z * z - 1  0.3154
        Mathf.Sqrt(15.0f / PI) * 0.5f,    //* zx
        Mathf.Sqrt(15.0f / PI) * 0.25f,   //* x*x - y*y 0.546275

        Mathf.Sqrt(35.0f / (2.0f * PI)) * 0.25f,  //* y*(3*x*x - y*y) 0.59
        Mathf.Sqrt(105.0f / PI) * 0.5f,  //* x*y*z    2.8906
        Mathf.Sqrt(21.0f / (2.0f * PI)) * 0.25f,  //* y*(5*z*z - 1)   0.4570458
        Mathf.Sqrt(7.0f / PI) * 0.25f,  //* 5*z*z*z - 3*z 0.3732
        Mathf.Sqrt(21.0f / (2.0f * PI)) * 0.25f,  //* x*(5*z*z - 1)
        Mathf.Sqrt(105.0f / PI) * 0.25f,  //* z*(x*x - y*y)   1.4453
        Mathf.Sqrt(35.0f / (2.0f * PI)) * 0.25f,  //* x*(x*x - 3*y*y)
    };

    private void Awake()
    {
        L = new float[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        T = new float[4] { 0, 0, 0, 0 };
    }

    private void OnEnable()
    {
        RefreshVolumeToShader();
    }

    private void OnValidate()
    {
        RefreshVolumeToShader();
    }

    public void RefreshVolumeToShader()
    {
        Shader.SetGlobalTexture("_PRT_SH0", PRT_SH0);
        Shader.SetGlobalTexture("_PRT_SH1", PRT_SH1);
        Shader.SetGlobalTexture("_PRT_SH2", PRT_SH2);
        Shader.SetGlobalTexture("_PRT_SH3", PRT_SH3);

        if(L != null && T != null)
        {
            Shader.SetGlobalFloatArray("LCofe", L);
            Shader.SetGlobalFloatArray("TCofe", T);
        }

        /*
        Shader.SetGlobalTexture("_VisibilityVolume_SH0", VisibilityVolume_SH0);
        Shader.SetGlobalTexture("_VisibilityVolume_SH1", VisibilityVolume_SH1);
        Shader.SetGlobalTexture("_VisibilityVolume_SH2", VisibilityVolume_SH2);
        Shader.SetGlobalTexture("_VisibilityVolume_SH3", VisibilityVolume_SH3);
        */

    }

    public void createRenderTexture()
    {
        PRT_SH0_CS = new RenderTexture(mapSize, mapSize, 0, RenderTextureFormat.ARGBFloat);
        PRT_SH0_CS.enableRandomWrite = true;
        PRT_SH0_CS.Create();

        PRT_SH1_CS = new RenderTexture(mapSize, mapSize, 0, RenderTextureFormat.ARGBFloat);
        PRT_SH1_CS.enableRandomWrite = true;
        PRT_SH1_CS.Create();

        PRT_SH2_CS = new RenderTexture(mapSize, mapSize, 0, RenderTextureFormat.ARGBFloat);
        PRT_SH2_CS.enableRandomWrite = true;
        PRT_SH2_CS.Create();

        PRT_SH3_CS = new RenderTexture(mapSize, mapSize, 0, RenderTextureFormat.ARGBFloat);
        PRT_SH3_CS.enableRandomWrite = true;
        PRT_SH3_CS.Create();

    }

    public void createTexture2D()
    {

        RenderTexture old = RenderTexture.active;

        PRT_SH0 = new Texture2D(mapSize, mapSize, TextureFormat.RGBAFloat, false);
        RenderTexture.active = PRT_SH0_CS;
        PRT_SH0.ReadPixels(new Rect(0, 0, mapSize, mapSize), 0, 0);
        PRT_SH0_CS.Release();

        PRT_SH0.wrapMode = TextureWrapMode.Clamp;
        PRT_SH0.Apply();
        AssetDatabase.CreateAsset(PRT_SH0, "Assets/URP/RP script/Tool/PRT/texture/PRT_SH0.asset");
        AssetDatabase.Refresh();

        PRT_SH1 = new Texture2D(mapSize, mapSize, TextureFormat.RGBAFloat, false);
        RenderTexture.active = PRT_SH1_CS;
        PRT_SH1.ReadPixels(new Rect(0, 0, mapSize, mapSize), 0, 0);
        PRT_SH1_CS.Release();

        PRT_SH1.wrapMode = TextureWrapMode.Clamp;
        PRT_SH1.Apply();
        AssetDatabase.CreateAsset(PRT_SH1, "Assets/URP/RP script/Tool/PRT/texture/PRT_SH1.asset");
        AssetDatabase.Refresh();

        PRT_SH2 = new Texture2D(mapSize, mapSize, TextureFormat.RGBAFloat, false);
        RenderTexture.active = PRT_SH2_CS;
        PRT_SH2.ReadPixels(new Rect(0, 0, mapSize, mapSize), 0, 0);
        PRT_SH2_CS.Release();

        PRT_SH2.wrapMode = TextureWrapMode.Clamp;
        PRT_SH2.Apply();
        AssetDatabase.CreateAsset(PRT_SH2, "Assets/URP/RP script/Tool/PRT/texture/PRT_SH2.asset");
        AssetDatabase.Refresh();

        PRT_SH3 = new Texture2D(mapSize, mapSize, TextureFormat.RGBAFloat, false);
        RenderTexture.active = PRT_SH3_CS;
        PRT_SH3.ReadPixels(new Rect(0, 0, mapSize, mapSize), 0, 0);
        PRT_SH3_CS.Release();

        PRT_SH3.wrapMode = TextureWrapMode.Clamp;
        PRT_SH3.Apply();
        AssetDatabase.CreateAsset(PRT_SH3, "Assets/URP/RP script/Tool/PRT/texture/PRT_SH3.asset");
        AssetDatabase.Refresh();

        RenderTexture.active = old;

    }

    public float[] getSHFunction_normal(Vector3 normal)
    {
        float x = normal.x;
        float y = normal.y;
        float z = normal.z;
        float[] SHFunction_Normal = {
              Mathf.Sqrt(1.0f / PI) * 0.5f,

              Mathf.Sqrt(3.0f / (4.0f * PI)) * y,
              Mathf.Sqrt(3.0f / (4.0f * PI)) * z,
              Mathf.Sqrt(3.0f / (4.0f * PI)) * x,

              Mathf.Sqrt(15.0f / PI) * 0.5f * x * y,
              Mathf.Sqrt(15.0f / PI) * 0.5f * y * z,
              Mathf.Sqrt(5.0f / PI) * 0.25f * (-x * x - y * y + 2 * z * z),
              Mathf.Sqrt(15.0f / PI) * 0.5f * z * x,
              Mathf.Sqrt(15.0f / PI) * 0.25f * (x * x - y * y),

              Mathf.Sqrt(35.0f / (2.0f * PI)) * 0.25f * (3 * x * x - y * y) * y,
              Mathf.Sqrt(105.0f / PI) * 0.5f * x * z * y,
              Mathf.Sqrt(21.0f / (2.0f * PI)) * 0.25f * y * (4 * z * z - x * x - y * y),
              Mathf.Sqrt(7.0f / PI) * 0.25f * z * (2 * z * z - 3 * x * x - 3 * y * y),
              Mathf.Sqrt(21.0f / (2.0f * PI)) * 0.25f * x * (4 * z * z - x * x - y * y),
              Mathf.Sqrt(105.0f / PI) * 0.25f * (x * x - y * y) * z,
              Mathf.Sqrt(35.0f / (2.0f * PI)) * 0.25f * (x * x - 3 * y * y) * x
        };
        return SHFunction_Normal;
    }

    public void makeSHCofeTexture()
    {

        /*
        RefreshVolumeToShader();
        createRenderTexture();

        int kernelHandle = PRTCS.FindKernel("PRTCSMain");
        SHFunction = new ComputeBuffer(16, sizeof(float));
        SHFunction.SetData(SHFunction16);
        PRTCS.SetBuffer(kernelHandle, "SHFunction", SHFunction);
        PRTCS.SetTexture(kernelHandle, "SH0", PRT_SH0_CS);
        PRTCS.SetTexture(kernelHandle, "SH1", PRT_SH1_CS);
        PRTCS.SetTexture(kernelHandle, "SH2", PRT_SH2_CS);
        PRTCS.SetTexture(kernelHandle, "SH3", PRT_SH3_CS);
        PRTCS.SetInt("mapSize", mapSize);
        PRTCS.Dispatch(kernelHandle, mapSize / 8, mapSize / 8, 1);

        createTexture2D();
        SHFunction.Release();
        */
        L = new float[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        T = new float[4] { 0, 0, 0, 0 };
        int N = 0;
        for (float theta = 0; theta < PI; theta += 0.02f)
        {
            for (float phi = 0; phi < 2 * PI; phi += 0.02f)
            {
                N++;
                Vector3 normal = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Sin(theta) * Mathf.Sin (phi), Mathf.Cos(theta));
                float[] SHFunction_Normal = getSHFunction_normal(normal);

                float cos = Vector3.Dot(normal, Vector3.forward);
                cos = Mathf.Max(0.0f, cos);
                for(int i = 0; i < 16; i++)
                {
                    //懒得采样天空盒，直接用余弦当作强度
                    L[i] += SHFunction_Normal[i] * cos;
                }

                T[0] += SHFunction_Normal[0] * cos;
                T[1] += SHFunction_Normal[2] * cos;
                T[2] += SHFunction_Normal[6] * cos;
                T[3] += SHFunction_Normal[12] * cos;

            }
        }

        for(int i = 0; i < 16; i++)
        {
            L[i] = L[i] * 4 * PI / N;
        }

        for(int i = 0; i < 4; i++)
        {
            T[i] = T[i] * 4 * PI / N;
        }

        RefreshVolumeToShader();

    }

}

[CustomEditor(typeof(PRTWithCS))]
[CanEditMultipleObjects]
class PRTWithCS_Inspector : Editor
{
    private PRTWithCS holder;

    private void OnEnable()
    {
        holder = (PRTWithCS)serializedObject.targetObject;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Create PRT Map"))
        {
            holder.makeSHCofeTexture();
        }
    }
}