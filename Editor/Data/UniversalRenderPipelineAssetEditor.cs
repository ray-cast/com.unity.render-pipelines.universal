using UnityEditor;
using UnityEditor.Rendering;
using UnityEditorInternal;

namespace UnityEngine.Rendering.Universal
{
    [CustomEditor(typeof(UniversalRenderPipelineAsset))]
    public class UniversalRenderPipelineAssetEditor : Editor
    {
        public class Styles
        {
            // Groups
            public static GUIContent generalSettingsText = EditorGUIUtility.TrTextContent("General");
            public static GUIContent qualitySettingsText = EditorGUIUtility.TrTextContent("Quality");
            public static GUIContent lightingSettingsText = EditorGUIUtility.TrTextContent("Lighting");
            public static GUIContent shadowSettingsText = EditorGUIUtility.TrTextContent("Shadows");
            public static GUIContent postProcessingSettingsText = EditorGUIUtility.TrTextContent("Post-processing");
            public static GUIContent advancedSettingsText = EditorGUIUtility.TrTextContent("Advanced");
            public static GUIContent adaptivePerformanceText = EditorGUIUtility.TrTextContent("Adaptive Performance");

            // General
            public static GUIContent rendererHeaderText = EditorGUIUtility.TrTextContent("Renderer List", "Lists all the renderers available to this Render Pipeline Asset.");
            public static GUIContent rendererDefaultText = EditorGUIUtility.TrTextContent("Default", "This renderer is currently the default for the render pipeline.");
            public static GUIContent rendererSetDefaultText = EditorGUIUtility.TrTextContent("Set Default", "Makes this renderer the default for the render pipeline.");
            public static GUIContent rendererSettingsText = EditorGUIUtility.TrIconContent("_Menu", "Opens settings for this renderer.");
            public static GUIContent rendererMissingText = EditorGUIUtility.TrIconContent("console.warnicon.sml", "Renderer missing. Click this to select a new renderer.");
            public static GUIContent rendererDefaultMissingText = EditorGUIUtility.TrIconContent("console.erroricon.sml", "Default renderer missing. Click this to select a new renderer.");
            public static GUIContent requireDepthTextureText = EditorGUIUtility.TrTextContent("Depth Texture", "If enabled the pipeline will generate camera's depth that can be bound in shaders as _CameraDepthTexture.");
            public static GUIContent requireOpaqueTextureText = EditorGUIUtility.TrTextContent("Opaque Texture", "If enabled the pipeline will copy the screen to texture after opaque objects are drawn. For transparent objects this can be bound in shaders as _CameraOpaqueTexture.");
            public static GUIContent requireTransparentTextureText = EditorGUIUtility.TrTextContent("Transparent Texture", "If enabled the pipeline will copy the screen to texture after opaque objects are drawn. For transparent objects this can be bound in shaders as _CameraOpaqueTexture.");
            public static GUIContent opaqueDownsamplingText = EditorGUIUtility.TrTextContent("Opaque Downsampling", "The downsampling method that is used for the opaque texture");
            public static GUIContent transparentDownsamplingText = EditorGUIUtility.TrTextContent("Transparent Downsampling", "The downsampling method that is used for the opaque texture");
            public static GUIContent supportsTerrainHolesText = EditorGUIUtility.TrTextContent("Terrain Holes", "When disabled, Universal Rendering Pipeline removes all Terrain hole Shader variants when you build for the Unity Player. This decreases build time.");

            // Quality
            public static GUIContent hdrText = EditorGUIUtility.TrTextContent("HDR", "Controls the global HDR settings.");
            public static GUIContent msaaText = EditorGUIUtility.TrTextContent("Anti Aliasing (MSAA)", "Controls the global anti aliasing settings.");
            public static GUIContent renderScaleText = EditorGUIUtility.TrTextContent("Render Scale", "Scales the camera render target allowing the game to render at a resolution different than native resolution. UI is always rendered at native resolution. When VR is enabled, this is overridden by XRSettings.");

            // Main light
            public static GUIContent mainLightRenderingModeText = EditorGUIUtility.TrTextContent("Main Light", "Main light is the brightest directional light.");
            public static GUIContent supportsMainLightShadowsText = EditorGUIUtility.TrTextContent("Cast Shadows", "If enabled the main light can be a shadow casting light.");
            public static GUIContent mainLightShadowmapResolutionText = EditorGUIUtility.TrTextContent("Shadow Resolution", "Resolution of the main light shadowmap texture. If cascades are enabled, cascades will be packed into an atlas and this setting controls the maximum shadows atlas resolution.");

            // Additional lights
            public static GUIContent addditionalLightsRenderingModeText = EditorGUIUtility.TrTextContent("Additional Lights", "Additional lights support.");
            public static GUIContent perObjectLimit = EditorGUIUtility.TrTextContent("Per Object Limit", "Maximum amount of additional lights. These lights are sorted and culled per-object.");
            public static GUIContent supportsAdditionalShadowsText = EditorGUIUtility.TrTextContent("Cast Shadows", "If enabled shadows will be supported for spot lights.\n");
            public static GUIContent additionalLightsShadowmapResolution = EditorGUIUtility.TrTextContent("Shadow Resolution", "All additional lights are packed into a single shadowmap atlas. This setting controls the atlas size.");

            // Deferred Lighting
            public static readonly GUIContent requireDeferredLightingText = EditorGUIUtility.TrTextContent("延迟光照", "If enabled the pipeline will copy the screen to texture after opaque objects are drawn. For transparent objects this can be bound in shaders as _CameraOpaqueTexture.");
            public static readonly GUIContent clusterPerObjectLimit = EditorGUIUtility.TrTextContent("每集群最大光源数", "Maximum amount of additional lights. These lights are sorted and culled per-object.");
            public static readonly GUIContent clusterRequireHeatMapLabel = EditorGUIUtility.TrTextContent("显示热力图");
            public static readonly GUIContent clusterRequireDrawClusterLabel = EditorGUIUtility.TrTextContent("显示光源集群体");
            public static readonly GUIContent clusterMaxDistanceLabel = EditorGUIUtility.TrTextContent("可视光源距离");

            // Shadow settings
            public static GUIContent shadowDistanceText = EditorGUIUtility.TrTextContent("Distance", "Maximum shadow rendering distance.");
            public static GUIContent shadowCascadesText = EditorGUIUtility.TrTextContent("Cascades", "Number of cascade splits used in for directional shadows");
            public static GUIContent shadowDepthBias = EditorGUIUtility.TrTextContent("Depth Bias", "Controls the distance at which the shadows will be pushed away from the light. Useful for avoiding false self-shadowing artifacts.");
            public static GUIContent shadowNormalBias = EditorGUIUtility.TrTextContent("Normal Bias", "Controls distance at which the shadow casting surfaces will be shrunk along the surface normal. Useful for avoiding false self-shadowing artifacts.");
            public static GUIContent supportsSoftShadows = EditorGUIUtility.TrTextContent("Soft Shadows", "If enabled pipeline will perform shadow filtering. Otherwise all lights that cast shadows will fallback to perform a single shadow sample.");

            // Post-processing
            public static GUIContent postProcessingFeatureSet = EditorGUIUtility.TrTextContent("Feature Set", "Sets the post-processing solution to use. To future proof your application, use Integrated instead of the comparability mode. Only use compatibility mode if your project still uses the Post-processing V2 package, but be aware that Unity plans to deprecate Post-processing V2 support for the Universal Render Pipeline in the near future.");
            public static GUIContent colorGradingMode = EditorGUIUtility.TrTextContent("Grading Mode", "Defines how color grading will be applied. Operators will react differently depending on the mode.");
            public static GUIContent colorGradingLutSize = EditorGUIUtility.TrTextContent("LUT size", "Sets the size of the internal and external color grading lookup textures (LUTs).");
            public static string postProcessingFeatureSetWarning = "Unity plans to deprecate Post-processing V2 support for the Universal Render Pipeline in the near future. You should only use this mode for compatibility purposes.";
            public static string colorGradingModeWarning = "HDR rendering is required to use the high dynamic range color grading mode. The low dynamic range will be used instead.";
            public static string colorGradingModeSpecInfo = "The high dynamic range color grading mode works best on platforms that support floating point textures.";
            public static string colorGradingLutSizeWarning = "The minimal recommended LUT size for the high dynamic range color grading mode is 32. Using lower values will potentially result in color banding and posterization effects.";
            public static string postProcessingGlobalWarning = "The Post-processing Feature Set in the URP Asset is set to Post-processing V2. This Volume component will not have any effect.";

            // Advanced settings
            public static GUIContent srpBatcher = EditorGUIUtility.TrTextContent("SRP Batcher", "If enabled, the render pipeline uses the SRP batcher.");
            public static GUIContent dynamicBatching = EditorGUIUtility.TrTextContent("Dynamic Batching", "If enabled, the render pipeline will batch drawcalls with few triangles together by copying their vertex buffers into a shared buffer on a per-frame basis.");
            public static GUIContent mixedLightingSupportLabel = EditorGUIUtility.TrTextContent("Mixed Lighting", "Makes the render pipeline include mixed-lighting Shader Variants in the build.");
            public static GUIContent debugLevel = EditorGUIUtility.TrTextContent("Debug Level", "Controls the level of debug information generated by the render pipeline. When Profiling is selected, the pipeline provides detailed profiling tags.");
            public static GUIContent shaderVariantLogLevel = EditorGUIUtility.TrTextContent("Shader Variant Log Level", "Controls the level logging in of shader variants information is outputted when a build is performed. Information will appear in the Unity console when the build finishes.");

            // Adaptive performance settings
            public static GUIContent useAdaptivePerformance = EditorGUIUtility.TrTextContent("Use adaptive performance", "Allows Adaptive Performance to adjust rendering quality during runtime");

            // Renderer List Messages
            public static GUIContent rendererListDefaultMessage =
                EditorGUIUtility.TrTextContent("Cannot remove Default Renderer",
                    "Removal of the Default Renderer is not allowed. To remove, set another Renderer to be the new Default and then remove.");

            public static GUIContent rendererMissingDefaultMessage =
                EditorGUIUtility.TrTextContent("Missing Default Renderer\nThere is no default renderer assigned, so Unity can’t perform any rendering. Set another renderer to be the new Default, or assign a renderer to the Default slot.");
            public static GUIContent rendererMissingMessage =
                EditorGUIUtility.TrTextContent("Missing Renderer(s)\nOne or more renderers are either missing or unassigned.  Switching to these renderers at runtime can cause issues.");

            // Dropdown menu options
            public static string[] mainLightOptions = { "Disabled", "Per Pixel" };
            public static string[] deferredLightOptions = { "Disabled", "Per Pixel", "Per Cluster" };
            public static string[] shadowCascadeOptions = { "No Cascades", "Two Cascades", "Four Cascades" };
            public static string[] opaqueDownsamplingOptions = { "None", "2x (Bilinear)", "4x (Box)", "4x (Bilinear)" };
        }

        SavedBool _generalSettingsFoldout;
        SavedBool _qualitySettingsFoldout;
        SavedBool _lightingSettingsFoldout;
        SavedBool _shadowSettingsFoldout;
        SavedBool _postProcessingSettingsFoldout;
        SavedBool _advancedSettingsFoldout;
        SavedBool _adaptivePerformanceFoldout;

        SerializedProperty _rendererDataProp;
        SerializedProperty _defaultRendererProp;
        ReorderableList _rendererDataList;

        SerializedProperty _requireDepthTextureProp;
        SerializedProperty _requireOpaqueTextureProp;
        SerializedProperty _requireTransparentTextureProp;
        SerializedProperty _opaqueDownsamplingProp;
        SerializedProperty _transparentDownsamplingProp;
        SerializedProperty _supportsTerrainHolesProp;

        SerializedProperty _HDR;
        SerializedProperty _MSAA;
        SerializedProperty _renderScale;

        SerializedProperty _mainLightRenderingModeProp;
        SerializedProperty _mainLightShadowsSupportedProp;
        SerializedProperty _mainLightShadowmapResolutionProp;

        SerializedProperty _additionalLightsRenderingModeProp;
        SerializedProperty _additionalLightsPerObjectLimitProp;
        SerializedProperty _additionalLightShadowsSupportedProp;
        SerializedProperty _additionalLightShadowmapResolutionProp;

        SerializedProperty _deferredLightingModeProp;
        SerializedProperty _deferredRequireClusterHeatMapProp;
        SerializedProperty _deferredRequireDrawClusterProp;
        SerializedProperty _deferredMaxLightingDistanceProp;
        SerializedProperty _deferredLightsPerClusterLimitProp;

        SerializedProperty _shadowDistanceProp;
        SerializedProperty _shadowCascadesProp;
        SerializedProperty _shadowCascade2SplitProp;
        SerializedProperty _shadowCascade4SplitProp;
        SerializedProperty _shadowDepthBiasProp;
        SerializedProperty _shadowNormalBiasProp;

        SerializedProperty _softShadowsSupportedProp;

        SerializedProperty _SRPBatcher;
        SerializedProperty _supportsDynamicBatching;
        SerializedProperty _mixedLightingSupportedProp;
        SerializedProperty _debugLevelProp;

        SerializedProperty _shaderVariantLogLevel;

        LightRenderingMode selectedLightRenderingMode;
        DeferredRenderingMode selectedDeferredLightRenderingMode;

        SerializedProperty _postProcessingFeatureSet;
        SerializedProperty _colorGradingMode;
        SerializedProperty _colorGradingLutSize;

        SerializedProperty _useAdaptivePerformance;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawGeneralSettings();
            DrawQualitySettings();
            DrawLightingSettings();
            DrawShadowSettings();
            DrawPostProcessingSettings();
            DrawAdvancedSettings();
#if ADAPTIVE_PERFORMANCE_2_0_0_OR_NEWER
            DrawAdaptivePerformance();
#endif

            serializedObject.ApplyModifiedProperties();
        }

        void OnEnable()
        {
            _generalSettingsFoldout = new SavedBool($"{target.GetType()}.GeneralSettingsFoldout", false);
            _qualitySettingsFoldout = new SavedBool($"{target.GetType()}.QualitySettingsFoldout", false);
            _lightingSettingsFoldout = new SavedBool($"{target.GetType()}.LightingSettingsFoldout", false);
            _shadowSettingsFoldout = new SavedBool($"{target.GetType()}.ShadowSettingsFoldout", false);
            _postProcessingSettingsFoldout = new SavedBool($"{target.GetType()}.PostProcessingSettingsFoldout", false);
            _advancedSettingsFoldout = new SavedBool($"{target.GetType()}.AdvancedSettingsFoldout", false);
            _adaptivePerformanceFoldout = new SavedBool($"{target.GetType()}.AdaptivePerformanceFoldout", false);

            _rendererDataProp = serializedObject.FindProperty("m_RendererDataList");
            _defaultRendererProp = serializedObject.FindProperty("m_DefaultRendererIndex");
            _rendererDataList = new ReorderableList(serializedObject, _rendererDataProp, false, true, true, true);

            DrawRendererListLayout(_rendererDataList, _rendererDataProp);

            _requireDepthTextureProp = serializedObject.FindProperty("m_RequireDepthTexture");
            _requireOpaqueTextureProp = serializedObject.FindProperty("m_RequireOpaqueTexture");
            _requireTransparentTextureProp = serializedObject.FindProperty("m_RequireTransparentTexture");
            _opaqueDownsamplingProp = serializedObject.FindProperty("m_OpaqueDownsampling");
            _transparentDownsamplingProp = serializedObject.FindProperty("m_TransparentDownsampling");
            _supportsTerrainHolesProp = serializedObject.FindProperty("m_SupportsTerrainHoles");

            _HDR = serializedObject.FindProperty("m_SupportsHDR");
            _MSAA = serializedObject.FindProperty("m_MSAA");
            _renderScale = serializedObject.FindProperty("m_RenderScale");

            _mainLightRenderingModeProp = serializedObject.FindProperty("m_MainLightRenderingMode");
            _mainLightShadowsSupportedProp = serializedObject.FindProperty("m_MainLightShadowsSupported");
            _mainLightShadowmapResolutionProp = serializedObject.FindProperty("m_MainLightShadowmapResolution");

            _additionalLightsRenderingModeProp = serializedObject.FindProperty("m_AdditionalLightsRenderingMode");
            _additionalLightsPerObjectLimitProp = serializedObject.FindProperty("m_AdditionalLightsPerObjectLimit");
            _additionalLightShadowsSupportedProp = serializedObject.FindProperty("m_AdditionalLightShadowsSupported");
            _additionalLightShadowmapResolutionProp = serializedObject.FindProperty("m_AdditionalLightsShadowmapResolution");

            _deferredLightingModeProp = serializedObject.FindProperty("_deferredLightingMode");
            _deferredRequireClusterHeatMapProp = serializedObject.FindProperty("_deferredRequireClusterHeatMap");
            _deferredRequireDrawClusterProp = serializedObject.FindProperty("_deferredRequireDrawCluster");
            _deferredMaxLightingDistanceProp = serializedObject.FindProperty("_deferredMaxLightingDistance");
            _deferredLightsPerClusterLimitProp = serializedObject.FindProperty("_deferredLightsPerClusterLimit");

            _shadowDistanceProp = serializedObject.FindProperty("m_ShadowDistance");
            _shadowCascadesProp = serializedObject.FindProperty("m_ShadowCascades");
            _shadowCascade2SplitProp = serializedObject.FindProperty("m_Cascade2Split");
            _shadowCascade4SplitProp = serializedObject.FindProperty("m_Cascade4Split");
            _shadowDepthBiasProp = serializedObject.FindProperty("m_ShadowDepthBias");
            _shadowNormalBiasProp = serializedObject.FindProperty("m_ShadowNormalBias");
            _softShadowsSupportedProp = serializedObject.FindProperty("m_SoftShadowsSupported");

            _SRPBatcher = serializedObject.FindProperty("m_UseSRPBatcher");
            _supportsDynamicBatching = serializedObject.FindProperty("m_SupportsDynamicBatching");
            _mixedLightingSupportedProp = serializedObject.FindProperty("m_MixedLightingSupported");
            _debugLevelProp = serializedObject.FindProperty("m_DebugLevel");

            _shaderVariantLogLevel = serializedObject.FindProperty("m_ShaderVariantLogLevel");

            _postProcessingFeatureSet = serializedObject.FindProperty("m_PostProcessingFeatureSet");
            _colorGradingMode = serializedObject.FindProperty("m_ColorGradingMode");
            _colorGradingLutSize = serializedObject.FindProperty("m_ColorGradingLutSize");

            _useAdaptivePerformance = serializedObject.FindProperty("m_UseAdaptivePerformance");

            selectedLightRenderingMode = (LightRenderingMode)_additionalLightsRenderingModeProp.intValue;
            selectedDeferredLightRenderingMode = (DeferredRenderingMode)_deferredLightingModeProp.intValue;
        }

        void DrawGeneralSettings()
        {
            _generalSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(_generalSettingsFoldout.value, Styles.generalSettingsText);
            if (_generalSettingsFoldout.value)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.Space();
                EditorGUI.indentLevel--;
                _rendererDataList.DoLayoutList();
                EditorGUI.indentLevel++;

                UniversalRenderPipelineAsset asset = target as UniversalRenderPipelineAsset;

                if (!asset.ValidateRendererData(-1))
                    EditorGUILayout.HelpBox(Styles.rendererMissingDefaultMessage.text, MessageType.Error, true);
                else if (!asset.ValidateRendererDataList(true))
                    EditorGUILayout.HelpBox(Styles.rendererMissingMessage.text, MessageType.Warning, true);

                EditorGUILayout.PropertyField(_requireDepthTextureProp, Styles.requireDepthTextureText);

                EditorGUILayout.PropertyField(_requireOpaqueTextureProp, Styles.requireOpaqueTextureText);
                EditorGUI.indentLevel++;
                EditorGUI.BeginDisabledGroup(!_requireOpaqueTextureProp.boolValue);
                EditorGUILayout.PropertyField(_opaqueDownsamplingProp, Styles.opaqueDownsamplingText);
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;

                EditorGUILayout.PropertyField(_requireTransparentTextureProp, Styles.requireTransparentTextureText);
                EditorGUI.indentLevel++;
                EditorGUI.BeginDisabledGroup(!_requireTransparentTextureProp.boolValue);
                EditorGUILayout.PropertyField(_transparentDownsamplingProp, Styles.transparentDownsamplingText);
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;

                EditorGUILayout.PropertyField(_supportsTerrainHolesProp, Styles.supportsTerrainHolesText);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawQualitySettings()
        {
            _qualitySettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(_qualitySettingsFoldout.value, Styles.qualitySettingsText);
            if (_qualitySettingsFoldout.value)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_HDR, Styles.hdrText);
                EditorGUILayout.PropertyField(_MSAA, Styles.msaaText);
                EditorGUI.BeginDisabledGroup(XRGraphics.enabled);
                _renderScale.floatValue = EditorGUILayout.Slider(Styles.renderScaleText, _renderScale.floatValue, UniversalRenderPipeline.minRenderScale, UniversalRenderPipeline.maxRenderScale);
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawLightingSettings()
        {
            _lightingSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(_lightingSettingsFoldout.value, Styles.lightingSettingsText);
            if (_lightingSettingsFoldout.value)
            {
                EditorGUI.indentLevel++;

                // Main Light
                bool disableGroup = false;
                CoreEditorUtils.DrawPopup(Styles.mainLightRenderingModeText, _mainLightRenderingModeProp, Styles.mainLightOptions);
				{
                    EditorGUI.indentLevel++;
                    disableGroup |= !_mainLightRenderingModeProp.boolValue;

                    EditorGUI.BeginDisabledGroup(disableGroup);
                    EditorGUILayout.PropertyField(_mainLightShadowsSupportedProp, Styles.supportsMainLightShadowsText);
                    EditorGUI.EndDisabledGroup();

                    disableGroup |= !_mainLightShadowsSupportedProp.boolValue;
                    EditorGUI.BeginDisabledGroup(disableGroup);
                    EditorGUILayout.PropertyField(_mainLightShadowmapResolutionProp, Styles.mainLightShadowmapResolutionText);
                    EditorGUI.EndDisabledGroup();

                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space();
                }

                // Additional light
                selectedLightRenderingMode = (LightRenderingMode)EditorGUILayout.EnumPopup(Styles.addditionalLightsRenderingModeText, selectedLightRenderingMode);
				{
                    _additionalLightsRenderingModeProp.intValue = (int)selectedLightRenderingMode;
                    EditorGUI.indentLevel++;

                    disableGroup = _additionalLightsRenderingModeProp.intValue == (int)LightRenderingMode.Disabled;
                    EditorGUI.BeginDisabledGroup(disableGroup);
                    _additionalLightsPerObjectLimitProp.intValue = EditorGUILayout.IntSlider(Styles.perObjectLimit, _additionalLightsPerObjectLimitProp.intValue, 0, UniversalRenderPipeline.maxPerObjectLights);
                    EditorGUI.EndDisabledGroup();

                    disableGroup |= (_additionalLightsPerObjectLimitProp.intValue == 0 || _additionalLightsRenderingModeProp.intValue != (int)LightRenderingMode.PerPixel);
                    EditorGUI.BeginDisabledGroup(disableGroup);
                    EditorGUILayout.PropertyField(_additionalLightShadowsSupportedProp, Styles.supportsAdditionalShadowsText);
                    EditorGUI.EndDisabledGroup();

                    disableGroup |= !_additionalLightShadowsSupportedProp.boolValue;
                    EditorGUI.BeginDisabledGroup(disableGroup);
                    EditorGUILayout.PropertyField(_additionalLightShadowmapResolutionProp, Styles.additionalLightsShadowmapResolution);
                    EditorGUI.EndDisabledGroup();

                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space();
                }

                // Deferred Lighting
                selectedDeferredLightRenderingMode = (DeferredRenderingMode)EditorGUILayout.EnumPopup(Styles.requireDeferredLightingText, selectedDeferredLightRenderingMode);
                {
                    _deferredLightingModeProp.intValue = (int)selectedDeferredLightRenderingMode;

                    disableGroup = _deferredLightingModeProp.intValue == (int)DeferredRenderingMode.Disabled;
                    EditorGUI.indentLevel++;

                    EditorGUI.BeginDisabledGroup(disableGroup);
                    EditorGUILayout.PropertyField(_deferredMaxLightingDistanceProp, Styles.clusterMaxDistanceLabel, true);
                    EditorGUI.EndDisabledGroup();

                    disableGroup = !(_deferredLightingModeProp.intValue == (int)DeferredRenderingMode.PerCluster);
                    EditorGUI.BeginDisabledGroup(disableGroup);
                    _deferredLightsPerClusterLimitProp.intValue = EditorGUILayout.IntSlider(Styles.clusterPerObjectLimit, _deferredLightsPerClusterLimitProp.intValue, 1, UniversalRenderPipeline.maxPerClusterLights);
                    EditorGUILayout.PropertyField(_deferredRequireClusterHeatMapProp, Styles.clusterRequireHeatMapLabel, true);
                    EditorGUILayout.PropertyField(_deferredRequireDrawClusterProp, Styles.clusterRequireDrawClusterLabel, true);
                    EditorGUI.EndDisabledGroup();

                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space();
                }

                EditorGUI.indentLevel--;

                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawShadowSettings()
        {
            _shadowSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(_shadowSettingsFoldout.value, Styles.shadowSettingsText);
            if (_shadowSettingsFoldout.value)
            {
                _shadowDistanceProp.floatValue = Mathf.Max(0.0f, EditorGUILayout.FloatField(Styles.shadowDistanceText, _shadowDistanceProp.floatValue));
                CoreEditorUtils.DrawPopup(Styles.shadowCascadesText, _shadowCascadesProp, Styles.shadowCascadeOptions);

                ShadowCascadesOption cascades = (ShadowCascadesOption)_shadowCascadesProp.intValue;
                if (cascades == ShadowCascadesOption.FourCascades)
                    EditorUtils.DrawCascadeSplitGUI<Vector3>(ref _shadowCascade4SplitProp);
                else if (cascades == ShadowCascadesOption.TwoCascades)
                    EditorUtils.DrawCascadeSplitGUI<float>(ref _shadowCascade2SplitProp);

                _shadowDepthBiasProp.floatValue = EditorGUILayout.Slider(Styles.shadowDepthBias, _shadowDepthBiasProp.floatValue, 0.0f, UniversalRenderPipeline.maxShadowBias);
                _shadowNormalBiasProp.floatValue = EditorGUILayout.Slider(Styles.shadowNormalBias, _shadowNormalBiasProp.floatValue, 0.0f, UniversalRenderPipeline.maxShadowBias);
                EditorGUILayout.PropertyField(_softShadowsSupportedProp, Styles.supportsSoftShadows);

                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawPostProcessingSettings()
        {
            _postProcessingSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(_postProcessingSettingsFoldout.value, Styles.postProcessingSettingsText);
            if (_postProcessingSettingsFoldout.value)
            {
                bool isHdrOn = _HDR.boolValue;
                bool ppv2Enabled = false;

#if POST_PROCESSING_STACK_2_0_0_OR_NEWER
                EditorGUILayout.PropertyField(m_PostProcessingFeatureSet, Styles.postProcessingFeatureSet);

                if (m_PostProcessingFeatureSet.intValue == (int)PostProcessingFeatureSet.PostProcessingV2)
                {
                    EditorGUILayout.HelpBox(Styles.postProcessingFeatureSetWarning, MessageType.Warning);
                    ppv2Enabled = true;
                }
#endif

                if (!ppv2Enabled)
                {
                    EditorGUILayout.PropertyField(_colorGradingMode, Styles.colorGradingMode);
                    if (!isHdrOn && _colorGradingMode.intValue == (int)ColorGradingMode.HighDynamicRange)
                        EditorGUILayout.HelpBox(Styles.colorGradingModeWarning, MessageType.Warning);
                    else if (isHdrOn && _colorGradingMode.intValue == (int)ColorGradingMode.HighDynamicRange)
                        EditorGUILayout.HelpBox(Styles.colorGradingModeSpecInfo, MessageType.Info);

                    EditorGUILayout.DelayedIntField(_colorGradingLutSize, Styles.colorGradingLutSize);
                    _colorGradingLutSize.intValue = Mathf.Clamp(_colorGradingLutSize.intValue, UniversalRenderPipelineAsset.k_MinLutSize, UniversalRenderPipelineAsset.k_MaxLutSize);
                    if (isHdrOn && _colorGradingMode.intValue == (int)ColorGradingMode.HighDynamicRange && _colorGradingLutSize.intValue < 32)
                        EditorGUILayout.HelpBox(Styles.colorGradingLutSizeWarning, MessageType.Warning);

                    if (GUILayout.Button("Save as Texture"))
					{
                        UniversalRenderPipelineAsset asset = target as UniversalRenderPipelineAsset;
                        asset.colorLookupBake.Invoke();
                    }
                }

                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawAdvancedSettings()
        {
            _advancedSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(_advancedSettingsFoldout.value, Styles.advancedSettingsText);
            if (_advancedSettingsFoldout.value)
            {
                EditorGUILayout.PropertyField(_SRPBatcher, Styles.srpBatcher);
                EditorGUILayout.PropertyField(_supportsDynamicBatching, Styles.dynamicBatching);
                EditorGUILayout.PropertyField(_mixedLightingSupportedProp, Styles.mixedLightingSupportLabel);
                EditorGUILayout.PropertyField(_debugLevelProp, Styles.debugLevel);
                EditorGUILayout.PropertyField(_shaderVariantLogLevel, Styles.shaderVariantLogLevel);

                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawAdaptivePerformance()
        {
            _adaptivePerformanceFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(_adaptivePerformanceFoldout.value, Styles.adaptivePerformanceText);
            if (_adaptivePerformanceFoldout.value)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_useAdaptivePerformance, Styles.useAdaptivePerformance);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawRendererListLayout(ReorderableList list, SerializedProperty prop)
        {
            list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                rect.y += 2;
                Rect indexRect = new Rect(rect.x, rect.y, 14, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(indexRect, index.ToString());
                Rect objRect = new Rect(rect.x + indexRect.width, rect.y, rect.width - 134, EditorGUIUtility.singleLineHeight);

                EditorGUI.BeginChangeCheck();
                EditorGUI.ObjectField(objRect, prop.GetArrayElementAtIndex(index), GUIContent.none);
                if (EditorGUI.EndChangeCheck())
                    EditorUtility.SetDirty(target);

                Rect defaultButton = new Rect(rect.width - 90, rect.y, 86, EditorGUIUtility.singleLineHeight);
                var defaultRenderer = _defaultRendererProp.intValue;
                GUI.enabled = index != defaultRenderer;
                if (GUI.Button(defaultButton, !GUI.enabled ? Styles.rendererDefaultText : Styles.rendererSetDefaultText))
                {
                    _defaultRendererProp.intValue = index;
                    EditorUtility.SetDirty(target);
                }
                GUI.enabled = true;

                Rect selectRect = new Rect(rect.x + rect.width - 24, rect.y, 24, EditorGUIUtility.singleLineHeight);

                UniversalRenderPipelineAsset asset = target as UniversalRenderPipelineAsset;

                if (asset.ValidateRendererData(index))
                {
                    if (GUI.Button(selectRect, Styles.rendererSettingsText))
                    {
                        Selection.SetActiveObjectWithContext(prop.GetArrayElementAtIndex(index).objectReferenceValue,
                            null);
                    }
                }
                else // Missing ScriptableRendererData
                {
                    if (GUI.Button(selectRect, index == defaultRenderer ? Styles.rendererDefaultMissingText : Styles.rendererMissingText))
                    {
                        EditorGUIUtility.ShowObjectPicker<ScriptableRendererData>(null, false, null, index);
                    }
                }

                // If object selector chose an object, assign it to the correct ScriptableRendererData slot.
                if (Event.current.commandName == "ObjectSelectorUpdated" && EditorGUIUtility.GetObjectPickerControlID() == index)
                {
                    prop.GetArrayElementAtIndex(index).objectReferenceValue = EditorGUIUtility.GetObjectPickerObject();
                }
            };

            list.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, Styles.rendererHeaderText);
                list.index = list.count - 1;
            };

            list.onCanRemoveCallback = li => { return li.count > 1; };

            list.onCanAddCallback = li => { return li.count < UniversalRenderPipeline.maxScriptableRenderers; };

            list.onRemoveCallback = li =>
            {
                if (li.serializedProperty.arraySize - 1 != _defaultRendererProp.intValue)
                {
                    if (li.serializedProperty.GetArrayElementAtIndex(li.serializedProperty.arraySize - 1).objectReferenceValue != null)
                        li.serializedProperty.DeleteArrayElementAtIndex(li.serializedProperty.arraySize - 1);
                    li.serializedProperty.arraySize--;
                    li.index = li.count - 1;
                }
                else
                {
                    EditorUtility.DisplayDialog(Styles.rendererListDefaultMessage.text, Styles.rendererListDefaultMessage.tooltip,
                        "Close");
                }
                EditorUtility.SetDirty(target);
            };
        }
    }
}