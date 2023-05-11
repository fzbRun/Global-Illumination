using UnityEngine.Rendering;
using UnityEngine;
using System;
using static PipelineAsset;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public partial class PipelineAsset : RenderPipelineAsset
{

    [Serializable]
    public enum PipelineMode
    {
        ForwardPipeline,
        DeferredPipeline
    }

    [Serializable]
    public struct PipelineSystem
    {
        public PipelineMode pipelineMode;
        public bool useDynamicBatching;
        public bool useGPUInstancing;
        public bool useSRPBatching;
        public bool useLightPerObject;
    }
    public PipelineSystem pipelineSystem = new PipelineSystem
    {
        pipelineMode = PipelineMode.ForwardPipeline,
        useDynamicBatching = true,
        useGPUInstancing = true,
        useSRPBatching = true,
        useLightPerObject = true
    };

    [SerializeField]
    private ShadowSetting shadowSetting = default;

    [SerializeField]
    PostFXSetting postFXSetting = default;

    [SerializeField]
    CameraBufferSetting cameraBuffer = new CameraBufferSetting
    {
        allowHDR = true,
        renderScale = 1.0f,
        fxaa = new CameraBufferSetting.FXAA
        {
            fixedThreshold = 0.0833f,
            relativeThreshold = 0.166f,
            subpixelBlending = 0.75f
        }
    };

    [SerializeField]
    Shader cameraRendererShader = default;

    public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }

    [SerializeField]
    GISetting giSetting = default;

    [SerializeField]
    ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;

    protected override RenderPipeline CreatePipeline()
    {

        return new Pipeline(pipelineSystem, shadowSetting, postFXSetting, cameraBuffer, giSetting, (int)colorLUTResolution, cameraRendererShader);
    }

}
