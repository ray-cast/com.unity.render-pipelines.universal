namespace UnityEngine.Rendering.Universal
{
    [System.Serializable, VolumeComponentMenu("Fog/HeightFog")]
    public class HeightFog : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("启用")]
        public BoolParameter enable = new BoolParameter(false);

        [Tooltip("雾气高度衰减")]
        public MinFloatParameter fogAttenuationDistance = new MinFloatParameter(400.0f, 1.0f);

        [Tooltip("雾气基本高度")]
        public FloatParameter baseHeight = new FloatParameter(0f);

        [Tooltip("雾气最高高度")]
        public FloatParameter maximumHeight = new FloatParameter(500f);

        [Tooltip("雾气底部浓度")]
        public MinFloatParameter heightDensity = new MinFloatParameter(0.0f, 0.0f);

        [Tooltip("雾气颜色")]
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);

        [Tooltip("模糊后的天空图")]
        public FogColorModeParameter colorMode = new FogColorModeParameter(FogColorMode.Constant);

        [Tooltip("雾气的高度相对于相机位置")]
        public BoolParameter relativeRendering = new BoolParameter(false);

        public bool IsActive() => enable.value;

        public bool IsTileCompatible() => false;
    }
}