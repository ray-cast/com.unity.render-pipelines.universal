using System;

namespace UnityEngine.Rendering.Universal
{
    [Serializable]
    public sealed class GlobalLightingQualitySettings
    {
        static int s_QualitySettingCount = Enum.GetNames(typeof(ScalableSettingLevelParameter.Level)).Length;

        internal static GlobalLightingQualitySettings NewDefault() => new GlobalLightingQualitySettings();

        public bool[] CapsuleShadowFullRes = new bool[s_QualitySettingCount];
        public bool[] AmbientOcclusionFullRes = new bool[s_QualitySettingCount];

        internal GlobalLightingQualitySettings()
        {
            CapsuleShadowFullRes[(int)ScalableSettingLevelParameter.Level.Low] = false;
            CapsuleShadowFullRes[(int)ScalableSettingLevelParameter.Level.Medium] = false;
            CapsuleShadowFullRes[(int)ScalableSettingLevelParameter.Level.High] = true;

            AmbientOcclusionFullRes[(int)ScalableSettingLevelParameter.Level.Low] = false;
            AmbientOcclusionFullRes[(int)ScalableSettingLevelParameter.Level.Medium] = false;
            AmbientOcclusionFullRes[(int)ScalableSettingLevelParameter.Level.High] = true;
        }
    }
}
