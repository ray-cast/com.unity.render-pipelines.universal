namespace UnityEngine.Rendering.Universal
{
    [System.Serializable, VolumeComponentMenu("Post-processing/Fog/MipFog")]
    public class MipFog : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("雾气颜色")]
        public ColorParameter color = new ColorParameter(Color.white, false, false, true); //0.77647f, 0.84705f, 0.98039f

        [Tooltip("雾气浓度")]
        public MinFloatParameter density = new MinFloatParameter(0f, 0f);

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