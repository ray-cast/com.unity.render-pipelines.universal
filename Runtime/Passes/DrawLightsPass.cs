namespace UnityEngine.Rendering.Universal
{
    public class DrawLightsPass : ScriptableRenderPass
    {
        private Material _material;

        private RenderTargetHandle _colorAttachmentHandle { get; set; }
        private RenderTargetHandle _depthAttachmentHandle { get; set; }

        public DrawLightsPass(RenderPassEvent evt, Material lightingMaterial)
        {
            this.renderPassEvent = evt;
            this._material = lightingMaterial;
        }

        public void Setup(RenderTargetHandle colorAttachmentHandle, RenderTargetHandle depthAttachmentHandle)
        {
            this._colorAttachmentHandle = colorAttachmentHandle;
            this._depthAttachmentHandle = depthAttachmentHandle;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureTarget(_colorAttachmentHandle.Identifier(), _depthAttachmentHandle.Identifier());
            ConfigureClear(ClearFlag.None, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._profilerTag);

            cmd.Clear();
            cmd.ClearRenderTarget(false, true, Color.clear);
            cmd.DrawProcedural(Matrix4x4.identity, _material, renderingData.lightData.mainLightIndex >= 0 ? 1 : 0, MeshTopology.Triangles, 3);

            var lights = renderingData.lightData.visibleLights;
            var camera = renderingData.cameraData.camera;
            var maxAdditionalLightsCount = UniversalRenderPipeline.maxVisibleAdditionalLights;

            for (int i = 0, lightIter = 0; i < lights.Length && lightIter < maxAdditionalLightsCount; ++i)
            {
                if (renderingData.lightData.mainLightIndex != i)
                {
                    VisibleLight light = lights[i];

                    var lightRange = light.light.range + 0.05f;
                    var lightRangeSqr = lightRange * lightRange;
                    var distanceThreadhold = renderingData.lightData.maxLightingDistance * renderingData.lightData.maxLightingDistance;
                    var distanceSqr = Vector3.Magnitude(camera.transform.position - light.light.transform.position);
                    if (distanceSqr - lightRangeSqr > distanceThreadhold)
                        continue;
                    
                    switch (light.lightType)
                    {
                        case LightType.Directional:
                            cmd.SetGlobalVector(ShaderConstants._LightParams, new Vector4(lightIter, 1, 0, 0));
                            cmd.DrawProcedural(Matrix4x4.identity, _material, 2, MeshTopology.Triangles, 3);
                            break;
                        case LightType.Point:
                            cmd.SetGlobalVector(ShaderConstants._LightParams, new Vector4(lightIter, light.range, 0, 0));
                            cmd.DrawMesh(RenderingUtils.icosphereMesh, light.localToWorldMatrix, _material, 0, distanceSqr < lightRangeSqr ? 3 : 4);
                            break;
                        case LightType.Spot:
                            cmd.SetGlobalVector(ShaderConstants._LightParams, new Vector4(lightIter, light.range, 0, 0));
                            cmd.DrawMesh(RenderingUtils.icosphereMesh, light.localToWorldMatrix, _material, 0, distanceSqr < lightRangeSqr ? 3 : 4);
                            break;
                    }

                    lightIter++;
                }
            }

            context.ExecuteCommandBuffer(cmd);

            CommandBufferPool.Release(cmd);
        }

        static class ShaderConstants
        {
            public const string _profilerTag = "Draw Lights";

            public static readonly int _LightParams = Shader.PropertyToID("_LightParams");
        }
    }
}