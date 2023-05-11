using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(LightProbeGroup))]
[ExecuteInEditMode]
public class PRT : MonoBehaviour
{

    public Light light;

    [Range(0, 10)]
    public float GlobalIrradianceIntensity = 1.0f;
    [Range(0, 1)]
    public float GlobalIrradianceAOStrength = 1.0f;
    public float Density = 1.0f;
    public Vector3Int BoundSize = new Vector3Int(10, 10, 10);
    [SerializeField]
    private Vector3 Interval;
    [Range(0, 1)]
    public float judge_offset_size;
    [Range(0, 1)]
    public float worldPos_offset_size;

    protected LightProbeGroup lightProbeGroup;

    //public int sampleNum = 100;
    //测试只用白光，所以3阶9个系数
    public Texture3D IrradianceVolume_SH0;
    public Texture3D IrradianceVolume_SH1;
    public Texture3D IrradianceVolume_SH2;
    public Texture3D IrradianceVolume_SH3;
    //没有遮挡项，只考虑cos，所以所有probe共用一组系数
    public int textureSize = 128;
    public Texture2D VisibilityVolume_SH0;
    public Texture2D VisibilityVolume_SH1;
    public Texture2D VisibilityVolume_SH2;
    public Texture2D VisibilityVolume_SH3;

    protected Vector3Int probeCount;
    private Vector3 IrradianceVolume_start;

    protected NativeArray<Color> IrradianceVolumeCoefs0;
    protected NativeArray<Color> IrradianceVolumeCoefs1;
    protected NativeArray<Color> IrradianceVolumeCoefs2;
    protected NativeArray<Color> IrradianceVolumeCoefs3;
    protected NativeArray<Color> VisibilityVolumeCoefs0;
    protected NativeArray<Color> VisibilityVolumeCoefs1;
    protected NativeArray<Color> VisibilityVolumeCoefs2;
    protected NativeArray<Color> VisibilityVolumeCoefs3;

    protected List<Vector3> probePositions = new List<Vector3>();
    protected float PI = Mathf.PI;
    protected float[] SHFunction16 =
    {
        Mathf.Sqrt(1.0f / Mathf.PI) * 0.5f, //0.2821

        Mathf.Sqrt(3.0f / (4.0f * Mathf.PI)),   //* y 0.4886
        Mathf.Sqrt(3.0f / (4.0f * Mathf.PI)),   //* z
        Mathf.Sqrt(3.0f / (4.0f * Mathf.PI)),   //* x

        Mathf.Sqrt(15.0f / Mathf.PI) * 0.5f,    //* xy 1.09255
        Mathf.Sqrt(15.0f / Mathf.PI) * 0.5f,    //* yz
        Mathf.Sqrt(5.0f / Mathf.PI) * 0.25f,    //* 3 * z * z - 1  0.3154
        Mathf.Sqrt(15.0f / Mathf.PI) * 0.5f,    //* zx
        Mathf.Sqrt(15.0f / Mathf.PI) * 0.25f,   //* x*x - y*y 0.546275

        Mathf.Sqrt(35.0f / (2.0f * Mathf.PI)) * 0.25f,  //* y*(3*x*x - y*y) 0.59
        Mathf.Sqrt(105.0f / Mathf.PI) * 0.5f,  //* x*y*z    2.8906
        Mathf.Sqrt(21.0f / (2.0f * Mathf.PI)) * 0.25f,  //* y*(5*z*z - 1)   0.4570458
        Mathf.Sqrt(7.0f / Mathf.PI) * 0.25f,  //* 5*z*z*z - 3*z 0.3732
        Mathf.Sqrt(21.0f / (2.0f * Mathf.PI)) * 0.25f,  //* x*(5*z*z - 1)
        Mathf.Sqrt(105.0f / Mathf.PI) * 0.25f,  //* z*(x*x - y*y)   1.4453
        Mathf.Sqrt(35.0f / (2.0f * Mathf.PI)) * 0.25f,  //* x*(x*x - 3*y*y)
    };
    private int sampleNum_axis;
    private int sampleNum;
    private Vector3[] dirs;

    private void Awake()
    {
        lightProbeGroup = GetComponent<LightProbeGroup>();
        if (!lightProbeGroup)
        {
            lightProbeGroup = gameObject.AddComponent<LightProbeGroup>();
        }
    }

    private void OnEnable()
    {
        lightProbeGroup = GetComponent<LightProbeGroup>();
        if (!lightProbeGroup)
        {
            lightProbeGroup = gameObject.AddComponent<LightProbeGroup>();
        }
        if (Application.isPlaying)
        {
            RefreshVolumeToShader();
            if (lightProbeGroup)
            {
                lightProbeGroup.enabled = false;
            }
        }
    }

    private void Update()
    {

    }

    private void OnValidate()
    {
        RefreshVolumeToShader();
    }

    public void RefreshVolumeToShader()
    {

        probeCount = new Vector3Int(Mathf.FloorToInt(BoundSize.x * Density) + 1, Mathf.FloorToInt(BoundSize.y * Density) + 1, Mathf.FloorToInt(BoundSize.z * Density) + 1);
        Vector3 Volume_texelSize = new Vector3(1.0f / probeCount.x, 1.0f / probeCount.y, 1.0f / probeCount.z);
        Interval = new Vector3(1.0f / Density, 1.0f / Density, 1.0f / Density);
        IrradianceVolume_start = transform.position - new Vector3(BoundSize.x * 0.5f, BoundSize.y * 0.5f, BoundSize.z * 0.5f);
        sampleNum_axis = textureSize / 2;
        sampleNum = sampleNum_axis * sampleNum_axis * sampleNum_axis;
        dirs = new Vector3[sampleNum];
        Shader.SetGlobalTexture("_IrradianceVolume_SH0", IrradianceVolume_SH0);
        Shader.SetGlobalTexture("_IrradianceVolume_SH1", IrradianceVolume_SH1);
        Shader.SetGlobalTexture("_IrradianceVolume_SH2", IrradianceVolume_SH2);
        Shader.SetGlobalTexture("_IrradianceVolume_SH3", IrradianceVolume_SH3);
        Shader.SetGlobalTexture("_VisibilityVolume_SH0", VisibilityVolume_SH0);
        Shader.SetGlobalTexture("_VisibilityVolume_SH1", VisibilityVolume_SH1);
        Shader.SetGlobalTexture("_VisibilityVolume_SH2", VisibilityVolume_SH2);
        Shader.SetGlobalTexture("_VisibilityVolume_SH3", VisibilityVolume_SH3);
        Shader.SetGlobalVector("_Volume_start", IrradianceVolume_start);
        Shader.SetGlobalVector("_Volume_size", new Vector3(probeCount.x, probeCount.y, probeCount.z));
        Shader.SetGlobalVector("_VisibilityTexture_texelSize", new Vector2(1.0f / textureSize, 1.0f / textureSize));
        Shader.SetGlobalVector("_Volume_interval", Interval);
        Shader.SetGlobalVector("_Volume_texelSize", Volume_texelSize);
        Shader.SetGlobalFloat("_judge_offset_size", judge_offset_size);
        Shader.SetGlobalFloat("_worldPos_offset_size", worldPos_offset_size);

    }

    public void Prepare()
    {

        RefreshVolumeToShader();

        IrradianceVolumeCoefs0 = new NativeArray<Color>((int)probeCount.x * probeCount.y * probeCount.z, Allocator.Persistent);
        IrradianceVolumeCoefs1 = new NativeArray<Color>((int)probeCount.x * probeCount.y * probeCount.z, Allocator.Persistent);
        IrradianceVolumeCoefs2 = new NativeArray<Color>((int)probeCount.x * probeCount.y * probeCount.z, Allocator.Persistent);
        IrradianceVolumeCoefs3 = new NativeArray<Color>((int)probeCount.x * probeCount.y * probeCount.z, Allocator.Persistent);
        VisibilityVolumeCoefs0 = new NativeArray<Color>(textureSize * textureSize, Allocator.Persistent);
        VisibilityVolumeCoefs1 = new NativeArray<Color>(textureSize * textureSize, Allocator.Persistent);
        VisibilityVolumeCoefs2 = new NativeArray<Color>(textureSize * textureSize, Allocator.Persistent);
        VisibilityVolumeCoefs3 = new NativeArray<Color>(textureSize * textureSize, Allocator.Persistent);

        //后面用到的方向都相同，所以提前先得到所有方向，后面直接用
        for (int z_axis = 0; z_axis < sampleNum_axis; z_axis++)
        {
            int zOffset = z_axis * sampleNum_axis * sampleNum_axis;
            for (int y_axis = 0; y_axis < sampleNum_axis; y_axis++)
            {
                int yOffset = y_axis * sampleNum_axis;
                for (int x_axis = 0; x_axis < sampleNum_axis; x_axis++)
                {
                    Vector3 sampleDir = new Vector3(x_axis, y_axis, z_axis) / sampleNum_axis;
                    sampleDir = sampleDir * 2 - Vector3.one;
                    sampleDir = sampleDir.normalized;
                    dirs[x_axis + yOffset + zOffset] = sampleDir;
                }
            }
        }

        var lightProbePos = new List<Vector3>();
        for (int z = 0; z < probeCount.z; z++)
        {
            int zOffset = (int)(z * probeCount.y * probeCount.x);
            for (int y = 0; y < probeCount.y; y++)
            {
                int yOffset = (int)(y * probeCount.x);
                for (int x = 0; x < probeCount.x; x++)
                {

                    Vector3 probePos = Vector3.zero;
                    probePos.x = x * Interval.x;
                    probePos.y = y * Interval.y;
                    probePos.z = z * Interval.z;

                    //lightProbePos.Add(probePos - (Vector3)BoundSize * 0.5f);
                    probePos += IrradianceVolume_start;
                    lightProbePos.Add(probePos);

                    float[] lightCofes = getLightCofes(probePos);
                    IrradianceVolumeCoefs0[x + yOffset + zOffset] = new Vector4
                    (
                        lightCofes[0], lightCofes[1], lightCofes[2], lightCofes[3]
                    );
                    IrradianceVolumeCoefs1[x + yOffset + zOffset] = new Vector4
                    (
                        lightCofes[4], lightCofes[5], lightCofes[6], lightCofes[7]
                    );
                    IrradianceVolumeCoefs2[x + yOffset + zOffset] = new Vector4
                    (
                        lightCofes[8], lightCofes[9], lightCofes[10], lightCofes[11]
                    );
                    IrradianceVolumeCoefs3[x + yOffset + zOffset] = new Vector4
                    (
                        lightCofes[12], lightCofes[13], lightCofes[14], lightCofes[15]
                    );
                }
            }

        }

        lightProbeGroup.probePositions = lightProbePos.ToArray();

        /*
        for (int i0 = 0; i0 < sampleNum; i0++)
        {
            Vector3 dir = dirs[i0];
            //float[] visibilityCofes = getVisibilityCofes(dir);
            float[] visibilityRotateCofes = getVisibilityCofesWithRotate(dir);
            //同心圆映射
            float x = Mathf.Abs(dir.x);
            float y = Mathf.Abs(dir.y);
            float z = Mathf.Abs(dir.z);
            float r = Mathf.Sqrt(1.0f - y);
            float a = Mathf.Max(x, z);
            float b = Mathf.Min(x, z);
            float fai = a == 0.0f ? 0.0f : b / a;
            float fai2PI = (float)(0.00000406531 + 0.636227 * fai +
                                      0.00615523 * fai * fai -
                                      0.247326 * fai * fai * fai +
                                      0.0881627 * fai * fai * fai * fai +
                                      0.0419157 * fai * fai * fai * fai * fai -
                                      0.0251427 * fai * fai * fai * fai * fai * fai);
            if (x < z)
            {
                fai2PI = 1.0f - fai2PI;
            }
            float v = r * fai2PI;
            float u = r - v;
            if (dir.y < 0.0f)
            {
                float tempU2 = 1.0f - v;
                float tempV2 = 1.0f - u;
                v = tempV2;
                u = tempU2;
            }
            u *= dir.x >= 0.0f ? 1.0f : -1.0f;
            v *= dir.z >= 0.0f ? 1.0f : -1.0f;
            u = u * 0.5f + 0.5f;
            v = v * 0.5f + 0.5f;
            u *= textureSize;
            v *= textureSize;
            int index = (int)u + (int)v * textureSize;
            if (index >= textureSize * textureSize)
            {
                index = textureSize * textureSize - 1;
            }
            /*
            VisibilityVolumeCoefs0[index] = new Vector4(
                visibilityCofes[0], visibilityCofes[1], visibilityCofes[2], visibilityCofes[3]
            );
            VisibilityVolumeCoefs1[index] = new Vector4(
                visibilityCofes[4], visibilityCofes[5], visibilityCofes[6], visibilityCofes[7]
            );
            VisibilityVolumeCoefs2[index] = new Vector4(
                visibilityCofes[8], visibilityCofes[9], visibilityCofes[10], visibilityCofes[11]
            );
            VisibilityVolumeCoefs3[index] = new Vector4(
                visibilityCofes[12], visibilityCofes[13], visibilityCofes[14], visibilityCofes[15]
            );
            
            VisibilityVolumeCoefs0[index] = new Vector4(
                visibilityRotateCofes[0], visibilityRotateCofes[1], visibilityRotateCofes[2], visibilityRotateCofes[3]
            );
        }
        */
    }

    private float[] getLightCofes(Vector3 probePos)
    {

        float[] result = new float[16];
        Vector3 lightPos = light.transform.position;
        Vector3 dir = (lightPos - probePos).normalized;
        for (int i1 = 0; i1 < sampleNum; i1++)
        {
            Vector3 sampleDir = dirs[i1];

            float x1 = sampleDir.x;
            float z1 = sampleDir.y;
            float y1 = sampleDir.z;

            float[] SHFunction_normal =
            {
                1.0f,

                y1,
                z1,
                x1,

                x1 * y1,
                y1 * z1,
                - x1 * x1 - y1 * y1 + 2 * z1 *z1,
                z1 * x1,
                x1 * x1 - y1 * y1,

                y1 * (3 * x1 * x1 - y1 * y1),
                x1 * y1 * z1,
                y1 * (4 * z1 * z1 - x1 * x1 - y1 * y1),
                z1 * (2 * z1 * z1 - 3 * x1 * x1 - 3 * y1 * y1),
                x1 * (4 * z1 * z1 - x1 * x1 - y1 * y1),
                z1 * (x1 * x1 - y1 * y1),
                x1 * (x1 * x1 - 3 * y1 * y1)

            };

            //这里本来应该用罗德里格斯旋转将z轴的采样方向转为当前轴的采样方向
            //但是其实是一样的，只要使原本和z轴做余弦变为和当前轴做余弦
            float cos = Mathf.Clamp01(Vector3.Dot(sampleDir, dir));
            for (int j0 = 0; j0 < 16; j0++)
            {
                float sh = SHFunction16[j0] * SHFunction_normal[j0];
                result[j0] += sh * cos;// * Mathf.Sqrt(1.0f - cos * cos);
            }
        }
        for (int j0 = 0; j0 < 16; j0++)
        {
            result[j0] = result[j0] * 4 / sampleNum;   //物体表面反射率就当1好了,这个4是4PI/PI得到的，pdf为平均的，就是球面的1/4PI，提出来就是4PI与BRDF中的c/PI抵消
            result[j0] = result[j0] * 0.5f + 0.5f;
        }

        return result;

    }

    private float[] getVisibilityCofes(Vector3 dir)
    {

        float[] result = new float[16];
        for (int i2 = 0; i2 < sampleNum; i2++)
        {
            Vector3 sampleDir = dirs[i2];

            float x1 = sampleDir.x;
            float z1 = sampleDir.y;
            float y1 = sampleDir.z;

            float[] SHFunction_normal =
            {
                1.0f,

                y1,
                z1,
                x1,

                x1 * y1,
                y1 * z1,
                - x1 * x1 - y1 * y1 + 2 * z1 *z1,
                z1 * x1,
                x1 * x1 - y1 * y1,

                y1 * (3 * x1 * x1 - y1 * y1),
                x1 * y1 * z1,
                y1 * (4 * z1 * z1 - x1 * x1 - y1 * y1),
                z1 * (2 * z1 * z1 - 3 * x1 * x1 - 3 * y1 * y1),
                x1 * (4 * z1 * z1 - x1 * x1 - y1 * y1),
                z1 * (x1 * x1 - y1 * y1),
                x1 * (x1 * x1 - 3 * y1 * y1)

            };

            for (int j1 = 0; j1 < 16; j1++)
            {
                float sh = SHFunction16[j1] * SHFunction_normal[j1];
                result[j1] += sh * Mathf.Clamp01(Vector3.Dot(sampleDir, dir));// * Mathf.Sqrt(1.0f - Mathf.Clamp01(Vector3.Dot(dir, sampleDir) * Vector3.Dot(dir, sampleDir)));
            }
        }

        for (int j1 = 0; j1 < 16; j1++)
        {
            result[j1] = result[j1] * 4 * PI / sampleNum;
            result[j1] = result[j1] * 0.5f + 0.5f;
        }

        return result;


    }

    private float[] getVisibilityCofesWithRotate(Vector3 dir)
    {

        float[] result = new float[4];
        for (int i2 = 0; i2 < sampleNum; i2++)
        {
            Vector3 sampleDir = dirs[i2];

            float x1 = sampleDir.x;
            float z1 = sampleDir.y;
            float y1 = sampleDir.z;

            float[] SHFunction_normal =
            {
                1.0f,

                //y1,
                z1,
                //x1,

                //x1 * y1,
                //y1 * z1,
                - x1 * x1 - y1 * y1 + 2 * z1 *z1,
                //z1 * x1,
                //x1 * x1 - y1 * y1,

                //y1 * (3 * x1 * x1 - y1 * y1),
                //x1 * y1 * z1,
                //y1 * (4 * z1 * z1 - x1 * x1 - y1 * y1),
                z1 * (2 * z1 * z1 - 3 * x1 * x1 - 3 * y1 * y1),
                //x1 * (4 * z1 * z1 - x1 * x1 - y1 * y1),
                //z1 * (x1 * x1 - y1 * y1),
                //x1 * (x1 * x1 - 3 * y1 * y1)

            };

            int[] k = { 0, 2, 6, 12 };
            for (int j1 = 0; j1 < 4; j1++)
            {
                float sh = SHFunction16[k[j1]] * SHFunction_normal[j1];
                result[j1] += sh * Mathf.Clamp01(Vector3.Dot(sampleDir, dir));// * Mathf.Sqrt(1.0f - Mathf.Clamp01(Vector3.Dot(dir, sampleDir) * Vector3.Dot(dir, sampleDir)));
            }
        }

        for (int j1 = 0; j1 < 4; j1++)
        {
            result[j1] = result[j1] * 4 * PI / sampleNum;
            result[j1] = result[j1] * 0.5f + 0.5f;
        }

        return result;

    }

    public Texture3D Create3DTexture(Vector3Int size)
    {
        TextureFormat format = TextureFormat.ARGB32;
        Texture3D tex3D = new Texture3D(size.x, size.y, size.z, format, false);
        tex3D.filterMode = FilterMode.Trilinear;
        tex3D.wrapMode = TextureWrapMode.Clamp;
        return tex3D;
    }

    public Texture2D Create2DTexture(Vector2Int size)
    {
        TextureFormat format = TextureFormat.ARGB32;
        Texture2D tex2D = new Texture2D(size.x, size.y, format, false);
        tex2D.filterMode = FilterMode.Bilinear;
        tex2D.wrapMode = TextureWrapMode.Clamp;
        return tex2D;
    }

    public void Bake()
    {

        IrradianceVolume_SH0 = Create3DTexture(probeCount);
        IrradianceVolume_SH1 = Create3DTexture(probeCount);
        IrradianceVolume_SH2 = Create3DTexture(probeCount);
        IrradianceVolume_SH3 = Create3DTexture(probeCount);
        VisibilityVolume_SH0 = Create2DTexture(new Vector2Int(textureSize, textureSize));
        VisibilityVolume_SH1 = Create2DTexture(new Vector2Int(textureSize, textureSize));
        VisibilityVolume_SH2 = Create2DTexture(new Vector2Int(textureSize, textureSize));
        VisibilityVolume_SH3 = Create2DTexture(new Vector2Int(textureSize, textureSize));

        IrradianceVolume_SH0.SetPixels(IrradianceVolumeCoefs0.ToArray());
        IrradianceVolume_SH1.SetPixels(IrradianceVolumeCoefs1.ToArray());
        IrradianceVolume_SH2.SetPixels(IrradianceVolumeCoefs2.ToArray());
        IrradianceVolume_SH3.SetPixels(IrradianceVolumeCoefs3.ToArray());
        VisibilityVolume_SH0.SetPixels(VisibilityVolumeCoefs0.ToArray());
        VisibilityVolume_SH1.SetPixels(VisibilityVolumeCoefs1.ToArray());
        VisibilityVolume_SH2.SetPixels(VisibilityVolumeCoefs2.ToArray());
        VisibilityVolume_SH3.SetPixels(VisibilityVolumeCoefs3.ToArray());

        IrradianceVolume_SH0.Apply();
        IrradianceVolume_SH1.Apply();
        IrradianceVolume_SH2.Apply();
        IrradianceVolume_SH3.Apply();
        VisibilityVolume_SH0.Apply();
        VisibilityVolume_SH1.Apply();
        VisibilityVolume_SH2.Apply();
        VisibilityVolume_SH3.Apply();

        AssetDatabase.CreateAsset(IrradianceVolume_SH0, "Assets/URP/Tool/PRT/texture/IrradianceVolume_SH0.asset");
        AssetDatabase.CreateAsset(IrradianceVolume_SH1, "Assets/URP/Tool/PRT/texture/IrradianceVolume_SH1.asset");
        AssetDatabase.CreateAsset(IrradianceVolume_SH2, "Assets/URP/Tool/PRT/texture/IrradianceVolume_SH2.asset");
        AssetDatabase.CreateAsset(IrradianceVolume_SH3, "Assets/URP/Tool/PRT/texture/IrradianceVolume_SH3.asset");
        AssetDatabase.CreateAsset(VisibilityVolume_SH0, "Assets/URP/Tool/PRT/texture/VisibilityVolume_SH0.asset");
        AssetDatabase.CreateAsset(VisibilityVolume_SH1, "Assets/URP/Tool/PRT/texture/VisibilityVolume_SH1.asset");
        AssetDatabase.CreateAsset(VisibilityVolume_SH2, "Assets/URP/Tool/PRT/texture/VisibilityVolume_SH2.asset");
        AssetDatabase.CreateAsset(VisibilityVolume_SH3, "Assets/URP/Tool/PRT/texture/VisibilityVolume_SH3.asset");
        AssetDatabase.Refresh();

        RefreshVolumeToShader();

    }

    public void Clear()
    {

        if (IrradianceVolumeCoefs0.IsCreated)
        {
            IrradianceVolumeCoefs0.Dispose();
        }
        if (IrradianceVolumeCoefs1.IsCreated)
        {
            IrradianceVolumeCoefs1.Dispose();
        }
        if (IrradianceVolumeCoefs2.IsCreated)
        {
            IrradianceVolumeCoefs2.Dispose();
        }
        if (IrradianceVolumeCoefs3.IsCreated)
        {
            IrradianceVolumeCoefs3.Dispose();
        }

        if (VisibilityVolumeCoefs0.IsCreated)
        {
            VisibilityVolumeCoefs0.Dispose();
        }
        if (VisibilityVolumeCoefs1.IsCreated)
        {
            VisibilityVolumeCoefs1.Dispose();
        }
        if (VisibilityVolumeCoefs2.IsCreated)
        {
            VisibilityVolumeCoefs2.Dispose();
        }
        if (VisibilityVolumeCoefs3.IsCreated)
        {
            VisibilityVolumeCoefs3.Dispose();
        }
    }
}

[CustomEditor(typeof(PRT))]
[CanEditMultipleObjects]
class PRTInspector : Editor
{
    private PRT holder;

    private void OnEnable()
    {
        holder = (PRT)serializedObject.targetObject;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Prepare"))
        {
            holder.Prepare();
        }
        if (GUILayout.Button("Bake"))
        {
            holder.Bake();
        }
        if (GUILayout.Button("Clear"))
        {
            holder.Clear();
        }
    }
}
