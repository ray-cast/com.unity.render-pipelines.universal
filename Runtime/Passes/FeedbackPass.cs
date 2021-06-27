using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public sealed class FeedbackPass : ScriptableRenderPass
    {
        private int _mipmapBias = 0;
        private int m_TileSize = 256;

        private ScaleFactor m_Scale = ScaleFactor.Half;

        private RenderTexture _feedbackTexture;
        private RenderTexture _maxFeedbackTexture;

        private ProfilingSampler _profilingSampler;

        private FilteringSettings _filteringSettings;

        private PageTable _pageTable;

        private Material _virtualTextureMaterial;

        private ShaderTagId _shaderTagId = new ShaderTagId("Feedback");

        public FeedbackPass(RenderPassEvent renderPassEvent, LayerMask layerMask, Material virtualTextureMaterial)
        {
            this.renderPassEvent = renderPassEvent;

            _feedbackTexture = RenderTexture.GetTemporary(512, 512, 32, GraphicsFormat.R8G8B8A8_UNorm);
            _feedbackTexture.wrapMode = TextureWrapMode.Clamp;
            _feedbackTexture.filterMode = FilterMode.Point;

            _maxFeedbackTexture = RenderTexture.GetTemporary(64, 64, 0, GraphicsFormat.R8G8B8A8_UNorm);
            _maxFeedbackTexture.wrapMode = TextureWrapMode.Clamp;
            _maxFeedbackTexture.filterMode = FilterMode.Point;

            _pageTable = new PageTable();
            _pageTable.ActivatePage(0, 0, _pageTable.maxMipLevel);

            _virtualTextureMaterial = virtualTextureMaterial;
            _profilingSampler = new ProfilingSampler(ShaderConstants._ProfilerTag);
            _filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
        }

        public bool Setup(RenderTextureDescriptor baseDescriptor, int depthBufferBits = 32)
        {
            return true;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureTarget(_feedbackTexture);
            ConfigureClear(ClearFlag.All, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._ProfilerTag);

            using (new ProfilingScope(cmd, _profilingSampler))
            {
                cmd.SetGlobalVector("_VTFeedbackParam", new Vector4(_pageTable.tableSize, _pageTable.tableSize * m_TileSize * m_Scale.ToFloat(), _pageTable.maxMipLevel - 1, _mipmapBias));

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(_shaderTagId, ref renderingData, sortFlags);
                drawSettings.perObjectData = PerObjectData.None;

                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref _filteringSettings);

                cmd.SetRenderTarget(_maxFeedbackTexture);
                cmd.SetGlobalTexture(ShaderConstants._MainTex, _feedbackTexture);
                cmd.DrawProcedural(Matrix4x4.identity, _virtualTextureMaterial, 0, MeshTopology.Triangles, 3);

                cmd.RequestAsyncReadback(_maxFeedbackTexture, 0, OnAsyncFeedbackRequest);
            }
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void OnAsyncFeedbackRequest(AsyncGPUReadbackRequest req)
        {
            //_pageTable.ProcessFeedback(req.GetData<Color32>());
            _pageTable._renderTextureJob.Update();
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
        }

        static class ShaderConstants
        {
            public const string _ProfilerTag = "Feedback Pass";
            public const string _EnvironmentOcclusion = "_ENVIRONMENT_OCCLUSION";

            public static readonly int _MainTex = Shader.PropertyToID("_MainTex");
        }
    }
}