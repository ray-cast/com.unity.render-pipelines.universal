using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    public class DrawSkyboxPass : ScriptableRenderPass
    {
        private Material _hdriMaterial = null;
        private Material _gradientMaterial = null;

        private RenderTargetHandle _depthAttachmentHandle { get; set; }
        private RenderTargetHandle _colorAttachmentHandle { get; set; }

        private GradientSky _gradientSky;
        private static Dictionary<Texture, RenderTexture> _skyTextures = new Dictionary<Texture, RenderTexture>();

        public DrawSkyboxPass(RenderPassEvent evt, ClusterBasedDeferredRendererData.ShaderResources defaultResources)
        {
            renderPassEvent = evt;

            foreach (var texture in _skyTextures)
                RenderTexture.ReleaseTemporary(texture.Value);

            _skyTextures.Clear();
            _gradientSky = ScriptableObject.CreateInstance<GradientSky>();

            if (defaultResources != null)
			{
                _hdriMaterial = CoreUtils.CreateEngineMaterial(defaultResources.hdriSkyPS);
                _gradientMaterial = CoreUtils.CreateEngineMaterial(defaultResources.gradientSkyPS);
            }
            else
			{
                _hdriMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Universal Render Pipeline/Sky/HDRi Sky"));
                _gradientMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Universal Render Pipeline/Sky/GradientSky"));
            }
        }

        public void Setup(RenderTargetHandle colorAttachmentHandle, RenderTargetHandle depthAttachmentHandle)
        {
            this._depthAttachmentHandle = depthAttachmentHandle;
            this._colorAttachmentHandle = colorAttachmentHandle;
        }


        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureTarget(_colorAttachmentHandle.Identifier(), _depthAttachmentHandle.Identifier());
            ConfigureClear(ClearFlag.None, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ref CameraData cameraData = ref renderingData.cameraData;
            
            VolumeStack stack = VolumeManager.instance.stack;

            var env = stack.GetComponent<VisualEnvironment>();
            if (env.IsActive())
			{
                var skyLightingMode = env.sky.value;
                if (skyLightingMode == SkyMode.HDRISky)
                {
                    var hdriSky = stack.GetComponent<HDRISky>();
                    if (hdriSky.IsActive())
                    {
                        var phi = -Mathf.Deg2Rad * hdriSky.rotation.value;
                        var color = hdriSky.color.value;

                        this._hdriMaterial.SetColor(ShaderConstants._Tint, color);
                        this._hdriMaterial.SetTexture(ShaderConstants._Cubemap, hdriSky.HdriSky.value);
                        this._hdriMaterial.SetVector(ShaderConstants._SkyParam, new Vector4(hdriSky.exposure.value * 2, 0, Mathf.Cos(phi), Mathf.Sin(phi)));

                        CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._renderTag);
                        cmd.Clear();
                        cmd.DrawProcedural(Matrix4x4.identity, this._hdriMaterial, 0, MeshTopology.Triangles, 3);

                        if (!_skyTextures.TryGetValue(hdriSky.HdriSky.value, out var skyTexture))
						{
                            var descriptor = new RenderTextureDescriptor(512, 256, RenderTextureFormat.ARGBHalf)
                            {
                                useMipMap = true,
                                mipCount = 6,
                                autoGenerateMips = false,
                            };

                            skyTexture = RenderTexture.GetTemporary(descriptor);
                            skyTexture.filterMode = FilterMode.Trilinear;
                            skyTexture.wrapMode = TextureWrapMode.Repeat;

                            SkyManager.instance.RenderImageBasedLighting(cmd, hdriSky.HdriSky.value, skyTexture);

                            cmd.SetRenderTarget(this.colorAttachment, this.depthAttachment);

                            _skyTextures.Add(hdriSky.HdriSky.value, skyTexture);
                        }

                        var skyMipParams = new Vector4(
                            color.r * hdriSky.exposure.value * 2,
                            color.g * hdriSky.exposure.value * 2,
                            color.b * hdriSky.exposure.value * 2,
                            hdriSky.rotation.value * Mathf.Deg2Rad
                        );

                        cmd.SetGlobalTexture(ShaderConstants._SkyMipTexture, skyTexture);
                        cmd.SetGlobalVector(ShaderConstants._SkyMipParams, skyMipParams);

                        context.ExecuteCommandBuffer(cmd);
                        CommandBufferPool.Release(cmd);
                    }
                    else
                    {
                        CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._renderTag);
                        cmd.SetGlobalTexture(ShaderConstants._SkyMipTexture, Texture2D.blackTexture);
                        cmd.SetGlobalVector(ShaderConstants._SkyMipParams, new Vector4(1, 1, 1, 0));

                        context.ExecuteCommandBuffer(cmd);
                        CommandBufferPool.Release(cmd);
                    }
                }
                else if (skyLightingMode == SkyMode.GradientSky)
                {
                    var shouldUpdateTexture = false;
                    var cubemap = SkyManager.instance.standardSkyCubemap;

                    if (!_skyTextures.TryGetValue(cubemap, out var skyTexture))
                    {
                        var descriptor = new RenderTextureDescriptor(512, 256, RenderTextureFormat.ARGBHalf)
                        {
                            useMipMap = true,
                            mipCount = 6,
                            autoGenerateMips = false
                        };

                        skyTexture = RenderTexture.GetTemporary(descriptor);
                        skyTexture.filterMode = FilterMode.Trilinear;
                        skyTexture.wrapMode = TextureWrapMode.Repeat;

                        _skyTextures.Add(cubemap, skyTexture);

                        shouldUpdateTexture = true;
                    }

                    var gradientSky = stack.GetComponent<GradientSky>();
                    if (_gradientSky.top.value != gradientSky.top.value ||
                        _gradientSky.middle.value != gradientSky.middle.value ||
                        _gradientSky.bottom.value != gradientSky.bottom.value ||
                        _gradientSky.gradientDiffusion.value != gradientSky.gradientDiffusion.value ||
                        _gradientSky.exposure.value != gradientSky.exposure.value || shouldUpdateTexture)
                    {
                        _gradientSky.top.value = gradientSky.top.value;
                        _gradientSky.middle.value = gradientSky.middle.value;
                        _gradientSky.bottom.value = gradientSky.bottom.value;
                        _gradientSky.gradientDiffusion.value = gradientSky.gradientDiffusion.value;
                        _gradientSky.exposure.value = gradientSky.exposure.value;

                        _gradientMaterial.SetColor(ShaderConstants._GradientTop, gradientSky.top.value);
                        _gradientMaterial.SetColor(ShaderConstants._GradientMiddle, gradientSky.middle.value);
                        _gradientMaterial.SetColor(ShaderConstants._GradientBottom, gradientSky.bottom.value);
                        _gradientMaterial.SetFloat(ShaderConstants._GradientDiffusion, gradientSky.gradientDiffusion.value);
                        _gradientMaterial.SetFloat(ShaderConstants._SkyIntensity, gradientSky.exposure.value);

                        CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._bakeTag);

                        SkyManager.instance.RenderToCubemap(cmd, ref _gradientMaterial, 1, ref cameraData);
                        SkyManager.instance.RenderImageBasedLighting(cmd, cubemap, skyTexture);

                        cmd.SetRenderTarget(this.colorAttachment, this.depthAttachment);
                        cmd.SetGlobalTexture(ShaderConstants._SkyMipTexture, skyTexture);
                        cmd.SetGlobalVector(ShaderConstants._SkyMipParams, new Vector4(1, 1, 1, 0));

                        context.ExecuteCommandBuffer(cmd);
                        CommandBufferPool.Release(cmd);
                    }

                    if (gradientSky.IsActive())
                    {
                        _gradientMaterial.SetColor(ShaderConstants._GradientTop, gradientSky.top.value);
                        _gradientMaterial.SetColor(ShaderConstants._GradientMiddle, gradientSky.middle.value);
                        _gradientMaterial.SetColor(ShaderConstants._GradientBottom, gradientSky.bottom.value);
                        _gradientMaterial.SetFloat(ShaderConstants._GradientDiffusion, gradientSky.gradientDiffusion.value);
                        _gradientMaterial.SetFloat(ShaderConstants._SkyIntensity, gradientSky.exposure.value);

                        CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._renderTag);
                        cmd.SetGlobalTexture(ShaderConstants._SkyMipTexture, skyTexture);
                        cmd.SetGlobalVector(ShaderConstants._SkyMipParams, new Vector4(1, 1, 1, 0));
                        cmd.DrawProcedural(Matrix4x4.identity, _gradientMaterial, 0, MeshTopology.Triangles, 3);

                        context.ExecuteCommandBuffer(cmd);
                        CommandBufferPool.Release(cmd);
                    }
                    else
                    {
                        CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._renderTag);
                        cmd.SetGlobalTexture(ShaderConstants._SkyMipTexture, Texture2D.blackTexture);
                        cmd.SetGlobalVector(ShaderConstants._SkyMipParams, new Vector4(1, 1, 1, 0));

                        context.ExecuteCommandBuffer(cmd);
                        CommandBufferPool.Release(cmd);
                    }
                }
            }
            else
			{
                bool isRequireSkybox = RenderSettings.skybox != null;
                if (!isRequireSkybox)
                {
                    if (cameraData.camera.TryGetComponent<Skybox>(out var cameraSkybox))
                        isRequireSkybox |= cameraSkybox.material != null;
                }

                if (isRequireSkybox)
                    context.DrawSkybox(renderingData.cameraData.camera);
            }
        }

        static class ShaderConstants
        {
            public const string _bakeTag = "Bake Skybox";
            public const string _renderTag = "Draw Skybox";

            public static readonly int _Tex = Shader.PropertyToID("_Tex");
            public static readonly int _Tint = Shader.PropertyToID("_Tint");
            public static readonly int _Exposure = Shader.PropertyToID("_Exposure");
            public static readonly int _Rotation = Shader.PropertyToID("_Rotation");
            public static readonly int _Cubemap = Shader.PropertyToID("_Cubemap");

            public static readonly int _SkyParam = Shader.PropertyToID("_SkyParam");
            public static readonly int _SkyIntensity = Shader.PropertyToID("_SkyIntensity");
            public static readonly int _SkyMipTexture = Shader.PropertyToID("_SkyMipTexture");
            public static readonly int _SkyMipParams = Shader.PropertyToID("_SkyMipParams");

            public static readonly int _GradientBottom = Shader.PropertyToID("_GradientBottom");
            public static readonly int _GradientMiddle = Shader.PropertyToID("_GradientMiddle");
            public static readonly int _GradientTop = Shader.PropertyToID("_GradientTop");
            public static readonly int _GradientDiffusion = Shader.PropertyToID("_GradientDiffusion");
        }
    }
}