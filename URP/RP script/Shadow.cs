using UnityEngine;
using UnityEngine.Rendering;

public class Shadow : MonoBehaviour
{

    const string bufferName = "Shadow";
    const int maxShadowDirectionalLightCount = 4, maxShadowOtherLightCount = 16, maxCascades = 4;  //最大可以投射阴影的可射光的数量，联级的数量

    static int dirShadowAtlasID = Shader.PropertyToID("_DirectionalShadowAtlas");   //阴影纹理
    static int dirShadowMatrixID = Shader.PropertyToID("_DirectionalShadowMatrix"); //转换到光空间的VP矩阵
    static int cascadeCountID = Shader.PropertyToID("_CascadeCount");   //联级的数量
    static int cascadeCullingSpheresID = Shader.PropertyToID("_CascadeCullingSpheres");  //光映射场景的球，xyz为位置，w为半径。
    static int shadowDistanceFadeID = Shader.PropertyToID("_ShadowDistanceFade");   //
    static int cascadeDataID = Shader.PropertyToID("_CascadeData");
    static int shadowAtlasSizeID = Shader.PropertyToID("_ShadowAtlasSize");

    static int otherShadowAtlasID = Shader.PropertyToID("_OtherShadowAtlas");
    static int otherShadowMatricesID = Shader.PropertyToID("_OtherShadowMatrices");
    static int shadowPancakingID = Shader.PropertyToID("_ShadowPancaking");
    static int otherShadowTilesID = Shader.PropertyToID("_OtherShadowTiles");

    static Matrix4x4[] directionalShadowMatrices = new Matrix4x4[maxShadowDirectionalLightCount * maxCascades]; //转换到光空间的VP矩阵
    static Matrix4x4[] otherShadowMatrices = new Matrix4x4[maxShadowOtherLightCount];
    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades];  //光映射场景的球，xyz为位置，w为半径。
    static Vector4[] cascadeData = new Vector4[maxCascades];
    static Vector4[] otherShadowTiles = new Vector4[maxShadowOtherLightCount];

    int shadowedDirectionalLightCount;  //检查到的可射光的数量
    int shadowOtherLightCount;  //检查到的可射非平行光数量

    CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName
    };

    ScriptableRenderContext context;
    CullingResults cull;
    ShadowSetting shadowSetting;

    Vector4 atlasSizes;

    static string[] directionalFilterKeywords =
    {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7"
    };

    static string[] otherFilterKeywords = {
        "_OTHER_PCF3",
        "_OTHER_PCF5",
        "_OTHER_PCF7",
    };

    static string[] cascadeBlendKeywords = {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

    bool useShadowMask;
    static string[] shadowMaskKey = {
        "_SHADOW_MASK_ALWAYS",
        "_SHADOW_MASK_DISTANCE"
    };

    struct ShadowDirectionalLight
    {
        public int visibleLightIndex;   //可射光在内置数组中的索引
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }
    ShadowDirectionalLight[] shadowDirectionalLights = new ShadowDirectionalLight[maxShadowDirectionalLightCount];

    struct ShadowedOtherLight
    {
        public bool isPoint;
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
    }
    ShadowedOtherLight[] shadowedOtherLights = new ShadowedOtherLight[maxShadowOtherLightCount];

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void CleanUp()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasID);
        if (shadowOtherLightCount > 0)
        {
            buffer.ReleaseTemporaryRT(otherShadowAtlasID);
        }
        ExecuteBuffer();
    }

    public void setUp(ScriptableRenderContext context, CullingResults cullResult, ShadowSetting shadowSetting)
    {
        this.context = context;
        this.cull = cullResult;
        this.shadowSetting = shadowSetting;
        shadowedDirectionalLightCount = shadowOtherLightCount = 0;
        useShadowMask = false;
    }

    //返回可射光的阴影强度，第几个光的联级的起始（0，4，8.12）
    public Vector4 ReserveDirectionalShadow(Light light, int visibleLightIndex)
    {
        //判断光是否可射
        if (shadowedDirectionalLightCount < maxShadowDirectionalLightCount &&
            light.shadows != LightShadows.None &&
            light.shadowStrength > 0.0f)    //&&cull.GetShadowCasterBounds(visibleLightIndex, out Bounds b)先判断是否使用阴影遮罩
        {

            float maskChannel = -1;

            //判断是否使用阴影遮罩
            LightBakingOutput lightBakingOutput = light.bakingOutput;
            if (lightBakingOutput.lightmapBakeType == LightmapBakeType.Mixed && lightBakingOutput.mixedLightingMode == MixedLightingMode.Shadowmask)
            {
                useShadowMask = true;
                maskChannel = lightBakingOutput.occlusionMaskChannel;   //不使用阴影遮罩为-1，但不知道distance和always是不是不同
            }

            if (!cull.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
            {
                return new Vector4(-light.shadowStrength, 0.0f, 0.0f, maskChannel);
            }

            shadowDirectionalLights[shadowedDirectionalLightCount] = new ShadowDirectionalLight()
            {
                visibleLightIndex = visibleLightIndex,
                slopeScaleBias = light.shadowBias,
                nearPlaneOffset = light.shadowNearPlane
            };
            return new Vector4(light.shadowStrength, shadowSetting.directional.cascadeCount * shadowedDirectionalLightCount++, light.shadowNormalBias, maskChannel);
        }
        return new Vector4(0f, 0f, 0f, -1f);
    }

    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
    {
        //Debug.Log(visibleLightIndex);
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0.0f)
        {
            return new Vector4(0.0f, 0.0f, 0.0f, -1.0f);
        }

        float maskChannel = -1.0f;
        LightBakingOutput lightBakingOutput = light.bakingOutput;
        if (lightBakingOutput.lightmapBakeType == LightmapBakeType.Mixed && lightBakingOutput.mixedLightingMode == MixedLightingMode.Shadowmask)
        {
            useShadowMask = true;
            //Debug.Log(lightBakingOutput.occlusionMaskChannel);    //1，2，3依次交替
            maskChannel = lightBakingOutput.occlusionMaskChannel;
        }

        bool isPoint = light.type == LightType.Point;
        int newLightCount = shadowOtherLightCount + (isPoint ? 6 : 1);  //如果是点光源的话，需要渲染cubeMap，我们当它是6个光源各渲染一个shadowMap。

        //GetShadowCasterBounds函数需要当前光在所有光中的索引
        if (newLightCount >= maxShadowOtherLightCount || !cull.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
        {
            return new Vector4(-light.shadowStrength, 0.0f, 0.0f, maskChannel);
        }

        shadowedOtherLights[shadowOtherLightCount] = new ShadowedOtherLight
        {
            isPoint = isPoint,
            visibleLightIndex = visibleLightIndex,
            slopeScaleBias = light.shadowBias,
            normalBias = light.shadowNormalBias
        };

        Vector4 data = new Vector4(light.shadowStrength, shadowOtherLightCount, isPoint ? 1.0f : 0.0f, maskChannel);
        shadowOtherLightCount = newLightCount;

        return data;
    }

    //搞不懂，但好像是对VP矩阵进行偏移，因为阴影其实只有一个纹理，都是分区采样。
    private Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, float scale)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }

        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);

        return m;
    }

    //如果为PCF2x2,那么enabledIndex = -1,那么357的shader变体就会disable
    public void setKeywords(string[] keyWords, int enabledIndex)
    {
        for (int i = 0; i < keyWords.Length; i++)
        {
            if (i == enabledIndex)
            {
                buffer.EnableShaderKeyword(keyWords[i]);
            }
            else
            {
                buffer.DisableShaderKeyword(keyWords[i]);
            }
        }
    }

    //视口变换
    Vector2 setPortView(int index, int split, float titleSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(offset.x * titleSize, offset.y * titleSize, titleSize, titleSize));
        return offset;
    }

    void setCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        //cascadeData[index].x = 1.0f / cullingSphere.w;
        float texelSize = 2.0f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float)shadowSetting.directional.filter + 1f);
        //当前将所有阴影纹理存在一张大的纹理中，所以在阴影纹理边界时，可能采样的别的用阴影纹理中去，所以减小球的半径，使边界的像素不渲染阴影，从而避免采样到别的纹理。
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere;
        cascadeData[index] = new Vector4(1.0f / cullingSphere.w, filterSize * 1.4142136f);
    }

    //渲染阴影纹理
    public void RenderDirectionalShadow(int index, int split, int tileSize)
    {
        ShadowDirectionalLight light = shadowDirectionalLights[index];  //获得可射光
        //有裁剪结果和可射光得到一个阴影设置
        var shadowSettings = new ShadowDrawingSettings(cull, light.visibleLightIndex)
        {
            useRenderingLayerMaskTest = true
        };

        int cascadeCount = shadowSetting.directional.cascadeCount;
        int tileOffset = index * cascadeCount;  //每个可射光再分为4个联级
        Vector3 radios = shadowSetting.directional.cascadeRatios;

        float cullingFactor = Mathf.Max(0f, 0.8f - shadowSetting.directional.cascadeFade);

        //对每个联级渲染阴影贴
        for (int i = 0; i < cascadeCount; i++)
        {
            //通过一系列参数，获得光源映射场景的相关设置
            cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex, i, cascadeCount, radios, tileSize, light.nearPlaneOffset,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);

            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowSettings.splitData = splitData;

            //将剔除球传入数组，所有灯共用一个，所以只需要在第一盏灯时执行就行
            if (index == 0)
            {
                setCascadeData(i, splitData.cullingSphere, tileSize);
            }

            int tileIndex = tileOffset + i; //第几个可射光的第几个联级
            Vector2 offset = setPortView(tileIndex, split, tileSize);
            directionalShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, offset, 1.0f / split);
            //Debug.Log(directionalShadowMatrices[tileIndex].inverse);
            //Debug.Log("offset:" + offset + "       split:" + split);
            //setPortView(index, split, tileSize);
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            //buffer.SetGlobalDepthBias(0.0f, 3.0f);
            //buffer.SetGlobalDepthBias(500000.0f, 0.0f);
            buffer.SetGlobalDepthBias(0.0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0.0f, 0.0f);
        }
    }

    //设置缓冲区
    public void RenderDirectionalShadow()
    {

        int atlasSize = (int)shadowSetting.directional.atlasSize;
        atlasSizes.x = atlasSize;
        atlasSizes.y = 1.0f / atlasSize;

        buffer.GetTemporaryRT(dirShadowAtlasID, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(dirShadowAtlasID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingID, 1f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = shadowedDirectionalLightCount * shadowSetting.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;    //如果没有可射光，就为1，只有一个可射光为2，否则为4
        int tileSize = atlasSize / split;

        //渲染每个可射光
        for (int i = 0; i < shadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadow(i, split, tileSize);
        }

        buffer.SetGlobalVectorArray(cascadeCullingSpheresID, cascadeCullingSpheres);
        buffer.SetGlobalMatrixArray(dirShadowMatrixID, directionalShadowMatrices);
        buffer.SetGlobalVectorArray(cascadeDataID, cascadeData);

        setKeywords(directionalFilterKeywords, (int)shadowSetting.directional.filter - 1);
        setKeywords(cascadeBlendKeywords, (int)shadowSetting.directional.cascadeBlendMode - 1);

        buffer.EndSample(bufferName);
        ExecuteBuffer();

    }

    void SetOtherTileData(int index, Vector2 offset, float scale, float bias)
    {
        float border = atlasSizes.w * 0.5f;
        Vector4 data;
        data.x = offset.x * scale + border; //纹理
        data.y = offset.y * scale + border;
        data.z = scale - border - border;
        data.w = bias;
        otherShadowTiles[index] = data;
    }

    void RenderSpotShadow(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(cull, light.visibleLightIndex)
        {
            useRenderingLayerMaskTest = true
        };
        cull.ComputeSpotShadowMatricesAndCullingPrimitives(
            light.visibleLightIndex, out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
        );
        shadowSettings.splitData = splitData;
        float texelSize = 2f / (tileSize * projectionMatrix.m00);
        float filterSize = texelSize * ((float)shadowSetting.other.filter + 1f);    //通过标准像素大小的缩放得到normalBias.没搞懂
        float bias = light.normalBias * filterSize * 1.4142136f;

        Vector2 offset = setPortView(index, split, tileSize);
        float tileScale = 1.0f / split;
        SetOtherTileData(index, offset, tileScale, bias);

        otherShadowMatrices[index] = ConvertToAtlasMatrix(
            projectionMatrix * viewMatrix,
            offset, tileScale
        );
        buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
        ExecuteBuffer();
        context.DrawShadows(ref shadowSettings);
        buffer.SetGlobalDepthBias(0f, 0f);
    }

    void RenderPointShadow(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(cull, light.visibleLightIndex)
        {
            useRenderingLayerMaskTest = true
        };

        float texelSize = 2f / tileSize;
        float filterSize = texelSize * ((float)shadowSetting.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        float fovBias = Mathf.Atan(1.0f + bias + filterSize) * Mathf.Rad2Deg * 2.0f - 90.0f;
        float tileScale = 1.0f / split;

        for (int i = 0; i < 6; i++)
        {
            cull.ComputePointShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex,(CubemapFace)i, fovBias, out Matrix4x4 viewMatrix,
                out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
                );

            viewMatrix.m11 = -viewMatrix.m11;
            viewMatrix.m12 = -viewMatrix.m12;
            viewMatrix.m13 = -viewMatrix.m13;

            shadowSettings.splitData = splitData;
            int tileIndex = index + i;

            Vector2 offset = setPortView(tileIndex, split, tileSize);
            SetOtherTileData(tileIndex, offset, tileScale, bias);

            otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                projectionMatrix * viewMatrix,
                offset, tileScale
            );
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }
    }

    public void RenderOtherShadow()
    {
        int atlasSize = (int)shadowSetting.other.atlasSize;
        atlasSizes.z = atlasSize;
        atlasSizes.w = 1.0f / atlasSize;

        buffer.GetTemporaryRT(otherShadowAtlasID, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(otherShadowAtlasID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingID, 0f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = shadowOtherLightCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;    //如果没有可射光，就为1，只有一个可射光为2，否则为4
        int tileSize = atlasSize / split;

        //Debug.Log(shadowOtherLightCount);
        //渲染每个可射光
        for (int i = 0; i < shadowOtherLightCount;)
        {
            if (shadowedOtherLights[i].isPoint)
            {
                RenderPointShadow(i, split, tileSize);
                i += 6;
            }
            else
            {
                RenderSpotShadow(i, split, tileSize);
                i++;
            }
        }

        buffer.SetGlobalMatrixArray(otherShadowMatricesID, otherShadowMatrices);
        buffer.SetGlobalVectorArray(otherShadowTilesID, otherShadowTiles);

        setKeywords(otherFilterKeywords, (int)shadowSetting.other.filter - 1);

        buffer.EndSample(bufferName);
        ExecuteBuffer();

    }

    public void Render()
    {
        if (shadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadow();
        }
        else
        {
            buffer.GetTemporaryRT(dirShadowAtlasID, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
        if (shadowOtherLightCount > 0)
        {
            RenderOtherShadow();
        }
        else
        {
            buffer.SetGlobalTexture(otherShadowAtlasID, dirShadowAtlasID);
        }

        buffer.SetGlobalInt(cascadeCountID, shadowSetting.directional.cascadeCount);
        buffer.SetGlobalVector(shadowAtlasSizeID, atlasSizes);

        float f = 1.0f - shadowSetting.directional.cascadeFade;
        buffer.SetGlobalVector(shadowDistanceFadeID, new Vector4(1.0f / shadowSetting.maxDistance, 1.0f / shadowSetting.distanceFade, 1.0f / (1.0f - f * f)));

        buffer.BeginSample(bufferName);
        //-1表示不开启关键字，0表示第一个关键字，以此类推
        setKeywords(shadowMaskKey, useShadowMask ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 : -1);
        buffer.EndSample(bufferName);
        ExecuteBuffer();

    }

}
