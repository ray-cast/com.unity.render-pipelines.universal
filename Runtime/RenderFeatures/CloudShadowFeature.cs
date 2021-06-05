namespace UnityEngine.Rendering.Universal
{
    public sealed class CloudShadowFeature : ScriptableRendererFeature
    {
        CloudShadowRenderPass scriptablePass;

        public sealed class CloudShadowRenderPass : ScriptableRenderPass
        {
            CloudShadow _cloudShadow;

            public CloudShadowRenderPass()
            {
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get(ShaderConstants._renderTag);

                this._cloudShadow = VolumeManager.instance.stack.GetComponent<CloudShadow>();
                if (_cloudShadow.IsActive())
				{
                    var tiling = _cloudShadow.tiling.value;
                    cmd.EnableShaderKeyword("_MAIN_LIGHT_CLOUD_SHADOWS");
                    cmd.SetGlobalVector(ShaderConstants._CloudParams1, new Vector4(tiling.x, tiling.y, _cloudShadow.speed.value, _cloudShadow.strength.value));
                    cmd.SetGlobalTexture(ShaderConstants._CloudShadowMap, _cloudShadow.shadow.value);
                }
                else
				{
                    cmd.DisableShaderKeyword("_MAIN_LIGHT_CLOUD_SHADOWS");
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            static class ShaderConstants
            {
                public const string _renderTag = "Cloud Shadow Setting";

                public static readonly int _CloudParams1 = Shader.PropertyToID("_CloudParams1");
                public static readonly int _CloudShadowMap = Shader.PropertyToID("_CloudShadowMap");
            }
        }

        public override void Create()
        {
            scriptablePass = new CloudShadowRenderPass();
            scriptablePass.renderPassEvent = RenderPassEvent.BeforeRendering;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.postProcessEnabled)
                renderer.EnqueuePass(scriptablePass);
        }
    }
}