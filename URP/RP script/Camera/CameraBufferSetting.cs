using UnityEngine;
using System;

[System.Serializable]
public struct CameraBufferSetting
{
	public bool allowHDR;
	public bool copyColor, copyColorReflection, copyDepth, copyDepthReflection;

	[Range(0.1f, 2.0f)]
	public float renderScale;

	public enum BicubicRescalingMode
    {
		Off, UpOnly, UpAndDown
    }
	public BicubicRescalingMode bicubicRescaling;

	public enum Quality
	{
		High, Medium, Low
	}

	[Serializable]
	public struct FXAA
	{
		public bool enabled;

		[Range(0.0312f, 0.0833f)]
		public float fixedThreshold;

		[Range(0.063f, 0.333f)]
		public float relativeThreshold;

		[Range(0f, 1f)]
		public float subpixelBlending;

		public Quality quality;

	}

	public FXAA fxaa;

}