using System;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.Universal
{
    [Serializable]
    public sealed class QualityModeParameter : VolumeParameter<ScalableSettingLevelParameter.Level>
    {
        public QualityModeParameter(ScalableSettingLevelParameter.Level value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable, VolumeComponentMenu("Shadowing/Capsule Shadow")]
    public class CapsuleShadow : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("渲染质量")]
        public QualityModeParameter quality = new QualityModeParameter(ScalableSettingLevelParameter.Level.Medium);

        [Tooltip("阴影范围")]
        public NoInterpClampedFloatParameter angle = new NoInterpClampedFloatParameter(30.0f, 0f, 90.0f);

        [Tooltip("阴影强度")]
        public ClampedFloatParameter strength = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("全分辨率渲染")]
        public BoolParameter _fullRes = new BoolParameter(false);

        public bool shouledFullRes
        {
            get
            {
                switch (quality.value)
				{
                    case ScalableSettingLevelParameter.Level.Low:
                        return UniversalRenderPipeline.GetLightingQualitySettings().CapsuleShadowFullRes[(int)ScalableSettingLevelParameter.Level.Low];
                    case ScalableSettingLevelParameter.Level.Medium:
                        return UniversalRenderPipeline.GetLightingQualitySettings().CapsuleShadowFullRes[(int)ScalableSettingLevelParameter.Level.Medium];
                    case ScalableSettingLevelParameter.Level.High:
                        return UniversalRenderPipeline.GetLightingQualitySettings().CapsuleShadowFullRes[(int)ScalableSettingLevelParameter.Level.High];
                    default:
                        return _fullRes.value;
                }
            }
        }

        public bool IsActive() => strength.value > 0.0f;

        public bool IsTileCompatible() => false;
    }
}