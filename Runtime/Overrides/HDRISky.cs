namespace UnityEngine.Rendering.Universal
{
    [System.Serializable, VolumeComponentMenu("Post-processing/HDRI Sky")]
    public class HDRISky : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("天空图")]
        public CubemapParameter HdriSky = new CubemapParameter(null);

        [Tooltip("颜色")]
        public NoInterpColorParameter color = new NoInterpColorParameter(Color.white);

        [Tooltip("曝光强度")]
        public MinFloatParameter exposure = new MinFloatParameter(1.0f, 0f);

        [Tooltip("天空盒旋转")]
        public NoInterpClampedFloatParameter rotation = new NoInterpClampedFloatParameter(0, 0, 360);

        public bool IsActive() => HdriSky.value != null;

        public bool IsTileCompatible() => false;
    }
}