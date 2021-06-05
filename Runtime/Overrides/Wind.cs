using System;

namespace UnityEngine.Rendering.Universal
{
    public enum WindMode
    {
        Physics,
        Custom,
    }

    public enum WindLevel
    {
        Level0,
        Level1,
        Level2,
        Level3,
        Level4,
        Level5,
        Level6,
        Level7,
        Level8,
        Level9,
        Level10,
    }

    [Serializable]
    public sealed class WindModeParameter : VolumeParameter<WindMode>
    {
        public WindModeParameter(WindMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    public sealed class WindLevelParameter : VolumeParameter<WindLevel>
    {
        public WindLevelParameter(WindLevel value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [System.Serializable, VolumeComponentMenu("Environment/Wind")]
    public class Wind : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("风的朝向")]
        public Vector3Parameter direction = new Vector3Parameter(Vector3.right);

        [Tooltip("运动范围")]
        public MinFloatParameter range = new MinFloatParameter(20f, 0f);

        [Tooltip("模式")]
        public WindModeParameter mode = new WindModeParameter(WindMode.Physics);

        [Tooltip("风级")]
        public WindLevelParameter scale = new WindLevelParameter(WindLevel.Level0);

        [Tooltip("风速")]
        public MinFloatParameter speed = new MinFloatParameter(4.04f, 0f);

        [Tooltip("风压")]
        public MinFloatParameter load = new MinFloatParameter(1.0f, 0);

        [Tooltip("摇摆频率")]
        public MinFloatParameter frequency = new MinFloatParameter(1.0f, 0);

        [Tooltip("摇摆扰动")]
        public Vector2Parameter random = new Vector2Parameter(Vector2.one);

        [Tooltip("摇摆弯曲程度")]
        public MinFloatParameter bending = new MinFloatParameter(0.1f, 0f);

        [Tooltip("波浪平铺次数")]
        public Vector2Parameter tiling = new Vector2Parameter(Vector2.one * 10);

        [Tooltip("波浪噪波")]
        public TextureParameter noise = new TextureParameter(null);

        public bool IsActive() => load.value > 0;

        public bool IsTileCompatible() => false;
    }
}