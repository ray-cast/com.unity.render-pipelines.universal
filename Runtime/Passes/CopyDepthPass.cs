using System;

namespace UnityEngine.Rendering.Universal
{
    public class CopyDepthPass : ScriptableRenderPass
    {
        private RenderTargetHandle source { get; set; }
        private RenderTargetHandle destination { get; set; }
        Material _copyDepthMaterial;
        const string _profilerTag = "Copy Depth";

        int _scaleBiasId = Shader.PropertyToID("_ScaleBiasRT");

        public CopyDepthPass(RenderPassEvent evt, Material copyDepthMaterial)
        {
            _copyDepthMaterial = copyDepthMaterial;
            renderPassEvent = evt;
        }

        public void Setup(RenderTargetHandle source, RenderTargetHandle destination)
        {
            this.source = source;
            this.destination = destination;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureTarget(destination.Identifier());
            ConfigureClear(ClearFlag.All, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_copyDepthMaterial == null)
            {
                Debug.LogErrorFormat("Missing {0}. {1} render pass will not execute. Check for missing reference in the renderer resources.", _copyDepthMaterial, GetType().Name);
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);

            switch (renderingData.cameraData.cameraTargetDescriptor.msaaSamples)
            {
                case 8:
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                    cmd.EnableShaderKeyword(ShaderKeywordStrings.DepthMsaa8);
                    break;
                case 4:
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                    cmd.EnableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa8);
                    break;
                case 2:
                    cmd.EnableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa8);
                    break;
                default:
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa8);
                    break;
            }

            var flipSign = (renderingData.cameraData.IsCameraProjectionMatrixFlipped()) ? -1.0f : 1.0f;
            var scaleBias = (flipSign < 0.0f) ? new Vector4(flipSign, 1.0f, -1.0f, 1.0f) : new Vector4(flipSign, 0.0f, 1.0f, 1.0f);

            cmd.SetGlobalVector(_scaleBiasId, scaleBias);
            cmd.SetGlobalTexture("_CameraDepthAttachment", source.Identifier());
            cmd.DrawProcedural(Matrix4x4.identity, _copyDepthMaterial, 0, MeshTopology.Triangles, 3);

            context.ExecuteCommandBuffer(cmd);

            CommandBufferPool.Release(cmd);
        }
    }
}