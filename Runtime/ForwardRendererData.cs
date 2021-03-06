using System;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
#endif

namespace UnityEngine.Rendering.Universal
{
    [Serializable, ReloadGroup, ExcludeFromPreset]
    public class ForwardRendererData : ScriptableRendererData
    {
        [Serializable, ReloadGroup]
        public sealed class ShaderResources
        {
            [Reload("Shaders/Utils/Blit.shader")]
            public Shader blitPS;

            [Reload("Shaders/Utils/CopyDepth.shader")]
            public Shader copyDepthPS;

            [Reload("Shaders/Utils/ScreenSpaceShadows.shader")]
            public Shader screenSpaceShadowPS;

            [Reload("Shaders/Utils/Sampling.shader")]
            public Shader samplingPS;

            [Reload("Shaders/Utils/FallbackError.shader")]
            public Shader fallbackErrorPS;
        }

        [Reload("Runtime/Data/PostProcessData.asset")]
        public PostProcessData postProcessData = null;

        public ShaderResources shaders = null;

        [SerializeField]
        internal LayerMask _opaqueLayerMask = -1;
        [SerializeField]
        internal LayerMask _transparentLayerMask = -1;
        [SerializeField]
        internal StencilStateData _defaultStencilState = new StencilStateData();
        [SerializeField]
        internal bool _shadowTransparentReceive = true;

        protected override ScriptableRenderer Create()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ResourceReloader.TryReloadAllNullIn(this, UniversalRenderPipelineAsset.packagePath);
                ResourceReloader.TryReloadAllNullIn(postProcessData, UniversalRenderPipelineAsset.packagePath);
            }
#endif
            return new ForwardRenderer(this);
        }

        public LayerMask opaqueLayerMask
        {
            get => _opaqueLayerMask;
            set
            {
                SetDirty();
                _opaqueLayerMask = value;
            }
        }

        public LayerMask transparentLayerMask
        {
            get => _transparentLayerMask;
            set
            {
                SetDirty();
                _transparentLayerMask = value;
            }
        }

        public StencilStateData defaultStencilState
        {
            get => _defaultStencilState;
            set
            {
                SetDirty();
                _defaultStencilState = value;
            }
        }

        public bool shadowTransparentReceive
        {
            get => _shadowTransparentReceive;
            set
            {
                SetDirty();
                _shadowTransparentReceive = value;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (shaders == null)
                return;

#if UNITY_EDITOR
            ResourceReloader.TryReloadAllNullIn(this, UniversalRenderPipelineAsset.packagePath);
            ResourceReloader.TryReloadAllNullIn(postProcessData, UniversalRenderPipelineAsset.packagePath);
#endif
        }
    }
}