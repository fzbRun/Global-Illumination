using UnityEngine;
using System;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Setting")]
public class PostFXSetting : ScriptableObject
{

    [SerializeField]
    Shader shader = default;

    [System.NonSerialized]
    Material material = default;
    public Material Material
    {
        get
        {
            if(material == null && shader != null)
            {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;    //不显示也不保存，完全受脚本控制
            }
            return material;
        }
    }

    public enum Mode { Additive, Scattering }

    [System.Serializable]
    public struct BloomSetting {

        public bool ignoreRenderScale;

        [Range(0f, 16f)]
        public int maxIterations;

        [Min(1f)]
        public int downScaleLimit;

        [Min(0f)]
        public float threshould;

        [Range(0f, 1f)]
        public float threshouldKnee;

        [Min(0f)]
        public float intensity;

        public bool fadeFireflies;

        public Mode mode;

        [Range(0.05f, 0.95f)]
        public float scatter;

    }

    public bool bicubicUpsampling;

    [SerializeField]
    BloomSetting bloomSetting = new BloomSetting
    {
        scatter = 0.7f
    };

    public BloomSetting bloom => bloomSetting;

    [Serializable]
    public struct ColorAdjustmentsSettings {
        public float postExposure;

        [Range(-100f, 100f)]
        public float contrast;

        [ColorUsage(false, true)]
        public Color colorFilter;

        [Range(-180f, 180f)]
        public float hueShift;

        [Range(-100f, 100f)]
        public float saturation;
    }

    [SerializeField]
    ColorAdjustmentsSettings colorAdjustmentsSettings = new ColorAdjustmentsSettings
    {
        colorFilter = Color.white
    };

    public ColorAdjustmentsSettings ColorAdjustments => colorAdjustmentsSettings;

    [Serializable]
    public struct WhiteBalanceSettings
    {

        [Range(-100f, 100f)]
        public float temperature, tint;
    }

    [SerializeField]
    WhiteBalanceSettings whiteBalance = default;

    public WhiteBalanceSettings WhiteBalance => whiteBalance;

    [Serializable]
    public struct SplitToningSettings
    {

        [ColorUsage(false)]
        public Color shadows, highlights;

        [Range(-100f, 100f)]
        public float balance;
    }

    [SerializeField]
    SplitToningSettings splitToning = new SplitToningSettings
    {
        shadows = Color.gray,
        highlights = Color.gray
    };

    public SplitToningSettings SplitToning => splitToning;

    [Serializable]
    public struct ChannelMixerSettings
    {

        public Vector3 red, green, blue;
    }

    [SerializeField]
    ChannelMixerSettings channelMixer = new ChannelMixerSettings
    {
        red = Vector3.right,
        green = Vector3.up,
        blue = Vector3.forward
    };

    public ChannelMixerSettings ChannelMixer => channelMixer;

    [Serializable]
    public struct ShadowsMidtonesHighlightsSettings
    {

        [ColorUsage(false, true)]
        public Color shadows, midtones, highlights;

        [Range(0f, 2f)]
        public float shadowsStart, shadowsEnd, highlightsStart, highLightsEnd;
    }

    [SerializeField]
    ShadowsMidtonesHighlightsSettings shadowsMidtonesHighlights = new ShadowsMidtonesHighlightsSettings
    {
        shadows = Color.white,
        midtones = Color.white,
        highlights = Color.white,
        shadowsEnd = 0.3f,
        highlightsStart = 0.55f,
        highLightsEnd = 1f
    };

    public ShadowsMidtonesHighlightsSettings ShadowsMidtonesHighlights =>
        shadowsMidtonesHighlights;

    [System.Serializable]
    public struct ToneMappingSettings
    {
        public enum Mode
        {
            None,
            ACES,
            Neutral,
            Reinhard,
            E
        }
        public Mode mode;
    }

    [SerializeField]
    ToneMappingSettings toneMappingSettings = default;

    public ToneMappingSettings ToneMapping => toneMappingSettings;

}
