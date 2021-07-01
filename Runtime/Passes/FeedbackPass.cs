using Unity.Collections;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public sealed class FeedbackPass : ScriptableRenderPass
    {
        private int _width = 0;
        private int _height = 0;

        private Material _feedbackMaterial;
        private Material _drawLookupMat;

        private RenderTexture _feedbackTexture;
        private RenderTexture _maxFeedbackTexture;

        private ProfilingSampler _profilingSampler;

        private FilteringSettings _filteringSettings;

        private ShaderTagId _shaderTagId = new ShaderTagId("Feedback");

        private bool _isRequestComplete = true;
        private NativeArray<Color32> _feedbackData;

        private Vector3 _center = Vector3.zero;

        private VirtualTextureSystem _virtualTextureSystem = VirtualTextureSystem.instance;

        public FeedbackPass(RenderPassEvent renderPassEvent, LayerMask layerMask, Material feedbackMaterial, Material drawLookupMaterial)
        {
            this.renderPassEvent = renderPassEvent;

            _feedbackMaterial = feedbackMaterial;

            _drawLookupMat = drawLookupMaterial;
            _drawLookupMat.enableInstancing = true;

            _virtualTextureSystem.Init();
            _profilingSampler = new ProfilingSampler(ShaderConstants._ProfilerTag);
            _filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
        }

        public bool Setup(RenderTextureDescriptor baseDescriptor, int depthBufferBits = 32)
        {
            var width = Mathf.FloorToInt(baseDescriptor.width * _virtualTextureSystem.scale.ToFloat());
            var height = Mathf.FloorToInt(baseDescriptor.height * _virtualTextureSystem.scale.ToFloat());

            if (_width != width || _height != height)
            {
                if (_feedbackTexture)
                    RenderTexture.ReleaseTemporary(_feedbackTexture);

                if (_maxFeedbackTexture)
                    RenderTexture.ReleaseTemporary(_maxFeedbackTexture);

                _feedbackTexture = RenderTexture.GetTemporary(width, height, depthBufferBits, GraphicsFormat.R8G8B8A8_UNorm);
                _feedbackTexture.wrapMode = TextureWrapMode.Clamp;
                _feedbackTexture.filterMode = FilterMode.Point;

                _maxFeedbackTexture = RenderTexture.GetTemporary(width / 8, height / 8, 0, GraphicsFormat.R8G8B8A8_UNorm);
                _maxFeedbackTexture.wrapMode = TextureWrapMode.Clamp;
                _maxFeedbackTexture.filterMode = FilterMode.Point;

                _width = width;
                _height = height;
            }

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
                _virtualTextureSystem.UpdateJob();

                if (_isRequestComplete)
                {
                    _virtualTextureSystem.LoadPages(_feedbackData);
                    _virtualTextureSystem.UpdateLookup(_drawLookupMat);
                    _isRequestComplete = false;
                }

                if (!_isRequestComplete && _center != renderingData.cameraData.camera.transform.position)
				{
                    var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                    var drawSettings = CreateDrawingSettings(_shaderTagId, ref renderingData, sortFlags);
                    drawSettings.perObjectData = PerObjectData.None;

                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref _filteringSettings);

                    cmd.SetRenderTarget(_maxFeedbackTexture);
                    cmd.SetGlobalTexture(ShaderConstants._MainTex, _feedbackTexture);
                    cmd.DrawProcedural(Matrix4x4.identity, _feedbackMaterial, 0, MeshTopology.Triangles, 3);

                    cmd.RequestAsyncReadback(_maxFeedbackTexture, OnAsyncFeedbackRequest);

#if UNITY_EDITOR
                    cmd.WaitAllAsyncReadbackRequests();
#endif

                    _center = renderingData.cameraData.camera.transform.position;
                }
            }
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void OnAsyncFeedbackRequest(AsyncGPUReadbackRequest req)
        {
            _feedbackData = req.GetData<Color32>();
            _isRequestComplete = true;
        }

        static class ShaderConstants
        {
            public const string _ProfilerTag = "Feedback Pass";
            public const string _EnvironmentOcclusion = "_ENVIRONMENT_OCCLUSION";

            public static readonly int _MainTex = Shader.PropertyToID("_MainTex");
        }
    }
}