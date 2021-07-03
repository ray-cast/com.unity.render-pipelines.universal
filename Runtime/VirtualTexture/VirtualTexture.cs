using System;

namespace UnityEngine.Rendering.Universal
{
    [System.Serializable, VolumeComponentMenu("VirtualTexture")]
    public class VirtualTexture : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("启用虚拟纹理")]
        public BoolParameter enable = new BoolParameter(true);

        [Tooltip("区域中心")]
        public NoInterpVector2Parameter center = new NoInterpVector2Parameter(Vector2.zero);

        [Tooltip("区域大小")]
        public NoInterpFloatParameter size = new NoInterpFloatParameter(128);

        [Tooltip("自适应区域")]
        public BoolParameter regionAdaptation = new BoolParameter(false);

        public bool IsActive() => this.enable.value;

        public bool IsTileCompatible() => false;
    }
}