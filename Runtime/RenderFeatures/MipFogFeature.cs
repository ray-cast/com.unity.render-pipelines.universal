namespace UnityEngine.Rendering.Universal
{
    public sealed class MipFogFeature : ScriptableRendererFeature
    {
        MipFogRenderPass scriptablePass;

        public sealed class MipFogRenderPass : ScriptableRenderPass
        {
            MipFog _mipFog;
            HeightFog _heightFog;

            Material _fogMaterial;
            Material _heightFogMaterial;

            RenderTargetIdentifier _colorAttachment;
            RenderTargetIdentifier _depthAttachment;

            Material mipFogMaterial
			{
                get
				{
                    if (_fogMaterial == null)
                        _fogMaterial = CoreUtils.CreateEngineMaterial("Hidden/Universal Render Pipeline/Fog/MipFog");

                    return _fogMaterial;
                }
			}

            Material heightFogMaterial
            {
                get
                {
                    if (_heightFogMaterial == null)
                        _heightFogMaterial = CoreUtils.CreateEngineMaterial("Hidden/Universal Render Pipeline/Fog/HeightFog");

                    return _heightFogMaterial;
                }
            }

            public MipFogRenderPass()
            {
            }

            public bool Setup(RenderTargetIdentifier colorAttachment, RenderTargetIdentifier depthAttachment)
			{
                if (this.mipFogMaterial == null)
                {
                    UnityEngine.Debug.LogError("材质没找到！");
                    return false;
                }

                if (this.heightFogMaterial == null)
                {
                    UnityEngine.Debug.LogError("材质没找到！");
                    return false;
                }

                this._mipFog = VolumeManager.instance.stack.GetComponent<MipFog>();
                this._heightFog = VolumeManager.instance.stack.GetComponent<HeightFog>(); 
                this._colorAttachment = colorAttachment;
                this._depthAttachment = depthAttachment;

                if (_mipFog != null && _mipFog.IsActive())
                    return true;

                if (_heightFog != null && _heightFog.IsActive())
                    return true;

                return false;
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                ConfigureTarget(_colorAttachment, _depthAttachment);
            }

            float ScaleHeightFromLayerDepth(float d)
            {
                // Exp[-d / H] = 0.001
                // -d / H = Log[0.001]
                // H = d / -Log[0.001]
                return d * 0.144765f;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get(ShaderConstants._renderTag);

                if (_mipFog != null && _mipFog.IsActive())
				{
                    _fogMaterial.shaderKeywords = null;

                    if (_mipFog.mode.value == MipFogMode.Linear)
                        _fogMaterial.EnableKeyword("_FOG_LINEAR");
                    else if (_mipFog.mode.value == MipFogMode.Exponential)
                        _fogMaterial.EnableKeyword("_FOG_EXP");
                    else if (_mipFog.mode.value == MipFogMode.ExponentialSquared)
                        _fogMaterial.EnableKeyword("_FOG_EXP2");

                    if (_mipFog.colorMode.value == FogColorMode.SkyColor)
                        _fogMaterial.EnableKeyword("_MIPFOG_MAP");

                    var color = _mipFog.colorMode.value == FogColorMode.SkyColor ? _mipFog.tint.value.linear : _mipFog.color.value.linear;
                    _fogMaterial.SetVector(ShaderConstants._MipFogParams, new Vector4(color.r, color.g, color.b, 0));

                    if (_mipFog.mode.value == MipFogMode.Linear)
					{
                        var end = _mipFog.end.value;
                        var start = _mipFog.start.value;
                        var z = (-1 / (end - start));
                        var w = (end / (end - start));

                        _fogMaterial.SetVector(ShaderConstants._MipFogFactorParams, new Vector4(_mipFog.density.value, 1 - _mipFog.skyDensity.value, z, w));
                    }
					else
					{
                        _fogMaterial.SetVector(ShaderConstants._MipFogFactorParams, new Vector4(_mipFog.density.value, 1 - _mipFog.skyDensity.value, 0, 0));
                    }

                    cmd.DrawProcedural(Matrix4x4.identity, _fogMaterial, 0, MeshTopology.Triangles, 3, 1);
                }

                if (_heightFog != null && _heightFog.IsActive())
				{
                    if (_heightFog.colorMode.value == FogColorMode.SkyColor)
                        _heightFogMaterial.EnableKeyword("_MIPFOG_MAP");
                    else
                        _heightFogMaterial.DisableKeyword("_MIPFOG_MAP");

                    var color = _heightFog.tint.value.linear;
                    var baseHeight = _heightFog.baseHeight.value;
                    var maximumHeight = _heightFog.maximumHeight.value;
                    if (_heightFog.relativeRendering.value)
					{
                        baseHeight += renderingData.cameraData.camera.transform.position.y;
                        maximumHeight += renderingData.cameraData.camera.transform.position.y;
                    }
                    else
					{
                        baseHeight -= renderingData.cameraData.camera.transform.position.y;
                        maximumHeight -= renderingData.cameraData.camera.transform.position.y;
                    }

                    var extinction = 1.0f / _heightFog.fogAttenuationDistance.value;
                    var layerDepth = Mathf.Max(0.01f, maximumHeight - baseHeight);
                    var H = ScaleHeightFromLayerDepth(layerDepth);
                    var heightFogExponents = new Vector2(1.0f / H, H);

                    _heightFogMaterial.SetVector(ShaderConstants._HeightFogColor, new Vector4(color.r, color.g, color.b, _heightFog.heightDensity.value));
                    _heightFogMaterial.SetVector(ShaderConstants._HeightFogParams, new Vector4(heightFogExponents.x, heightFogExponents.y, extinction, baseHeight));

                    cmd.DrawProcedural(Matrix4x4.identity, _heightFogMaterial, 0, MeshTopology.Triangles, 3, 1);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            static class ShaderConstants
            {
                public const string _renderTag = "Fog Effects";

                public static readonly int _MipFogMap = Shader.PropertyToID("_MipFogMap");
                public static readonly int _MipFogParams = Shader.PropertyToID("_MipFogParams");
                public static readonly int _MipFogFactorParams = Shader.PropertyToID("_MipFogFactorParams");

                public static readonly int _HeightFogParams = Shader.PropertyToID("_HeightFogParams");
                public static readonly int _HeightFogColor = Shader.PropertyToID("_HeightFogColor");
                public static readonly int _HeightFogBaseScattering = Shader.PropertyToID("_HeightFogBaseScattering");
            }
        }

        public override void Create()
        {
            scriptablePass = new MipFogRenderPass();
            scriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.postProcessEnabled)
            {
                if (scriptablePass.Setup(renderer.cameraColorTarget, renderer.cameraDepth))
                    renderer.EnqueuePass(scriptablePass);
            }
        }
    }
}