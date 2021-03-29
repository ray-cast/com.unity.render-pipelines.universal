using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    public class DrawObjectsPass : ScriptableRenderPass
    {
        private bool _isOpaque;

        private string _profilerTag;
        private ProfilingSampler _profilingSampler;

        private FilteringSettings _filteringSettings;
        private RenderStateBlock _renderStateBlock;
        private List<ShaderTagId> _shaderTagIdList = new List<ShaderTagId>();

        public delegate void RefAction(ref CommandBuffer cmd, ref RenderingData renderingData);

        public static event RefAction ConfigureOpaqueAction;
        public static event RefAction ConfigureTransparentAction;

        public static event RefAction DrawOpaqueAction;
        public static event RefAction DrawTransparentAction;

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

        public DrawObjectsPass(string profilerTag, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, int stencilReference)
        {
            _profilerTag = profilerTag;
            _profilingSampler = new ProfilingSampler(profilerTag);
            _shaderTagIdList.Add(new ShaderTagId("UniversalForward"));
            _shaderTagIdList.Add(new ShaderTagId("LightweightForward"));
            _shaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
            renderPassEvent = evt;

            _filteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            _isOpaque = opaque;

            if (stencilState.enabled)
            {
                _renderStateBlock.stencilReference = stencilReference;
                _renderStateBlock.mask = RenderStateMask.Stencil;
                _renderStateBlock.stencilState = stencilState;
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);

            using (new ProfilingScope(cmd, _profilingSampler))
            {
                Vector4 drawObjectPassData = new Vector4(0.0f, 0.0f, 0.0f, (_isOpaque) ? 1.0f : 0.0f);
                cmd.SetGlobalVector(ShaderConstants._DrawObjectPassDataPropID, drawObjectPassData);

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

                Camera camera = renderingData.cameraData.camera;
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

        static class ShaderConstants
        {
            public static readonly int _DrawObjectPassDataPropID = Shader.PropertyToID("_DrawObjectPassData");

            public static readonly Color clearColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        }
    }
}