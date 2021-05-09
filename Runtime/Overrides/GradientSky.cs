namespace UnityEngine.Rendering.Universal
{
    [System.Serializable, VolumeComponentMenu("Post-processing/Gradient Sky")]
    public class GradientSky : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("天空颜色")]
        public ColorParameter top = new ColorParameter(new Color(0.7f, 0.87f, 1f), true, true, true);

        [Tooltip("水平线颜色")]
        public ColorParameter middle = new ColorParameter(new Color(0.3f, 0.7f, 1f), true, true, true);

        [Tooltip("地面颜色")]
        public ColorParameter bottom = new ColorParameter(new Color(1.0f, 0.75f, 0.5f), true, true, true);

        public MinFloatParameter gradientDiffusion = new MinFloatParameter(1.0f, 0.0f);

        [Tooltip("曝光强度")]
        public MinFloatParameter exposure = new MinFloatParameter(0.0f, 0f);

        public bool IsActive() => exposure.value > 0;

        public bool IsTileCompatible() => false;
    }
}