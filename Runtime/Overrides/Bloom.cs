using System;

namespace UnityEngine.Rendering.Universal
{
    public enum BloomMode
    {
        Gaussian,
        Kawase,
    }

    [Serializable]
    public sealed class BloomModeParameter : VolumeParameter<BloomMode>
    {
        public BloomModeParameter(BloomMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable, VolumeComponentMenu("Post-processing/Bloom")]
    public sealed class Bloom : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("设置泛光的模糊函数")]
        public BloomModeParameter mode = new BloomModeParameter(BloomMode.Kawase);

        [Tooltip("设置颜色的亮度超过一定阈值时进行泛光处理")]
        public MinFloatParameter threshold = new MinFloatParameter(0.9f, 0f);

        [Tooltip("设置泛光的显示强度")]
        public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);

        [Tooltip("设置模糊次数，设置过高时会导致性能问题")]
        public NoInterpClampedIntParameter iteration = new NoInterpClampedIntParameter(5, 1, 10);

        [Tooltip("设置模糊半径")]
        public NoInterpClampedFloatParameter radius = new NoInterpClampedFloatParameter(3.0f, 1f, 10.0f);

        [Tooltip("设置模糊后的图片与原图混合权重比")]
        public ClampedFloatParameter scatter = new ClampedFloatParameter(0.6f, 0f, 1f);

        [Tooltip("设置该参数用于抑制最大的亮度")]
        public MinFloatParameter clamp = new MinFloatParameter(65472f, 0f);

        [Tooltip("设置颜色叠加")]
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);

        [Tooltip("勾选后可减少闪烁问题")]
        public BoolParameter highQualityFiltering = new BoolParameter(true);

        [Tooltip("勾选后可支持外发光效果")]
        public BoolParameter glowFiltering = new BoolParameter(true);

        [Tooltip("设置遮罩纹理")]
        public TextureParameter dirtTexture = new TextureParameter(null);

        [Tooltip("设置遮罩的亮度")]
        public MinFloatParameter dirtIntensity = new MinFloatParameter(0f, 0f);

        public bool IsActive() => intensity.value > 0f;

        public bool IsTileCompatible() => false;
    }
}