using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
	public class CapsuleShadowPass : ScriptableRenderPass
    {
        Material _capsuleShadowMaterial;

        RenderTargetHandle _capsuleShadowMap;
        RenderTextureDescriptor _renderTextureDescriptor;
        RenderTargetHandle _depthAttachmentHandle { get; set; }

        private CapsuleShadow _capsuleShadow;
        private Vector4[] _additionalOccluderPositions;

        public CapsuleShadowPass(RenderPassEvent evt, Material capsuleOcclusionMaterial)
        {
            renderPassEvent = evt;

            _capsuleShadowMaterial = capsuleOcclusionMaterial;
            _capsuleShadowMap.Init("_CapsuleShadowTexture");
            _additionalOccluderPositions = new Vector4[UniversalRenderPipeline.maxVisibleAdditionalLights];
        }

        public bool Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle depthAttachmentHandle)
        {
#if UNITY_EDITOR
            if (_capsuleShadowMaterial == null)
            {
                Debug.LogErrorFormat("Missing {0}. {1} render pass will not execute. Check for missing reference in the renderer resources.", _capsuleShadowMaterial, GetType().Name);
                return false;
            }
#endif

            VolumeStack stack = VolumeManager.instance.stack;

            _capsuleShadow = stack.GetComponent<CapsuleShadow>();
            if (_capsuleShadow.IsActive())
			{
                _depthAttachmentHandle = depthAttachmentHandle;

                _renderTextureDescriptor = baseDescriptor;
                _renderTextureDescriptor.depthBufferBits = 0;
                _renderTextureDescriptor.msaaSamples = 1;

                if (!_capsuleShadow.shouledFullRes)
				{
                    _renderTextureDescriptor.width = _renderTextureDescriptor.width >> 1;
                    _renderTextureDescriptor.height = _renderTextureDescriptor.height >> 1;
                }

                if (RenderingUtils.SupportsGraphicsFormat(GraphicsFormat.R8_UNorm, FormatUsage.Linear | FormatUsage.Render))
                    _renderTextureDescriptor.graphicsFormat = GraphicsFormat.R8_UNorm;
                else
                    _renderTextureDescriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;

                return true;
            }
            else
			{
                Shader.DisableKeyword(ShaderConstants._CapsuleShadow);
                return false;
			}
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            cmd.GetTemporaryRT(_capsuleShadowMap.id, _renderTextureDescriptor, FilterMode.Point);

            ConfigureTarget(_capsuleShadowMap.Identifier(), _capsuleShadow.shouledFullRes ? _depthAttachmentHandle.Identifier() : _capsuleShadowMap.Identifier());
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._profilerTag);

            var occluderCount = CapsuleOccluderManager.instance.occluders.Count;
            if (occluderCount > 0)
            {
                for (int i = 0; i < occluderCount; i++)
                {
                    var occluder = CapsuleOccluderManager.instance.occluders[i];
                    var pointOffset = (occluder.height - (occluder.radius * 2)) / 2;
                    var localToWorldMatrix = Matrix4x4.TRS(occluder.transform.position, occluder.transform.rotation, occluder.transform.lossyScale);

                    var axis = Vector3.up;

                    if (occluder.axis == CapsuleOccluder.Axis.X)
                        axis = Vector3.right;
                    else if (occluder.axis == CapsuleOccluder.Axis.Z)
                        axis = Vector3.forward;

                    var up = localToWorldMatrix.MultiplyPoint(occluder.center + axis * pointOffset);
                    var down = localToWorldMatrix.MultiplyPoint(occluder.center - axis * pointOffset);

                    _additionalOccluderPositions[i * 2].Set(up.x, up.y, up.z, occluder.radius * 2);
                    _additionalOccluderPositions[i * 2 + 1].Set(down.x, down.y, down.z, occluder.radius * 2);
                }
                
                cmd.EnableShaderKeyword(ShaderConstants._CapsuleShadow);

                cmd.SetGlobalFloat(ShaderConstants._AdditionalOccludersCount, occluderCount);
                cmd.SetGlobalVector(ShaderConstants._LightParams, Vector4.zero);
                cmd.SetGlobalVector(ShaderConstants._ConeParams, new Vector4(_capsuleShadow.angle.value * Mathf.Deg2Rad, _capsuleShadow.strength.value, 0f, 0f));
                cmd.SetGlobalVectorArray(ShaderConstants._AdditionalOccluderPosition, _additionalOccluderPositions);

                cmd.DrawProcedural(Matrix4x4.identity, _capsuleShadowMaterial, 0, MeshTopology.Triangles, 3);
            }
            else
			{
                cmd.DisableShaderKeyword(ShaderConstants._CapsuleShadow);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_capsuleShadowMap.id);
        }

        static class ShaderConstants
        {
            public const string _profilerTag = "Resolve Capsules";
            public const string _CapsuleShadow = "_CAPSULE_SHADOWS";

            public static readonly int _LightParams = Shader.PropertyToID("_LightParams");
            public static readonly int _ConeParams = Shader.PropertyToID("_ConeParams");

            public static readonly int _AdditionalOccludersCount = Shader.PropertyToID("_AdditionalOccludersCount");
            public static readonly int _AdditionalOccluderPosition = Shader.PropertyToID("_AdditionalOccluderPosition");
        }
    }
}