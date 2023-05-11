using UnityEngine.Rendering;
using UnityEngine;
using Random = UnityEngine.Random;

[ExecuteInEditMode]
public class makeSpheres : MonoBehaviour
{

    static int baseColorID = Shader.PropertyToID("_BaseColor");
    static int cutOffID = Shader.PropertyToID("_CutOff");
    static int metallicID = Shader.PropertyToID("_Metallic");
    static int smoothnessID = Shader.PropertyToID("_Smoothness");

    [SerializeField]
    private Mesh mesh = default;

    [SerializeField]
    private Material material = default;

    [SerializeField]
    LightProbeProxyVolume lightProbeVolume = null;

    private Matrix4x4[] matrices = new Matrix4x4[1023];
    private Vector4[] colors = new Vector4[1023];
    private float[] cutOffs = new float[1023];
    private float[] metallicValues = new float[1023];
    private float[] smoothnessValues = new float[1023];

    private MaterialPropertyBlock block;

    private void Awake()
    {
        for(int i = 0; i < matrices.Length; i++)
        {
            matrices[i] = Matrix4x4.TRS(
                Random.insideUnitSphere * 10.0f,
                Quaternion.identity,
                Vector3.one
                );
            colors[i] = new Vector4(Random.value, Random.value, Random.value, Random.value);
            cutOffs[i] = Random.value;
            metallicValues[i] = Random.value < 0.25f ? 1f : 0f;
            smoothnessValues[i] = Random.Range(0.05f, 0.95f);
        }
    }

    private void Update()
    {
        if(block == null)
        {
            block = new MaterialPropertyBlock();
            block.SetVectorArray(baseColorID, colors);
            block.SetFloatArray(cutOffID, cutOffs);
            block.SetFloatArray(smoothnessID, smoothnessValues);
            block.SetFloatArray(metallicID, metallicValues);

            if (!lightProbeVolume)
            {
                var positions = new Vector3[1023];
                for (int i = 0; i < matrices.Length; i++)
                {
                    positions[i] = matrices[i].GetColumn(3);
                }
                var occlusionProbes = new Vector4[1023];
                var lightProbes = new SphericalHarmonicsL2[1023];
                LightProbes.CalculateInterpolatedLightAndOcclusionProbes(
                    positions, lightProbes, occlusionProbes
                );
                block.CopySHCoefficientArraysFrom(lightProbes);
                block.CopyProbeOcclusionArrayFrom(occlusionProbes);
            }
        }
        Graphics.DrawMeshInstanced(mesh, 0, material, matrices, matrices.Length, block,
            ShadowCastingMode.On, true, 0, null, lightProbeVolume ? LightProbeUsage.UseProxyVolume : LightProbeUsage.CustomProvided, lightProbeVolume);
    }

}
