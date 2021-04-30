namespace UnityEngine.Rendering.Universal
{
    public class GlowFeature : ScriptableRendererFeature
    {
        GlowPass glowPass;
        RenderTargetHandle glowTexture;

        class GlowPass : ScriptableRenderPass
        {
            private RenderTargetHandle glowAttachmentHandle { get; set; }

            internal RenderTextureDescriptor descriptor { get; private set; }
            internal RenderTargetIdentifier depthDescriptor { get; private set; }

            string _profilerTag = "Glow Pass";
            private ProfilingSampler _profilingSampler;

            private FilteringSettings _filteringSettings;
            ShaderTagId _shaderTagId = new ShaderTagId("OutGlow");

            public GlowPass(RenderQueueRange renderQueueRange, LayerMask layerMask)
            {
                _profilingSampler = new ProfilingSampler(_profilerTag);
                _filteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            }

            public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetIdentifier depthTargetIdentifier, RenderTargetHandle emissiveAttachmentHandle, int depthBufferBits = 0)
            {
                this.glowAttachmentHandle = emissiveAttachmentHandle;
                baseDescriptor.colorFormat = RenderTextureFormat.ARGB32;
                baseDescriptor.depthBufferBits = depthBufferBits;

                descriptor = baseDescriptor;
                depthDescriptor = depthTargetIdentifier;
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                cmd.GetTemporaryRT(glowAttachmentHandle.id, descriptor, FilterMode.Point);

                ConfigureTarget(glowAttachmentHandle.Identifier(), this.depthDescriptor);
                ConfigureClear(ClearFlag.Color, Color.black);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);

                using (new ProfilingScope(cmd, _profilingSampler))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                    var drawSettings = CreateDrawingSettings(_shaderTagId, ref renderingData, sortFlags);
                    drawSettings.perObjectData = PerObjectData.None;

                    ref CameraData cameraData = ref renderingData.cameraData;
                    Camera camera = cameraData.camera;
                    if (cameraData.isStereoEnabled)
                        context.StartMultiEye(camera);

                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref _filteringSettings);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void FrameCleanup(CommandBuffer cmd)
            {
                if (glowAttachmentHandle != RenderTargetHandle.CameraTarget)
                {
                    cmd.ReleaseTemporaryRT(glowAttachmentHandle.id);
                    glowAttachmentHandle = RenderTargetHandle.CameraTarget;
                }
            }
        }

        public override void Create()
        {
            glowPass = new GlowPass(RenderQueueRange.all, -1);
            glowPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            glowTexture.Init("_CameraGlowTexture");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            glowPass.Setup(renderingData.cameraData.cameraTargetDescriptor, renderer.cameraDepth, glowTexture);
            renderer.EnqueuePass(glowPass);
        }
    }
}