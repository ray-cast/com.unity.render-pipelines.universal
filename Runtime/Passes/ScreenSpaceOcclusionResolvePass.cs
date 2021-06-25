using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
	public sealed class ScreenSpaceOcclusionResolvePass : ScriptableRenderPass
    {
        private Material _screenSpaceOcclusionMaterial;

        private RenderTargetHandle _screenSpaceOcclusionMap;
        private RenderTargetHandle _screenSpaceOcclusionTempMap;

        private RenderTextureDescriptor _renderTextureDescriptor;
        private RenderTargetHandle _depthAttachmentHandle { get; set; }

        private ScreenSpaceAmbientOcclusion _ambientOcclusion;

        public ScreenSpaceOcclusionResolvePass(RenderPassEvent evt, Material capsuleOcclusionMaterial)
        {
            renderPassEvent = evt;

            _screenSpaceOcclusionMaterial = capsuleOcclusionMaterial;
            _screenSpaceOcclusionMap.Init("_ScreenSpaceOcclusionTexture");
            _screenSpaceOcclusionTempMap.Init("_ScreenSpaceOcclusionTempTexture");
        }

        public bool Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle depthAttachmentHandle)
        {
#if UNITY_EDITOR
            if (_screenSpaceOcclusionMaterial == null)
            {
                Debug.LogErrorFormat("Missing {0}. {1} render pass will not execute. Check for missing reference in the renderer resources.", _screenSpaceOcclusionMaterial, GetType().Name);
                return false;
            }
#endif

            VolumeStack stack = VolumeManager.instance.stack;

            _ambientOcclusion = stack.GetComponent<ScreenSpaceAmbientOcclusion>();
            if (_ambientOcclusion.IsActive())
			{
                _depthAttachmentHandle = depthAttachmentHandle;

                _renderTextureDescriptor = baseDescriptor;
                _renderTextureDescriptor.depthBufferBits = 0;
                _renderTextureDescriptor.msaaSamples = 1;

                if (!_ambientOcclusion.shouledFullRes)
				{
                    _renderTextureDescriptor.width = _renderTextureDescriptor.width >> 1;
                    _renderTextureDescriptor.height = _renderTextureDescriptor.height >> 1;
                }

                if (RenderingUtils.SupportsGraphicsFormat(GraphicsFormat.R8_UNorm, FormatUsage.Linear | FormatUsage.Render))
                    _renderTextureDescriptor.graphicsFormat = GraphicsFormat.R8_UNorm;
                else
                    _renderTextureDescriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;

                return true;
            }
            else
			{
                Shader.DisableKeyword(ShaderConstants._EnvironmentOcclusion);
                return false;
			}
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            cmd.GetTemporaryRT(_screenSpaceOcclusionMap.id, _renderTextureDescriptor, FilterMode.Point);
            cmd.GetTemporaryRT(_screenSpaceOcclusionTempMap.id, _renderTextureDescriptor, FilterMode.Point);

            ConfigureTarget(_screenSpaceOcclusionMap.Identifier(), _ambientOcclusion.shouledFullRes ? _depthAttachmentHandle.Identifier() : _screenSpaceOcclusionMap.Identifier());
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._ProfilerTag);

            var radius = _ambientOcclusion.shouledFullRes ? _ambientOcclusion.radius.value : _ambientOcclusion.radius.value * 0.5f;

            cmd.EnableShaderKeyword(ShaderConstants._EnvironmentOcclusion);
            cmd.SetGlobalVector(ShaderConstants._SSDO_SampleParams, new Vector4(_renderTextureDescriptor.width, _renderTextureDescriptor.height, radius, _ambientOcclusion.strength.value));
            cmd.SetGlobalVector(ShaderConstants._SSDO_SampleParams2, new Vector4(_ambientOcclusion.bias.value, 0, 0, 0));
            cmd.DrawProcedural(Matrix4x4.identity, _screenSpaceOcclusionMaterial, 0, MeshTopology.Triangles, 3);

            var sharpness = _ambientOcclusion.sharpness.value;
            if (sharpness > 0.0f)
			{
                cmd.SetRenderTarget(_screenSpaceOcclusionTempMap.Identifier());
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetGlobalTexture(ShaderConstants._SSDO_Texture, _screenSpaceOcclusionMap.Identifier());
                cmd.SetGlobalVector(ShaderConstants._SSDO_BlurParams, new Vector4(1.0f / _renderTextureDescriptor.width, 0, _ambientOcclusion.sharpness.value, 0));
                cmd.DrawProcedural(Matrix4x4.identity, _screenSpaceOcclusionMaterial, 1, MeshTopology.Triangles, 3);

                cmd.SetRenderTarget(_screenSpaceOcclusionMap.Identifier());
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetGlobalTexture(ShaderConstants._SSDO_Texture, _screenSpaceOcclusionTempMap.Identifier());
                cmd.SetGlobalVector(ShaderConstants._SSDO_BlurParams, new Vector4(0, 1.0f / _renderTextureDescriptor.height, _ambientOcclusion.sharpness.value, 0));
                cmd.DrawProcedural(Matrix4x4.identity, _screenSpaceOcclusionMaterial, 1, MeshTopology.Triangles, 3);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_screenSpaceOcclusionMap.id);
            cmd.ReleaseTemporaryRT(_screenSpaceOcclusionTempMap.id);
        }

        static class ShaderConstants
        {
            public const string _ProfilerTag = "Resolve Occlusion";
            public const string _EnvironmentOcclusion = "_ENVIRONMENT_OCCLUSION";

            public static readonly int _SSDO_Texture = Shader.PropertyToID("_MainTex");
            public static readonly int _SSDO_BlurParams = Shader.PropertyToID("_SSDO_BlurParams");
            public static readonly int _SSDO_SampleParams = Shader.PropertyToID("_SSDO_SampleParams");
            public static readonly int _SSDO_SampleParams2 = Shader.PropertyToID("_SSDO_SampleParams2");

            public static readonly int _AdditionalOccludersCount = Shader.PropertyToID("_AdditionalOccludersCount");
            public static readonly int _AdditionalOccluderPosition = Shader.PropertyToID("_AdditionalOccluderPosition");
        }
    }
}