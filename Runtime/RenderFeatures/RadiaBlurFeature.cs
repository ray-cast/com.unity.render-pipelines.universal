using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class RadiaBlurFeature : ScriptableRendererFeature
{
    RadiaBlurRenderPass scriptablePass;
    RenderTargetHandle renderTargetHandle;

    public sealed class RadiaBlurRenderPass : ScriptableRenderPass
    {
        static readonly string k_RenderTag = "Radia Blur Effects";
        static readonly int _MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int _Params = Shader.PropertyToID("_Params");

        public int blitShaderPassIndex = 0;
        private RenderTargetHandle destination { get; set; }
        Material holoMat;
        RadiaBlur radiaBlur;
        RenderTargetIdentifier currentTarget;
        RenderTargetHandle temporaryColorTexture;
        public FilterMode filterMode { get; set; }
        public RadiaBlurRenderPass()
        {
            var shader = Shader.Find("Hidden/Blur/RadiaBlur");
            holoMat = CoreUtils.CreateEngineMaterial(shader);
            temporaryColorTexture.Init("temporaryColorTexture");

        }
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            cameraTextureDescriptor.depthBufferBits = 0;
            cmd.GetTemporaryRT(temporaryColorTexture.id, cameraTextureDescriptor, filterMode);
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (holoMat == null)
            {
                UnityEngine.Debug.LogError("材质没找到！");
                return;
            }

            radiaBlur = VolumeManager.instance.stack.GetComponent<RadiaBlur>();
            if (radiaBlur)
            {
                if (radiaBlur.IsActive())
                {
                    var cmd = CommandBufferPool.Get(k_RenderTag);
                    Render(cmd, ref renderingData);
                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                }
            }
        }

        void Render(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.isSceneViewCamera) return;

            holoMat.SetVector(_Params, new Vector4(radiaBlur.center.value.x, radiaBlur.center.value.y, radiaBlur.intensity.value * 50f, radiaBlur.exponential.value));

            if (destination == RenderTargetHandle.CameraTarget)
            {
                Blit(cmd, currentTarget, temporaryColorTexture.Identifier(), holoMat, blitShaderPassIndex);
                Blit(cmd, temporaryColorTexture.Identifier(), currentTarget);
            }
            else
            {
                Blit(cmd, currentTarget, destination.Identifier(), holoMat, blitShaderPassIndex);
            }
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (destination == RenderTargetHandle.CameraTarget)
                cmd.ReleaseTemporaryRT(temporaryColorTexture.id);
        }

        public void Setup(in RenderTargetIdentifier currentTarget, RenderTargetHandle dest)
        {
            this.destination = dest;
            this.currentTarget = currentTarget;
        }
    }
    public override void Create()
    {
        scriptablePass = new RadiaBlurRenderPass();
        scriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        renderTargetHandle.Init("_ScreenTexture2");
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.postProcessEnabled)
        {
            var dest = RenderTargetHandle.CameraTarget;
            scriptablePass.Setup(renderer.cameraColorTarget, dest);
            renderer.EnqueuePass(scriptablePass);
        }
    }
}