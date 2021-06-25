using System;
using System.Diagnostics;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [Serializable, VolumeComponentMenu("Post-processing/Vignette Lookup")]
    public sealed class VignetteLookup : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("A custom 2D texture lookup table to apply.")]
        public TextureParameter texture = new TextureParameter(null);

        [Tooltip("How much of the lookup texture will contribute to the color grading effect.")]
        public ClampedFloatParameter contribution = new ClampedFloatParameter(1f, 0f, 1f);

        [Tooltip("Vignette color.")]
        public ColorParameter color = new ColorParameter(Color.white, false, false, true);

        [Tooltip("Sets the vignette center point (screen center is [0.5,0.5]).")]
        public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.5f));

        [Tooltip("Amount of vignetting on screen.")]
        public ClampedFloatParameter radius = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("Smoothness of the vignette borders.")]
        public ClampedFloatParameter smoothness = new ClampedFloatParameter(1.0f, 0.01f, 1f);

        [Tooltip("Should the vignette be perfectly round or be dependent on the current aspect ratio?")]
        public BoolParameter rounded = new BoolParameter(false);

        [Tooltip("How much of the lookup texture will contribute to the color grading effect.")]
        public ClampedFloatParameter strength = new ClampedFloatParameter(0.2f, 0f, 1.0f);

        public bool IsActive() => contribution.value > 0f && ValidateLUT();

        public bool IsTileCompatible() => true;

        public bool ValidateLUT()
        {
            bool valid = false;

            switch (texture.value)
            {
                case Texture2D t:
                    valid |= t.width == texture.value.height * texture.value.height && !GraphicsFormatUtility.IsSRGBFormat(t.graphicsFormat) && GraphicsFormatUtility.IsHalfFormat(t.graphicsFormat);
                    break;
                case RenderTexture rt:
                    valid |= rt.dimension == TextureDimension.Tex2D && rt.width == texture.value.height * texture.value.height && !rt.sRGB && GraphicsFormatUtility.IsHalfFormat(rt.graphicsFormat);
                    break;
            }

            return valid;
        }
    }
}
