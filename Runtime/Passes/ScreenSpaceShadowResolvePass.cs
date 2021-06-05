using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public class ScreenSpaceShadowResolvePass : ScriptableRenderPass
    {
        const string _profilerTag = "Resolve Shadows";

        Material _screenSpaceShadowsMaterial;

        RenderTargetHandle _screenSpaceShadowmap;
        RenderTargetHandle _screenSpaceShadowmapTemp;
        RenderTextureDescriptor _renderTextureDescriptor;
        RenderTargetHandle _depthAttachmentHandle { get; set; }

        public ScreenSpaceShadowResolvePass(RenderPassEvent evt, Material screenspaceShadowsMaterial)
        {
            _screenSpaceShadowsMaterial = screenspaceShadowsMaterial;
            _screenSpaceShadowmap.Init("_ScreenSpaceShadowmapTexture");
            _screenSpaceShadowmapTemp.Init("_ScreenSpaceShadowmapTextureTemp");
            renderPassEvent = evt;
        }

        public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle depthAttachmentHandle)
        {
            _depthAttachmentHandle = depthAttachmentHandle;

            _renderTextureDescriptor = baseDescriptor;
            _renderTextureDescriptor.depthBufferBits = 0;
            _renderTextureDescriptor.msaaSamples = 1;

            if (RenderingUtils.SupportsGraphicsFormat(GraphicsFormat.R8_UNorm, FormatUsage.Linear | FormatUsage.Render))
                _renderTextureDescriptor.graphicsFormat = GraphicsFormat.R8_UNorm;
            else
                _renderTextureDescriptor.graphicsFormat = GraphicsFormat.B8G8R8A8_UNorm;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            cmd.GetTemporaryRT(_screenSpaceShadowmap.id, _renderTextureDescriptor, FilterMode.Point);
            cmd.GetTemporaryRT(_screenSpaceShadowmapTemp.id, _renderTextureDescriptor, FilterMode.Point);

            ConfigureTarget(_screenSpaceShadowmap.Identifier(), _depthAttachmentHandle.Identifier());
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
#if UNITY_EDITOR
            if (_screenSpaceShadowsMaterial == null)
            {
                Debug.LogErrorFormat("Missing {0}. {1} render pass will not execute. Check for missing reference in the renderer resources.", _screenSpaceShadowsMaterial, GetType().Name);
                return;
            }
#endif

            int shadowLightIndex = renderingData.lightData.mainLightIndex;
            if (shadowLightIndex >= 0)
			{
                VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];

                CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);
                cmd.DrawProcedural(Matrix4x4.identity, _screenSpaceShadowsMaterial, 0, MeshTopology.Triangles, 3);

                bool softShadows = shadowLight.light.shadows == LightShadows.Soft && renderingData.shadowData.supportsSoftShadows;
                if (softShadows)
				{
                    cmd.SetRenderTarget(_screenSpaceShadowmapTemp.Identifier(), _depthAttachmentHandle.Identifier());
                    cmd.ClearRenderTarget(false, true, Color.clear);
                    cmd.SetGlobalTexture("_MainTex", _screenSpaceShadowmap.Identifier());
                    cmd.SetGlobalVector("_Offset", new Vector2(1.5f / _renderTextureDescriptor.width, 0));
                    cmd.DrawProcedural(Matrix4x4.identity, _screenSpaceShadowsMaterial, 1, MeshTopology.Triangles, 3);

                    cmd.SetRenderTarget(_screenSpaceShadowmap.Identifier(), _depthAttachmentHandle.Identifier());
                    cmd.ClearRenderTarget(false, true, Color.clear);
                    cmd.SetGlobalTexture("_MainTex", _screenSpaceShadowmapTemp.Identifier());
                    cmd.SetGlobalVector("_Offset", new Vector2(0, 1.5f / _renderTextureDescriptor.height));
                    cmd.DrawProcedural(Matrix4x4.identity, _screenSpaceShadowsMaterial, 1, MeshTopology.Triangles, 3);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_screenSpaceShadowmap.id);
            cmd.ReleaseTemporaryRT(_screenSpaceShadowmapTemp.id);
        }
    }
}