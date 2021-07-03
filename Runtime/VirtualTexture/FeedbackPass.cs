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

        private bool _isAsyncRequestComplete = true;
        private bool _shouldUpdateRegion = true;

        private NativeArray<Color32> _feedbackData;

        private Vector3 _center = Vector3.zero;
        private Quaternion _rotation = Quaternion.identity;

        private VirtualTexture _virtualTexture;
        private VirtualTextureSystem _virtualTextureSystem = VirtualTextureSystem.instance;

        public FeedbackPass(RenderPassEvent renderPassEvent, LayerMask layerMask, Material feedbackMaterial, Material drawLookupMaterial)
        {
            this.renderPassEvent = renderPassEvent;

            _feedbackMaterial = feedbackMaterial;

            _drawLookupMat = drawLookupMaterial;
            _drawLookupMat.enableInstancing = true;

            _virtualTextureSystem.Init();

            VirtualTextureSystem.resetPageTable += () =>
            {
                _shouldUpdateRegion = true;
            };

            _profilingSampler = new ProfilingSampler(ShaderConstants._ProfilerTag);
            _filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);

        #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (scene, mode) =>
            {
                _virtualTextureSystem.Reset();
            };
        #endif
        }

        public bool Setup(RenderTextureDescriptor baseDescriptor, int depthBufferBits = 32)
        {
            VolumeStack stack = VolumeManager.instance.stack;

            _virtualTexture = stack.GetComponent<VirtualTexture>();
            if (_virtualTexture.IsActive())
            {
                var center = _virtualTexture.center.value;
                var size = _virtualTexture.size.value;
                _virtualTextureSystem.SetRegion(new Rect(center.x - size / 2, center.y - size / 2, size, size));

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

            return false;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureTarget(_feedbackTexture);
            ConfigureClear(ClearFlag.All, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ref var camera = ref renderingData.cameraData.camera;
            if (camera.cameraType == CameraType.Preview || renderingData.cameraData.renderType == CameraRenderType.Overlay)
                return;

            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._ProfilerTag);

            using (new ProfilingScope(cmd, _profilingSampler))
            {
                if (_isAsyncRequestComplete)
                {
                    _virtualTextureSystem.LoadPages(_feedbackData);
                    _isAsyncRequestComplete = false;
                }

                if (_virtualTexture.regionAdaptation.value)
				{
                    if (_virtualTextureSystem.UpdateRegion(camera.transform.position))
                        _shouldUpdateRegion |= true;
                }

                if (!_isAsyncRequestComplete && (_center != camera.transform.position || _rotation != camera.transform.rotation || _shouldUpdateRegion))
				{
                    var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                    var drawSettings = CreateDrawingSettings(_shaderTagId, ref renderingData, sortFlags);
                    drawSettings.perObjectData = PerObjectData.None;

                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref _filteringSettings);

                    cmd.SetRenderTarget(_maxFeedbackTexture);
                    cmd.SetGlobalTexture(ShaderConstants._MainTex, _feedbackTexture);
                    cmd.DrawProcedural(Matrix4x4.identity, _feedbackMaterial, 0, MeshTopology.Triangles, 3);

                    cmd.RequestAsyncReadback(_maxFeedbackTexture, OnAsyncFeedbackRequest);

                    if (Application.isPlaying)
					{
                        if (_shouldUpdateRegion)
                        {
                            var fence = cmd.CreateGraphicsFence(GraphicsFenceType.CPUSynchronisation, SynchronisationStageFlags.PixelProcessing);
                            cmd.WaitOnAsyncGraphicsFence(fence);
                        }
                    }
                    else
                    {
#if UNITY_EDITOR
                        cmd.WaitAllAsyncReadbackRequests();
#endif
                    }

                    _center = camera.transform.position;
                    _rotation = camera.transform.rotation;
                    _shouldUpdateRegion = false;
                }

                _virtualTextureSystem.UpdateJob(cmd);
                _virtualTextureSystem.UpdateLookup(cmd, _drawLookupMat);
            }
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void OnAsyncFeedbackRequest(AsyncGPUReadbackRequest req)
        {
            _feedbackData = req.GetData<Color32>();
            _isAsyncRequestComplete = true;
        }

        static class ShaderConstants
        {
            public const string _ProfilerTag = "Feedback Pass";
            public const string _EnvironmentOcclusion = "_ENVIRONMENT_OCCLUSION";

            public static readonly int _MainTex = Shader.PropertyToID("_MainTex");
        }
    }
}