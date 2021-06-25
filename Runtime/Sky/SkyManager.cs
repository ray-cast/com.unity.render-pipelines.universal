using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.Rendering.Universal
{
	class SkyManager : IDisposable
    {
        /// <summary>
        /// List of look at matrices for cubemap faces.
        /// Ref: https://msdn.microsoft.com/en-us/library/windows/desktop/bb204881(v=vs.85).aspx
        /// </summary>
        static public readonly Vector3[] lookAtList =
        {
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(-1.0f, 0.0f, 0.0f),
            new Vector3(0.0f, -1.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 1.0f),
            new Vector3(0.0f, 0.0f, -1.0f),
        };

        /// <summary>
        /// List of up vectors for cubemap faces.
        /// Ref: https://msdn.microsoft.com/en-us/library/windows/desktop/bb204881(v=vs.85).aspx
        /// </summary>
        static public readonly Vector3[] upVectorList =
        {
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 1.0f),
            new Vector3(0.0f, 0.0f, -1.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
        };

        static readonly Lazy<SkyManager> s_Instance = new Lazy<SkyManager>(() => new SkyManager());

        public static SkyManager instance => s_Instance.Value;

        private Cubemap _cubemap;

        private Material _iblMaterial;
        private Material _hdriMaterial;

        public Cubemap standardSkyCubemap
        {
            get
            {
                if (_cubemap == null)
                    _cubemap = new Cubemap(128, Experimental.Rendering.DefaultFormat.HDR, Experimental.Rendering.TextureCreationFlags.None);

                return _cubemap;
            }
        }

        public Material standardSkyboxMaterial
        {
            get
            {
                return _hdriMaterial;
            }
        }

        public void Build(ClusterBasedDeferredRendererData.ShaderResources defaultResources)
		{
            this._cubemap = new Cubemap(128, Experimental.Rendering.DefaultFormat.HDR, Experimental.Rendering.TextureCreationFlags.None);
            this._iblMaterial = CoreUtils.CreateEngineMaterial(defaultResources.imageBasedLightingPS);
            this._hdriMaterial = CoreUtils.CreateEngineMaterial(defaultResources.skyboxCubemapPS);
        }

        internal void SetupAmbientProbe(Camera camera)
		{
		}

        internal void RenderToCubemap(CommandBuffer cmd, ref Material material, int pass, ref CameraData cameraData)
        {
            var faceDescriptor = new RenderTextureDescriptor(_cubemap.width, _cubemap.width, RenderTextureFormat.ARGBHalf);
            var faceTexture = RenderTexture.GetTemporary(faceDescriptor);
            faceTexture.filterMode = FilterMode.Trilinear;
            faceTexture.wrapMode = TextureWrapMode.Repeat;

            for (int i = 0; i < 6; i++)
            {
                var lookAt = Matrix4x4.LookAt(Vector3.zero, lookAtList[i], upVectorList[i]);
                var projectionMatrix = Matrix4x4.Perspective(90, 1.0f, 0.01f, cameraData.camera.farClipPlane);
                projectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, cameraData.IsCameraProjectionMatrixFlipped());

                RenderingUtils.SetViewAndProjectionMatrices(cmd, lookAt, projectionMatrix, true);

                cmd.SetRenderTarget(faceTexture);
                cmd.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3);
                cmd.CopyTexture(faceTexture, 0, 0, _cubemap, i, 0);
            }

            RenderTexture.ReleaseTemporary(faceTexture);
            RenderingUtils.SetViewAndProjectionMatrices(cmd, cameraData.GetViewMatrix(), cameraData.GetGPUProjectionMatrix(), true);
        }

        internal void RenderImageBasedLighting(CommandBuffer cmd, Cubemap cubemap, RenderTexture texture)
		{
            _iblMaterial.SetTexture(ShaderConstants._Cubemap, cubemap);

            cmd.SetRenderTarget(texture);
            cmd.DrawProcedural(Matrix4x4.identity, _iblMaterial, 0, MeshTopology.Triangles, 3);
            cmd.GenerateMips(texture);
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
                    RenderSettings.skybox = null;
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

                if (env.sky.value == SkyMode.HDRISky)
                {
                    var hdriSky = stack.GetComponent<HDRISky>();
                    if (hdriSky.IsActive())
					{
                        this.standardSkyboxMaterial.SetTexture(ShaderConstants._Tex, hdriSky.HdriSky.value);
                        this.standardSkyboxMaterial.SetFloat(ShaderConstants._Exposure, hdriSky.exposure.value);
                        this.standardSkyboxMaterial.SetFloat(ShaderConstants._Rotation, hdriSky.rotation.value);
                        this.standardSkyboxMaterial.SetColor(ShaderConstants._Tint, hdriSky.color.value);

                        RenderSettings.skybox = this.standardSkyboxMaterial;
                        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                    }
                    else
					{
                        RenderSettings.skybox = null;
                    }
                }
                else if (env.sky.value == SkyMode.GradientSky)
				{
                    var gradientSky = stack.GetComponent<GradientSky>();
                    if (gradientSky.IsActive())
                    {
                        this.standardSkyboxMaterial.SetTexture(ShaderConstants._Tex, _cubemap);
                        this.standardSkyboxMaterial.SetFloat(ShaderConstants._Exposure, 1);
                        this.standardSkyboxMaterial.SetFloat(ShaderConstants._Rotation, 0);
                        this.standardSkyboxMaterial.SetColor(ShaderConstants._Tint, Color.white);

                        RenderSettings.skybox = this.standardSkyboxMaterial;
                        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                    }
                    else
					{
                        RenderSettings.skybox = null;
                    }                        
                }
                else
                {
                    RenderSettings.skybox = null;
                }
            }
        }

		public void Dispose()
		{
			
		}

		static class ShaderConstants
        {
            public static readonly int _Tex = Shader.PropertyToID("_Tex");
            public static readonly int _Tint = Shader.PropertyToID("_Tint");
            public static readonly int _Exposure = Shader.PropertyToID("_Exposure");
            public static readonly int _Rotation = Shader.PropertyToID("_Rotation");
            public static readonly int _Cubemap = Shader.PropertyToID("_Cubemap");
        }
    }
}
