using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public class ScenePostProcess : ScriptableRenderPass
    {
        const string _profilerTag = "Scene Post Process";

        Material _scenePostProcessMaterial;

        SceneLookup _sceneLookup;
        RenderTargetHandle _sceneColorTextureTemp;
        RenderTextureDescriptor _renderTextureDescriptor;
        RenderTargetHandle _colorAttachmentHandle { get; set; }

        public ScenePostProcess(RenderPassEvent evt, PostProcessData data)
        {
            _scenePostProcessMaterial = CoreUtils.CreateEngineMaterial(data.shaders.sceneLookupPS);
            _sceneColorTextureTemp.Init("_CameraColorTextureTemp");
            renderPassEvent = evt;
        }

        public bool Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle colorAttachmentHandle)
        {
            VolumeStack stack = VolumeManager.instance.stack;

            _sceneLookup = stack.GetComponent<SceneLookup>();
            if (_sceneLookup.IsActive())
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
            cmd.GetTemporaryRT(_sceneColorTextureTemp.id, _renderTextureDescriptor, FilterMode.Point);

            ConfigureTarget(_sceneColorTextureTemp.Identifier());
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
#if UNITY_EDITOR
            if (_scenePostProcessMaterial == null)
            {
                Debug.LogErrorFormat("Missing {0}. {1} render pass will not execute. Check for missing reference in the renderer resources.", _scenePostProcessMaterial, GetType().Name);
                return;
            }
#endif

            int lutHeight = _sceneLookup.texture.value.height;
            int lutWidth = lutHeight * lutHeight;
            var lutParameters = new Vector4(lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f));

            CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);
            cmd.SetGlobalTexture(ShaderConstants._SceneLut, _sceneLookup.texture.value);
            cmd.SetGlobalVector(ShaderConstants._userLutParams, lutParameters);
            cmd.DrawProcedural(Matrix4x4.identity, _scenePostProcessMaterial, 0, MeshTopology.Triangles, 3);
            //cmd.SetRenderTarget(_colorAttachmentHandle.Identifier());

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_sceneColorTextureTemp.id);
        }

        static class ShaderConstants
        {
            public static readonly int _MainTex = Shader.PropertyToID("_MainTex");
            public static readonly int _userLutParams = Shader.PropertyToID("_userLutParams");
            public static readonly int _SceneLut = Shader.PropertyToID("_SceneLut");
        }
    }
}