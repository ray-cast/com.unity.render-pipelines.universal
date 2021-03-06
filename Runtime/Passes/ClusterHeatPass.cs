namespace UnityEngine.Rendering.Universal
{
    public class ClusterHeatPass : ScriptableRenderPass
    {
        private Material _material;

        public ClusterHeatPass(RenderPassEvent evt, Material material)
        {
            this.renderPassEvent = evt;
            this._material = material;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._profilerTag);

            var flipSign = renderingData.cameraData.IsCameraProjectionMatrixFlipped() ? -1.0f : 1.0f;
            var scaleBias = flipSign < 0.0f ? new Vector4(flipSign, 1.0f, -1.0f, 1.0f) : new Vector4(flipSign, 0.0f, 1.0f, 1.0f);

            cmd.Clear();
            cmd.SetGlobalVector(ShaderConstants._scaleBiasId, scaleBias);
            cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, _material);

            context.ExecuteCommandBuffer(cmd);

            CommandBufferPool.Release(cmd);
        }

        static class ShaderConstants
        {
            public const string _profilerTag = "Cluster Heat Map";

            public static readonly int _scaleBiasId = Shader.PropertyToID("_ScaleBiasRT");
        }
    }
}