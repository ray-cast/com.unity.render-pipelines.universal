namespace UnityEngine.Rendering.Universal
{
    public class CapsuleShadowFeature : ScriptableRendererFeature
    {
        CapsuleShadowPass shadowPass;
        RenderTargetHandle shadowTexture;

        class CapsuleShadowPass : ScriptableRenderPass
        {
            private RenderTargetHandle shadowAttachmentHandle { get; set; }

            internal RenderTextureDescriptor descriptor { get; private set; }
            internal RenderTargetIdentifier depthDescriptor { get; private set; }

            private Vector4[] _additionalOccluderPositions;

            private ProfilingSampler _profilingSampler;

            public CapsuleShadowPass()
            {
                int maxLights = UniversalRenderPipeline.maxVisibleAdditionalLights;

                _profilingSampler = new ProfilingSampler(ShaderConstants._profilerTag);
                _additionalOccluderPositions = new Vector4[maxLights];
            }

            public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetIdentifier depthTargetIdentifier, RenderTargetHandle emissiveAttachmentHandle, int depthBufferBits = 0)
            {
                this.shadowAttachmentHandle = emissiveAttachmentHandle;
                baseDescriptor.colorFormat = RenderTextureFormat.ARGB32;
                baseDescriptor.depthBufferBits = depthBufferBits;

                descriptor = baseDescriptor;
                depthDescriptor = depthTargetIdentifier;
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                cmd.GetTemporaryRT(shadowAttachmentHandle.id, descriptor, FilterMode.Point);

                ConfigureTarget(shadowAttachmentHandle.Identifier(), this.depthDescriptor);
                ConfigureClear(ClearFlag.Color, Color.black);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._profilerTag);

                using (new ProfilingScope(cmd, _profilingSampler))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    var occluderCount = OccluderManager.instance.occluders.Count;
                    if (occluderCount > 0)
                    {
                        for (int i = 0; i < occluderCount; i++)
                        {
                            var occluder = OccluderManager.instance.occluders[i];
                            var position = occluder.position;

                            _additionalOccluderPositions[i].Set(position.x, position.y, position.z, occluder.radius * 2);
                        }

                        cmd.SetGlobalFloat(ShaderConstants._AdditionalOccludersCount, occluderCount);
                        cmd.SetGlobalVectorArray(ShaderConstants._AdditionalOccluderPosition, _additionalOccluderPositions);
                        cmd.SetGlobalVector(ShaderConstants._ConeParams, new Vector4(45.0f * Mathf.Deg2Rad, 30f * Mathf.Deg2Rad, 0f, 0f));
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void FrameCleanup(CommandBuffer cmd)
            {
                if (shadowAttachmentHandle != RenderTargetHandle.CameraTarget)
                {
                    cmd.ReleaseTemporaryRT(shadowAttachmentHandle.id);
                    shadowAttachmentHandle = RenderTargetHandle.CameraTarget;
                }
            }
        }

        public override void Create()
        {
            shadowPass = new CapsuleShadowPass();
            shadowPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            shadowTexture.Init("_CameraShadowTexture");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            shadowPass.Setup(renderingData.cameraData.cameraTargetDescriptor, renderer.cameraDepth, shadowTexture);
            renderer.EnqueuePass(shadowPass);
        }

        static class ShaderConstants
        {
            public const string _profilerTag = "Capsule Shadow Pass";

            public static readonly Color black = new Color(0, 0, 0, 0);

            public static readonly int _ConeParams = Shader.PropertyToID("_ConeParams");

            public static readonly int _AdditionalOccludersCount = Shader.PropertyToID("_AdditionalOccludersCount");
            public static readonly int _AdditionalOccluderPosition = Shader.PropertyToID("_AdditionalOccluderPosition");
        }
    }
}