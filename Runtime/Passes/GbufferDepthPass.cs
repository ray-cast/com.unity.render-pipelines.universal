using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    public class GbufferDepthPass : ScriptableRenderPass
    {
        private string _profilerTag;
        private ProfilingSampler _profilingSampler;

        private FilteringSettings _filteringSettings;
        private RenderStateBlock _renderStateBlock;
        private List<ShaderTagId> _shaderTagIdList = new List<ShaderTagId>();

        static List<ShaderTagId> _LegacyShaderPassNames = new List<ShaderTagId>()
        {
            new ShaderTagId("Always"),
            new ShaderTagId("ForwardBase"),
            new ShaderTagId("PrepassBase"),
            new ShaderTagId("Vertex"),
            new ShaderTagId("VertexLMRGBM"),
            new ShaderTagId("VertexLM"),
        };

        static Material s_ErrorMaterial;
        static Material errorMaterial
        {
            get
            {
                if (s_ErrorMaterial == null)
                {
                    try
                    {
                        s_ErrorMaterial = new Material(Shader.Find("Hidden/Universal Render Pipeline/FallbackError"));
                    }
                    catch { }
                }

                return s_ErrorMaterial;
            }
        }

        private static readonly int s_DrawObjectPassDataPropID = Shader.PropertyToID("_DrawObjectPassData");

        public GbufferDepthPass(string profilerTag, RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, int stencilReference)
        {
            _profilerTag = profilerTag;
            _profilingSampler = new ProfilingSampler(profilerTag);
            _shaderTagIdList.Add(new ShaderTagId("Deferred"));
            renderPassEvent = evt;

            _filteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

            if (stencilState.enabled)
            {
                _renderStateBlock.stencilReference = stencilReference;
                _renderStateBlock.mask = RenderStateMask.Stencil;
                _renderStateBlock.stencilState = stencilState;
            }
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);

            using (new ProfilingScope(cmd, _profilingSampler))
            {
                Vector4 drawObjectPassData = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
                cmd.SetGlobalVector(s_DrawObjectPassDataPropID, drawObjectPassData);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                Camera camera = renderingData.cameraData.camera;
                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(_shaderTagIdList, ref renderingData, sortFlags);
                var filterSettings = _filteringSettings;

#if UNITY_EDITOR
                if (renderingData.cameraData.isPreviewCamera)
                {
                    filterSettings.layerMask = -1;
                }
#endif

                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings, ref _renderStateBlock);

                if (errorMaterial)
                {
                    SortingSettings sortingSettings = new SortingSettings(camera) { criteria = sortFlags };
                    DrawingSettings errorSettings = new DrawingSettings(_LegacyShaderPassNames[0], sortingSettings)
                    {
                        perObjectData = PerObjectData.None,
                        overrideMaterial = errorMaterial,
                        overrideMaterialPassIndex = 0
                    };

                    for (int i = 1; i < _LegacyShaderPassNames.Count; ++i)
                        errorSettings.SetShaderPassName(i, _LegacyShaderPassNames[i]);

                    context.DrawRenderers(renderingData.cullResults, ref errorSettings, ref filterSettings);
                }
                else
                {
                    Debug.LogErrorFormat("Missing {0}. {1} render pass will not execute. Check for missing reference in the renderer resources.", errorMaterial, GetType().Name);
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}