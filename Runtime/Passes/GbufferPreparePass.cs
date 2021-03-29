using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public class GbufferPreparePass : ScriptableRenderPass
    {
        private bool _isOpaque;

        private string _profilerTag;
        private ProfilingSampler _profilingSampler;

        private FilteringSettings _filteringSettings;
        private RenderStateBlock _renderStateBlock;
        private List<ShaderTagId> _shaderTagIdList;

        private RenderTargetIdentifier[] _colorAttachments;
        private RenderTextureDescriptor[] _colorDescriptor;

        private RenderTargetHandle[] _colorAttachmentHandle { get; set; }
        private RenderTargetHandle _depthAttachmentHandle { get; set; }

        public delegate void RefAction(ref CommandBuffer cmd, ref RenderingData renderingData);

        public static event RefAction ConfigureOpaqueAction;
        public static event RefAction ConfigureTransparentAction;

        public static event RefAction DrawOpaqueAction;
        public static event RefAction DrawTransparentAction;

        public GbufferPreparePass(string profilerTag, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, int stencilReference)
        {
            renderPassEvent = evt;

            _profilerTag = profilerTag;
            _profilingSampler = new ProfilingSampler(profilerTag);
            _shaderTagIdList = new List<ShaderTagId>();
            _shaderTagIdList.Add(new ShaderTagId("Deferred"));

            _filteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            _isOpaque = opaque;

            _colorAttachments = new RenderTargetIdentifier[4];
            _colorDescriptor = new RenderTextureDescriptor[4];

            if (stencilState.enabled)
            {
                _renderStateBlock.stencilReference = stencilReference;
                _renderStateBlock.mask = RenderStateMask.Stencil;
                _renderStateBlock.stencilState = stencilState;
            }
        }

        RenderTextureDescriptor GetStereoCompatibleDescriptor(RenderTextureDescriptor cameraTextureDescriptor, int width, int height, GraphicsFormat format, int depthBufferBits = 0)
        {
            var desc = cameraTextureDescriptor;
            desc.depthBufferBits = depthBufferBits;
            desc.msaaSamples = 1;
            desc.width = width;
            desc.height = height;
            desc.graphicsFormat = format;

            return desc;
        }

        public void Setup(RenderTextureDescriptor cameraTextureDescriptor, RenderTargetHandle[] colorAttachmentHandle, RenderTargetHandle depthAttachmentHandle)
        {
            var width = cameraTextureDescriptor.width;
            var height = cameraTextureDescriptor.height;

            this._colorDescriptor[0] = GetStereoCompatibleDescriptor(cameraTextureDescriptor, width, height, GraphicsFormat.R8G8B8A8_UNorm);
            this._colorDescriptor[1] = GetStereoCompatibleDescriptor(cameraTextureDescriptor, width, height, GraphicsFormat.R8G8B8A8_UNorm);
            this._colorDescriptor[2] = GetStereoCompatibleDescriptor(cameraTextureDescriptor, width, height, GraphicsFormat.A2B10G10R10_UNormPack32);
            this._colorDescriptor[3] = GetStereoCompatibleDescriptor(cameraTextureDescriptor, width, height, GraphicsFormat.R8G8B8A8_UNorm);

            _colorAttachmentHandle = colorAttachmentHandle;
            _depthAttachmentHandle = depthAttachmentHandle;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            cmd.GetTemporaryRT(_colorAttachmentHandle[0].id, _colorDescriptor[0], FilterMode.Point);
            cmd.GetTemporaryRT(_colorAttachmentHandle[1].id, _colorDescriptor[1], FilterMode.Point);
            cmd.GetTemporaryRT(_colorAttachmentHandle[2].id, _colorDescriptor[2], FilterMode.Point);
            cmd.GetTemporaryRT(_colorAttachmentHandle[3].id, _colorDescriptor[3], FilterMode.Point);

            this._colorAttachments[0] = _colorAttachmentHandle[0].Identifier();
            this._colorAttachments[1] = _colorAttachmentHandle[1].Identifier();
            this._colorAttachments[2] = _colorAttachmentHandle[2].Identifier();
            this._colorAttachments[3] = _colorAttachmentHandle[3].Identifier();

            ConfigureTarget(_colorAttachments, _depthAttachmentHandle.Identifier());
            ConfigureClear(ClearFlag.Color, ShaderConstants.clearColor);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);

            using (new ProfilingScope(cmd, _profilingSampler))
            {
                cmd.SetGlobalVector(ShaderConstants._DrawObjectPassDataPropID, ShaderConstants.drawObjectPassData);

                if (_isOpaque)
                {
                    if (ConfigureOpaqueAction != null)
                        ConfigureOpaqueAction(ref cmd, ref renderingData);
                }
                else
                {
                    if (ConfigureTransparentAction != null)
                        ConfigureTransparentAction(ref cmd, ref renderingData);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var sortFlags = (_isOpaque) ? renderingData.cameraData.defaultOpaqueSortFlags : SortingCriteria.CommonTransparent;
                var drawSettings = CreateDrawingSettings(_shaderTagIdList, ref renderingData, sortFlags);
                var filterSettings = _filteringSettings;

#if UNITY_EDITOR
                if (renderingData.cameraData.isPreviewCamera)
                {
                    filterSettings.layerMask = -1;
                }
#endif

                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings, ref _renderStateBlock);

                if (_isOpaque)
                {
                    if (DrawOpaqueAction != null)
                        DrawOpaqueAction(ref cmd, ref renderingData);
                }
                else
                {
                    if (DrawTransparentAction != null)
                        DrawTransparentAction(ref cmd, ref renderingData);
                }
            }

            context.ExecuteCommandBuffer(cmd);

            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            for (int i = 0; i < _colorAttachmentHandle.Length; i++)
                cmd.ReleaseTemporaryRT(_colorAttachmentHandle[i].id);
        }

        static class ShaderConstants
        {
            public static readonly int _DrawObjectPassDataPropID = Shader.PropertyToID("_DrawObjectPassData");

            public static readonly Color clearColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            public static readonly Vector4 drawObjectPassData = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
        }
    }
}