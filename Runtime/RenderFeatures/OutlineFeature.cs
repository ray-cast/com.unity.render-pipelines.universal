namespace UnityEngine.Rendering.Universal
{
    public class OutlineFeature : ScriptableRendererFeature
    {
        OutlinePass outlinePass;

        class OutlinePass : ScriptableRenderPass
        {
            internal RenderTargetIdentifier colorDescriptor { get; private set; }
            internal RenderTargetIdentifier depthDescriptor { get; private set; }

            private string _profilerTag = "Outline Pass";
            private ProfilingSampler _profilingSampler;

            private FilteringSettings _filteringSettings;
            private ShaderTagId _shaderTagId = new ShaderTagId("Outline");

            public OutlinePass(RenderQueueRange renderQueueRange, LayerMask layerMask)
            {
                _profilingSampler = new ProfilingSampler(_profilerTag);
                _filteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            }

            public void Setup(RenderTargetIdentifier colorTargetIdentifier, RenderTargetIdentifier depthTargetIdentifier)
            {
                this.colorDescriptor = colorTargetIdentifier;
                this.depthDescriptor = depthTargetIdentifier;
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                ConfigureTarget(this.colorDescriptor, this.depthDescriptor);
                ConfigureClear(ClearFlag.None, Color.white);
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
                    drawSettings.perObjectData = PerObjectData.LightProbe;

                    ref CameraData cameraData = ref renderingData.cameraData;
                    if (cameraData.isStereoEnabled)
                        context.StartMultiEye(cameraData.camera);

                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref _filteringSettings);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        public override void Create()
        {
            outlinePass = new OutlinePass(RenderQueueRange.all, -1);
            outlinePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            outlinePass.Setup(renderer.cameraColorTarget, renderer.cameraDepth);
            renderer.EnqueuePass(outlinePass);
        }
    }
}