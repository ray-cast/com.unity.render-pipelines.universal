namespace UnityEngine.Rendering.Universal
{
    public class DrawClusterPass : ScriptableRenderPass
    {
        private int _clusterNums;
        private bool _requireMainCamera;
        private Material _debugCluster;
        private ProfilingSampler _profilingSampler;

        public DrawClusterPass(RenderPassEvent evt, Material debugCluster)
        {
            this.renderPassEvent = evt;

            this._debugCluster = debugCluster;
            this._requireMainCamera = false;
            this._profilingSampler = new ProfilingSampler(ShaderConstants._profilerTag);
        }

        public void Setup(int clusterCount, bool requireMainCamera = false)
        {
            this._clusterNums = clusterCount;
            this._requireMainCamera = requireMainCamera;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._profilerTag);

            using (new ProfilingScope(cmd, _profilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);

                var transform = this._requireMainCamera ? Camera.main.transform.localToWorldMatrix : renderingData.cameraData.camera.transform.localToWorldMatrix;

                cmd.Clear();
                cmd.DrawProcedural(transform, _debugCluster, 0, MeshTopology.Points, this._clusterNums);
            }

            context.ExecuteCommandBuffer(cmd);

            CommandBufferPool.Release(cmd);
        }

        static class ShaderConstants
        {
            public const string _profilerTag = "Debug Cluster";
        }
    }
}