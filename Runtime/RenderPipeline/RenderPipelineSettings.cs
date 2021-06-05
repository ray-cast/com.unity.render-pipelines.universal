using System;

namespace UnityEngine.Rendering.Universal
{
    [Serializable]
    public sealed class RenderPipelineSettings
    {
        public GlobalLightingQualitySettings lightingQualitySettings;

        internal RenderPipelineSettings()
        {
        }

        internal static RenderPipelineSettings NewDefault() => new RenderPipelineSettings()
        {
            lightingQualitySettings = GlobalLightingQualitySettings.NewDefault()
        };
    }
}