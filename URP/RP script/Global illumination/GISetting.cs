using UnityEngine;
using System;

[System.Serializable]
public class GISetting
{
    public bool OnlyGI;
    [Range(0.0f, 1.0f)]
    public float Intensity = 1.0f;

    [Serializable]
    public struct IBLSetting
    {
        public bool useIBL;
        public Texture2D irradianceMap;
        public Texture2D LobeSpecualrMap;
        public Texture2D BRDFMap;

        [Range(0.0f, 1.0f)]
        public float Intensity;

    }
    public IBLSetting IBL = new IBLSetting {
        useIBL = true,
        Intensity = 1.0f
    };

    [Serializable]
    public struct VoxelSetting
    {
        public Vector3 VoxelBoxStartPoint;
        public Vector3Int VoxelBoxSize;
        [Range(0.1f, 2.0f)]
        public float VoxelSize;
        [Range(1, 50)]
        public int CameraBoundSize;
    }
    public VoxelSetting voxelSetting = new VoxelSetting
    {
        VoxelBoxSize = Vector3Int.one,
        VoxelSize = 1,
        CameraBoundSize = 20
    };

    [Serializable]
    public struct RSMSetting
    {
        public bool useRSM;
        public bool fastRsm;
        //public Shader RSMLUTShader;
        public ComputeShader RSMLUTCS;
        public int mapSize;
        [Range(1, 100)]
        public float Intensity;
        public int SampelSize;

    }
    public RSMSetting RSM = new RSMSetting
    {
        useRSM = false,
        fastRsm = true,
        //RSMLUTShader = null,
        RSMLUTCS = null,
        Intensity = 10.0f,
        SampelSize = 400,
        mapSize = 512
    };

    [Serializable]
    public enum LPVMode
    {
        LPV,
        SSLPV
    }
    [Serializable]
    public struct LPVSetting
    {
        public bool useLPV;
        public LPVMode LPVMode;
        public ComputeShader LPVCS;
        public ComputeShader SSLPVCS;
        [Range(0, 10)]
        public int radiateSize;
        [Range(0.1f, 2)]
        public float Intensity;
        [Range(0.1f, 5)]
        public float radiateIntensity;
    }
    public LPVSetting LPV = new LPVSetting
    {
        useLPV = false,
        LPVMode = LPVMode.LPV,
        radiateSize = 1,
        Intensity = 1,
        radiateIntensity = 1
    };
}
