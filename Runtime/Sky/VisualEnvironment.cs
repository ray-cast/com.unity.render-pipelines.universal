using System;

namespace UnityEngine.Rendering.Universal
{
    public enum SkyMode
    {
        None,
        HDRISky
    }

    public enum AmbientLightingMode
    {
        None,
        HDRISky,
        GradientSky
    }

    [Serializable]
    public sealed class SkyModeParameter : VolumeParameter<SkyMode>
    {
        public SkyModeParameter(SkyMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    public sealed class AmbientLightingModeParameter : VolumeParameter<AmbientLightingMode>
    {
        public AmbientLightingModeParameter(AmbientLightingMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [System.Serializable, VolumeComponentMenu("VisualEnvironment")]
    public class VisualEnvironment : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("天空球设置")]
        public SkyModeParameter sky = new SkyModeParameter(SkyMode.None);

        [Tooltip("环境光颜色")]
        public AmbientLightingModeParameter ambient = new AmbientLightingModeParameter(AmbientLightingMode.None);

        public bool IsActive() => this.active;

        public bool IsTileCompatible() => false;

        VisualEnvironment()
		{
            this.active = false;
		}
    }
}