using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.Rendering.Universal
{
	class SkyManager
	{
		static readonly Lazy<SkyManager> s_Instance = new Lazy<SkyManager>(() => new SkyManager());

        public static SkyManager instance => s_Instance.Value;

        Material _hdriMaterial;

        Material hdriMaterial
        {
            get
            {
                if (_hdriMaterial == null)
                    _hdriMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Skybox/Cubemap"));

                return _hdriMaterial;
            }
        }

        internal void SetupAmbientProbe(Camera camera)
		{

		}

		public void UpdateEnvironment(Camera camera)
		{
            VolumeStack stack = VolumeManager.instance.stack;
            var env = stack.GetComponent<VisualEnvironment>();
            if (env.IsActive())
            {
                var ambientLightingMode = env.ambient.value;
                if (ambientLightingMode == AmbientLightingMode.None)
                {
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = Color.black;
                }
                else if (ambientLightingMode == AmbientLightingMode.HDRISky)
                {
                    var sky = stack.GetComponent<HDRISky>();
                    RenderSettings.ambientMode = AmbientMode.Skybox;
                    RenderSettings.ambientIntensity = sky.exposure.value;
                }
                else if (ambientLightingMode == AmbientLightingMode.GradientSky)
                {
                    var gradientSky = stack.GetComponent<GradientSky>();
                    RenderSettings.ambientMode = AmbientMode.Trilight;
                    RenderSettings.ambientSkyColor = gradientSky.top.value * gradientSky.exposure.value;
                    RenderSettings.ambientEquatorColor = gradientSky.middle.value * gradientSky.exposure.value;
                    RenderSettings.ambientGroundColor = gradientSky.bottom.value * gradientSky.exposure.value;
                }

                var skyLightingMode = env.sky.value;
                if (skyLightingMode == SkyMode.None)
                    RenderSettings.skybox = null;
                else if (skyLightingMode == SkyMode.HDRISky)
                {
                    var hdriSky = stack.GetComponent<HDRISky>();
                    if (hdriSky.IsActive())
                    {
                        this.hdriMaterial.SetTexture(ShaderConstants._Tex, hdriSky.HdriSky.value);
                        this.hdriMaterial.SetFloat(ShaderConstants._Exposure, hdriSky.exposure.value);
                        this.hdriMaterial.SetFloat(ShaderConstants._Rotation, hdriSky.rotation.value);
                        this.hdriMaterial.SetColor(ShaderConstants._Tint, hdriSky.color.value);

                        RenderSettings.skybox = this.hdriMaterial;
                        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                    }
                    else
                    {
                        RenderSettings.skybox = null;
                    }
                }
            }
        }

        static class ShaderConstants
        {
            public static readonly int _Tex = Shader.PropertyToID("_Tex");
            public static readonly int _Tint = Shader.PropertyToID("_Tint");
            public static readonly int _Exposure = Shader.PropertyToID("_Exposure");
            public static readonly int _Rotation = Shader.PropertyToID("_Rotation");
        }
    }
}
