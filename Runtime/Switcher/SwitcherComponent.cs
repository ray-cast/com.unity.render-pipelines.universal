using System;

namespace UnityEngine.Rendering.Universal
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class SwitcherComponentMenu : Attribute
    {
        public readonly string menu;

        public SwitcherComponentMenu(string menu)
        {
            this.menu = menu;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class SwitcherComponentDeprecated : Attribute
    {
    }

    [Serializable]
    public class SwitcherComponent : ScriptableObject
    {
        public bool active = true;

        public string displayName { get; protected set; } = "";

        public Light light;

        public float weight;

        public virtual void Override(float interpFactor)
        {
            if (light)
			{
                float interp = Mathf.Lerp(light.GetUniversalAdditionalLightData().weight, this.weight, interpFactor);
                light.enabled = interp > 0 ? true : false;
                light.GetUniversalAdditionalLightData().weight = interp;
            }
        }
    }
}