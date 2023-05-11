using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class makeBRDFMap : MonoBehaviour
{

    public Texture2D BRDFMap;
    private RenderTexture BRDFMapCS;
    public ComputeShader makeBRDFMapCS;
    public int mapSize = 512;

    public void getBRDFMap()
    {

        int kernelHandle = makeBRDFMapCS.FindKernel("makeBRDFMapMain");

        BRDFMapCS = new RenderTexture(mapSize, mapSize, 0);
        BRDFMapCS.enableRandomWrite = true;
        BRDFMapCS.Create();

        makeBRDFMapCS.SetTexture(kernelHandle, "Result", BRDFMapCS);
        makeBRDFMapCS.SetInt("mapSize", mapSize);
        makeBRDFMapCS.Dispatch(kernelHandle, mapSize / 8, mapSize / 8, 1);

        BRDFMap = new Texture2D(mapSize, mapSize, TextureFormat.RGFloat, false);
        RenderTexture old = RenderTexture.active;
        RenderTexture.active = BRDFMapCS;
        BRDFMap.ReadPixels(new Rect(0, 0, mapSize, mapSize), 0, 0);
        RenderTexture.active = old;
        BRDFMapCS.Release();

        BRDFMap.wrapMode = TextureWrapMode.Clamp;
        BRDFMap.Apply();
        AssetDatabase.CreateAsset(BRDFMap, "Assets/URP/texture/IBL/BRDFMap.asset");
        AssetDatabase.Refresh();

    }

}

[CustomEditor(typeof(makeBRDFMap))]
[CanEditMultipleObjects]
class makeBRDFMap_Inspector : Editor
{
    private makeBRDFMap holder;

    private void OnEnable()
    {
        holder = (makeBRDFMap)serializedObject.targetObject;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Create BRDF Map"))
        {
            holder.getBRDFMap();
        }
    }
}

