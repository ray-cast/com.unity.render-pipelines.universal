namespace UnityEngine.Rendering.Universal
{
    public class GlobalSettingsPass : ScriptableRenderPass
    {
        private Vector4 _WindParams1 = Vector4.zero;
        private Vector4 _WindParams2 = Vector4.zero;
        private Vector4 _WindParams3 = Vector4.zero;
        private PostProcessData _postProcessData;

        public GlobalSettingsPass(RenderPassEvent evt, PostProcessData postProcessData)
        {
            renderPassEvent = evt;
            _postProcessData = postProcessData;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._profilerTag);

            cmd.SetGlobalTexture(ShaderConstants._SkinRamp, _postProcessData.textures.shadowTex);

            var wind = VolumeManager.instance.stack.GetComponent<Wind>();
            if (wind.IsActive())
			{
                var windDirection = wind.direction.value;
                var windRandom = wind.random.value;
                var windTiling = wind.tiling.value;

                _WindParams1.Set(windDirection.x, windDirection.y, windDirection.z, wind.load.value);
                _WindParams2.Set(windRandom.x, windRandom.y, wind.bending.value, wind.frequency.value);
                _WindParams3.Set(windTiling.x, windTiling.y, wind.range.value, wind.speed.value);

                cmd.SetGlobalTexture(ShaderConstants._WindNoiseMap, wind.noise.value);

                cmd.SetGlobalVector(ShaderConstants._WindParams1, _WindParams1);
                cmd.SetGlobalVector(ShaderConstants._WindParams2, _WindParams2);
                cmd.SetGlobalVector(ShaderConstants._WindParams3, _WindParams3);
            }
            else
			{
                cmd.SetGlobalVector(ShaderConstants._WindParams1, ShaderConstants.defaultWindParams1);
                cmd.SetGlobalVector(ShaderConstants._WindParams2, ShaderConstants.defaultWindParams2);
                cmd.SetGlobalVector(ShaderConstants._WindParams3, ShaderConstants.defaultWindParams3);
            }

            context.ExecuteCommandBuffer(cmd);

            CommandBufferPool.Release(cmd);
        }

        static class ShaderConstants
        {
            public const string _profilerTag = "Wind Settings Pass";

            public static readonly Vector4 defaultWindParams1 = new Vector4(1, 0, 0, 0);
            public static readonly Vector4 defaultWindParams2 = new Vector4(0, 0, 0, 0);
            public static readonly Vector4 defaultWindParams3 = new Vector4(1, 1, 20, 0);

            public static readonly int _SkinRamp = Shader.PropertyToID("_SkinRamp");

            public static readonly int _WindNoiseMap = Shader.PropertyToID("_WindNoiseMap");

            public static readonly int _WindParams1 = Shader.PropertyToID("_WindParams1");
            public static readonly int _WindParams2 = Shader.PropertyToID("_WindParams2");
            public static readonly int _WindParams3 = Shader.PropertyToID("_WindParams3");
        }
    }
}