using System;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public class GbufferPreparePass : ScriptableRenderPass
    {
        private int _width;
        private int _height;

        private bool _isOpaque;

        private string _profilerTag;
        private ProfilingSampler _profilingSampler;

        private FilteringSettings _filteringSettings;
        private RenderStateBlock _renderStateBlock;
        private List<ShaderTagId> _shaderTagIdList;

        private RenderTargetHandle[] bufferAttachmentHandle { get; set; }
        private RenderTargetIdentifier[] bufferAttachments { get; set; }
        private RenderTextureDescriptor[] bufferDescriptor { get; set; }

        private RenderTargetHandle _depthAttachmentHandle { get; set; }

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

            bufferAttachments = new RenderTargetIdentifier[4];
            bufferDescriptor = new RenderTextureDescriptor[4];

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
            this._width = cameraTextureDescriptor.width;
            this._height = cameraTextureDescriptor.height;
            this.bufferAttachmentHandle = colorAttachmentHandle;
            this._depthAttachmentHandle = depthAttachmentHandle;

            this.bufferDescriptor = new RenderTextureDescriptor[4];
            bufferDescriptor[0] = GetStereoCompatibleDescriptor(cameraTextureDescriptor, this._width, this._height, GraphicsFormat.R8G8B8A8_UNorm);
            bufferDescriptor[1] = GetStereoCompatibleDescriptor(cameraTextureDescriptor, this._width, this._height, GraphicsFormat.R8G8B8A8_UNorm);
            bufferDescriptor[2] = GetStereoCompatibleDescriptor(cameraTextureDescriptor, this._width, this._height, GraphicsFormat.A2B10G10R10_UNormPack32);
            bufferDescriptor[3] = GetStereoCompatibleDescriptor(cameraTextureDescriptor, this._width, this._height, GraphicsFormat.R8G8B8A8_UNorm);

            _depthAttachmentHandle = depthAttachmentHandle;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            cmd.GetTemporaryRT(bufferAttachmentHandle[0].id, bufferDescriptor[0], FilterMode.Point);
            cmd.GetTemporaryRT(bufferAttachmentHandle[1].id, bufferDescriptor[1], FilterMode.Point);
            cmd.GetTemporaryRT(bufferAttachmentHandle[2].id, bufferDescriptor[2], FilterMode.Point);
            cmd.GetTemporaryRT(bufferAttachmentHandle[3].id, bufferDescriptor[3], FilterMode.Point);

            this.bufferAttachments[0] = bufferAttachmentHandle[0].Identifier();
            this.bufferAttachments[1] = bufferAttachmentHandle[1].Identifier();
            this.bufferAttachments[2] = bufferAttachmentHandle[2].Identifier();
            this.bufferAttachments[3] = bufferAttachmentHandle[3].Identifier();

            ConfigureTarget(bufferAttachments, _depthAttachmentHandle.Identifier());
            ConfigureClear(ClearFlag.All, new Color(0, 0, 0, 0));
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);

            using (new ProfilingScope(cmd, _profilingSampler))
            {
                Vector4 drawObjectPassData = new Vector4(0.0f, 0.0f, 0.0f, (_isOpaque) ? 1.0f : 0.0f);
                cmd.SetGlobalVector(ShaderConstants._DrawObjectPassDataPropID, drawObjectPassData);
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
            }

            context.ExecuteCommandBuffer(cmd);

            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");

            for (int i = 0; i < bufferAttachmentHandle.Length; i++)
                cmd.ReleaseTemporaryRT(bufferAttachmentHandle[i].id);
        }

        static class ShaderConstants
        {
            public static readonly int _DrawObjectPassDataPropID = Shader.PropertyToID("_DrawObjectPassData");
        }
    }
}