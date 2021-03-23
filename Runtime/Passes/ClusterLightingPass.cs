namespace UnityEngine.Rendering.Universal
{
    public class ClusterLightingPass : ScriptableRenderPass
    {
        private Material _lightingMaterial;

        private RenderTargetHandle _colorAttachmentHandle { get; set; }

        private RenderTextureDescriptor _descriptor { get; set; }

        public ClusterLightingPass(RenderPassEvent evt, Material lightingMaterial)
        {
            this.renderPassEvent = evt;
            this._lightingMaterial = lightingMaterial;
        }

        public void Setup(RenderTextureDescriptor cameraTextureDescriptor, RenderTargetHandle colorAttachmentHandle)
        {
            this._descriptor = cameraTextureDescriptor;
            this._colorAttachmentHandle = colorAttachmentHandle;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureTarget(_colorAttachmentHandle.Identifier());
            ConfigureClear(ClearFlag.None, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._profilerTag);

            var flipSign = renderingData.cameraData.IsCameraProjectionMatrixFlipped() ? -1.0f : 1.0f;
            var scaleBias = flipSign < 0.0f ? new Vector4(flipSign, 1.0f, -1.0f, 1.0f) : new Vector4(flipSign, 0.0f, 1.0f, 1.0f);

            cmd.Clear();
            cmd.ClearRenderTarget(false, true, new Color(0, 0, 0, 0));
            cmd.SetGlobalVector(ShaderConstants._scaleBiasId, scaleBias);

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

            public static readonly int _scaleBiasId = Shader.PropertyToID("_ScaleBiasRT");
        }
    }
}