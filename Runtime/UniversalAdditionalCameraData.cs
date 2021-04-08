using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;

using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Holds information about whether to override certain camera rendering options from the render pipeline asset.
    /// When set to <c>Off</c> option will be disabled regardless of what is set on the pipeline asset.
    /// When set to <c>On</c> option will be enabled regardless of what is set on the pipeline asset.
    /// When set to <c>UsePipelineSetting</c> value set in the <see cref="UniversalRenderPipelineAsset"/>.
    /// </summary>
    public enum CameraOverrideOption
    {
        Off,
        On,
        UsePipelineSettings,
    }

    public enum CameraDeferredLightingOption
    {
        Off,
        PerPixel,
        PerCluster,
        UsePipelineSettings,
    }

    /// <summary>
    /// Holds information about the post-processing anti-aliasing mode.
    /// When set to <c>None</c> no post-processing anti-aliasing pass will be performed.
    /// When set to <c>Fast</c> a fast approximated anti-aliasing pass will render when resolving the camera to screen.
    /// When set to <c>SubpixelMorphologicalAntiAliasing</c> SMAA pass will render when resolving the camera to screen. You can choose the SMAA quality by setting <seealso cref="AntialiasingQuality"/>
    /// </summary>
    public enum AntialiasingMode
    {
        None,
        FastApproximateAntialiasing,
        SubpixelMorphologicalAntiAliasing,
        //TemporalAntialiasing
    }

    /// <summary>
    /// Holds information about the render type of a camera. Options are Base or Overlay.
    /// Base rendering type allows the camera to render to either the screen or to a texture.
    /// Overlay rendering type allows the camera to render on top of a previous camera output, thus compositing camera results.
    /// </summary>
    public enum CameraRenderType
    {
        Base,
        Overlay,
    }

    /// <summary>
    /// Holds information about the output target for a camera.
    /// Only used for cameras of render type Base. <seealso cref="CameraRenderType"/>.
    /// </summary>
    [Obsolete("This enum is deprecated.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public enum CameraOutput
    {
        Screen,
        Texture,

        [Obsolete("Use CameraOutput.Screen instead.", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        Camera = Screen,
    }

    /// <summary>
    /// Controls SMAA anti-aliasing quality.
    /// </summary>
    public enum AntialiasingQuality
    {
        Low,
        Medium,
        High
    }

    /// <summary>
    /// Contains extension methods for Camera class.
    /// </summary>
    public static class CameraExtensions
    {
        /// <summary>
        /// Universal Render Pipeline exposes additional rendering data in a separate component.
        /// This method returns the additional data component for the given camera or create one if it doesn't exists yet.
        /// </summary>
        /// <param name="camera"></param>
        /// <returns>The <c>UniversalAdditinalCameraData</c> for this camera.</returns>
        /// <see cref="UniversalAdditionalCameraData"/>
        public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera)
        {
            var gameObject = camera.gameObject;
            bool componentExists = gameObject.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData);
            if (!componentExists)
                cameraData = gameObject.AddComponent<UniversalAdditionalCameraData>();

            return cameraData;
        }
    }

    public static class CameraTypeUtility
    {
        static string[] s_CameraTypeNames = Enum.GetNames(typeof(CameraRenderType)).ToArray();

        public static string GetName(this CameraRenderType type)
        {
            int typeInt = (int)type;
            if (typeInt < 0 || typeInt >= s_CameraTypeNames.Length)
                typeInt = (int)CameraRenderType.Base;
            return s_CameraTypeNames[typeInt];
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [ImageEffectAllowedInSceneView]
    public class UniversalAdditionalCameraData : MonoBehaviour, ISerializationCallbackReceiver
    {
        [Tooltip("If enabled shadows will render for this camera.")]
        [FormerlySerializedAs("renderShadows"), SerializeField]
        bool m_RenderShadows = true;

        [Tooltip("If enabled lights will render for this camera.")]
        [FormerlySerializedAs("requireLightData"), SerializeField]
        bool m_RequireLightData = true;

        [Tooltip("If enabled depth texture will render for this camera bound as _CameraDepthTexture.")]
        [SerializeField]
        CameraOverrideOption m_RequiresDepthTextureOption = CameraOverrideOption.UsePipelineSettings;

        [Tooltip("If enabled opaque color texture will render for this camera and bound as _CameraOpaqueTexture.")]
        [SerializeField]
        CameraOverrideOption m_RequiresOpaqueTextureOption = CameraOverrideOption.UsePipelineSettings;

        [Tooltip("If enabled transparent color texture will render for this camera and bound as _CameraTransparentTexture.")]
        [SerializeField]
        CameraOverrideOption m_RequiresTransparentTextureOption = CameraOverrideOption.UsePipelineSettings;

        [Tooltip("If enabled deferred lighting will render for this camera bound as Cluster Based Deferred Lighting.")]
        [SerializeField]
        CameraDeferredLightingOption m_DeferredLightingModeOption = CameraDeferredLightingOption.UsePipelineSettings;

        [SerializeField]
        CameraOverrideOption m_RequiresHeatMapOption = CameraOverrideOption.UsePipelineSettings;

        [SerializeField]
        CameraOverrideOption m_RequiresDrawClusterOption = CameraOverrideOption.UsePipelineSettings;

        [SerializeField] CameraRenderType m_CameraType = CameraRenderType.Base;
        [SerializeField] List<Camera> m_Cameras = new List<Camera>();
        [SerializeField] int m_RendererIndex = -1;

        [SerializeField] LayerMask m_VolumeLayerMask = 1; // "Default"
        [SerializeField] Transform m_VolumeTrigger = null;

        [SerializeField] bool m_RenderPostProcessing = false;
        [SerializeField] AntialiasingMode m_Antialiasing = AntialiasingMode.None;
        [SerializeField] AntialiasingQuality m_AntialiasingQuality = AntialiasingQuality.High;
        [SerializeField] bool m_StopNaN = false;
        [SerializeField] bool m_Dithering = false;
        [SerializeField] bool m_ClearDepth = true;

        // Deprecated:
        [FormerlySerializedAs("requiresDepthTexture"), SerializeField]
        bool m_RequiresDepthTexture = false;

        [FormerlySerializedAs("requiresColorTexture"), SerializeField]
        bool m_RequiresColorTexture = false;

        [FormerlySerializedAs("requiresTransparentTexture"), SerializeField]
        bool m_RequiresTransparentTexture = false;

        [FormerlySerializedAs("requiresHeatMap"), SerializeField]
        bool m_RequiresHeatMap = false;

        [FormerlySerializedAs("requiresDrawCluster"), SerializeField]
        bool m_RequiresDrawCluster = false;

        [FormerlySerializedAs("deferredRenderingMode"), SerializeField]
        DeferredRenderingMode m_DeferredLightingMode = DeferredRenderingMode.Disabled;

        [HideInInspector] [SerializeField]
        float m_Version = 2;

        public float version => m_Version;

        static UniversalAdditionalCameraData s_DefaultAdditionalCameraData = null;
        internal static UniversalAdditionalCameraData defaultAdditionalCameraData
        {
            get
            {
#pragma warning disable UNT0010 // Component instance creation
                if (s_DefaultAdditionalCameraData == null)
					s_DefaultAdditionalCameraData = new UniversalAdditionalCameraData();
#pragma warning restore UNT0010 // Component instance creation

				return s_DefaultAdditionalCameraData;
            }
        }

        /// <summary>
        /// Controls if this camera should render shadows.
        /// </summary>
        public bool requireLightData
        {
            get => m_RequireLightData;
            set => m_RequireLightData = value;
        }

        /// <summary>
        /// Controls if this camera should render shadows.
        /// </summary>
        public bool renderShadows
        {
            get => m_RenderShadows;
            set => m_RenderShadows = value;
        }

        /// <summary>
        /// Controls if a camera should render depth.
        /// The depth is available to be bound in shaders as _CameraDepthTexture.
        /// <seealso cref="CameraOverrideOption"/>
        /// </summary>
        public CameraOverrideOption requiresDepthOption
        {
            get => m_RequiresDepthTextureOption;
            set => m_RequiresDepthTextureOption = value;
        }

        /// <summary>
        /// Controls if a camera should copy the color contents of a camera after rendering opaques.
        /// The color texture is available to be bound in shaders as _CameraOpaqueTexture.
        /// </summary>
        public CameraOverrideOption requiresColorOption
        {
            get => m_RequiresOpaqueTextureOption;
            set => m_RequiresOpaqueTextureOption = value;
        }

        public CameraOverrideOption requiresTransparentOption
        {
            get => m_RequiresTransparentTextureOption;
            set => m_RequiresTransparentTextureOption = value;
        }       

        public CameraDeferredLightingOption requiresDeferredLightingOption
        {
            get => m_DeferredLightingModeOption;
            set => m_DeferredLightingModeOption = value;
        }

        public CameraOverrideOption requireHeatMapOption
        {
            get => m_RequiresHeatMapOption;
            set => m_RequiresHeatMapOption = value;
        }

        public CameraOverrideOption requireDrawClusterOption
        {
            get => m_RequiresDrawClusterOption;
            set => m_RequiresDrawClusterOption = value;
        }

        /// <summary>
        /// Returns the camera renderType.
        /// <see cref="CameraRenderType"/>.
        /// </summary>
        public CameraRenderType renderType
        {
            get => m_CameraType;
            set => m_CameraType = value;
        }

        #region deprecated
        /// <summary>
        /// Returns the camera output type. Only valid for Base cameras.
        /// <see cref="CameraOutput"/>.
        /// <seealso cref="CameraRenderType"/>.
        /// <seealso cref="Camera"/>
        /// </summary>
        [Obsolete("CameraOutput has been deprecated. Use Camera.targetTexture instead.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public CameraOutput cameraOutput
        {
            get
            {
                if (gameObject.TryGetComponent<Camera>(out var camera))
				{
                    if (camera.targetTexture)
                        return CameraOutput.Texture;
                }

                return CameraOutput.Screen;
            }
            set { }
        }

        [Obsolete("AddCamera has been deprecated. You can add cameras to the stack by calling <c>cameraStack</c> property and modifying the camera stack list.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void AddCamera(Camera camera)
        {
            m_Cameras.Add(camera);
        }

        [Obsolete("cameras property has been deprecated. Use cameraStack property instead.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public List<Camera> cameras => cameraStack;
        #endregion

        /// <summary>
        /// Returns the camera stack. Only valid for Base cameras.
        /// Overlay cameras have no stack and will return null.
        /// <seealso cref="CameraRenderType"/>.
        /// </summary>
        public List<Camera> cameraStack
        {
            get
            {
                if (renderType != CameraRenderType.Base)
                {
                    var camera = gameObject.GetComponent<Camera>();
                    Debug.LogWarning(string.Format("{0}: This camera is of {1} type. Only Base cameras can have a camera stack.", camera.name, renderType));
                    return null;
                }

                if (scriptableRenderer.supportedRenderingFeatures.cameraStacking == false)
                {
                    var camera = gameObject.GetComponent<Camera>();
                    Debug.LogWarning(string.Format("{0}: This camera has a ScriptableRenderer that doesn't support camera stacking. Camera stack is null.", camera.name));
                    return null;
                }

                return m_Cameras;
            }
        }

        public void UpdateCameraStack()
        {
#if UNITY_EDITOR
            Undo.RecordObject(this, "Update camera stack");
#endif
            int prev = m_Cameras.Count;
            m_Cameras.RemoveAll(cam => cam == null);
            int curr = m_Cameras.Count;
            int removedCamsCount = prev - curr;
            if (removedCamsCount != 0)
            {
                Debug.LogWarning(name + ": " + removedCamsCount + " camera overlay" + (removedCamsCount > 1 ? "s" : "") + " no longer exists and will be removed from the camera stack.");
            }
        }

        /// <summary>
        /// If true, this camera will clear depth value before rendering. Only valid for Overlay cameras.
        /// </summary>
        public bool clearDepth
        {
            get => m_ClearDepth;
        }

        /// <summary>
        /// Returns true if this camera needs to render depth information in a texture.
        /// If enabled, depth texture is available to be bound and read from shaders as _CameraDepthTexture after rendering skybox.
        /// </summary>
        public bool requiresDepthTexture
        {
            get
            {
                if (m_RequiresDepthTextureOption == CameraOverrideOption.UsePipelineSettings)
                {
                    return UniversalRenderPipeline.asset.supportsCameraDepthTexture;
                }
                else
                {
                    return m_RequiresDepthTextureOption == CameraOverrideOption.On;
                }
            }
            set { m_RequiresDepthTextureOption = (value) ? CameraOverrideOption.On : CameraOverrideOption.Off; }
        }

        /// <summary>
        /// Returns true if this camera requires to color information in a texture.
        /// If enabled, color texture is available to be bound and read from shaders as _CameraOpaqueTexture after rendering skybox.
        /// </summary>
        public bool requiresColorTexture
        {
            get
            {
                if (m_RequiresOpaqueTextureOption == CameraOverrideOption.UsePipelineSettings)
                {
                    return UniversalRenderPipeline.asset.supportsCameraOpaqueTexture;
                }
                else
                {
                    return m_RequiresOpaqueTextureOption == CameraOverrideOption.On;
                }
            }
            set { m_RequiresOpaqueTextureOption = (value) ? CameraOverrideOption.On : CameraOverrideOption.Off; }
        }

        public bool requiresTransparentTexture
        {
            get
            {
                if (m_RequiresTransparentTextureOption == CameraOverrideOption.UsePipelineSettings)
                {
                    return UniversalRenderPipeline.asset.supportsCameraTransparentTexture;
                }
                else
                {
                    return m_RequiresTransparentTextureOption == CameraOverrideOption.On;
                }
            }
            set { m_RequiresTransparentTextureOption = (value) ? CameraOverrideOption.On : CameraOverrideOption.Off; }
        }

        public DeferredRenderingMode deferredLightingMode
        {
            get
            {
                if (m_DeferredLightingModeOption == CameraDeferredLightingOption.UsePipelineSettings)
                {
                    return UniversalRenderPipeline.asset.deferredLightingMode;
                }
                else
                {
                    if (m_DeferredLightingModeOption == CameraDeferredLightingOption.PerPixel)
                        return DeferredRenderingMode.PerPixel;
                    else if (m_DeferredLightingModeOption == CameraDeferredLightingOption.PerCluster)
                        return DeferredRenderingMode.PerCluster;
                    else
                        return DeferredRenderingMode.Disabled;
                }
            }
            set
            {
                if (value == DeferredRenderingMode.PerPixel)
                    m_DeferredLightingModeOption = CameraDeferredLightingOption.PerPixel;
                else if (value == DeferredRenderingMode.PerCluster)
                    m_DeferredLightingModeOption = CameraDeferredLightingOption.PerCluster;
                else
                    m_DeferredLightingModeOption = CameraDeferredLightingOption.Off;
            }
        }

        public bool requiresHeatMap
        {
            get
            {
                if (m_RequiresHeatMapOption == CameraOverrideOption.UsePipelineSettings)
                {
                    return UniversalRenderPipeline.asset.requireHeatMap;
                }
                else
                {
                    return m_RequiresHeatMapOption == CameraOverrideOption.On;
                }
            }
            set { m_RequiresHeatMapOption = (value) ? CameraOverrideOption.On : CameraOverrideOption.Off; }
        }

        public bool requiresDrawCluster
        {
            get
            {
                if (m_RequiresDrawClusterOption == CameraOverrideOption.UsePipelineSettings)
                {
                    return UniversalRenderPipeline.asset.requireDrawCluster;
                }
                else
                {
                    return m_RequiresDrawClusterOption == CameraOverrideOption.On;
                }
            }
            set { m_RequiresDrawClusterOption = (value) ? CameraOverrideOption.On : CameraOverrideOption.Off; }
        }

        /// <summary>
        /// Returns the <see cref="ScriptableRenderer"/> that is used to render this camera.
        /// </summary>
        public ScriptableRenderer scriptableRenderer
        {
            get => UniversalRenderPipeline.asset.GetRenderer(m_RendererIndex);
        }

        /// <summary>
        /// Use this to set this Camera's current <see cref="ScriptableRenderer"/> to one listed on the Render Pipeline Asset. Takes an index that maps to the list on the Render Pipeline Asset.
        /// </summary>
        /// <param name="index">The index that maps to the RendererData list on the currently assigned Render Pipeline Asset</param>
        public void SetRenderer(int index)
        {
            m_RendererIndex = index;
        }

        public LayerMask volumeLayerMask
        {
            get => m_VolumeLayerMask;
            set => m_VolumeLayerMask = value;
        }

        public Transform volumeTrigger
        {
            get => m_VolumeTrigger;
            set => m_VolumeTrigger = value;
        }

        /// <summary>
        /// Returns true if this camera should render post-processing.
        /// </summary>
        public bool renderPostProcessing
        {
            get => m_RenderPostProcessing;
            set => m_RenderPostProcessing = value;
        }

        /// <summary>
        /// Returns the current anti-aliasing mode used by this camera.
        /// <see cref="AntialiasingMode"/>.
        /// </summary>
        public AntialiasingMode antialiasing
        {
            get => m_Antialiasing;
            set => m_Antialiasing = value;
        }

        /// <summary>
        /// Returns the current anti-aliasing quality used by this camera.
        /// <seealso cref="antialiasingQuality"/>.
        /// </summary>
        public AntialiasingQuality antialiasingQuality
        {
            get => m_AntialiasingQuality;
            set => m_AntialiasingQuality = value;
        }

        public bool stopNaN
        {
            get => m_StopNaN;
            set => m_StopNaN = value;
        }

        public bool dithering
        {
            get => m_Dithering;
            set => m_Dithering = value;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (version <= 1)
            {
                m_RequiresDepthTextureOption = (m_RequiresDepthTexture) ? CameraOverrideOption.On : CameraOverrideOption.Off;
                m_RequiresOpaqueTextureOption = (m_RequiresColorTexture) ? CameraOverrideOption.On : CameraOverrideOption.Off;
                m_RequiresTransparentTextureOption = (m_RequiresTransparentTexture) ? CameraOverrideOption.On : CameraOverrideOption.Off;
                m_RequiresHeatMapOption = (m_RequiresHeatMap) ? CameraOverrideOption.On : CameraOverrideOption.Off;
                m_RequiresDrawClusterOption = (m_RequiresDrawCluster) ? CameraOverrideOption.On : CameraOverrideOption.Off;

                if (m_DeferredLightingMode == DeferredRenderingMode.PerPixel)
                    m_DeferredLightingModeOption = CameraDeferredLightingOption.PerPixel;
                else if (m_DeferredLightingMode == DeferredRenderingMode.PerCluster)
                    m_DeferredLightingModeOption = CameraDeferredLightingOption.PerCluster;
                else
                    m_DeferredLightingModeOption = CameraDeferredLightingOption.Off;
            }
        }

        public void OnDrawGizmos()
        {
            string path = "Packages/com.unity.render-pipelines.universal/Editor/Gizmos/";
            string gizmoName = "";
            Color tint = Color.white;

            if (m_CameraType == CameraRenderType.Base)
            {
                gizmoName = $"{path}Camera_Base.png";
            }
            else if (m_CameraType == CameraRenderType.Overlay)
            {
                gizmoName = $"{path}Camera_Overlay.png";
            }

#if UNITY_2019_2_OR_NEWER
#if UNITY_EDITOR
            if (Selection.activeObject == gameObject)
            {
                // Get the preferences selection color
                tint = SceneView.selectedOutlineColor;
            }
#endif
            if (!string.IsNullOrEmpty(gizmoName))
            {
                Gizmos.DrawIcon(transform.position, gizmoName, true, tint);
            }

            if (renderPostProcessing)
            {
                Gizmos.DrawIcon(transform.position, $"{path}Camera_PostProcessing.png", true, tint);
            }
#else
            if (renderPostProcessing)
            {
                Gizmos.DrawIcon(transform.position, $"{path}Camera_PostProcessing.png");
            }
            Gizmos.DrawIcon(transform.position, gizmoName);
#endif
        }
    }
}