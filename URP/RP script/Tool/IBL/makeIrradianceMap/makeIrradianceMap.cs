using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class makeIrradianceMap : MonoBehaviour
{

    public Cubemap environmentMap;
    private RenderTexture irradianceMapCS; //通过将normal转天顶角和方位角两个维度来存储irradiance
    public int mapSize = 64;
    public Texture2D irradianceMap;
    public ComputeShader makeIrradianceMapCS;


    public void getIrradianceMap()
    {

        int kernelHandle = makeIrradianceMapCS.FindKernel("makeIrradianceMapMain");

        irradianceMapCS = new RenderTexture(mapSize, mapSize, 0);
        irradianceMapCS.enableRandomWrite = true;
        irradianceMapCS.Create();

        makeIrradianceMapCS.SetTexture(kernelHandle, "Result", irradianceMapCS);
        makeIrradianceMapCS.SetTexture(kernelHandle, "environmentMap", environmentMap);
        makeIrradianceMapCS.SetInt("mapSize", mapSize);
        makeIrradianceMapCS.Dispatch(kernelHandle, mapSize / 8, mapSize / 8, 1);

        irradianceMap = new Texture2D(mapSize, mapSize, TextureFormat.RGBAFloat, false);
        RenderTexture old = RenderTexture.active;
        RenderTexture.active = irradianceMapCS;
        irradianceMap.ReadPixels(new Rect(0, 0, mapSize, mapSize), 0, 0);
        RenderTexture.active = old;
        irradianceMapCS.Release();

        irradianceMap.wrapMode = TextureWrapMode.Clamp;
        irradianceMap.Apply();
        AssetDatabase.CreateAsset(irradianceMap, "Assets/URP/texture/IBL/irradianceMap.asset");
        AssetDatabase.Refresh();

    }

}

[CustomEditor(typeof(makeIrradianceMap))]
[CanEditMultipleObjects]
class makeIrradianceMap_Inspector : Editor
{
    private makeIrradianceMap holder;

    private void OnEnable()
    {
        holder = (makeIrradianceMap)serializedObject.targetObject;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Create Irradiance Map"))
        {
            holder.getIrradianceMap();
        }
    }
}
