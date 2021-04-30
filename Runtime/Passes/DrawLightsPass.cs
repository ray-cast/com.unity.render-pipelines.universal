namespace UnityEngine.Rendering.Universal
{
    public class DrawLightsPass : ScriptableRenderPass
    {
        private Material _material;

        private RenderTargetHandle _colorAttachmentHandle { get; set; }
        private RenderTargetHandle _depthAttachmentHandle { get; set; }

        private RenderTextureDescriptor _descriptor { get; set; }

        public DrawLightsPass(RenderPassEvent evt, Material lightingMaterial)
        {
            this.renderPassEvent = evt;
            this._material = lightingMaterial;
        }

        public void Setup(RenderTextureDescriptor cameraTextureDescriptor, RenderTargetHandle colorAttachmentHandle, RenderTargetHandle depthAttachmentHandle)
        {
            this._descriptor = cameraTextureDescriptor;
            this._colorAttachmentHandle = colorAttachmentHandle;
            this._depthAttachmentHandle = depthAttachmentHandle;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureTarget(_colorAttachmentHandle.Identifier(), _depthAttachmentHandle.Identifier());
            ConfigureClear(ClearFlag.None, ShaderConstants.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._profilerTag);

            var camera = renderingData.cameraData.camera;
            var flipSign = renderingData.cameraData.IsCameraProjectionMatrixFlipped() ? -1.0f : 1.0f;
            var scaleBias = flipSign < 0.0f ? new Vector4(flipSign, 1.0f, -1.0f, 1.0f) : new Vector4(flipSign, 0.0f, 1.0f, 1.0f);

            cmd.Clear();
            cmd.ClearRenderTarget(false, true, ShaderConstants.black);
            cmd.SetGlobalVector(ShaderConstants._scaleBiasId, scaleBias);

            if (renderingData.lightData.mainLightIndex >= 0)
                cmd.DrawProcedural(Matrix4x4.identity, _material, 0, MeshTopology.Triangles, 3);
            else
                cmd.DrawProcedural(Matrix4x4.identity, _material, 4, MeshTopology.Triangles, 3);

            var lights = renderingData.lightData.visibleLights;
            int maxAdditionalLightsCount = UniversalRenderPipeline.maxVisibleAdditionalLights;

            for (int i = 0, lightIter = 0; i < lights.Length && lightIter < maxAdditionalLightsCount; ++i)
            {
                if (renderingData.lightData.mainLightIndex != i)
                {
                    VisibleLight light = lights[i];

                    var lightRangeSqr = light.light.range * light.light.range;
                    var distanceThreadhold = renderingData.lightData.maxLightingDistance * renderingData.lightData.maxLightingDistance;
                    var distanceSqr = Vector3.SqrMagnitude(camera.transform.position - light.light.transform.position);
                    if (distanceSqr - lightRangeSqr > distanceThreadhold)
                        continue;
                    
                    switch (light.lightType)
                    {
                        case LightType.Directional:
                            cmd.SetGlobalVector(ShaderConstants._LightParams, new Vector4(lightIter, 1, 0, 0));
                            cmd.DrawProcedural(Matrix4x4.identity, _material, 1, MeshTopology.Triangles, 3);
                            break;
                        case LightType.Point:
                            cmd.SetGlobalVector(ShaderConstants._LightParams, new Vector4(lightIter, light.range, 0, 0));
                            cmd.DrawMesh(RenderingUtils.icosphereMesh, light.localToWorldMatrix, _material, 0, distanceSqr < lightRangeSqr ? 2 : 3);
                            break;
                        case LightType.Spot:
                            cmd.SetGlobalVector(ShaderConstants._LightParams, new Vector4(lightIter, light.range, 0, 0));
                            cmd.DrawMesh(RenderingUtils.icosphereMesh, light.localToWorldMatrix, _material, 0, distanceSqr < lightRangeSqr ? 2 : 3);
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

            public static readonly Color black = new Color(0, 0, 0, 0);

            public static readonly int _scaleBiasId = Shader.PropertyToID("_ScaleBiasRT");
            public static readonly int _LightParams = Shader.PropertyToID("_LightParams");
        }
    }
}