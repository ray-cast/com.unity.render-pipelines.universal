using System;

namespace UnityEngine.Rendering.Universal
{
    [Serializable, VolumeComponentMenu("Environment/Cloud Shadow")]
    public class CloudShadow : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("云的速度")]
        public MinFloatParameter speed = new MinFloatParameter(0.1f, 0);

        [Tooltip("云的阴影权重")]
        public MinFloatParameter strength = new MinFloatParameter(1.0f, 0);

        [Tooltip("平铺次数")]
        public Vector2Parameter tiling = new Vector2Parameter(Vector2.one * 50);

        [Tooltip("阴影噪音图")]
        public TextureParameter shadow = new TextureParameter(null);

        public bool IsActive() => shadow.value != null;

        public bool IsTileCompatible() => false;
    }
}