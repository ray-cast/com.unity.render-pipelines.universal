namespace UnityEngine.Rendering.Universal
{
    public class ClusterLightingPass : ScriptableRenderPass
    {
        private Material _lightingMaterial;

        private RenderTargetHandle _depthAttachmentHandle { get; set; }
        private RenderTargetHandle _colorAttachmentHandle { get; set; }

        public ClusterLightingPass(RenderPassEvent evt, Material lightingMaterial)
        {
            this.renderPassEvent = evt;
            this._lightingMaterial = lightingMaterial;
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
            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._profilerTag);

            cmd.Clear();
            cmd.ClearRenderTarget(false, true, Color.clear);

            if (renderingData.lightData.mainLightIndex >= 0)
                cmd.DrawProcedural(Matrix4x4.identity, _lightingMaterial, 0, MeshTopology.Triangles, 3);
            else
                cmd.DrawProcedural(Matrix4x4.identity, _lightingMaterial, 1, MeshTopology.Triangles, 3);

            context.ExecuteCommandBuffer(cmd);

            CommandBufferPool.Release(cmd);
        }

        static class ShaderConstants
        {
            public const string _profilerTag = "Cluster Lighting";
        }
    }
}