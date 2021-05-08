namespace UnityEngine.Rendering.Universal
{
    [System.Serializable, VolumeComponentMenu("Post-processing/Gradient Sky")]
    public class GradientSky : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("天空颜色")]
        public ColorParameter top = new ColorParameter(Color.blue, true, true, true);

        [Tooltip("水平线颜色")]
        public ColorParameter middle = new ColorParameter(new Color(0.3f, 0.7f, 1f), true, true, true);

        [Tooltip("地面颜色")]
        public ColorParameter bottom = new ColorParameter(Color.white, true, true, true);

        [Tooltip("曝光强度")]
        public MinFloatParameter exposure = new MinFloatParameter(0.0f, 0f);

        public bool IsActive() => exposure.value > 0;

        public bool IsTileCompatible() => false;
    }
}