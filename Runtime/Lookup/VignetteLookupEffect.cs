using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public class VignetteLookupEffect : ScriptableRenderPass
    {
        const string _profilerTag = "Vignette Lookup Effect";

        Material _vignetteMaterial;

        VignetteLookup _vignetteLookup;

        RenderTargetHandle _vignetteLookupTextureTemp;
        RenderTextureDescriptor _renderTextureDescriptor;
        RenderTargetHandle _colorAttachmentHandle { get; set; }

        private Vector4[] _additionalLookupPositions;

        public VignetteLookupEffect(RenderPassEvent evt, PostProcessData data)
        {
            renderPassEvent = evt;

            _vignetteMaterial = CoreUtils.CreateEngineMaterial(data.shaders.vignetteLookupPS);
            _vignetteLookupTextureTemp.Init("_CameraColorTextureTemp");
            _additionalLookupPositions = new Vector4[UniversalRenderPipeline.maxVisibleAdditionalLights];
        }

        public bool Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle colorAttachmentHandle)
        {
            VolumeStack stack = VolumeManager.instance.stack;

            _vignetteLookup = stack.GetComponent<VignetteLookup>();
            if (_vignetteLookup.IsActive())
            {
                _colorAttachmentHandle = colorAttachmentHandle;

                _renderTextureDescriptor = baseDescriptor;
                _renderTextureDescriptor.depthBufferBits = 0;
                _renderTextureDescriptor.msaaSamples = 1;
                _renderTextureDescriptor.graphicsFormat = baseDescriptor.graphicsFormat;

                return true;
            }

            return false;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            cmd.GetTemporaryRT(_vignetteLookupTextureTemp.id, _renderTextureDescriptor, FilterMode.Point);

            ConfigureTarget(_vignetteLookupTextureTemp.Identifier());
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
#if UNITY_EDITOR
            if (_vignetteMaterial == null)
            {
                Debug.LogErrorFormat("Missing {0}. {1} render pass will not execute. Check for missing reference in the renderer resources.", _vignetteMaterial, GetType().Name);
                return;
            }
#endif

            var aspectRatio = _renderTextureDescriptor.width / (float)_renderTextureDescriptor.height;
            if (renderingData.cameraData.isStereoEnabled && XRGraphics.stereoRenderingMode == XRGraphics.StereoRenderingMode.SinglePass)
                aspectRatio *= 0.5f;

            var lutHeight = _vignetteLookup.texture.value.height;
            var lutWidth = lutHeight * lutHeight;
            var lutParameters = new Vector4(1.0f / lutWidth, 1.0f / lutHeight, lutHeight - 1f, _vignetteLookup.contribution.value);
            var color = _vignetteLookup.color.value;
            var center = _vignetteLookup.center.value;

            var v1 = new Vector4(
                color.r, color.g, color.b,
                _vignetteLookup.rounded.value ? aspectRatio : 1f
            );
            var v2 = new Vector4(
                center.x, center.y,
                _vignetteLookup.radius.value,
                _vignetteLookup.smoothness.value
            );
            var v3 = new Vector4(
                _vignetteLookup.strength.value,
                0, 0, 0
            );

            var occluderCount = VignetteLookupManager.instance.lookups.Count;
            if (occluderCount > 0)
            {
                for (int i = 0; i < occluderCount; i++)
                {
                    var occluder = VignetteLookupManager.instance.lookups[i];
                    var anchorCenter = occluder.position;

                    _additionalLookupPositions[i].Set(anchorCenter.x, anchorCenter.y, anchorCenter.z, occluder.radius);
                }
            }

            CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);
            cmd.SetGlobalVector(ShaderConstants._Vignette_Params1, v1);
            cmd.SetGlobalVector(ShaderConstants._Vignette_Params2, v2);
            cmd.SetGlobalVector(ShaderConstants._Vignette_Params3, v3);
            cmd.SetGlobalTexture(ShaderConstants._SceneLut, _vignetteLookup.texture.value);
            cmd.SetGlobalVector(ShaderConstants._userLutParams, lutParameters);
            cmd.SetGlobalFloat(ShaderConstants._AdditionalLookupCount, occluderCount);
            cmd.SetGlobalVectorArray(ShaderConstants._AdditionalLookupPosition, _additionalLookupPositions);
            cmd.DrawProcedural(Matrix4x4.identity, _vignetteMaterial, 0, MeshTopology.Triangles, 3);
            cmd.Blit(_vignetteLookupTextureTemp.Identifier(), _colorAttachmentHandle.Identifier());

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_vignetteLookupTextureTemp.id);
        }

        static class ShaderConstants
        {
            public static readonly int _MainTex = Shader.PropertyToID("_MainTex");
            public static readonly int _userLutParams = Shader.PropertyToID("_userLutParams");
            public static readonly int _SceneLut = Shader.PropertyToID("_SceneLut");

            public static readonly int _Vignette_Params1 = Shader.PropertyToID("_Vignette_Params1");
            public static readonly int _Vignette_Params2 = Shader.PropertyToID("_Vignette_Params2");
            public static readonly int _Vignette_Params3 = Shader.PropertyToID("_Vignette_Params3");

            public static readonly int _AdditionalLookupCount = Shader.PropertyToID("_AdditionalLookupCount");
            public static readonly int _AdditionalLookupPosition = Shader.PropertyToID("_AdditionalLookupPosition");
        }
    }
}