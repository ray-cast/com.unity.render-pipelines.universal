namespace UnityEngine.Rendering.Universal
{
    public class SceneViewDepthCopyPass : ScriptableRenderPass
    {
        private Material _copyDepthMaterial;
        private RenderTargetHandle source { get; set; }

        public SceneViewDepthCopyPass(RenderPassEvent evt, Material copyDepthMaterial)
        {
            _copyDepthMaterial = copyDepthMaterial;
            renderPassEvent = evt;
        }

        public void Setup(RenderTargetHandle source)
        {
            this.source = source;
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_copyDepthMaterial == null)
            {
                Debug.LogErrorFormat("Missing {0}. {1} render pass will not execute. Check for missing reference in the renderer resources.", _copyDepthMaterial, GetType().Name);
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._profilerTag);
            CoreUtils.SetRenderTarget(cmd, BuiltinRenderTextureType.CameraTarget);

            var flipSign = (renderingData.cameraData.IsCameraProjectionMatrixFlipped()) ? -1.0f : 1.0f;
            var scaleBias = (flipSign < 0.0f) ? new Vector4(flipSign, 1.0f, -1.0f, 1.0f) : new Vector4(flipSign, 0.0f, 1.0f, 1.0f);

            cmd.SetGlobalVector(ShaderConstants._scaleBiasId, scaleBias);
            cmd.SetGlobalTexture("_CameraDepthAttachment", source.Identifier());

            cmd.EnableShaderKeyword(ShaderKeywordStrings.DepthNoMsaa);
            cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
            cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
            cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa8);

            cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, _copyDepthMaterial);

            context.ExecuteCommandBuffer(cmd);

            CommandBufferPool.Release(cmd);
        }

        static class ShaderConstants
        {
            public const string _profilerTag = "Copy Depth for Scene View";
            public static readonly int _scaleBiasId = Shader.PropertyToID("_ScaleBiasRT");
        }
    }
}