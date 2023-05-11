using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.GlobalIllumination;
using LightType = UnityEngine.LightType;

public partial class Pipeline : RenderPipeline
{

    partial void InitializeForEditor();
    partial void DisposeForEditor();

#if UNITY_EDITOR
    static Lightmapping.RequestLightsDelegate lightDelefate =
    (Light[] lights, NativeArray<LightDataGI> output) =>
    {
        var lightData = new LightDataGI();  //传递相应光源到烘培后端的结构
        for(int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            switch (light.type) {
                case LightType.Directional:
                    var directionalLight = new DirectionalLight();
                    LightmapperUtils.Extract(light, ref directionalLight);  //提取光源信息
                    lightData.Init(ref directionalLight);   //利用光源信息初始化结构
                    break;
                case LightType.Point:
                    var pointLight = new PointLight();
                    LightmapperUtils.Extract(light, ref pointLight);
                    lightData.Init(ref pointLight);
                    break;
                case LightType.Spot:
                    var spotLight = new SpotLight();
                    LightmapperUtils.Extract(light, ref spotLight); //提取光源信息到spotLight中，但会忽略内外角，所以仍要手动赋值
                    spotLight.innerConeAngle =  //手动设置内外角
                            light.innerSpotAngle * Mathf.Deg2Rad;
                    spotLight.angularFalloff =
                        AngularFalloffType.AnalyticAndInnerAngle;
                    lightData.Init(ref spotLight);
                    break;
                case LightType.Area:
                    var rectangleLight = new RectangleLight();
                    LightmapperUtils.Extract(light, ref rectangleLight);
                    rectangleLight.mode = LightMode.Baked;  //不支持实时区域光，只能烘培
                    lightData.Init(ref rectangleLight);
                    break;
                default:
                    lightData.InitNoBake(light.GetInstanceID());    //初始化结构但是不烘培
                    break;
            }
            lightData.falloff = FalloffType.InverseSquared; //设置衰减
            output[i] = lightData;
        }
    };

    partial void InitializeForEditor()
    {
        Lightmapping.SetDelegate(lightDelefate);
    }

    protected void DisposeForEditor(bool disposing)
    {
        Lightmapping.ResetDelegate();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DisposeForEditor(disposing);
        renderer.Dispose(); //只在编辑器中删除，因为运行时需要一直使用，不需要删除。
    }

#endif

}
