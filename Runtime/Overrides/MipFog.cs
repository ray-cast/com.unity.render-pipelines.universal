﻿using System;

namespace UnityEngine.Rendering.Universal
{
    public enum MipFogMode
    {
        Linear,
        Exponential,
        ExponentialSquared
    }

    [Serializable]
    public sealed class MipFogModeParameter : VolumeParameter<MipFogMode>
    {
        public MipFogModeParameter(MipFogMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable, VolumeComponentMenu("Post-processing/Fog/MipFog")]
    public class MipFog : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("雾气模式")]
        public MipFogModeParameter mode = new MipFogModeParameter(MipFogMode.Exponential);

        [Tooltip("雾气颜色")]
        public ColorParameter color = new ColorParameter(Color.white, false, false, true); //0.77647f, 0.84705f, 0.98039f

        [Tooltip("雾气浓度")]
        public MinFloatParameter density = new MinFloatParameter(0f, 0f);

        [Tooltip("雾气开始位置")]
        public FloatParameter start = new FloatParameter(10f);

        [Tooltip("雾气结束位置")]
        public FloatParameter end = new FloatParameter(100f);

        [Tooltip("雾气天空浓度")]
        public NoInterpClampedFloatParameter skyDensity = new NoInterpClampedFloatParameter(0f, 0f, 1.0f);

        [Tooltip("模糊后的天空图")]
        public TextureParameter skybox = new TextureParameter(null);

        [Tooltip("天空盒旋转")]
        public NoInterpClampedFloatParameter rotation = new NoInterpClampedFloatParameter(0, 0, 360);

        public bool IsActive() => density.value > 0f;

        public bool IsTileCompatible() => false;
    }
}