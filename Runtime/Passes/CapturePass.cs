namespace UnityEngine.Rendering.Universal
{
    class CapturePass : ScriptableRenderPass
    {
        RenderTargetHandle _cameraColorHandle;

        public CapturePass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
        }

        public void Setup(RenderTargetHandle colorHandle)
        {
            _cameraColorHandle = colorHandle;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmdBuf = CommandBufferPool.Get(ShaderConstants._profilerTag);

            var captureActions = renderingData.cameraData.captureActions;
            for (captureActions.Reset(); captureActions.MoveNext();)
                captureActions.Current(_cameraColorHandle.Identifier(), cmdBuf);

            context.ExecuteCommandBuffer(cmdBuf);
            CommandBufferPool.Release(cmdBuf);
        }

        static class ShaderConstants
        {
            public const string _profilerTag = "Capture Pass";
        }
    }
}