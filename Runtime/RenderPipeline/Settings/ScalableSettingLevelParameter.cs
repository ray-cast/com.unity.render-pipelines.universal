using System;

namespace UnityEngine.Rendering.Universal
{
	[Serializable]
	public sealed class ScalableSettingLevelParameter : IntParameter
    {
        public const int LevelCount = 4;

        public enum Level
        {
            Custom,
            Low,
            Medium,
            High
        }

        public ScalableSettingLevelParameter(int level, bool useOverride, bool overrideState = false)
            : base(useOverride ? LevelCount : (int)level, overrideState)
        {

        }

        public (int level, bool useOverride) levelAndOverride
        {
            get => value == LevelCount ? ((int)Level.Low, true) : (value, false);
            set
            {
                var (level, useOverride) = value;
                this.value = useOverride ? LevelCount : (int)level;
            }
        }
    }
}
