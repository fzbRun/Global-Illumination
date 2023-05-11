using UnityEngine;

[DisallowMultipleComponent]
public class changeColor : MonoBehaviour
{

    static int baseColoID = Shader.PropertyToID("_BaseColor");
    static int CutOffID = Shader.PropertyToID("_CutOff");
    static int TextureSTID = Shader.PropertyToID("_BaseMap_ST");
    static int metallicID = Shader.PropertyToID("_Metallic");
	static int smoothnessID = Shader.PropertyToID("_Smoothness");
    static int emissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField]
    public Color baseColor = Color.white;

    [SerializeField]
    public float CutOff = 0.5f;

    [SerializeField]
    public Color baseMap_ST  = Color.white;

    [SerializeField]
    public float metallic = 0.5f;

    [SerializeField]
    public float smoothness = 0.5f;

    [SerializeField, ColorUsage(false, true)]
    Color emissionColor = Color.black;

    static MaterialPropertyBlock block;

    private void OnValidate()
    {
        if(block == null)
        {
            block = new MaterialPropertyBlock();
        }
        block.SetColor(baseColoID, baseColor);
        block.SetFloat(CutOffID, CutOff);
        block.SetColor(TextureSTID, baseMap_ST);
        block.SetFloat(metallicID, metallic);
        block.SetFloat(smoothnessID, smoothness);
        block.SetColor(emissionColorId, emissionColor);
        GetComponent<Renderer>().SetPropertyBlock(block);
    }

    public void change()
    {
        OnValidate();
    }

    private void Awake()
    {
        OnValidate();
    }

}
