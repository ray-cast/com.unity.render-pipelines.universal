using System;

namespace UnityEngine.Rendering.Universal
{
    [Serializable, VolumeComponentMenu("Lighting/Exposure")]
    public sealed class Exposure : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("ÆôÓÃ")]
        public BoolParameter enable = new BoolParameter(false);

        [Tooltip("Adjusts the overall exposure of the scene in EV100. This is applied after HDR effect and right before tonemapping so it won't affect previous effects in the chain.")]
        public FloatParameter mainLighting = new FloatParameter(1f);

        public bool IsActive() { return enable.value; }

        public bool IsTileCompatible() => true;
    }
}
