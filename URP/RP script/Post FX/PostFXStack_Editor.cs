using UnityEngine;
using UnityEditor;

public partial class PostFXStack
{

    partial void applySceneViewState();

#if UNITY_EDITOR

    //如果当前场景视图禁用图像处理，按我们就禁用栈
    partial void applySceneViewState()
    {
        if(camera.cameraType == CameraType.SceneView && !SceneView.currentDrawingSceneView.sceneViewState.showImageEffects)
        {
            postFXSetting = null;
        }
    }

#endif

}
