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

            [Reload("Shaders/Utils/CapsuleShadow.shader")]
            public Shader capsuleShadowPS;

            [Reload("Shaders/Utils/ScreenSpaceShadows.shader")]
            public Shader screenSpaceShadowPS;

            [Reload("Shaders/Utils/ScreenSpaceOcclusion.shader")]
            public Shader screenSpaceOcclusionPS;

            [Reload("Shaders/Utils/Sampling.shader")]
            public Shader samplingPS;

            [Reload("Shaders/Utils/FallbackError.shader")]
            public Shader fallbackErrorPS;

            [Reload("Shaders/Utils/Lighting.shader")]
            public Shader lightingPS;

#if UNITY_EDITOR && !(UNITY_IOS || UNITY_STANDALONE_OSX)
            [Reload("Shaders/Utils/DebugCluster.shader")]
            public Shader clusterGS;
#endif

#if UNITY_EDITOR
            [Reload("Shaders/Utils/HeatLight.shader")]
            public Shader heatMapPS;
#endif

            [Reload("Shaders/Utils/HIZ.compute")]
            public ComputeShader HizCS;

            [Reload("Shaders/Utils/ClusterLights.compute")]
            public ComputeShader clusterCS;

            [Reload("Shaders/Utils/ClusterLighting.shader")]
            public Shader clusterLightingPS;

            [Reload("Shaders/Environment/MipFog.shader")]
            public Shader mipFogPS;

            [Reload("Shaders/Environment/HeightFog.shader")]
            public Shader heightFogPS;

            [Reload("Shaders/Sky/HDRiSky.shader")]
            public Shader hdriSkyPS;

            [Reload("Shaders/Sky/GradientSky.shader")]
            public Shader gradientSkyPS;

            [Reload("Shaders/Sky/ImageBasedLighting.shader")]
            public Shader imageBasedLightingPS;

            [Reload("Skybox/Cubemap", ReloadAttribute.Package.Builtin)]
            public Shader skyboxCubemapPS;

            [Reload("Shaders/Utils/RVT.shader")]
            public Shader virtualTexturePS;
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