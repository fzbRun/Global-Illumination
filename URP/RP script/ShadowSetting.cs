using UnityEngine;
using System;

[System.Serializable]
public class ShadowSetting
{

    public enum TextureSize
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096,
        _8192 = 8192
    }

    public enum FilterMode
    {
        PCF2x2, PCF3x3, PCF5x5, PCF7x7
    }

    public enum CascadeBlendMode
    {
        Hard, Soft, Dither
    }

    [System.Serializable]
    public struct Direction
    {
        public TextureSize textureSize;
    }

    public Direction direction = new Direction
    {
        textureSize = TextureSize._1024
    };

    [Serializable]
    public struct Directional
    {

        public TextureSize atlasSize;

        [Range(1, 4)]
        public int cascadeCount;

        [Range(0.0f, 1.0f)]
        public float cascadeRatio1, cascadeRatio2, cascadeRatio3;

        public Vector3 cascadeRatios => new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);

        [Range(0.001f, 1.0f)]
        public float cascadeFade;

        public FilterMode filter;

        public CascadeBlendMode cascadeBlendMode;

    }

    [Min(0.001f)]
    public float maxDistance = 100.0f;

    [Range(0.001f, 1.0f)]
    public float distanceFade = 0.1f;

    public Directional directional = new Directional
    {
        atlasSize = TextureSize._1024,
        cascadeCount = 4,
        cascadeRatio1 = 0.1f,
        cascadeRatio2 = 0.25f,
        cascadeRatio3 = 0.5f,
        cascadeFade = 0.1f,
        filter = FilterMode.PCF2x2,
        cascadeBlendMode = CascadeBlendMode.Hard

    };

    [System.Serializable]
    public struct Other
    {

        public TextureSize atlasSize;

        public FilterMode filter;
    }

    public Other other = new Other
    {
        atlasSize = TextureSize._1024,
        filter = FilterMode.PCF2x2
    };

}
