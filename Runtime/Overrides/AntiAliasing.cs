using System;

namespace UnityEngine.Rendering.Universal
{
    [Serializable]
    public sealed class AntialiasingModeParameter : VolumeParameter<AntialiasingMode>
    {
        public AntialiasingModeParameter(AntialiasingMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable, VolumeComponentMenu("Post-processing/AntiAliasing")]
    public sealed class AntiAliasing : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("设置抗锯齿模式")]
        public AntialiasingModeParameter mode = new AntialiasingModeParameter(AntialiasingMode.None);

        public bool IsActive() => mode.value != AntialiasingMode.None;

        public bool IsTileCompatible() => false;
    }
}