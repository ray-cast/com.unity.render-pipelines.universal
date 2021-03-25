﻿namespace UnityEngine.Rendering.Universal
{
    public sealed class MipFogFeature : ScriptableRendererFeature
    {
        MipFogRenderPass scriptablePass;

        public sealed class MipFogRenderPass : ScriptableRenderPass
        {
            MipFog _mipFog;
            HeightFog _heightFog;
            Material _fogMaterial;

            RenderTargetIdentifier _colorAttachment;
            RenderTargetIdentifier _depthAttachment;

            Material mipForMaterial
			{
                get
				{
                    if (_fogMaterial == null)
                        _fogMaterial = CoreUtils.CreateEngineMaterial("Hidden/Universal Render Pipeline/Fog/MipFog");

                    return _fogMaterial;
                }
			}

            public MipFogRenderPass()
            {
            }

            public bool Setup(RenderTargetIdentifier colorAttachment, RenderTargetIdentifier depthAttachment)
			{
                if (this.mipForMaterial == null)
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

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get(ShaderConstants._renderTag);

                if (_mipFog != null && _mipFog.IsActive())
				{
                    CoreUtils.SetKeyword(_fogMaterial, "_MIPFOG_MAP", _mipFog.skybox.value);

                    var color = _mipFog.color.value.linear;
                    _fogMaterial.EnableKeyword("_MIPFOG");
                    _fogMaterial.SetTexture(ShaderConstants._MipFogMap, _mipFog.skybox.value);
                    _fogMaterial.SetVector(ShaderConstants._MipFogParams, new Vector4(color.r, color.g, color.b, _mipFog.density.value));
                    _fogMaterial.SetVector(ShaderConstants._MipFogParams2, new Vector2(_mipFog.rotation.value * Mathf.Deg2Rad, 1 - _mipFog.skyDensity.value));
                }
                else
				{
                    _fogMaterial.DisableKeyword("_MIPFOG");
                }

                if (_heightFog != null && _heightFog.IsActive())
				{
                    CoreUtils.SetKeyword(_fogMaterial, "_HEIGHTFOG_CAMERA_HEIGHT", _heightFog.followCamera.value);

                    _fogMaterial.EnableKeyword("_HEIGHTFOG");
                    _fogMaterial.SetVector(ShaderConstants._HeightFogDeepColor, _heightFog.deepColor.value.linear);
                    _fogMaterial.SetVector(ShaderConstants._HeightFogShallowColor, _heightFog.shallowColor.value.linear);
                    _fogMaterial.SetVector(ShaderConstants._HeightFogParams, new Vector4(_heightFog.density.value, _heightFog.heightFalloff.value, _heightFog.height.value, 0));
                }
                else
                {
                    _fogMaterial.DisableKeyword("_HEIGHTFOG");
                }

                cmd.DrawProcedural(Matrix4x4.identity, _fogMaterial, 0, MeshTopology.Triangles, 3, 1);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            static class ShaderConstants
            {
                public const string _renderTag = "Fog Effects";

                public static readonly int _MipFogMap = Shader.PropertyToID("_MipFogMap");
                public static readonly int _MipFogParams = Shader.PropertyToID("_MipFogParams");
                public static readonly int _MipFogParams2 = Shader.PropertyToID("_MipFogParams2");

                public static readonly int _HeightFogParams = Shader.PropertyToID("_HeightFogParams");
                public static readonly int _HeightFogDeepColor = Shader.PropertyToID("_HeightFogDeepColor");
                public static readonly int _HeightFogShallowColor = Shader.PropertyToID("_HeightFogShallowColor");
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