using System;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.Universal
{
    [Serializable, VolumeComponentMenu("Shadowing/Screen Space Ambient Occlusion")]
    public class ScreenSpaceAmbientOcclusion : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("渲染质量")]
        public QualityModeParameter quality = new QualityModeParameter(ScalableSettingLevelParameter.Level.Medium);

        [Tooltip("遮蔽范围")]
        public NoInterpClampedFloatParameter radius = new NoInterpClampedFloatParameter(1.0f, 0.0f, 5.0f);

        [Tooltip("遮蔽强度")]
        public ClampedFloatParameter strength = new ClampedFloatParameter(0f, 0f, 5f);

        [Tooltip("清晰度")]
        public ClampedFloatParameter sharpness = new ClampedFloatParameter(5f, 0f, 10f);

        [Tooltip("偏离率")]
        public ClampedFloatParameter bias = new ClampedFloatParameter(0.01f, 0.1f, 1.0f);

        [Tooltip("全分辨率渲染")]
        public BoolParameter _fullRes = new BoolParameter(false);

        public bool shouledFullRes
        {
            get
            {
                switch (quality.value)
				{
                    case ScalableSettingLevelParameter.Level.Low:
                        return UniversalRenderPipeline.GetLightingQualitySettings().AmbientOcclusionFullRes[(int)ScalableSettingLevelParameter.Level.Low];
                    case ScalableSettingLevelParameter.Level.Medium:
                        return UniversalRenderPipeline.GetLightingQualitySettings().AmbientOcclusionFullRes[(int)ScalableSettingLevelParameter.Level.Medium];
                    case ScalableSettingLevelParameter.Level.High:
                        return UniversalRenderPipeline.GetLightingQualitySettings().AmbientOcclusionFullRes[(int)ScalableSettingLevelParameter.Level.High];
                    default:
                        return _fullRes.value;
                }
            }
        }

        public bool IsActive() => strength.value > 0.0f;

        public bool IsTileCompatible() => false;
    }
}