using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Profiling;

public partial class CameraRenderer
{

	static Material errorMaterial;

	static ShaderTagId[] legacyShaderTagIds = {
		new ShaderTagId("Always"),
		new ShaderTagId("ForwardBase"),
		new ShaderTagId("PrepassBase"),
		new ShaderTagId("Vertex"),
		new ShaderTagId("VertexLMRGBM"),
		new ShaderTagId("VertexLM")
	};

	partial void DrawGizmos();
	partial void DrawUnSupportedShaders();
	partial void PrepareForSceneWindow();
	partial void PrepareBuffer();
	partial void DrawGizmosBeforeFX();
	partial void DrawGizmosAfterFX();

#if UNITY_EDITOR

	string SampleName { get; set; }

	partial void PrepareForSceneWindow()
    {
		if(camera.cameraType == CameraType.SceneView)
        {
			ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
			useRenderScale = false;
        }
    }

	partial void PrepareBuffer()
    {
		Profiler.BeginSample("Editor Only");
		buffer.name = SampleName = camera.name;
		Profiler.EndSample();
    }

	partial void DrawGizmos()
    {
        if (Handles.ShouldRenderGizmos())
        {
			context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
			context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }

	partial void DrawUnSupportedShaders()
    {
		
		if(errorMaterial == null)
        {
			errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

		var drawingSettings = new DrawingSettings(legacyShaderTagIds[0], new SortingSettings())
		{
			overrideMaterial = errorMaterial
		};
		for(int i = 1; i < legacyShaderTagIds.Length; i++)
        {
			drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }

		var filteringSettings = FilteringSettings.defaultValue;
		context.DrawRenderers(cull, ref drawingSettings, ref filteringSettings);

    }

    partial void DrawGizmosBeforeFX()
    {
        if (Handles.ShouldRenderGizmos())
        {
			//Gizmos是直接渲染到摄像机中的，如果将透明物体的深度放入临时纹理，那么Gizmos将无法与透明纹理进行深度测试，导致Gizmos一直可见
            if (useIntermediateBuffer)
            {
				Draw(depthAttachmentID, BuiltinRenderTextureType.CameraTarget, true);
				ExecuteBuffer();
            }
			context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
        }
    }

	partial void DrawGizmosAfterFX()
	{
		if (Handles.ShouldRenderGizmos())
		{
			context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
		}
	}

#else
	const string SampleName = bufferName;
#endif
}
