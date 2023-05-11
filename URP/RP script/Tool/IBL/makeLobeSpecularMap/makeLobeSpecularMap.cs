using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class makeLobeSpecularMap : MonoBehaviour
{

    public Cubemap environmentMap;
    public Texture2D lobeSpecularMap;
    private Texture2D tempMap;
    private RenderTexture lobeSpecularMapCS;
    public ComputeShader makeLobeSpecularMapCS;
    public int mapSize = 512;

    public void getLobeSpecular()
    {

        int kernelHandle = makeLobeSpecularMapCS.FindKernel("makeLobeSpecularMapMain");

        lobeSpecularMapCS = new RenderTexture(mapSize, mapSize, 0);
        lobeSpecularMapCS.enableRandomWrite = true;
        lobeSpecularMapCS.Create();

        makeLobeSpecularMapCS.SetTexture(kernelHandle, "Result", lobeSpecularMapCS);
        makeLobeSpecularMapCS.SetTexture(kernelHandle, "environmentMap", environmentMap);
        makeLobeSpecularMapCS.SetInt("mapSize", mapSize);

        int maxMipLevels = 5;
        lobeSpecularMap = new Texture2D(mapSize, mapSize, TextureFormat.RGBAFloat, maxMipLevels, true);
        lobeSpecularMap.filterMode = FilterMode.Trilinear;
        tempMap = new Texture2D(mapSize, mapSize, TextureFormat.RGBAFloat, maxMipLevels, true);
        RenderTexture old = RenderTexture.active;
        for (int mip = 0; mip < maxMipLevels; mip++)
        {

            int width = (int)(mapSize * Mathf.Pow(0.5f, mip));
            int height = (int)(mapSize * Mathf.Pow(0.5f, mip));

            makeLobeSpecularMapCS.SetFloat("roughness", (float)mip / (float)(maxMipLevels - 1));
            makeLobeSpecularMapCS.Dispatch(kernelHandle, mapSize / 8, mapSize / 8, 1);

            RenderTexture.active = lobeSpecularMapCS;
            tempMap.ReadPixels(new Rect(0, 0, mapSize, mapSize), 0, 0);
            lobeSpecularMap.SetPixels(tempMap.GetPixels(mip), mip);

        }
        RenderTexture.active = old;
        lobeSpecularMapCS.Release();

        lobeSpecularMap.wrapMode = TextureWrapMode.Clamp;
        lobeSpecularMap.Apply(false);
        AssetDatabase.CreateAsset(lobeSpecularMap, "Assets/URP/texture/IBL/lobeSpecularMap.asset");
        AssetDatabase.Refresh();

    }

}

[CustomEditor(typeof(makeLobeSpecularMap))]
[CanEditMultipleObjects]
class makeLobeSpecularMap_Inspector : Editor
{
    private makeLobeSpecularMap holder;

    private void OnEnable()
    {
        holder = (makeLobeSpecularMap)serializedObject.targetObject;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Create Lobe Specular Map"))
        {
            holder.getLobeSpecular();
        }
    }
}
