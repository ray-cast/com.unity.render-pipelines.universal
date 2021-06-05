namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Render all objects that have a 'PrepassDepth' pass into the given depth buffer.
    ///
    /// You can use this pass to prime a depth buffer for subsequent rendering.
    /// Use it as a z-prepass
    /// </summary>
    public class TransparentDepthPrepass : ScriptableRenderPass
    {
        private RenderTargetHandle depthAttachmentHandle { get; set; }

        ShaderTagId _shaderTagId;
        FilteringSettings _filteringSettings;
        ProfilingSampler _profilingSampler;

        public TransparentDepthPrepass(RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask)
        {
            renderPassEvent = evt;

            _shaderTagId = new ShaderTagId("TransparentDepthPrepass");
            _profilingSampler = new ProfilingSampler(ShaderConstants._profilerTag);
            _filteringSettings = new FilteringSettings(renderQueueRange, layerMask);
        }

        public void Setup(RenderTargetHandle depthAttachmentHandle)
        {
            this.depthAttachmentHandle = depthAttachmentHandle;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureTarget(depthAttachmentHandle.Identifier());
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._profilerTag);

            using (new ProfilingScope(cmd, _profilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(_shaderTagId, ref renderingData, sortFlags);
                drawSettings.perObjectData = PerObjectData.None;

                ref CameraData cameraData = ref renderingData.cameraData;
                Camera camera = cameraData.camera;
                if (cameraData.isStereoEnabled)
                {
                    context.StartMultiEye(camera, eyeIndex);
                }

                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref _filteringSettings);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        static class ShaderConstants
        {
            public const string _profilerTag = "Transparent Depth prepass";
        }
    }
}