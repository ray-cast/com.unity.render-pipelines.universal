namespace UnityEngine.Rendering.Universal
{
    [System.Serializable, VolumeComponentMenu("Post-processing/Fog/HeightFog")]
    public class HeightFog : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("雾气深处颜色")]
        public ColorParameter deepColor = new ColorParameter(Color.white, false, false, true); //0.77647f, 0.84705f, 0.98039f

        [Tooltip("雾气浅处颜色")]
        public ColorParameter shallowColor = new ColorParameter(Color.white, false, false, true); //0.77647f, 0.84705f, 0.98039f

        [Tooltip("雾气浓度")]
        public MinFloatParameter density = new MinFloatParameter(0f, 0f);

        [Tooltip("雾气高度衰减")]
        public ClampedFloatParameter heightFalloff = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("雾气开始高度")]
        public FloatParameter height = new FloatParameter(0f);

        [Tooltip("雾气根据相机位置衰减")]
        public BoolParameter followCamera = new BoolParameter(false);

        public bool IsActive() => density.value > 0f;

        public bool IsTileCompatible() => false;
    }
}