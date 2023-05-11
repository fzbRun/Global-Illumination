using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Runtime;
using Unity.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor.Rendering.LookDev;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static GIEditor;
using static GISetting;
using static PipelineAsset;
using static Unity.Burst.Intrinsics.X86.Avx;
using static UnityEngine.GraphicsBuffer;

public class Global_illumination
{

    public GISetting giSetting;
    ScriptableRenderContext context;
    public Camera camera;
    Matrix4x4 ViewMatrix;
    //const string bufferName = "Global Illumination";
    CommandBuffer buffer;

    //------------RSM-------------

    static int RSMLUTTextureID = Shader.PropertyToID("_RSMLUTTexture");
    //Material RSMLUTMaterial;
    ComputeShader RSMLUTCS;
    RenderTexture RSMLUT;

    //--------------LPV--------------
    CommandBuffer LPVbuffer = new CommandBuffer { name = "LPV" };
    RenderTexture LPVTexture_R;
    RenderTexture LPVTexture_G;
    RenderTexture LPVTexture_B;

    RenderTexture LPVTexture_R_temp;
    RenderTexture LPVTexture_G_temp;
    RenderTexture LPVTexture_B_temp;

    RenderTexture SSLPVAvgDepthTexture;
    RenderTexture SSLPVVPLAvgDepthTexture;
    RenderTexture SSLPVAvgDepthSquareTexture;

    ComputeShader LPVTexture3DCS;
    int LPVmode;

    //----------VoxelBox-----------

    bool getSceneBox;
    public float[] voxelBox;
    //public float[] voxelBox_compare;
    public Vector3 VoxelBoxCenterPoint;
    private Vector3 VoxelBoxStartPoint;
    public Vector3Int VoxelBoxSize;
    //体素为立方体
    public float VoxelSize;
    //public float VoxelSize_cpmpare;
    public bool VoxelBoxSizeChange;

    //-----------Light--------------

    int visibleLightIndex;
    public VisibleLight visibleLight;
    public Light light;
    Vector3 lightDir;

    public Matrix4x4[] lightVPMatrixs;
    public Matrix4x4 DirectionalLightViewMatrix;
    public Matrix4x4 DirectionalLightProjectionMatrix;

    public Global_illumination()
    {
        this.VoxelBoxSize = new Vector3Int(16, 16, 16);
        this.VoxelBoxSizeChange = false;
        this.LPVmode = 0;
        createLPVRenderTexture3D();
        createSSLPVDepthTexture3D();
    }

    public void SetUp(ScriptableRenderContext context, GISetting giSetting, Camera camera, CommandBuffer buffer)
    {
        this.context = context;
        this.buffer = buffer;
        this.giSetting = giSetting;
        this.camera = camera;
        /*
        if (giSetting.RSM.RSMLUTShader)
        {
            this.RSMLUTMaterial = CoreUtils.CreateEngineMaterial(giSetting.RSM.RSMLUTShader);
        }
        */
        this.visibleLightIndex = 0;
        this.RSMLUTCS = giSetting.RSM.RSMLUTCS;
        this.lightVPMatrixs = new Matrix4x4[6];
        this.voxelBox = new float[6];
        //this.voxelBox_compare = new float[6];
        this.getSceneBox = false;
        if (giSetting.LPV.LPVMode == LPVMode.LPV)
        {
            this.LPVTexture3DCS = giSetting.LPV.LPVCS;
        }
        else
        {
            this.LPVTexture3DCS = giSetting.LPV.SSLPVCS;
        }


    }

    void ExecuteBuffer()
    {

        context.ExecuteCommandBuffer(buffer);   //将buffer中的命令拿到context中
        buffer.Clear();

    }

    void ExecuteBuffer(CommandBuffer executeBuffer)
    {
        context.ExecuteCommandBuffer(executeBuffer);
        executeBuffer.Clear();
    }

    void bufferBeginSample(CommandBuffer sampleBuffer)
    {
        sampleBuffer.BeginSample(sampleBuffer.name);
        ExecuteBuffer(sampleBuffer);
    }

    void bufferEndSample(CommandBuffer sampleBuffer)
    {
        sampleBuffer.EndSample(sampleBuffer.name);
        ExecuteBuffer(sampleBuffer);
    }

    public void Dispose()
    {
        //CoreUtils.Destroy(RSMLUTMaterial);
        CleanUpWithoutBuffer();
    }

    public void CleanUp()
    {

        if (RSMLUT)
        {
            //RSMLUT.Release();
        }
        if (giSetting.LPV.useLPV)
        {
            //buffer.ReleaseTemporaryRT(RSMLUTTextureID); 
        }

    }

    public void CleanUpWithoutBuffer()
    {
        if (LPVTexture_R)
        {
            LPVTexture_R.Release();
            LPVTexture_G.Release();
            LPVTexture_B.Release();

        }

        if (SSLPVAvgDepthTexture)
        {
            SSLPVAvgDepthTexture.Release();
            SSLPVVPLAvgDepthTexture.Release();
            SSLPVAvgDepthSquareTexture.Release();
        }

    }

    public void makeIBL()
    {
        if (giSetting.IBL.useIBL)
        {

            buffer.EnableShaderKeyword("_USEIBL");
            Shader.SetGlobalTexture("IBL_irradianceMap", giSetting.IBL.irradianceMap);
            Shader.SetGlobalTexture("IBL_lobeSpecularMap", giSetting.IBL.LobeSpecualrMap);
            Shader.SetGlobalTexture("IBL_BRDFMap", giSetting.IBL.BRDFMap);
            Shader.SetGlobalFloat("IBL_Intensity", giSetting.IBL.Intensity);

        }
        else
        {
            buffer.DisableShaderKeyword("_USEIBL");
        }

        ExecuteBuffer();

    }

    private Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m)
    {

        //projectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, false);
        //原来GL.GetGPUProjectionMatrix就是在干下面的这个事啊
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }

        m.m00 = 0.5f * (m.m00 + m.m30);
        m.m01 = 0.5f * (m.m01 + m.m31);
        m.m02 = 0.5f * (m.m02 + m.m32);
        m.m03 = 0.5f * (m.m03 + m.m33);
        m.m10 = 0.5f * (m.m10 + m.m30);
        m.m11 = 0.5f * (m.m11 + m.m31);
        m.m12 = 0.5f * (m.m12 + m.m32);
        m.m13 = 0.5f * (m.m13 + m.m33);
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);

        return m;
    }
    public void calcLightVPMatrix(CullingResults cull, int visibleLightIndex)
    {

        //调用Unity的函数可以直接得到一个平行光渲染的view矩阵和projection矩阵
        //本工程的RSM只渲染最重要的一个光源
        //int visibleLightIndex = cull.GetLightIndexMap(Allocator.Temp)[0];
        //int visibleLightIndex = 0;
        Light light = cull.visibleLights[visibleLightIndex].light;
        lightVPMatrixs = new Matrix4x4[6];
        if(light.type == LightType.Directional)
        {
            cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(visibleLightIndex, 0, 1, Vector3.right, giSetting.RSM.mapSize, light.shadowNearPlane,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);

            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            ExecuteBuffer();

            //巨巨巨奇怪，只有矩阵经过ConvertToAtlasMatrix函数后才能正常采样由ComputeDirectionalShadowMatricesAndCullingPrimitives函数得到的阴影图（深度图），搞了我5个小时
            Shader.SetGlobalMatrix("sampleLightVPMatrix", ConvertToAtlasMatrix(projectionMatrix * viewMatrix));
            Shader.SetGlobalMatrix("inversesampleLightVPMatrix", ConvertToAtlasMatrix(projectionMatrix * viewMatrix).inverse);
            projectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, false);
            DirectionalLightViewMatrix = viewMatrix;
            DirectionalLightProjectionMatrix = projectionMatrix;
            Matrix4x4 VPMatrix = projectionMatrix * viewMatrix;
            //凑得，不是推的，不知道为什么，可能Unity内部干了什么吧,好像是y轴上下颠倒
            VPMatrix.SetRow(1, -VPMatrix.GetRow(1));
            //VPMatrix = ConvertToAtlasMatrix(VPMatrix);
            lightVPMatrixs[0] = VPMatrix;
        }
        else if(light.type == LightType.Point)
        {

            float texelSize = 2f / giSetting.RSM.mapSize;
            float filterSize = texelSize * 2;
            float bias = light.shadowNormalBias * filterSize * 1.4142136f;
            float fovBias = Mathf.Atan(1.0f + bias + filterSize) * Mathf.Rad2Deg * 2.0f - 90.0f;

            for (int i = 0; i < 6; i++)
            {

                cull.ComputePointShadowMatricesAndCullingPrimitives(
                visibleLightIndex, (CubemapFace)i, fovBias, out Matrix4x4 viewMatrix,
                out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
                );

                viewMatrix.m11 = -viewMatrix.m11;
                viewMatrix.m12 = -viewMatrix.m12;
                viewMatrix.m13 = -viewMatrix.m13;

                lightVPMatrixs[i] = projectionMatrix * viewMatrix;

            }

        }
        else
        {

            cull.ComputeSpotShadowMatricesAndCullingPrimitives(
            visibleLightIndex, out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
            );

            lightVPMatrixs[0] = projectionMatrix * viewMatrix;

        }

    }

    public void calcCustomLightVPMatrixAndSceneBox(CullingResults cull, int visibleLightIndex)
    {
        this.light = cull.visibleLights[visibleLightIndex].light;

        if (light.type == LightType.Point)
        {

        }
        else//先不考虑聚光
        {

            //事实证明调用Unity自带的函数确实可以得到光源空间的VP矩阵
            //但是通过这个VP矩阵得到的纹理，只能用Unity的SAMPLE_TEXTURE2D_SHADOW内置函数得到结果
            //但是这个结果只能给出是否在阴影中，无法得到其余信息，所以还是得自己建立光源VP矩阵
            int lightBoundsSize = giSetting.voxelSetting.CameraBoundSize;
            Vector3 cameraDir = camera.transform.rotation * Vector3.forward;
            Bounds bounds = new Bounds(camera.transform.position + cameraDir * lightBoundsSize, Vector3.one * lightBoundsSize);
            //List<Renderer> renderers = new List<Renderer>();
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] rendererObjects = scene.GetRootGameObjects();

            Bounds totalBouds = new Bounds();
            for (int i = 0; i < rendererObjects.Length; i++)
            {
                Renderer[] ObjRenderers = rendererObjects[i].GetComponentsInChildren<Renderer>();
                for (int j = 0; j < ObjRenderers.Length; j++)
                {
                    // 如果要避免包围盒过大及只接受相机视野内的物体可以加上bounds.Intersects(ObjRenderers[j].bounds)进行判断
                    //这里为了RSM图的稳定，且场景较小，则删去
                    //但是SSLPV里有需要，则加回来，而且设的稍微大一些则不影响
                    if (ObjRenderers[j].isVisible && bounds.Intersects(ObjRenderers[j].bounds))
                    {
                        totalBouds.Encapsulate(ObjRenderers[j].bounds);
                    }

                }
            }
            /*
            //C#的list其实还是数组，LinkedList才是链表
            //for (int i = 0; i < renderers.Count; i++)
            //{
            //totalBouds.Encapsulate(renderers[i].bounds);
            //}

            //Vector3 lightDir = URPMath.RotateXYZ(Vector3.forward, light.transform.rotation.eulerAngles);
            //DirectionalLightViewMatrix = Matrix4x4.LookAt(totalBouds.center - lightDir * 100, totalBouds.center, Vector3.up);
            //DirectionalLightViewMatrix.SetRow(2, -DirectionalLightViewMatrix.GetRow(2));
            //DirectionalLightViewMatrix.m23 = -DirectionalLightViewMatrix.m23;
            */
            DirectionalLightViewMatrix = URPMath.makeLightViewMatrix4x4(light, totalBouds.center - lightDir * lightBoundsSize);

            Vector3[] lightSpaceVertexPoint = new Vector3[8];
            lightSpaceVertexPoint[0] = URPMath.mul(DirectionalLightViewMatrix, URPMath.transVec3ToVec4(totalBouds.center + new Vector3(-totalBouds.extents.x, -totalBouds.extents.y, -totalBouds.extents.z)));
            lightSpaceVertexPoint[1] = URPMath.mul(DirectionalLightViewMatrix, URPMath.transVec3ToVec4(totalBouds.center + new Vector3(totalBouds.extents.x, -totalBouds.extents.y, -totalBouds.extents.z)));
            lightSpaceVertexPoint[2] = URPMath.mul(DirectionalLightViewMatrix, URPMath.transVec3ToVec4(totalBouds.center + new Vector3(-totalBouds.extents.x, totalBouds.extents.y, -totalBouds.extents.z)));
            lightSpaceVertexPoint[3] = URPMath.mul(DirectionalLightViewMatrix, URPMath.transVec3ToVec4(totalBouds.center + new Vector3(-totalBouds.extents.x, -totalBouds.extents.y, totalBouds.extents.z)));
            lightSpaceVertexPoint[4] = URPMath.mul(DirectionalLightViewMatrix, URPMath.transVec3ToVec4(totalBouds.center + new Vector3(totalBouds.extents.x, totalBouds.extents.y, -totalBouds.extents.z)));
            lightSpaceVertexPoint[5] = URPMath.mul(DirectionalLightViewMatrix, URPMath.transVec3ToVec4(totalBouds.center + new Vector3(totalBouds.extents.x, -totalBouds.extents.y, totalBouds.extents.z)));
            lightSpaceVertexPoint[6] = URPMath.mul(DirectionalLightViewMatrix, URPMath.transVec3ToVec4(totalBouds.center + new Vector3(-totalBouds.extents.x, totalBouds.extents.y, totalBouds.extents.z)));
            lightSpaceVertexPoint[7] = URPMath.mul(DirectionalLightViewMatrix, URPMath.transVec3ToVec4(totalBouds.center + new Vector3(totalBouds.extents.x, totalBouds.extents.y, totalBouds.extents.z)));

            float Xmax = 0.0f;
            float Ymax = 0.0f;
            float Zmax = 0.0f;
            for (int i = 0; i < 8; i++)
            {
                Xmax = Mathf.Max(Mathf.Abs(lightSpaceVertexPoint[i].x), Xmax);
                Ymax = Mathf.Max(Mathf.Abs(lightSpaceVertexPoint[i].y), Ymax);
                Zmax = Mathf.Max(Mathf.Abs(lightSpaceVertexPoint[i].z), Zmax);
            }

            DirectionalLightProjectionMatrix = Matrix4x4.Ortho(-Xmax * 1.5f, Xmax * 1.5f, -Ymax * 1.5f, Ymax * 1.5f, 0.1f, Zmax);

            
            voxelBox[0] = totalBouds.center.x;
            voxelBox[1] = totalBouds.center.y;
            voxelBox[2] = totalBouds.center.z;
            voxelBox[3] = totalBouds.extents.x;
            voxelBox[4] = totalBouds.extents.y;
            voxelBox[5] = totalBouds.extents.z;
            
            /*
            if (giSetting.LPV.LPVMode == LPVMode.LPV)
            {
                voxelBox[0] = totalBouds.center.x;
                voxelBox[1] = totalBouds.center.y;
                voxelBox[2] = totalBouds.center.z;
                voxelBox[3] = totalBouds.extents.x;
                voxelBox[4] = totalBouds.extents.y;
                voxelBox[5] = totalBouds.extents.z;
            }
            else
            {
                
                ViewMatrix = URPMath.makeViewMatrix4x4(camera);
                Vector3[] cameraSpaceVertexPoint = new Vector3[8];
                cameraSpaceVertexPoint[0] = URPMath.mul(ViewMatrix, URPMath.transVec3ToVec4(totalBouds.center + new Vector3(-totalBouds.extents.x, -totalBouds.extents.y, -totalBouds.extents.z)));
                cameraSpaceVertexPoint[1] = URPMath.mul(ViewMatrix, URPMath.transVec3ToVec4(totalBouds.center + new Vector3(totalBouds.extents.x, -totalBouds.extents.y, -totalBouds.extents.z)));
                cameraSpaceVertexPoint[2] = URPMath.mul(ViewMatrix, URPMath.transVec3ToVec4(totalBouds.center + new Vector3(-totalBouds.extents.x, totalBouds.extents.y, -totalBouds.extents.z)));
                cameraSpaceVertexPoint[3] = URPMath.mul(ViewMatrix, URPMath.transVec3ToVec4(totalBouds.center + new Vector3(-totalBouds.extents.x, -totalBouds.extents.y, totalBouds.extents.z)));
                cameraSpaceVertexPoint[4] = URPMath.mul(ViewMatrix, URPMath.transVec3ToVec4(totalBouds.center + new Vector3(totalBouds.extents.x, totalBouds.extents.y, -totalBouds.extents.z)));
                cameraSpaceVertexPoint[5] = URPMath.mul(ViewMatrix, URPMath.transVec3ToVec4(totalBouds.center + new Vector3(totalBouds.extents.x, -totalBouds.extents.y, totalBouds.extents.z)));
                cameraSpaceVertexPoint[6] = URPMath.mul(ViewMatrix, URPMath.transVec3ToVec4(totalBouds.center + new Vector3(-totalBouds.extents.x, totalBouds.extents.y, totalBouds.extents.z)));
                cameraSpaceVertexPoint[7] = URPMath.mul(ViewMatrix, URPMath.transVec3ToVec4(totalBouds.center + new Vector3(totalBouds.extents.x, totalBouds.extents.y, totalBouds.extents.z)));
                Xmax = float.MinValue;
                float Xmin = float.MaxValue;
                Ymax = float.MinValue;
                float Ymin = float.MaxValue;
                Zmax = float.MinValue;
                float Zmin = float.MaxValue;
                for (int i = 0; i < 8; i++)
                {
                    Xmax = Mathf.Max(cameraSpaceVertexPoint[i].x, Xmax);
                    Ymax = Mathf.Max(cameraSpaceVertexPoint[i].y, Ymax);
                    Zmax = Mathf.Max(cameraSpaceVertexPoint[i].z, Zmax);

                    Xmin = Mathf.Min(cameraSpaceVertexPoint[i].x, Xmin);
                    Ymin = Mathf.Min(cameraSpaceVertexPoint[i].y, Ymin);
                    Zmin = Mathf.Min(cameraSpaceVertexPoint[i].z, Zmin);
                }

                Vector3 cameraPosCenter = new Vector3((Xmax + Xmin) / 2, (Ymax + Ymin) / 2, (Zmax + Zmin) / 2);
                voxelBox[0] = cameraPosCenter.x;
                voxelBox[1] = cameraPosCenter.y;
                voxelBox[2] = cameraPosCenter.z;
                voxelBox[3] = (Xmax - Xmin) / 2;
                voxelBox[4] = (Ymax - Ymin) / 2;
                voxelBox[5] = (Zmax - Zmin) / 2;

                /*
                Xmax = float.MinValue;
                float Xmin = float.MaxValue;
                Ymax = float.MinValue;
                float Ymin = float.MaxValue;
                Zmax = float.MinValue;
                float Zmin = float.MaxValue;
                for (int i = 0; i < 8; i++)
                {
                    Xmax = Mathf.Max(lightSpaceVertexPoint[i].x, Xmax);
                    Ymax = Mathf.Max(lightSpaceVertexPoint[i].y, Ymax);
                    Zmax = Mathf.Max(lightSpaceVertexPoint[i].z, Zmax);

                    Xmin = Mathf.Min(lightSpaceVertexPoint[i].x, Xmin);
                    Ymin = Mathf.Min(lightSpaceVertexPoint[i].y, Ymin);
                    Zmin = Mathf.Min(lightSpaceVertexPoint[i].z, Zmin);
                }

                voxelBox[0] = (Xmax + Xmin) / 2;
                voxelBox[1] = (Ymax + Ymin) / 2;
                voxelBox[2] = (Zmax + Zmin) / 2;
                voxelBox[3] = (Xmax - Xmin) / 2;
                voxelBox[4] = (Ymax - Ymin) / 2;
                voxelBox[5] = (Zmax - Zmin) / 2;
                
            }
            */
            this.getSceneBox = true;

            buffer.SetViewProjectionMatrices(DirectionalLightViewMatrix, DirectionalLightProjectionMatrix);
            ExecuteBuffer();
            DirectionalLightProjectionMatrix = GL.GetGPUProjectionMatrix(DirectionalLightProjectionMatrix, false);
            //DirectionalLightViewMatrix.SetRow(1, -DirectionalLightViewMatrix.GetRow(1));
            lightVPMatrixs[0] = DirectionalLightProjectionMatrix * DirectionalLightViewMatrix;
            DirectionalLightProjectionMatrix = DirectionalLightProjectionMatrix.inverse * lightVPMatrixs[0];
            lightVPMatrixs[0].SetRow(1, -lightVPMatrixs[0].GetRow(1));

            //Vector4 test = URPMath.mul(DirectionalLightProjectionMatrix, new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
            //Debug.Log(test);
        }

    }

    public bool makeRSM(CullingResults cull)
    {

        if (giSetting.RSM.useRSM)
        {
            buffer.EnableShaderKeyword("_RSM");
        }
        else
        {
            buffer.DisableShaderKeyword("_RSM");
            return false;
        }

        if(cull.visibleLights.Length == 0)
        {
            Debug.LogError("至少需要一个光源才能开启RSM");
            return false;
        }



        if (giSetting.RSM.fastRsm)
        {
            buffer.EnableShaderKeyword("_FastRSM");
        }
        else
        {
            buffer.DisableShaderKeyword("_FastRSM");
        }

        visibleLight = cull.visibleLights[visibleLightIndex];
        light = visibleLight.light;
        lightDir = visibleLight.light.transform.rotation * Vector3.forward;
        Color lightColor = visibleLight.light.color;
        Shader.SetGlobalFloat("RSMSampleSize", giSetting.RSM.SampelSize);
        Shader.SetGlobalFloat("RSMIntensity", giSetting.RSM.Intensity);
        Shader.SetGlobalVector("RSMLightDir", lightDir);
        Shader.SetGlobalColor("RSMLightColor", lightColor);

        //calcLightVPMatrix(cull, visibleLightIndex);
        calcCustomLightVPMatrixAndSceneBox(cull, visibleLightIndex);
        if(light.type == LightType.Point)
        {

        }
        else
        {
            Shader.SetGlobalMatrix("LightVPMatrix", lightVPMatrixs[0]);
            Shader.SetGlobalMatrix("LightViewMatrix", DirectionalLightViewMatrix);
            Shader.SetGlobalMatrix("LightProjectionMatrix", DirectionalLightProjectionMatrix);
            Shader.SetGlobalMatrix("inverseLightViewMatrix", DirectionalLightViewMatrix.inverse);
            Shader.SetGlobalMatrix("inverseLightProjectionMatrix", DirectionalLightProjectionMatrix.inverse);
            Shader.SetGlobalMatrix("inverseLightViewProjectionMatrix", lightVPMatrixs[0].inverse);
        }
        Shader.SetGlobalFloat("RSMMapSize", giSetting.RSM.mapSize);

        return true;

    }

    public void makeRSMlUT(Vector4 lightPos)
    {

        /*
        if (!RSMLUTMaterial)
        {
            Debug.LogError("需要转换RSM的Shader");
            return;
        }
        buffer.GetTemporaryRT(RSMLUTTextureID, mapSize, mapSize, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);
        buffer.SetRenderTarget(RSMLUTTextureID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, RSMLUTMaterial, 0, MeshTopology.Triangles, 3);
        ExecuteBuffer();
        */
        if (RSMLUTCS == null)
        {
            Debug.LogError("需要转换RSM的Compute Shader");
            return;
        }else if (!(Shader.GetGlobalTexture("_RSMTexture") && Shader.GetGlobalTexture("_RSMDepthTexture")))
        {
            return;
        }
        
        int mapSize = giSetting.RSM.mapSize;
        int kernelHandle = RSMLUTCS.FindKernel("RSMLUTCSMain");

        if (!RSMLUT) {
            RSMLUT = new RenderTexture(mapSize, mapSize, 0);
            RSMLUT.enableRandomWrite = true;
            RSMLUT.Create();
        }

        RSMLUTCS.SetTexture(kernelHandle, "Result", RSMLUT);
        RSMLUTCS.SetTextureFromGlobal(kernelHandle, "RSMTexture", "_RSMTexture");
        RSMLUTCS.SetTextureFromGlobal(kernelHandle, "RSMDepthTexture", "_RSMDepthTexture");
        RSMLUTCS.SetInt("mapSize", mapSize);
        RSMLUTCS.SetVector("lightPos", lightPos);
        RSMLUTCS.SetVector("VoxelBoxStartPoint", giSetting.voxelSetting.VoxelBoxStartPoint);
        RSMLUTCS.SetVector("VoxelBoxSize", new Vector4(giSetting.voxelSetting.VoxelBoxSize.x, giSetting.voxelSetting.VoxelBoxSize.y, giSetting.voxelSetting.VoxelBoxSize.z, 1.0f));
        RSMLUTCS.SetFloat("VoxelSize", VoxelSize);

        RSMLUTCS.Dispatch(kernelHandle, mapSize / 8, mapSize / 8, 1);

        Shader.SetGlobalTexture("_RSMLUTTexture", RSMLUT);

    }

    public bool makeVoxelBox(int k)
    {
        /*
        //RSM的时候设置过了
        if (!this.getSceneBox)
        {
            calcCustomLightVPMatrixAndSceneBox(cull, visibleLightIndex);
        }
        */
        this.VoxelSize = giSetting.voxelSetting.VoxelSize;

        this.VoxelBoxCenterPoint = new Vector3(voxelBox[0], voxelBox[1], voxelBox[2]);
        Vector3 VoxelBoxLength = new Vector3(voxelBox[3], voxelBox[4], voxelBox[5]) * 2;
        Vector3 voxelBoxSizeTemp = VoxelBoxLength / VoxelSize;
        Vector3Int VoxelBoxSize_Temp = URPMath.floatVec3TOIntVec3(voxelBoxSizeTemp, 1, 1);
        if(k == 1)
        {
            //转为2的幂次
            int x = Mathf.NextPowerOfTwo(VoxelBoxSize_Temp.x);
            int y = Mathf.NextPowerOfTwo(VoxelBoxSize_Temp.y);
            int z = Mathf.NextPowerOfTwo(VoxelBoxSize_Temp.z);
            VoxelBoxSize_Temp = new Vector3Int(x, y, z);
        }
        if(VoxelBoxSize_Temp != VoxelBoxSize)
        {
            CleanUpWithoutBuffer();
            this.VoxelBoxSizeChange = true;
        }
        else
        {
            this.VoxelBoxSizeChange = false;
        }
        VoxelBoxSize = VoxelBoxSize_Temp;


        //如果是体素组轴是奇数，则需要再偏移半个体素
        Vector3 halfVoxelOffset = new Vector3(VoxelBoxSize.x % 2, VoxelBoxSize.y % 2, VoxelBoxSize.z % 2);
        this.VoxelBoxStartPoint = VoxelBoxCenterPoint - (Vector3)VoxelBoxSize * VoxelSize / 2 - halfVoxelOffset * VoxelSize * 0.5f;

        giSetting.voxelSetting.VoxelBoxStartPoint = this.VoxelBoxStartPoint;
        giSetting.voxelSetting.VoxelBoxSize = this.VoxelBoxSize;

        if (VoxelBoxSize.x <= 0 || VoxelBoxSize.y <= 0 || VoxelBoxSize.z <= 0)
        {
            VoxelBoxSize = Vector3Int.one;
            return false;
        }

        return true;

    }

    public void createLPVRenderTexture3D()
    {

        LPVTexture_R = new RenderTexture(VoxelBoxSize.x, VoxelBoxSize.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        LPVTexture_R.filterMode = FilterMode.Trilinear;
        LPVTexture_R.dimension = TextureDimension.Tex3D;
        LPVTexture_R.volumeDepth = VoxelBoxSize.z;
        LPVTexture_R.enableRandomWrite = true;
        LPVTexture_R.Create();

        /*
        _LPVTexture_R = RenderTexture.GetTemporary(VoxelBoxSize.x, VoxelBoxSize.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        _LPVTexture_R.filterMode = FilterMode.Point;
        _LPVTexture_R.dimension = TextureDimension.Tex3D;
        _LPVTexture_R.volumeDepth = VoxelBoxSize.z;
        _LPVTexture_R.enableRandomWrite = true;
        _LPVTexture_R.Create();
        */

        LPVTexture_G = new RenderTexture(VoxelBoxSize.x, VoxelBoxSize.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        LPVTexture_G.filterMode = FilterMode.Trilinear;
        LPVTexture_G.dimension = TextureDimension.Tex3D;
        LPVTexture_G.volumeDepth = VoxelBoxSize.z;
        LPVTexture_G.enableRandomWrite = true;
        LPVTexture_G.Create();

        /*
        _LPVTexture_G = RenderTexture.GetTemporary(VoxelBoxSize.x, VoxelBoxSize.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        _LPVTexture_G.filterMode = FilterMode.Point;
        _LPVTexture_G.dimension = TextureDimension.Tex3D;
        _LPVTexture_G.volumeDepth = VoxelBoxSize.z;
        _LPVTexture_G.enableRandomWrite = true;
        _LPVTexture_G.Create();
        */

        LPVTexture_B = new RenderTexture(VoxelBoxSize.x, VoxelBoxSize.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        LPVTexture_B.filterMode = FilterMode.Trilinear;
        LPVTexture_B.dimension = TextureDimension.Tex3D;
        LPVTexture_B.volumeDepth = VoxelBoxSize.z;
        LPVTexture_B.enableRandomWrite = true;
        LPVTexture_B.Create();

        /*
        _LPVTexture_B = RenderTexture.GetTemporary(VoxelBoxSize.x, VoxelBoxSize.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        _LPVTexture_B.filterMode = FilterMode.Point;
        _LPVTexture_B.dimension = TextureDimension.Tex3D;
        _LPVTexture_B.volumeDepth = VoxelBoxSize.z;
        _LPVTexture_B.enableRandomWrite = true;
        _LPVTexture_B.Create();
        */

    }

    public void createLPVRenderTexture3DTemp()
    {
        LPVTexture_R_temp = new RenderTexture(VoxelBoxSize.x, VoxelBoxSize.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        LPVTexture_R_temp.filterMode = FilterMode.Point;
        LPVTexture_R_temp.dimension = TextureDimension.Tex3D;
        LPVTexture_R_temp.volumeDepth = VoxelBoxSize.z;
        LPVTexture_R_temp.enableRandomWrite = true;
        LPVTexture_R_temp.Create();

        LPVTexture_G_temp = new RenderTexture(VoxelBoxSize.x, VoxelBoxSize.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        LPVTexture_G_temp.filterMode = FilterMode.Point;
        LPVTexture_G_temp.dimension = TextureDimension.Tex3D;
        LPVTexture_G_temp.volumeDepth = VoxelBoxSize.z;
        LPVTexture_G_temp.enableRandomWrite = true;
        LPVTexture_G_temp.Create();

        LPVTexture_B_temp = new RenderTexture(VoxelBoxSize.x, VoxelBoxSize.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        LPVTexture_B_temp.filterMode = FilterMode.Point;
        LPVTexture_B_temp.dimension = TextureDimension.Tex3D;
        LPVTexture_B_temp.volumeDepth = VoxelBoxSize.z;
        LPVTexture_B_temp.enableRandomWrite = true;
        LPVTexture_B_temp.Create();
    }

    public void createSSLPVDepthTexture3D()
    {
        SSLPVAvgDepthTexture = new RenderTexture(VoxelBoxSize.x, VoxelBoxSize.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        SSLPVAvgDepthTexture.filterMode = FilterMode.Point;
        SSLPVAvgDepthTexture.dimension = TextureDimension.Tex3D;
        SSLPVAvgDepthTexture.volumeDepth = VoxelBoxSize.z;
        SSLPVAvgDepthTexture.enableRandomWrite = true;
        SSLPVAvgDepthTexture.Create();

        SSLPVVPLAvgDepthTexture = new RenderTexture(VoxelBoxSize.x, VoxelBoxSize.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        SSLPVVPLAvgDepthTexture.filterMode = FilterMode.Point;
        SSLPVVPLAvgDepthTexture.dimension = TextureDimension.Tex3D;
        SSLPVVPLAvgDepthTexture.volumeDepth = VoxelBoxSize.z;
        SSLPVVPLAvgDepthTexture.enableRandomWrite = true;
        SSLPVVPLAvgDepthTexture.Create();

        SSLPVAvgDepthSquareTexture = new RenderTexture(VoxelBoxSize.x, VoxelBoxSize.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        SSLPVAvgDepthSquareTexture.filterMode = FilterMode.Point;
        SSLPVAvgDepthSquareTexture.dimension = TextureDimension.Tex3D;
        SSLPVAvgDepthSquareTexture.volumeDepth = VoxelBoxSize.z;
        SSLPVAvgDepthSquareTexture.enableRandomWrite = true;
        SSLPVAvgDepthSquareTexture.Create();

    }

    /*
    public void createLPVRenderTexture3D_less()
    {
        bool textureChange = false;
        if(VoxelSize != VoxelSize_cpmpare)
        {
            VoxelSize_cpmpare = VoxelSize;
            textureChange = true;
        }
        for(int i = 0; i < 6; i++)
        {
            if(voxelBox[i] != voxelBox_compare[i])
            {
                for (int j = 0; j < 6; j++)
                {
                    voxelBox_compare[j] = voxelBox[j];
                }
                textureChange = true;
                break;
            }
        }

        if (textureChange)
        {
            createLPVRenderTexture3D();
        }

    }
    */

    public void reCreateLPVTexture3D()
    {

        if (VoxelBoxSizeChange)
        {
            createLPVRenderTexture3D();
            if(giSetting.LPV.LPVMode == LPVMode.SSLPV)
            {
                createSSLPVDepthTexture3D();
            }
        }
        if (this.LPVmode == 1 && giSetting.LPV.LPVMode == LPVMode.LPV)
        {
            LPVmode = 0;
            CleanUpWithoutBuffer();
            createLPVRenderTexture3D();
        }
        else if (this.LPVmode == 0 && giSetting.LPV.LPVMode == LPVMode.SSLPV)
        {
            LPVmode = 1;
            CleanUpWithoutBuffer();
            createLPVRenderTexture3D();
            createSSLPVDepthTexture3D();
        }

    }

    public void makeLPV(Vector2 cameraMapSize)
    {

        if (!giSetting.LPV.useLPV)
        {
            LPVbuffer.DisableShaderKeyword("_LPV");
            LPVbuffer.DisableShaderKeyword("_SSLPV");
            ExecuteBuffer(LPVbuffer);
            return;
        }

        if (!giSetting.RSM.useRSM)
        {
            Debug.LogError("需要先开启RSM才能使用LPV");
            LPVbuffer.DisableShaderKeyword("_LPV");
            LPVbuffer.DisableShaderKeyword("_SSLPV");
            ExecuteBuffer(LPVbuffer);
            return;
        }

        if (!LPVTexture3DCS)
        {
            Debug.LogError("需要生成LPV 3D纹理的计算着色器");
            LPVbuffer.DisableShaderKeyword("_LPV");
            LPVbuffer.DisableShaderKeyword("_SSLPV");
            ExecuteBuffer(LPVbuffer);
            return;
        }

        if (!makeVoxelBox(0))
        {
            LPVbuffer.DisableShaderKeyword("_LPV");
            LPVbuffer.DisableShaderKeyword("_SSLPV");
            ExecuteBuffer(LPVbuffer);
            return;
        }

        //使用LPV就不需要再使用RSM了
        LPVbuffer.DisableShaderKeyword("_RSM");

        if (giSetting.LPV.LPVMode == LPVMode.LPV)
        {
            /*
            if (!makeVoxelBox(0))
            {
                LPVbuffer.DisableShaderKeyword("_LPV");
                return;
            }
            */
            LPVbuffer.EnableShaderKeyword("_LPV");
            LPVbuffer.DisableShaderKeyword("_SSLPV");
            makeCommonLPV();
        }
        else
        {
            /*
            //对3D纹理并行缩减太麻烦了，耗费大脑，体素数量设为2的幂次简化问题
            if (!makeVoxelBox(1))
            {
                LPVbuffer.DisableShaderKeyword("_SSLPV");
                return;
            }
            */
            LPVbuffer.EnableShaderKeyword("_SSLPV");
            LPVbuffer.DisableShaderKeyword("_LPV");
            makeSSLPV(cameraMapSize);
        }
        ExecuteBuffer(LPVbuffer);

#if UNITY_EDITOR
        UnityEngine.Object tool = GameObject.Find("tool");
        if (tool)
        {
            GIEditor giEditor = tool.GetComponent<GIEditor>();
            if (giEditor)
            {

                Vector3 lightCenterPos = this.VoxelBoxCenterPoint;
                if (giSetting.LPV.LPVMode == LPVMode.SSLPV)
                {
                    giEditor.SSLPV = true;
                    //lightCenterPos = URPMath.mul(DirectionalLightViewMatrix.inverse, URPMath.transVec3ToVec4(this.VoxelBoxCenterPoint));
                    //lightCenterPos = URPMath.mul(ViewMatrix.inverse, URPMath.transVec3ToVec4(this.VoxelBoxCenterPoint));
                }
                else
                {
                    giEditor.SSLPV = false;
                }

                giEditor.VoxelBoxSize = VoxelBoxSize;
                giEditor.VoxelBoxCenterPoint = lightCenterPos;
                //giEditor.voxelBox = voxelBox;
                giEditor.VoxelSize = this.VoxelSize;
            }
        }
#endif

    }

    public void makeCommonLPV()
    {

        reCreateLPVTexture3D();
        //createLPVRenderTexture3D();
        //createLPVRenderTexture3DTemp();
        RenderTargetIdentifier[] targets = new RenderTargetIdentifier[3];
        targets[0] = LPVTexture_R;
        targets[1] = LPVTexture_G;
        targets[2] = LPVTexture_B;
        /*
        targets[3] = LPVTexture_R_temp;
        targets[4] = LPVTexture_G_temp;
        targets[5] = LPVTexture_B_temp;
        */
        LPVbuffer.SetRenderTarget(targets, LPVTexture_R.depthBuffer);

        /*
        int kernelHandle = LPVTexture3DCS.FindKernel("LPVSetUpCSMain");
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_R", LPVTexture_R);
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_G", LPVTexture_G);
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_B", LPVTexture_B);
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_R_temp", LPVTexture_R_temp);
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_G_temp", LPVTexture_G_temp);
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_B_temp", LPVTexture_B_temp);
        LPVbuffer.SetComputeIntParam(LPVTexture3DCS, "radiateSize", giSetting.LPV.radiateSize);
        LPVbuffer.DispatchCompute(LPVTexture3DCS, kernelHandle, Mathf.CeilToInt((float)VoxelBoxSize.x / 8),
                                                                Mathf.CeilToInt((float)VoxelBoxSize.y / 8),
                                                                Mathf.CeilToInt((float)VoxelBoxSize.z / 8));
        ExecuteBuffer(LPVbuffer);
        */

        int kernelHandle = LPVTexture3DCS.FindKernel("LPVInjectCSMain");
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_R", LPVTexture_R);
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_G", LPVTexture_G);
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_B", LPVTexture_B);
        /*
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_R_temp", LPVTexture_R_temp);
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_G_temp", LPVTexture_G_temp);
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_B_temp", LPVTexture_B_temp);
        */
        LPVbuffer.SetComputeVectorParam(LPVTexture3DCS, "VoxelBoxStartPoint", VoxelBoxStartPoint);
        LPVbuffer.SetComputeVectorParam(LPVTexture3DCS, "VoxelBoxSize", (Vector3)VoxelBoxSize);
        LPVbuffer.SetComputeFloatParam(LPVTexture3DCS, "VoxelSize", VoxelSize);
        LPVbuffer.SetComputeIntParam(LPVTexture3DCS, "radiateSize", giSetting.LPV.radiateSize);
        LPVbuffer.SetComputeFloatParam(LPVTexture3DCS, "radiateIntensity", giSetting.LPV.radiateIntensity);
        /*
        LPVTexture3DCS.Dispatch(kernelHandle, Mathf.CeilToInt((float)VoxelBoxSize.x / 8), 
                                              Mathf.CeilToInt((float)VoxelBoxSize.y / 8), 
                                              Mathf.CeilToInt((float)VoxelBoxSize.z / 8));
        //这里搞错了，我以3D纹理为基础，找每一个体素内的VPL
        //但是我可以直接用每个VPL去找体素，这样更快，更准
        //但是会出现线程同步问题，寄！！！还是原来的吧

        LPVbuffer.DispatchCompute(LPVTexture3DCS, kernelHandle, Mathf.CeilToInt((float)giSetting.RSM.mapSize / 8), 
                                                                Mathf.CeilToInt((float)giSetting.RSM.mapSize / 8), 1);
        */
        LPVbuffer.DispatchCompute(LPVTexture3DCS, kernelHandle, Mathf.CeilToInt((float)VoxelBoxSize.x / 8),
                                                                Mathf.CeilToInt((float)VoxelBoxSize.y / 8),
                                                                Mathf.CeilToInt((float)VoxelBoxSize.z / 8));

        ExecuteBuffer(LPVbuffer);

        LPVRadiate();


    }

    public Vector3 avgScreenVoxelIndex()
    {

        int kernelHandle = LPVTexture3DCS.FindKernel("SSLPVAcc3DCSMain");
        /*
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "SSLPVCalcSizeTexture", SSLPVCalcSizeTexture);

        //不要线程组开始减一半了，这样会导致2维和3维的线程无法完全相加
        Vector3Int threadGroupSize = VoxelBoxSize / 8;
        ComputeBuffer newVoxelSizeBuffer = new ComputeBuffer(1, 3 * sizeof(float));
        LPVbuffer.SetComputeBufferParam(LPVTexture3DCS, kernelHandle, "SSLPVSize", newVoxelSizeBuffer);
        LPVbuffer.SetComputeVectorParam(LPVTexture3DCS, "threadGroupSize", (Vector3)threadGroupSize);
        LPVbuffer.DispatchCompute(LPVTexture3DCS, kernelHandle, threadGroupSize.x,
                                                                threadGroupSize.y,
                                                                threadGroupSize.z);
        ExecuteBuffer(LPVbuffer);
        */

        LPVTexture3DCS.SetTexture(kernelHandle, "SSLPVAvgDepthTexture", SSLPVAvgDepthTexture);

        //不要线程组开始减一半了，这样会导致2维和3维的线程无法完全相加
        Vector3Int threadGroupSize = VoxelBoxSize / 8;
        ComputeBuffer newVoxelSizeBuffer = new ComputeBuffer(1, 3 * sizeof(float));
        LPVTexture3DCS.SetBuffer(kernelHandle, "SSLPVSize", newVoxelSizeBuffer);
        LPVTexture3DCS.SetVector("threadGroupSize", (Vector3)threadGroupSize);
        LPVTexture3DCS.Dispatch(kernelHandle, threadGroupSize.x,
                                              threadGroupSize.y,
                                              threadGroupSize.z);

        Vector3[] newVoxelSize = new Vector3[1];
        newVoxelSizeBuffer.GetData(newVoxelSize);
        //Debug.Log(newVoxelSize[0]);
        newVoxelSizeBuffer.Release();
        newVoxelSizeBuffer.Dispose();
        return newVoxelSize[0];

    }

    public bool reCalcVoxelBox(Vector2 cameraMapSize)
    {
       
        SSLPVAvgDepthTexture = new RenderTexture(VoxelBoxSize.x, VoxelBoxSize.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        SSLPVAvgDepthTexture.filterMode = FilterMode.Point;
        SSLPVAvgDepthTexture.dimension = TextureDimension.Tex3D;
        SSLPVAvgDepthTexture.volumeDepth = VoxelBoxSize.z;
        SSLPVAvgDepthTexture.enableRandomWrite = true;
        //SSLPVCalcRangeTexture.useMipMap = true;
        SSLPVAvgDepthTexture.Create();
        //LPVbuffer.SetRenderTarget(SSLPVAvgDepthTexture);

        int kernelHandle = LPVTexture3DCS.FindKernel("SSLPVSetUpCSMain");
        /*
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "SSLPVCalcSizeTexture", SSLPVCalcSizeTexture);
        LPVbuffer.SetComputeVectorParam(LPVTexture3DCS, "VoxelBoxStartPoint", VoxelBoxStartPoint);
        LPVbuffer.SetComputeVectorParam(LPVTexture3DCS, "VoxelBoxSize", (Vector3)VoxelBoxSize);
        LPVbuffer.SetComputeFloatParam(LPVTexture3DCS, "VoxelSize", VoxelSize);
        LPVbuffer.SetComputeIntParam(LPVTexture3DCS, "radiateSize", giSetting.LPV.radiateSize);
        LPVbuffer.DispatchCompute(LPVTexture3DCS, kernelHandle, Mathf.CeilToInt(cameraMapSize.x / 8),
                                                                Mathf.CeilToInt(cameraMapSize.y / 8), 1);
        ExecuteBuffer(LPVbuffer);
        */
        LPVTexture3DCS.SetTexture(kernelHandle, "SSLPVAvgDepthTexture", SSLPVAvgDepthTexture);
        LPVTexture3DCS.SetVector("VoxelBoxStartPoint", VoxelBoxStartPoint);
        LPVTexture3DCS.SetVector("VoxelBoxSize", (Vector3)VoxelBoxSize);
        LPVTexture3DCS.SetFloat("VoxelSize", VoxelSize);
        LPVTexture3DCS.SetInt("radiateSize", giSetting.LPV.radiateSize);
        LPVTexture3DCS.SetVector("cameraPos", camera.transform.position);
        LPVTexture3DCS.Dispatch(kernelHandle, Mathf.CeilToInt(cameraMapSize.x / 8),
                                              Mathf.CeilToInt(cameraMapSize.y / 8), 1);

        Vector3 newVoxelSize = avgScreenVoxelIndex();
        Vector3 SSVoxelSize = URPMath.floatVec3ToKMul(newVoxelSize, 1, this.VoxelSize);
        Vector3 SSVoxelBoxCenter = this.VoxelBoxStartPoint + URPMath.Vec3Opfloat(SSVoxelSize * this.VoxelSize, 0.5f * this.VoxelSize, 0);
        this.voxelBox[3] = SSVoxelBoxCenter.x;
        this.voxelBox[4] = SSVoxelBoxCenter.y;
        this.voxelBox[5] = SSVoxelBoxCenter.z;
        this.voxelBox[3] = SSVoxelSize.x * VoxelSize;
        this.voxelBox[4] = SSVoxelSize.y * VoxelSize;
        this.voxelBox[5] = SSVoxelSize.z * VoxelSize;
        if (!makeVoxelBox(0))
        {
            return false;
        }
        ExecuteBuffer(LPVbuffer);
        return true;
    }

    public void makeSSLPV(Vector2 cameraMapSize)
    {
        /*
        if (!reCalcVoxelBox(cameraMapSize))
        {
            return;
        }

        */

        reCreateLPVTexture3D();
        //createLPVRenderTexture3D();
        //createSSLPVDepthTexture3D();
        RenderTargetIdentifier[] targets = new RenderTargetIdentifier[6];
        targets[0] = LPVTexture_R;
        targets[1] = LPVTexture_G;
        targets[2] = LPVTexture_B;
        targets[3] = SSLPVAvgDepthTexture;
        targets[3] = SSLPVVPLAvgDepthTexture;
        targets[4] = SSLPVAvgDepthSquareTexture;
        LPVbuffer.SetRenderTarget(targets, LPVTexture_R.depthBuffer);
        //LPVbuffer.SetViewProjectionMatrices(camera.cameraToWorldMatrix, camera.projectionMatrix);

        LPVTexture3DCS.SetVector("VoxelBoxStartPoint", VoxelBoxStartPoint);
        LPVTexture3DCS.SetVector("VoxelBoxSize", (Vector3)VoxelBoxSize);
        LPVTexture3DCS.SetFloat("VoxelSize", VoxelSize);
        LPVTexture3DCS.SetInt("radiateSize", giSetting.LPV.radiateSize);
        LPVTexture3DCS.SetFloat("radiateIntensity", giSetting.LPV.radiateIntensity);
        LPVTexture3DCS.SetVector("cameraPos", camera.transform.position);
        LPVTexture3DCS.SetVector("lightDir", (this.light.transform.rotation * Vector3.forward).normalized);

        int kernelHandle = LPVTexture3DCS.FindKernel("SSLPVInjectCSMain");
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_R", LPVTexture_R);
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_G", LPVTexture_G);
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_B", LPVTexture_B);
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "SSLPVAvgDepthTexture", SSLPVAvgDepthTexture);
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "SSLPVVPLAvgDepthTexture", SSLPVVPLAvgDepthTexture);
        LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "SSLPVAvgDepthSquareTexture", SSLPVAvgDepthSquareTexture);
        LPVbuffer.DispatchCompute(LPVTexture3DCS, kernelHandle, Mathf.CeilToInt((float)VoxelBoxSize.x / 8),
                                                                Mathf.CeilToInt((float)VoxelBoxSize.y / 8),
                                                                Mathf.CeilToInt((float)VoxelBoxSize.z / 8));
        ExecuteBuffer(LPVbuffer);

        Shader.SetGlobalTexture("_LPVTexture_R", LPVTexture_R);
        Shader.SetGlobalTexture("_LPVTexture_G", LPVTexture_G);
        Shader.SetGlobalTexture("_LPVTexture_B", LPVTexture_B);
        Shader.SetGlobalVector("VoxelBoxStartPoint", VoxelBoxStartPoint);
        Shader.SetGlobalVector("VoxelBoxSize", (Vector3)VoxelBoxSize);
        Shader.SetGlobalFloat("VoxelSize", VoxelSize);
        Shader.SetGlobalFloat("LPV_Intensity", giSetting.LPV.Intensity);

        //LPVRadiate();

        /*
        float halfVoxelSize = 0.5f;
        Vector3 normal = new Vector3(1.0f, 0.0f, 0.0f).normalized;
        Vector3 worldPos = new Vector3(0.6f, 0.6f, 0.5f);
        Vector3 centerVoxelWorldPos = new Vector3(0.5f, 0.5f, 0.5f);

        Vector3 voxelPlanePoint = centerVoxelWorldPos + sign(normal) * halfVoxelSize;
        Vector3 judgeVPLVoxelPlane = URPMath.vec3Mulvec3(normal - (voxelPlanePoint - worldPos).normalized, normal);
        judgeVPLVoxelPlane = judgeVPLVoxelPlane == Vector3.zero ? abs(normal) : judgeVPLVoxelPlane;
        float maxAxle = MathF.Max(judgeVPLVoxelPlane.x, MathF.Max(judgeVPLVoxelPlane.y, judgeVPLVoxelPlane.z));
        judgeVPLVoxelPlane = URPMath.Vec3Opfloat(sign(URPMath.Vec3Opfloat(judgeVPLVoxelPlane, maxAxle, 0)), 1, 1);
        judgeVPLVoxelPlane = sign(URPMath.vec3Mulvec3(judgeVPLVoxelPlane, normal));
        Vector3 voxelPlane = centerVoxelWorldPos + judgeVPLVoxelPlane * halfVoxelSize;
        Vector3 voxelPoint = normal / length(URPMath.vec3Mulvec3(normal, judgeVPLVoxelPlane)) * length(URPMath.vec3Mulvec3((voxelPlane - worldPos), judgeVPLVoxelPlane)) + worldPos;

        Vector3 disVPLToAvg = worldPos - new Vector3(0.5f, 0.5f, 0.5f);
        Vector3 VPLMoveDistance = voxelPoint - worldPos + Vector3.Dot(disVPLToAvg, normal) * normal;
        Debug.Log(worldPos + VPLMoveDistance);
        */
    }
    /*
    Vector3 sign(Vector3 a)
    {
        return new Vector3(a.x > 0.0f ? 1.0f : a.x == 0.0f ? 0.0f : -1.0f,
                           a.y > 0.0f ? 1.0f : a.y == 0.0f ? 0.0f : -1.0f,
                           a.z > 0.0f ? 1.0f : a.z == 0.0f ? 0.0f : -1.0f);
    }

    float length(Vector3 a)
    {
        return MathF.Sqrt(a.x * a.x + a.y * a.y + a.z * a.z);
    }

    Vector3 abs(Vector3 a)
    {
        return new Vector3(Mathf.Abs(a.x), Mathf.Abs(a.y), Mathf.Abs(a.z));
    }
    */
    public void LPVRadiate()
    {

        if(giSetting.LPV.radiateSize != 0)
        {
            int kernelHandle = LPVTexture3DCS.FindKernel("LPVRadiateCSMain");
            LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_R", LPVTexture_R);
            LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_G", LPVTexture_G);
            LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_B", LPVTexture_B);
            if (giSetting.LPV.LPVMode == LPVMode.SSLPV)
            {
                LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "SSLPVAvgDepthTexture", SSLPVAvgDepthTexture);
                LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "SSLPVVPLAvgDepthTexture", SSLPVVPLAvgDepthTexture);
                LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "SSLPVAvgDepthSquareTexture", SSLPVAvgDepthSquareTexture);
            }
            /*
            LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_R_temp", LPVTexture_R_temp);
            LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_G_temp", LPVTexture_G_temp);
            LPVbuffer.SetComputeTextureParam(LPVTexture3DCS, kernelHandle, "LPVTexture_B_temp", LPVTexture_B_temp);
            */

            LPVbuffer.DispatchCompute(LPVTexture3DCS, kernelHandle, Mathf.CeilToInt((float)VoxelBoxSize.x / 8),
                                                                    Mathf.CeilToInt((float)VoxelBoxSize.y / 8),
                                                                    Mathf.CeilToInt((float)VoxelBoxSize.z / 8));
            /*
            for (int i = 0; i < giSetting.LPV.radiateSize; i++)
            {
                LPVbuffer.DispatchCompute(LPVTexture3DCS, kernelHandle, Mathf.CeilToInt((float)VoxelBoxSize.x / 8),
                                                                        Mathf.CeilToInt((float)VoxelBoxSize.y / 8),
                                                                        Mathf.CeilToInt((float)VoxelBoxSize.z / 8));
                ExecuteBuffer(LPVbuffer);
            }
            */
            /*
            LPVTexture3DCS.Dispatch(kernelHandle, Mathf.CeilToInt((float)VoxelBoxSize.x / 8), 
                                                  Mathf.CeilToInt((float)VoxelBoxSize.y / 8), 
                                                  Mathf.CeilToInt((float)VoxelBoxSize.z / 8));
            */
            /*
            //我们需要保证片元着色器渲染LPV前，计算着色器中已经对LPV3DTexture完成赋值
            //这里ChatGPT说用自定义插件，创建自定义事件，调用cmd.IssuePluginEvent(MyEventID, 0);来完成效率更高
            //但是有点超出我能力范围，以后一定要学，但现在先用效率低一点的吧
            //LPVBuffer.CopyTexture(LPVTexture_R, _LPVTexture_R);
            //LPVBuffer.CopyTexture(LPVTexture_G, _LPVTexture_G);
            //LPVBuffer.CopyTexture(LPVTexture_B, _LPVTexture_B);
            //GraphicsFence computeFence = buffer.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.ComputeProcessing);
            //LPVbuffer.WaitOnAsyncGraphicsFence(computeFence);
            */
            /*
            LPVbuffer.Blit(LPVTexture_R, _LPVTexture_R);
            LPVbuffer.Blit(LPVTexture_G, _LPVTexture_G);
            LPVbuffer.Blit(LPVTexture_B, _LPVTexture_B);
            //GraphicsFence computeFence = LPVbuffer.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.ComputeProcessing);
            //LPVbuffer.WaitOnAsyncGraphicsFence(computeFence);
            */
            ExecuteBuffer(LPVbuffer);
        }


        Shader.SetGlobalTexture("_LPVTexture_R", LPVTexture_R);
        Shader.SetGlobalTexture("_LPVTexture_G", LPVTexture_G);
        Shader.SetGlobalTexture("_LPVTexture_B", LPVTexture_B);
        Shader.SetGlobalVector("VoxelBoxStartPoint", VoxelBoxStartPoint);
        Shader.SetGlobalVector("VoxelBoxSize", (Vector3)VoxelBoxSize);
        Shader.SetGlobalFloat("VoxelSize", VoxelSize);
        Shader.SetGlobalFloat("LPV_Intensity", giSetting.LPV.Intensity);

    }

}
