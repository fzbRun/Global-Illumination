using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class Lighting
{

    const string bufferName = "Lighting";
    const int maxDirLightCount = 4, maxOtherLightCount = 64;

    Shadow shadow = new Shadow();

    //平行光
    static int dirLightCountID = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsID = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsID = Shader.PropertyToID("_DirectionalLightDirectionsAndMask"),
        dirLightShadowDataID = Shader.PropertyToID("_DirectionalLightShadowData");

    static Vector4[] dirLightColors = new Vector4[maxDirLightCount];
    static Vector4[] dirLightDirectionsAndMask = new Vector4[maxDirLightCount];
    static Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];

    //非平行光
    static int
    otherLightCountID = Shader.PropertyToID("_OtherLightCount"),
    otherLightColorsID = Shader.PropertyToID("_OtherLightColors"),
    otherLightPositionsID = Shader.PropertyToID("_OtherLightPositions"),
    otherLightDirectionsID = Shader.PropertyToID("_OtherLightDirectionsAndMask"), //聚光方向
    otherLightSpotAnglesID = Shader.PropertyToID("_OtherLightSpotAngles"),
    otherLightShadowDataID = Shader.PropertyToID("_OtherLightShadowData");

    static Vector4[] otherLightColors = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightPositions = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightDirectionsAndMask = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightSpotAngles = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightShadowData = new Vector4[maxOtherLightCount];

    static string lightPerObjectKeyWord = "_LIGHTS_PER_OBJECT";

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    CullingResults cull;

    public void CleanUp()
    {
        shadow.CleanUp();
    }

    void setUpDirectionalLight(int index, int visibilityIndex, ref VisibleLight visibleLight, Light light)
    {
        dirLightColors[index] = visibleLight.finalColor;
        Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
        dirLightDirectionsAndMask[index] = dirAndMask;
        Vector4 position = shadow.ReserveDirectionalShadow(light, index);
        dirLightShadowData[index] = position;
    }

    void setUpPointLight(int index, int visibilityIndex, ref VisibleLight visibleLight, Light light)
    {
        otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1.0f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
        otherLightDirectionsAndMask[index] = new Vector4(1.0f, 1.0f, 1.0f, light.renderingLayerMask.ReinterpretAsFloat());
        otherLightSpotAngles[index] = new Vector4(0.0f, 1.0f, 1.0f, 1.0f);
        otherLightShadowData[index] = shadow.ReserveOtherShadows(light, visibilityIndex);
    }

    void SetupSpotLight(int index, int visibilityIndex, ref VisibleLight visibleLight, Light light)
    {
        otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;

        Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
        otherLightDirectionsAndMask[index] = dirAndMask;

        //公式
        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
        float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
        otherLightSpotAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
        otherLightShadowData[index] = shadow.ReserveOtherShadows(light, visibilityIndex);
    }

    void setUpLight(bool useLightPerObject, int renderingLayerMask)
    {

        NativeArray<int> indexMap = useLightPerObject ? cull.GetLightIndexMap(Allocator.Temp) : default;    //所有灯光的索引，按重要度排序
        NativeArray<VisibleLight> visibleLights = cull.visibleLights;
        int dirLightCount = 0, otherLightCount = 0;
        int i;
        //Debug.Log(visibleLights.Length);
        for (i = 0; i < visibleLights.Length; i++)
        {
            int newIndex = -1;
            VisibleLight visibleLight = visibleLights[i];
            Light light = visibleLight.light;
            if((light.renderingLayerMask & renderingLayerMask) != 0)    //如果场景的掩码和光照的掩码不同，那么就排除光照
            {
                switch (visibleLight.lightType)
                {
                    case LightType.Directional:
                        if (dirLightCount < maxDirLightCount)
                        {
                            setUpDirectionalLight(dirLightCount++, i, ref visibleLight, light);
                        }
                        break;
                    case LightType.Point:
                        if (otherLightCount < maxOtherLightCount)
                        {
                            newIndex = otherLightCount;
                            setUpPointLight(otherLightCount++, i, ref visibleLight, light);
                        }
                        break;
                    case LightType.Spot:
                        if (otherLightCount < maxOtherLightCount)
                        {
                            newIndex = otherLightCount;
                            SetupSpotLight(otherLightCount++, i, ref visibleLight, light);
                        }
                        break;
                }
            }
            if (useLightPerObject)
            {
                indexMap[i] = newIndex;
            }
        }
        //Debug.Log(indexMap.Length);
        //Debug.Log(i);
        if (useLightPerObject)
        {
            for (; i < indexMap.Length; i++) //不重要的光（这里是非可见光）排在后面，将非可见光的索引设为-1
            {
                indexMap[i] = -1;
            }
            cull.SetLightIndexMap(indexMap);    //索引为-1的光为平行光或不可见光，跳过，这样可以得到所有非平行光的索引。
            indexMap.Dispose();
            Shader.EnableKeyword(lightPerObjectKeyWord);
        }
        else
        {
            Shader.DisableKeyword(lightPerObjectKeyWord);
        }

        buffer.SetGlobalInt(dirLightCountID, dirLightCount);
        if (dirLightCount > 0)
        {
            buffer.SetGlobalVectorArray(dirLightColorsID, dirLightColors);
            buffer.SetGlobalVectorArray(dirLightDirectionsID, dirLightDirectionsAndMask);
            buffer.SetGlobalVectorArray(dirLightShadowDataID, dirLightShadowData);
        }

        buffer.SetGlobalInt(otherLightCountID, otherLightCount);
        if (otherLightCount > 0)
        {
            buffer.SetGlobalVectorArray(otherLightColorsID, otherLightColors);
            buffer.SetGlobalVectorArray(otherLightPositionsID, otherLightPositions);
            buffer.SetGlobalVectorArray(otherLightDirectionsID, otherLightDirectionsAndMask);
            buffer.SetGlobalVectorArray(otherLightSpotAnglesID, otherLightSpotAngles);
            buffer.SetGlobalVectorArray(otherLightShadowDataID, otherLightShadowData);
        }

    }

    public void setUp(ScriptableRenderContext context, CullingResults cullResult, ShadowSetting shadowSetting, bool useLightPerObject, int renderingLayerMask)
    {
        cull = cullResult;
        buffer.BeginSample(bufferName);
        shadow.setUp(context, cullResult, shadowSetting);
        setUpLight(useLightPerObject, renderingLayerMask);
        shadow.Render();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

}
