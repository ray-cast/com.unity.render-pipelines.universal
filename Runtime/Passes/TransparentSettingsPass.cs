namespace UnityEngine.Rendering.Universal
{
    public class TransparentSettingsPass : ScriptableRenderPass
    {
        bool _shouldReceiveShadows;

        public TransparentSettingsPass(RenderPassEvent evt, bool shadowReceiveSupported)
        {
            renderPassEvent = evt;
            _shouldReceiveShadows = shadowReceiveSupported;
        }

        public bool Setup(ref RenderingData renderingData)
        {
            return !_shouldReceiveShadows;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._profilerTag);

            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, _shouldReceiveShadows);
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowCascades, _shouldReceiveShadows);
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.AdditionalLightShadows, _shouldReceiveShadows);

            context.ExecuteCommandBuffer(cmd);

            CommandBufferPool.Release(cmd);
        }

        static class ShaderConstants
        {
            public const string _profilerTag = "Transparent Settings Pass";
        }
    }
}