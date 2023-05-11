using UnityEngine;
using UnityEngine.Rendering;
using static PostFXSetting; //可以直接调用其所有常量，静态和类型成员

public partial class PostFXStack
{

    const int maxBloomPyramidLevels = 16;
    //纹理
    int fxSourceID = Shader.PropertyToID("_PostFXSource");
    int fxSource2ID = Shader.PropertyToID("_PostFXSource2");
    int colorGradingLUTID = Shader.PropertyToID("_ColorGradingLUT");
    int finalResultID = Shader.PropertyToID("_FinalResult");
    int colorGradingResultID = Shader.PropertyToID("_ColorGradingResult");

    int bloomBucibicUpsamplingID = Shader.PropertyToID("_BloomBicubicUpsampling");
    int bloomThreshouldID = Shader.PropertyToID("_BloomThreshould");
    int bloomIntensityID = Shader.PropertyToID("_BloomIntensity");
    int bloomResultID = Shader.PropertyToID("_BloomResult");

    int colorAdjustmentsID = Shader.PropertyToID("_ColorAdjustments");

    int colorFilterID = Shader.PropertyToID("_ColorFilter");

    int whiteBalanceID = Shader.PropertyToID("_WhiteBalance");

    int splitToningShadowID = Shader.PropertyToID("_SplitToningShadow");
    int splitToningHighlightID = Shader.PropertyToID("_SplitToningHighlight");

    int channelMixerRedID = Shader.PropertyToID("_ChannelMixerRed");
    int channelMixerGreenID = Shader.PropertyToID("_ChannelMixerGreen");
    int channelMixerBlueID = Shader.PropertyToID("_ChannelMixerBlue");

    int smhShadowID = Shader.PropertyToID("_SMHShadow");
    int smhMidtonesID = Shader.PropertyToID("_SMHMidtone");
    int smhHighlightID = Shader.PropertyToID("_SMHHighlight");
    int smhRangeID = Shader.PropertyToID("_SMHRange");

    int colorGradingLUTParametersID = Shader.PropertyToID("_ColorGradingLUTParameters");
    int colorGradingLUTInLogCID = Shader.PropertyToID("_ColorGradingLUTInLogC");

    int finalSrcBlendID = Shader.PropertyToID("_FinalSrcBlend");
    int finalDstBlendID = Shader.PropertyToID("_FinalDstBlend");

    int copyBicubicID = Shader.PropertyToID("_CopyBicubic");

    int fxaaConfigID = Shader.PropertyToID("_FXAAConfig");

    const string bufferName = "Post FX";
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };
    ScriptableRenderContext context;
    Camera camera;
    PostFXSetting postFXSetting = null;
    bool allowHDR;
    RenderTextureFormat renderTextureFormat;
    int colorLUTResolution;
    CameraSetting.FinalBlendMode finalBlendMode;

    public bool isActive => postFXSetting != null;

    Vector2Int bufferSize;

    CameraBufferSetting.BicubicRescalingMode BicubicRescaling;

    CameraBufferSetting.FXAA fxaa;
    bool keepAlpha;

    public enum Quality { Low, Medium, High }

    public Quality quality;
    const string
        fxaaQualityLowKeyword = "FXAA_QUALITY_LOW",
        fxaaQualityMediumKeyword = "FXAA_QUALITY_MEDIUM";

    enum Pass
    {
        Copy,
        BloomHorizontal,
        BloomVertical,
        BloomAdd,
        BloomScatter,
        BloomScatterFianl,
        BloomPrefilter,
        BloomPrefilterFirefiles,
        ToneGradingNone,
        ToneGradingACES,
        ToneGradingNeutral,
        ToneGradingReinhard,
        ToneGradingE,
        ApplyColorGrading,
        ApplyColorGradingWithLuma,
        FinalRescale,
        FXAA,
        FXAAWithLuma
    }

    int bloomPyramidID;

    public PostFXStack()
    {
        bloomPyramidID = Shader.PropertyToID("_BloomPyramid0");
        for(int i = 1; i < maxBloomPyramidLevels + 1; i++)
        {
            Shader.PropertyToID("_BloomPyramid" + i);   //申请,连续的
        }
    }

    public void setUp(ScriptableRenderContext context, Camera camera, PostFXSetting postFXSetting, 
        bool allowHDR, int colorLUTResolution, CameraSetting.FinalBlendMode finalBlendMode, 
        Vector2Int bufferSize, CameraBufferSetting.BicubicRescalingMode BicubicRescaling, CameraBufferSetting.FXAA fxaa, bool keepAlpha)
    {
        this.context = context;
        this.camera = camera;
        this.postFXSetting = postFXSetting;
        this.allowHDR = allowHDR;
        this.colorLUTResolution = colorLUTResolution;
        this.finalBlendMode = finalBlendMode;
        this.bufferSize = bufferSize;
        this.BicubicRescaling = BicubicRescaling;
        this.fxaa = fxaa;
        this.keepAlpha = keepAlpha;

        //渲染反射探针的相机的cameraType是Reflection=16，也就是说只有game和scene场景的摄像机可以进行后期处理
        //Game = 1，SceneView = 2,Preview = 4,VR = 8,Reflection = 16
        //如果反射探针为baked，不受后期处理影响，即使不加判断条件。
        this.postFXSetting = camera.cameraType <= CameraType.SceneView ? postFXSetting : null;
        applySceneViewState();
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
    {
        buffer.SetGlobalTexture(fxSourceID, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        //用当前材质的shader中的pass
        buffer.DrawProcedural(Matrix4x4.identity, postFXSetting.Material, (int)pass, MeshTopology.Triangles, 3);
    }

    void DrawFinal(RenderTargetIdentifier from, Pass pass)
    {
        buffer.SetGlobalFloat(finalSrcBlendID, (float)finalBlendMode.source);
        buffer.SetGlobalFloat(finalDstBlendID, (float)finalBlendMode.destination);
        buffer.SetGlobalTexture(fxSourceID, from);
        buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, 
            finalBlendMode.destination == BlendMode.Zero ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load, 
            RenderBufferStoreAction.Store);
        buffer.SetViewport(camera.pixelRect);   //SetRenderTargetz之后会将视口变换到屏幕大小，所以需要修改
        buffer.DrawProcedural(Matrix4x4.identity, postFXSetting.Material, (int)pass, MeshTopology.Triangles, 3);
    }

    void doCombine(int i, int fromID, int sourceID, BloomSetting bloom)
    {

        buffer.SetGlobalFloat(bloomBucibicUpsamplingID, postFXSetting.bicubicUpsampling ? 1.0f : 0.0f);

        if (i == 1)
        {
            buffer.SetGlobalTexture(fxSource2ID, sourceID);
            buffer.GetTemporaryRT(bloomResultID, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, renderTextureFormat);
            Draw(fromID, bloomResultID, Pass.BloomAdd);
            return;
        }

        Pass BloomCombine, finalPass;
        if(bloom.mode == Mode.Additive)
        {
            BloomCombine = finalPass = Pass.BloomAdd;
            buffer.SetGlobalFloat(bloomIntensityID, 1.0f);
        }
        else
        {
            BloomCombine = Pass.BloomScatter;   //这样的话采用混合，其实当scatter=0时，i=1和i=4相同，但i=4分辨率更低，所以光晕块化明显
            finalPass = Pass.BloomScatterFianl;
            buffer.SetGlobalFloat(bloomIntensityID, bloom.scatter);
        }

        buffer.SetGlobalTexture(fxSource2ID, fromID);
        Draw(fromID - 1, fromID + 1, BloomCombine);
        fromID -= 2;

        //不能一边采样纹理一边输出到纹理
        for(i -= 1; i > 1; i--)
        {
            buffer.SetGlobalTexture(fxSource2ID, fromID);
            Draw(fromID + 3, fromID + 2, BloomCombine);
            fromID--;
        }

        buffer.SetGlobalFloat(bloomIntensityID, bloom.intensity);
        buffer.SetGlobalTexture(fxSource2ID, sourceID);
        buffer.GetTemporaryRT(bloomResultID, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, renderTextureFormat);
        Draw(fromID + 3, bloomResultID, finalPass);

        for(i = 0; i < bloom.maxIterations + 1; i++)
        {
            buffer.ReleaseTemporaryRT(bloomPyramidID + i);
        }

    }

    bool doBloom(int sourceID)
    {

        BloomSetting bloom = postFXSetting.bloom;

        if (bloom.maxIterations > 0 && bloom.intensity > 0.0f)
        {

            int width, height;
            buffer.BeginSample(bufferName);

            if (postFXSetting.bloom.ignoreRenderScale)
            {
                width = camera.pixelWidth / 2;
                height = camera.pixelHeight / 2;
            }
            else
            {
                width = bufferSize.x / 2;
                height = bufferSize.y / 2;
            }

            renderTextureFormat = allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            int fromID = sourceID, toID = bloomPyramidID, temID = bloomPyramidID + bloom.maxIterations;   //第0层

            Vector4 threshould;
            threshould.x = Mathf.GammaToLinearSpace(bloom.threshould);  //t
            threshould.y = threshould.x * bloom.threshouldKnee; //tk
            threshould.z = 2.0f * threshould.y; //2tk
            threshould.w = 1.0f / (2 * threshould.z + 0.00001f);    //1/(4tk + 0.0001)
            threshould.y -= threshould.x;   // 2tk - 1
            buffer.SetGlobalVector(bloomThreshouldID, threshould);

            int i;
            for (i = 0; i < bloom.maxIterations; i++)
            {
                if (width < bloom.downScaleLimit || height < bloom.downScaleLimit)
                {
                    break;
                }
                buffer.GetTemporaryRT(toID, width, height, 0, FilterMode.Bilinear, renderTextureFormat);
                Draw(fromID, toID, bloom.fadeFireflies ? Pass.BloomPrefilterFirefiles : Pass.BloomPrefilter);  //下一级mipmap
                fromID = toID;

                /*
                int midID = fromID + 1;
                buffer.GetTemporaryRT(midID, width, hegiht, 0, FilterMode.Bilinear, renderTextureFormat);
                Draw(fromID, midID, Pass.BloomHorizontal);

                toID = midID + 1;
                buffer.GetTemporaryRT(toID, width, hegiht, 0, FilterMode.Bilinear, renderTextureFormat);
                Draw(midID, toID, Pass.BloomVertical);
                */

                buffer.GetTemporaryRT(temID, width, height, 0, FilterMode.Bilinear, renderTextureFormat);
                Draw(fromID, temID, Pass.BloomHorizontal);
                Draw(temID, fromID, Pass.BloomVertical);

                toID++;
                width /= 2;
                height /= 2;

            }

            doCombine(i, fromID, sourceID, bloom);

            buffer.EndSample(bufferName);

            return true;

        }
        else
        {
            //Draw(sourceID, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
            return false;
        }

    }

    void ConfigureColorAdjustments()
    {

        ColorAdjustmentsSettings colorAdjustments = postFXSetting.ColorAdjustments;
        buffer.SetGlobalVector(colorAdjustmentsID, new Vector4(
            Mathf.Pow(2, colorAdjustments.postExposure), 
            colorAdjustments.contrast * 0.01f + 1.0f,   //0 - 2
            colorAdjustments.hueShift * (1.0f / 360.0f), //-0.5 - 0.5
            colorAdjustments.saturation * 0.01f + 1.0f  //0 - 2
        ));
        buffer.SetGlobalColor(colorFilterID, colorAdjustments.colorFilter.linear);

    }

    void ConfigureWhiteBalance()
    {
        WhiteBalanceSettings whiteBalance = postFXSetting.WhiteBalance;
        buffer.SetGlobalVector(whiteBalanceID, ColorUtils.ColorBalanceToLMSCoeffs(whiteBalance.temperature, whiteBalance.tint));
    }

    void ConfigureSplitToToning()
    {
        SplitToningSettings splitToning = postFXSetting.SplitToning;
        Color splitColor = splitToning.shadows;
        splitColor.a = splitToning.balance * 0.01f; //-1 - 1
        buffer.SetGlobalColor(splitToningShadowID, splitColor);
        buffer.SetGlobalColor(splitToningHighlightID, splitToning.highlights);
    }

    void ConfigureChannelMixer()
    {
        ChannelMixerSettings channelMixer = postFXSetting.ChannelMixer;
        buffer.SetGlobalVector(channelMixerRedID, channelMixer.red);
        buffer.SetGlobalVector(channelMixerGreenID, channelMixer.green);
        buffer.SetGlobalVector(channelMixerBlueID, channelMixer.blue);

    }

    void ConfigureShadowMidtonesHighlight()
    {
        ShadowsMidtonesHighlightsSettings shadowsMidtonesHighlights = postFXSetting.ShadowsMidtonesHighlights;
        buffer.SetGlobalColor(smhShadowID, shadowsMidtonesHighlights.shadows.linear);
        buffer.SetGlobalColor(smhMidtonesID, shadowsMidtonesHighlights.midtones.linear);
        buffer.SetGlobalColor(smhHighlightID, shadowsMidtonesHighlights.highlights.linear);
        buffer.SetGlobalVector(smhRangeID, new Vector4(
            shadowsMidtonesHighlights.shadowsStart, shadowsMidtonesHighlights.shadowsEnd, shadowsMidtonesHighlights.highlightsStart, shadowsMidtonesHighlights.highLightsEnd
        ));
    }

    void ConfigureFXAA()
    {
        if (fxaa.quality == CameraBufferSetting.Quality.Low)
        {
            buffer.EnableShaderKeyword(fxaaQualityLowKeyword);
            buffer.DisableShaderKeyword(fxaaQualityMediumKeyword);
        }
        else if (fxaa.quality == CameraBufferSetting.Quality.Medium)
        {
            buffer.DisableShaderKeyword(fxaaQualityLowKeyword);
            buffer.EnableShaderKeyword(fxaaQualityMediumKeyword);
        }
        else
        {
            buffer.DisableShaderKeyword(fxaaQualityLowKeyword);
            buffer.DisableShaderKeyword(fxaaQualityMediumKeyword);
        }
        buffer.SetGlobalVector(fxaaConfigID, new Vector4(
            fxaa.fixedThreshold, fxaa.relativeThreshold, fxaa.subpixelBlending
        ));
    }

    void DoFinal(int sourceID)
    {
        ConfigureColorAdjustments();
        ConfigureWhiteBalance();
        ConfigureSplitToToning();
        ConfigureChannelMixer();
        ConfigureShadowMidtonesHighlight();

        int lutHeight = colorLUTResolution;
        int lutWidth = lutHeight * lutHeight;
        buffer.GetTemporaryRT(colorGradingLUTID, lutWidth, lutHeight, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
        buffer.SetGlobalVector(colorGradingLUTParametersID, new Vector4(lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight/ (lutHeight - 1.0f)));

        ToneMappingSettings.Mode mode = postFXSetting.ToneMapping.mode;
        Pass pass = Pass.ToneGradingNone + (int)mode;
        buffer.SetGlobalFloat(colorGradingLUTInLogCID, allowHDR && pass != Pass.ToneGradingNone ? 1.0f : 0.0f);
        Draw(sourceID, colorGradingLUTID, pass);

        buffer.SetGlobalVector(colorGradingLUTParametersID, new Vector4(1.0f / lutWidth, 1.0f / lutHeight, lutHeight - 1.0f));

        buffer.SetGlobalFloat(finalSrcBlendID, 1.0f);
        buffer.SetGlobalFloat(finalDstBlendID, 0.0f);
        if (fxaa.enabled)
        {
            ConfigureFXAA();
            buffer.GetTemporaryRT(colorGradingResultID, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            Draw(sourceID, colorGradingResultID, keepAlpha ? Pass.ApplyColorGrading : Pass.ApplyColorGradingWithLuma);
        }

        if(bufferSize.x == camera.pixelWidth)
        {
            if (fxaa.enabled)
            {
                DrawFinal(colorGradingResultID, keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma);
                buffer.ReleaseTemporaryRT(colorGradingResultID);
            }
            else
            {
                DrawFinal(sourceID, Pass.ApplyColorGrading);
            }
        }
        else
        {
            buffer.GetTemporaryRT(finalResultID, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            if (fxaa.enabled)
            {
                Draw(colorGradingResultID, finalResultID, keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma);
                buffer.ReleaseTemporaryRT(colorGradingResultID);
            }
            else
            {
                Draw(sourceID, finalResultID, Pass.ApplyColorGrading);
            }
            bool bicubicSampling = BicubicRescaling == CameraBufferSetting.BicubicRescalingMode.UpAndDown ||
                BicubicRescaling == CameraBufferSetting.BicubicRescalingMode.UpOnly && bufferSize.x < camera.pixelWidth;
            buffer.SetGlobalFloat(copyBicubicID, bicubicSampling ? 1.0f : 0.0f);
            DrawFinal(finalResultID, Pass.FinalRescale);
            buffer.ReleaseTemporaryRT(finalResultID);
        }
        buffer.ReleaseTemporaryRT(colorGradingLUTID);
    }

    public void Render(int sourceID)
    {
        //buffer.Blit(sourceID, BuiltinRenderTextureType.CameraTarget);   //将下渲染结果渲染到屏幕上
        //Draw(sourceID, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
        if (doBloom(sourceID))
        {
            DoFinal(bloomResultID);
            buffer.ReleaseTemporaryRT(bloomResultID);
        }
        else
        {
            DoFinal(sourceID);
        }
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

}
