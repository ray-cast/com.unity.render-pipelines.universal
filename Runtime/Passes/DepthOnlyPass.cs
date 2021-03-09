using System;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Render all objects that have a 'DepthOnly' pass into the given depth buffer.
    ///
    /// You can use this pass to prime a depth buffer for subsequent rendering.
    /// Use it as a z-prepass, or use it to generate a depth buffer.
    /// </summary>
    public class DepthOnlyPass : ScriptableRenderPass
    {
        int kDepthBufferBits = 32;

        private RenderTargetHandle depthAttachmentHandle { get; set; }

        FilteringSettings _filteringSettings;
        const string _profilerTag = "Depth Prepass";
        ProfilingSampler _profilingSampler = new ProfilingSampler(_profilerTag);
        ShaderTagId _shaderTagId = new ShaderTagId("DepthOnly");

        public DepthOnlyPass(RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask)
        {
            _filteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            renderPassEvent = evt;
        }

        public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle depthAttachmentHandle)
        {
            this.depthAttachmentHandle = depthAttachmentHandle;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureTarget(depthAttachmentHandle.Identifier());
            ConfigureClear(ClearFlag.All, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);
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

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");

            if (depthAttachmentHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(depthAttachmentHandle.id);
                depthAttachmentHandle = RenderTargetHandle.CameraTarget;
            }
        }
    }
}