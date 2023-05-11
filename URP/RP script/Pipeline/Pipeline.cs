using UnityEngine;
using UnityEngine.Rendering;
using static PipelineAsset;

public partial class Pipeline : RenderPipeline
{

    PipelineSystem pipelineSystem;

    CameraRenderer renderer;

    ShadowSetting shadowSetting;
    PostFXSetting postFXSetting;
    CameraBufferSetting cameraBuffer;
    GISetting giSetting;
    int colorLUTResolution;

    public Pipeline(PipelineSystem pipelineSystem, ShadowSetting shadowSetting, PostFXSetting postFXSetting, 
        CameraBufferSetting cameraBuffer, GISetting giSetting, int colorLUTResolution, Shader shader)
    {
        this.pipelineSystem = pipelineSystem;
        this.shadowSetting = shadowSetting;
        this.postFXSetting = postFXSetting;
        this.cameraBuffer = cameraBuffer;
        this.giSetting = giSetting;
        this.colorLUTResolution = colorLUTResolution;
        GraphicsSettings.useScriptableRenderPipelineBatching = pipelineSystem.useSRPBatching;
        GraphicsSettings.lightsUseLinearIntensity = true;

        renderer = new CameraRenderer(shader);

        InitializeForEditor();

    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach(Camera camera in cameras)
        {
            renderer.Render(context, camera, pipelineSystem, shadowSetting, postFXSetting, cameraBuffer, giSetting, colorLUTResolution);
        }
    }

}
