using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.Rendering.Universal
{
    [Serializable, ReloadGroup, ExcludeFromPreset]
    public sealed class ClusterBasedDeferredRendererData : ScriptableRendererData
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

            [Reload("Shaders/Utils/GbufferError.shader")]
            public Shader gbufferErrorPS;

            [Reload("Shaders/Utils/Lighting.shader")]
            public Shader lightingPS;

#if !(UNITY_IOS || UNITY_STANDALONE_OSX)
            [Reload("Shaders/Utils/DebugCluster.shader")]
            public Shader clusterGS;
#endif

            [Reload("Shaders/Utils/HeatLight.shader")]
            public Shader heatMapPS;

            [Reload("Shaders/Utils/ClusterLights128.compute")]
            public ComputeShader clusterX128CS;

            [Reload("Shaders/Utils/ClusterLights256.compute")]
            public ComputeShader clusterX256CS;

            [Reload("Shaders/Utils/ClusterLights512.compute")]
            public ComputeShader clusterX512CS;

            [Reload("Shaders/Utils/ClusterLights1024.compute")]
            public ComputeShader clusterX1024CS;

            [Reload("Shaders/Utils/ClusterLighting.shader")]
            public Shader clusterLightingPS;
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

        /// <summary>
        /// 获取不透明物体的层级过滤
        /// </summary>
        public LayerMask opaqueLayerMask
        {
            get => _opaqueLayerMask;
            set
            {
                SetDirty();
                _opaqueLayerMask = value;
            }
        }

        /// <summary>
        /// 获取透明物体的层级过滤
        /// </summary>
        public LayerMask transparentLayerMask
        {
            get => _transparentLayerMask;
            set
            {
                SetDirty();
                _transparentLayerMask = value;
            }
        }

        /// <summary>
        /// 获取模板测试参数
        /// </summary>
        public StencilStateData defaultStencilState
        {
            get => _defaultStencilState;
            set
            {
                SetDirty();
                _defaultStencilState = value;
            }
        }

        /// <summary>
        /// 返回透明物体是否接受阴影
        /// </summary>
        public bool shadowTransparentReceive
        {
            get => _shadowTransparentReceive;
            set
            {
                SetDirty();
                _shadowTransparentReceive = value;
            }
        }

        protected override ScriptableRenderer Create()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ResourceReloader.TryReloadAllNullIn(this, UniversalRenderPipelineAsset.packagePath);
                ResourceReloader.TryReloadAllNullIn(postProcessData, UniversalRenderPipelineAsset.packagePath);
            }
#endif
            return new ClusterBasedDeferredRenderer(this);
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