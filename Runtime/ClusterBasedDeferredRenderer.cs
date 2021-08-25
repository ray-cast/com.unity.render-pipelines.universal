namespace UnityEngine.Rendering.Universal
{
    public sealed class ClusterBasedDeferredRenderer : ScriptableRenderer
    {
        const int k_DepthStencilBufferBits = 32;
        const string k_CreateCameraTextures = "Create Camera Texture";

        GlobalSettingsPass _globalSettingsPass;

        DepthOnlyPass _depthOnlyPass;
        DepthPrePass _depthPrePass;
        HizPass _hizPass;
        FeedbackPass _feedbackPass;
        MainLightShadowCasterPass _mainLightShadowCasterPass;
        AdditionalLightsShadowCasterPass _additionalLightsShadowCasterPass;
        CharacterShadowPass _characterShadowPass;
        ScreenSpaceShadowResolvePass _screenSpaceShadowResolvePass;
        ScreenSpaceOcclusionResolvePass _screenSpaceOcclusionResolvePass;
        CapsuleShadowPass _capsuleShadowResolvePass;
        GbufferDepthPass _renderGbufferDepthPass;
        GbufferPreparePass _renderOpaqueGbufferPass;
        CopyDepthPass _copyGbufferDepthPass;
        ClusterSettingPass _clusterSettingPass;
        ClusterLightingPass _clusterOpaqueLightingPass;
        DrawObjectsPass _renderOpaqueForwardPass;
        DrawLightsPass _deferredLightingPass;
        DrawSkyboxPass _drawSkyboxPass;
        CopyColorPass _copyOpaqueColorPass;
        CopyColorPass _copyTransparentColorPass;
        CopyDepthPass _copyDepthPass;
        TransparentSettingsPass _transparentSettingsPass;
        TransparentDepthPrepass _renderTransparentDepthPass;
        DrawObjectsPass _renderTransparentForwardPass;
        InvokeOnRenderObjectCallbackPass _onRenderObjectCallbackPass;
        ColorGradingLutPass _colorGradingLutPass;
        VignetteLookupEffect _scenePostProcessPass;
        PostProcessPass _postProcessPass;
        PostProcessPass _finalPostProcessPass;
        CapturePass _capturePass;
        FinalBlitPass _finalBlitPass;

#if UNITY_EDITOR
#if !(UNITY_IOS || UNITY_STANDALONE_OSX)
        DrawClusterPass _drawClusterPass;
#endif
        ClusterHeatPass _drawClusterHeatPass;
        SceneViewDepthCopyPass _sceneViewDepthCopyPass;
#endif

        RenderTargetHandle _cameraColorAttachment;
        RenderTargetHandle _cameraDepthAttachment;
        RenderTargetHandle _cameraDepthTexture;
        RenderTargetHandle _cameraOpaqueTexture;
        RenderTargetHandle _cameraTransparentTexture;
        RenderTargetHandle _afterPostProcessColor;
        RenderTargetHandle _colorGradingLut;
        RenderTargetHandle[] _cameraGbufferAttachments;

        RenderTargetHandle _activeCameraColorAttachment;
        RenderTargetHandle _activeCameraDepthAttachment;
        RenderTargetHandle _activeCameraDepthTexture;

        ForwardLights _forwardLights;

        StencilState _defaultStencilState;

        Material _blitMaterial;
        Material _copyDepthMaterial;
        Material _clusterLightingMaterial;
        Material _lightingMaterial;
        Material _screenSpaceShadowMaterial;
        Material _screenSpaceOcclusionMaterial;
        Material _capsuleShadowMaterial;
        Material _feedbackMaterial;
        Material _drawLookupMaterial;
        Material _samplingMaterial;
        Material _debugCluster;
        Material _heatMapCluster;

        bool _supportsClusterLighting;

        public ClusterBasedDeferredRenderer(ClusterBasedDeferredRendererData data) : base(data)
        {
            _blitMaterial = CoreUtils.CreateEngineMaterial(data.shaders.blitPS);
            _copyDepthMaterial = CoreUtils.CreateEngineMaterial(data.shaders.copyDepthPS);
            _samplingMaterial = CoreUtils.CreateEngineMaterial(data.shaders.samplingPS);
            _clusterLightingMaterial = CoreUtils.CreateEngineMaterial(data.shaders.clusterLightingPS);
            _lightingMaterial = CoreUtils.CreateEngineMaterial(data.shaders.lightingPS);
            _screenSpaceShadowMaterial = CoreUtils.CreateEngineMaterial(data.shaders.screenSpaceShadowPS);
            _screenSpaceOcclusionMaterial = CoreUtils.CreateEngineMaterial(data.shaders.screenSpaceOcclusionPS);
            _capsuleShadowMaterial = CoreUtils.CreateEngineMaterial(data.shaders.capsuleShadowPS);
            _feedbackMaterial = CoreUtils.CreateEngineMaterial(data.shaders.feedbackPS);
            _drawLookupMaterial = CoreUtils.CreateEngineMaterial(data.shaders.drawLookupPS);

#if UNITY_EDITOR && !(UNITY_IOS || UNITY_STANDALONE_OSX)
            _debugCluster = CoreUtils.CreateEngineMaterial(data.shaders.clusterGS);
#endif
#if UNITY_EDITOR
            _heatMapCluster = CoreUtils.CreateEngineMaterial(data.shaders.heatMapPS);
#endif

            StencilStateData stencilData = data.defaultStencilState;
            _defaultStencilState = StencilState.defaultValue;
            _defaultStencilState.enabled = stencilData.overrideStencilState;
            _defaultStencilState.SetCompareFunction(stencilData.stencilCompareFunction);
            _defaultStencilState.SetPassOperation(stencilData.passOperation);
            _defaultStencilState.SetFailOperation(stencilData.failOperation);
            _defaultStencilState.SetZFailOperation(stencilData.zFailOperation);

            _forwardLights = new ForwardLights();

            _globalSettingsPass = new GlobalSettingsPass(RenderPassEvent.BeforeRendering, data.postProcessData);
            _mainLightShadowCasterPass = new MainLightShadowCasterPass(RenderPassEvent.BeforeRenderingShadows);
            _screenSpaceShadowResolvePass = new ScreenSpaceShadowResolvePass(RenderPassEvent.BeforeRenderingOpaques, _screenSpaceShadowMaterial);
            _screenSpaceOcclusionResolvePass = new ScreenSpaceOcclusionResolvePass(RenderPassEvent.BeforeRenderingOpaques, _screenSpaceOcclusionMaterial);
            _capsuleShadowResolvePass = new CapsuleShadowPass(RenderPassEvent.BeforeRenderingOpaques, _capsuleShadowMaterial);
            _additionalLightsShadowCasterPass = new AdditionalLightsShadowCasterPass(RenderPassEvent.BeforeRenderingShadows);
            _characterShadowPass = new CharacterShadowPass(RenderPassEvent.BeforeRenderingShadows);
            _depthOnlyPass = new DepthOnlyPass(RenderPassEvent.BeforeRenderingPrepasses, RenderQueueRange.opaque, data.opaqueLayerMask);
            _depthPrePass = new DepthPrePass(RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, data.opaqueLayerMask);
            _renderGbufferDepthPass = new GbufferDepthPass("G-Buffer Depth", RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, data.opaqueLayerMask, _defaultStencilState, stencilData.stencilReference);
            _renderOpaqueGbufferPass = new GbufferPreparePass("G-Buffer Prepare", true, RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, data.opaqueLayerMask, _defaultStencilState, stencilData.stencilReference);
            _copyGbufferDepthPass = new CopyDepthPass(RenderPassEvent.BeforeRenderingOpaques, _copyDepthMaterial);
            _deferredLightingPass = new DrawLightsPass(RenderPassEvent.BeforeRenderingOpaques, _lightingMaterial);
            _renderOpaqueForwardPass = new DrawObjectsPass("Render Opaques", true, RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, data.opaqueLayerMask, _defaultStencilState, stencilData.stencilReference);
            _drawSkyboxPass = new DrawSkyboxPass(RenderPassEvent.BeforeRenderingOpaques, data.shaders);
            _copyDepthPass = new CopyDepthPass(RenderPassEvent.AfterRenderingOpaques, _copyDepthMaterial);
            _feedbackPass = new FeedbackPass(RenderPassEvent.BeforeRenderingOpaques, data.opaqueLayerMask, _feedbackMaterial, _drawLookupMaterial);
            _hizPass = new HizPass(RenderPassEvent.AfterRenderingOpaques, data.shaders.HizCS);
            _copyOpaqueColorPass = new CopyColorPass(RenderPassEvent.AfterRenderingSkybox, _samplingMaterial);
            _copyTransparentColorPass = new CopyColorPass(RenderPassEvent.BeforeRenderingTransparents, _samplingMaterial);
            _transparentSettingsPass = new TransparentSettingsPass(RenderPassEvent.BeforeRenderingTransparents, data.shadowTransparentReceive);
            _renderTransparentDepthPass = new TransparentDepthPrepass(RenderPassEvent.BeforeRenderingTransparents, RenderQueueRange.transparent, data.transparentLayerMask);
            _renderTransparentForwardPass = new DrawObjectsPass("Render Transparents", false, RenderPassEvent.BeforeRenderingTransparents, RenderQueueRange.transparent, data.transparentLayerMask, _defaultStencilState, stencilData.stencilReference);
            _onRenderObjectCallbackPass = new InvokeOnRenderObjectCallbackPass(RenderPassEvent.BeforeRenderingPostProcessing);
            _colorGradingLutPass = new ColorGradingLutPass(RenderPassEvent.BeforeRenderingPrepasses, data.postProcessData);
            _scenePostProcessPass = new VignetteLookupEffect(RenderPassEvent.BeforeRenderingOpaques, data.postProcessData);
            _postProcessPass = new PostProcessPass(RenderPassEvent.BeforeRenderingPostProcessing, data.postProcessData, _blitMaterial);
            _finalPostProcessPass = new PostProcessPass(RenderPassEvent.AfterRendering + 1, data.postProcessData, _blitMaterial);
            _capturePass = new CapturePass(RenderPassEvent.AfterRendering);
            _finalBlitPass = new FinalBlitPass(RenderPassEvent.AfterRendering + 1, _blitMaterial);

            _supportsClusterLighting = (SystemInfo.supportsComputeShaders && data.shaders.clusterCS) ? true : false;
            if (_supportsClusterLighting)
			{
                _clusterSettingPass = new ClusterSettingPass(RenderPassEvent.BeforeRenderingOpaques, _forwardLights, data.shaders.clusterCS);
                _clusterOpaqueLightingPass = new ClusterLightingPass(RenderPassEvent.BeforeRenderingOpaques, _clusterLightingMaterial);
#if UNITY_EDITOR && !(UNITY_IOS || UNITY_STANDALONE_OSX)
                _drawClusterPass = new DrawClusterPass(RenderPassEvent.BeforeRenderingOpaques, _debugCluster);
#endif
#if UNITY_EDITOR
                _drawClusterHeatPass = new ClusterHeatPass(RenderPassEvent.AfterRenderingSkybox, _heatMapCluster);
#endif
            }

#if UNITY_EDITOR
            _sceneViewDepthCopyPass = new SceneViewDepthCopyPass(RenderPassEvent.AfterRendering + 9, _copyDepthMaterial);
#endif

            _cameraColorAttachment.Init("_CameraColorTexture");
            _cameraDepthAttachment.Init("_CameraDepthAttachment");
            _cameraOpaqueTexture.Init("_CameraOpaqueTexture");
            _cameraTransparentTexture.Init("_CameraTransparentTexture");
            _cameraDepthTexture.Init("_CameraDepthTexture");
            _afterPostProcessColor.Init("_AfterPostProcessTexture");
            _colorGradingLut.Init("_InternalGradingLut");

            _cameraGbufferAttachments = new RenderTargetHandle[4];
            for (int i = 0; i < 4; i++)
                _cameraGbufferAttachments[i].Init("_CameraGBufferTexture" + i);

            supportedRenderingFeatures = new RenderingFeatures()
            {
                cameraStacking = true,
            };

            SkyManager.instance.Build(data.shaders);
        }

        protected override void Dispose(bool disposing)
        {
            _postProcessPass?.Cleanup();
            _finalPostProcessPass?.Cleanup();
            _colorGradingLutPass?.Cleanup();
            _clusterSettingPass?.Cleanup();

            CoreUtils.Destroy(_blitMaterial);
            CoreUtils.Destroy(_copyDepthMaterial);
            CoreUtils.Destroy(_lightingMaterial);
            CoreUtils.Destroy(_screenSpaceShadowMaterial);
            CoreUtils.Destroy(_clusterLightingMaterial);
            CoreUtils.Destroy(_samplingMaterial);
#if UNITY_EDITOR && !(UNITY_IOS || UNITY_STANDALONE_OSX)
            CoreUtils.Destroy(_debugCluster);
#endif
#if UNITY_EDITOR
            CoreUtils.Destroy(_heatMapCluster);
#endif
        }

        public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            ref CameraData cameraData = ref renderingData.cameraData;
            RenderTextureDescriptor cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            bool isOffscreenDepthTexture = cameraData.targetTexture != null && cameraData.targetTexture.format == RenderTextureFormat.Depth;
            if (isOffscreenDepthTexture)
            {
                ConfigureCameraTarget(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.CameraTarget);

                for (int i = 0; i < rendererFeatures.Count; ++i)
                {
                    if (rendererFeatures[i].isActive)
                        rendererFeatures[i].AddRenderPasses(this, ref renderingData);
                }

                if (cameraData.deferredLightingMode != DeferredRenderingMode.Disabled)
                    EnqueuePass(_renderGbufferDepthPass);

                EnqueuePass(_renderOpaqueForwardPass);
                EnqueuePass(_drawSkyboxPass);
#if ADAPTIVE_PERFORMANCE_2_1_0_OR_NEWER
                if (!needTransparencyPass)
                    return;
#endif
                EnqueuePass(_renderTransparentForwardPass);
                return;
            }

            bool isSceneViewCamera = cameraData.isSceneViewCamera;
            bool isPreviewCamera = cameraData.isPreviewCamera;
            bool requiresForwardPrepass = true;
            bool isStereoEnabled = cameraData.isStereoEnabled;

            EnqueuePass(_globalSettingsPass);

            bool mainLightShadows = _mainLightShadowCasterPass.Setup(ref renderingData, HizPass.GetAverageLinearDepth(ref camera));
            if (mainLightShadows)
                EnqueuePass(_mainLightShadowCasterPass);

            bool additionalLightShadows = _additionalLightsShadowCasterPass.Setup(ref renderingData);
            if (additionalLightShadows)
                EnqueuePass(_additionalLightsShadowCasterPass);

            if (mainLightShadows && _characterShadowPass.Setup(ref renderingData))
                EnqueuePass(_characterShadowPass);

            bool isRunningHololens = false;
#if ENABLE_VR && ENABLE_VR_MODULE
            isRunningHololens = UniversalRenderPipeline.IsRunningHololens(camera);
#endif

            bool createColorTexture = RequiresIntermediateColorTexture(ref cameraData);
            createColorTexture |= (rendererFeatures.Count != 0 && !isRunningHololens);
            createColorTexture &= !isPreviewCamera;
            createColorTexture |= (cameraData.deferredLightingMode != DeferredRenderingMode.Disabled);

            bool createDepthTexture = cameraData.requiresDepthTexture;
            createDepthTexture |= (cameraData.renderType == CameraRenderType.Base && !cameraData.resolveFinalTarget);
            createDepthTexture |= (cameraData.deferredLightingMode != DeferredRenderingMode.Disabled);

#if UNITY_ANDROID || UNITY_WEBGL
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan)
            {
                // GLES can not use render texture's depth buffer with the color buffer of the backbuffer
                // in such case we create a color texture for it too.
                createColorTexture |= createDepthTexture;
            }
#endif

            if (cameraData.renderType == CameraRenderType.Base)
            {
                _activeCameraColorAttachment = createColorTexture ? _cameraColorAttachment : RenderTargetHandle.CameraTarget;
                _activeCameraDepthAttachment = createDepthTexture ? _cameraDepthAttachment : RenderTargetHandle.CameraTarget;
                _activeCameraDepthTexture = _cameraDepthTexture;

                bool intermediateRenderTexture = createColorTexture || createDepthTexture;
                CreateCameraRenderTarget(context, ref renderingData.cameraData);

                int backbufferMsaaSamples = (intermediateRenderTexture) ? 1 : cameraTargetDescriptor.msaaSamples;
                if (Camera.main == camera && camera.cameraType == CameraType.Game && cameraData.targetTexture == null)
                    SetupBackbufferFormat(backbufferMsaaSamples, isStereoEnabled);
            }
            else
            {
                _activeCameraColorAttachment = _cameraColorAttachment;
                _activeCameraDepthAttachment = _cameraDepthAttachment;
                _activeCameraDepthTexture = _cameraDepthTexture;
            }

            _depthPrePass.Setup(cameraTargetDescriptor, _activeCameraDepthAttachment);
            EnqueuePass(_depthPrePass);

            if (cameraData.deferredLightingMode != DeferredRenderingMode.Disabled)
            {
                _renderOpaqueGbufferPass.Setup(cameraTargetDescriptor, _cameraGbufferAttachments, _activeCameraDepthAttachment);
                EnqueuePass(_renderOpaqueGbufferPass);

                if (renderingData.cameraData.requiresDepthTexture && createDepthTexture)
                {
                    _copyGbufferDepthPass.Setup(_activeCameraDepthAttachment, _cameraDepthTexture);
                    EnqueuePass(_copyGbufferDepthPass);
                }
            }

            if (createDepthTexture)
			{
                if (_screenSpaceOcclusionResolvePass.Setup(cameraTargetDescriptor, _activeCameraDepthAttachment))
                    EnqueuePass(_screenSpaceOcclusionResolvePass);

                if (_capsuleShadowResolvePass.Setup(cameraTargetDescriptor, _activeCameraDepthAttachment))
                    EnqueuePass(_capsuleShadowResolvePass);

                _screenSpaceShadowResolvePass.Setup(cameraTargetDescriptor, _activeCameraDepthAttachment);
                EnqueuePass(_screenSpaceShadowResolvePass);
            }

            if (camera.cameraType != CameraType.Preview && renderingData.cameraData.renderType != CameraRenderType.Overlay)
			{
                if (_feedbackPass.Setup(cameraTargetDescriptor))
                    EnqueuePass(_feedbackPass);
            }

            if (cameraData.deferredLightingMode != DeferredRenderingMode.Disabled)
			{
                bool requireClusterLighting = _supportsClusterLighting;
                requireClusterLighting &= renderingData.lightData.visibleLights.Length > 5;
                requireClusterLighting &= cameraData.deferredLightingMode == DeferredRenderingMode.PerCluster;
                
                if (requireClusterLighting)
                {
                    _clusterSettingPass.Setup(cameraData.requireDrawCluster);
                    EnqueuePass(_clusterSettingPass);

                    _clusterOpaqueLightingPass.Setup(_activeCameraColorAttachment, _activeCameraDepthAttachment);
                    EnqueuePass(_clusterOpaqueLightingPass);
                }
                else
                {
                    _deferredLightingPass.Setup(_activeCameraColorAttachment, _activeCameraDepthAttachment);
                    EnqueuePass(_deferredLightingPass);
                }
            }

            ConfigureCameraTarget(_activeCameraColorAttachment.Identifier(), _activeCameraDepthAttachment.Identifier());

            for (int i = 0; i < rendererFeatures.Count; ++i)
            {
                if (rendererFeatures[i].isActive)
                    rendererFeatures[i].AddRenderPasses(this, ref renderingData);
            }

            int count = activeRenderPassQueue.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                if (activeRenderPassQueue[i] == null)
                    activeRenderPassQueue.RemoveAt(i);
            }

            bool isOverlayCamera = cameraData.renderType == CameraRenderType.Overlay;
            if (camera.clearFlags == CameraClearFlags.Skybox && !isOverlayCamera)
			{
                _drawSkyboxPass.Setup(_activeCameraColorAttachment, _activeCameraDepthAttachment);
                EnqueuePass(_drawSkyboxPass);
            }

            if (cameraData.postProcessEnabled)
            {
                if (_scenePostProcessPass.Setup(cameraTargetDescriptor, _activeCameraColorAttachment))
                    EnqueuePass(_scenePostProcessPass);

                _colorGradingLutPass.Setup(_colorGradingLut);
                EnqueuePass(_colorGradingLutPass);
            }

            if (requiresForwardPrepass)
                EnqueuePass(_renderOpaqueForwardPass);

#if UNITY_EDITOR && !(UNITY_IOS || UNITY_STANDALONE_OSX)
            if (SystemInfo.supportsGeometryShaders && _supportsClusterLighting && cameraData.requireDrawCluster && _clusterSettingPass.clusterData.clusterDimXYZ > 0)
            {
                _drawClusterPass.Setup(_clusterSettingPass.clusterData.clusterDimXYZ, cameraData.requireDrawCluster);
                EnqueuePass(_drawClusterPass);
            }
#endif
#if UNITY_EDITOR
            if (_supportsClusterLighting && cameraData.renderType == CameraRenderType.Base && cameraData.requireHeatMap && _clusterSettingPass.clusterData.clusterDimXYZ > 0)
            {
                _drawClusterHeatPass.ConfigureTarget(this.cameraColorTarget);
                EnqueuePass(_drawClusterHeatPass);
            }
#endif

            if (renderingData.cameraData.requiresDepthTexture && createDepthTexture)
            {
                _copyDepthPass.Setup(_activeCameraDepthAttachment, _cameraDepthTexture);
                EnqueuePass(_copyDepthPass);
            }
			
            if (createDepthTexture)
			{
                _hizPass.Setup(_cameraDepthTexture);
                EnqueuePass(_hizPass);
            }

            if (renderingData.cameraData.requiresOpaqueTexture)
            {
                Downsampling downsamplingMethod = UniversalRenderPipeline.asset.opaqueDownsampling;
                _copyOpaqueColorPass.Setup(_activeCameraColorAttachment.Identifier(), _cameraOpaqueTexture, downsamplingMethod);
                EnqueuePass(_copyOpaqueColorPass);
            }

            bool transparentsNeedSettingsPass = _transparentSettingsPass.Setup(ref renderingData);
            if (transparentsNeedSettingsPass)
                EnqueuePass(_transparentSettingsPass);

            _renderTransparentDepthPass.Setup(_activeCameraDepthAttachment);
            EnqueuePass(_renderTransparentDepthPass);

            _renderTransparentForwardPass.ConfigureTarget(_activeCameraColorAttachment.Identifier(), _activeCameraDepthAttachment.Identifier());
            EnqueuePass(_renderTransparentForwardPass);

            if (renderingData.cameraData.requiresTransparentTexture)
            {
                Downsampling downsamplingMethod = UniversalRenderPipeline.asset.transparentDownsampling;
                _copyTransparentColorPass.Setup(_activeCameraColorAttachment.Identifier(), _cameraTransparentTexture, downsamplingMethod);
                EnqueuePass(_copyTransparentColorPass);
            }

            EnqueuePass(_onRenderObjectCallbackPass);

            if (cameraData.postProcessEnabled)
            {
                bool anyPostProcessing = renderingData.postProcessingEnabled;

                bool lastCameraInTheStack = cameraData.resolveFinalTarget;
                if (lastCameraInTheStack)
                {
                    bool hasCaptureActions = renderingData.cameraData.captureActions != null && lastCameraInTheStack;
                    bool applyFinalPostProcessing = anyPostProcessing && lastCameraInTheStack && renderingData.cameraData.antialiasing == AntialiasingMode.FastApproximateAntialiasing;
                    bool hasPassesAfterPostProcessing = activeRenderPassQueue.Find(x => x.renderPassEvent == RenderPassEvent.AfterRendering) != null;

                    bool resolvePostProcessingToCameraTarget = !hasCaptureActions && !hasPassesAfterPostProcessing && !applyFinalPostProcessing;
                    var destination = resolvePostProcessingToCameraTarget ? RenderTargetHandle.CameraTarget : _afterPostProcessColor;

                    bool doSRGBConvertion = resolvePostProcessingToCameraTarget;
                    _postProcessPass.Setup(cameraTargetDescriptor, _cameraColorAttachment, destination, _cameraDepthAttachment, _colorGradingLut, applyFinalPostProcessing, doSRGBConvertion);
                    EnqueuePass(_postProcessPass);

                    if (renderingData.cameraData.captureActions != null)
                    {
                        _capturePass.Setup(_cameraColorAttachment);
                        EnqueuePass(_capturePass);
                    }

                    var sourceForFinalPass = _afterPostProcessColor;
                    if (applyFinalPostProcessing)
                    {
                        _finalPostProcessPass.SetupFinalPass(_afterPostProcessColor);
                        EnqueuePass(_finalPostProcessPass);
                    }

                    bool cameraTargetResolved = applyFinalPostProcessing || !hasPassesAfterPostProcessing || _cameraColorAttachment == RenderTargetHandle.CameraTarget;
                    if (!cameraTargetResolved)
                    {
                        _finalBlitPass.Setup(cameraTargetDescriptor, _afterPostProcessColor);
                        EnqueuePass(_finalBlitPass);
                    }
                }
                else
                {
                    _postProcessPass.Setup(cameraTargetDescriptor, _cameraColorAttachment, _afterPostProcessColor, _cameraDepthAttachment, _colorGradingLut, false, false);
                    EnqueuePass(_postProcessPass);
                }
            }
            else
            {
                _finalBlitPass.Setup(cameraTargetDescriptor, _activeCameraColorAttachment);
                EnqueuePass(_finalBlitPass);
            }

#if UNITY_EDITOR
            if (isSceneViewCamera)
            {
                UnityEngine.Assertions.Assert.IsTrue(cameraData.resolveFinalTarget, "Editor camera must resolve target upon finish rendering.");
                _sceneViewDepthCopyPass.Setup(_cameraDepthTexture);
                EnqueuePass(_sceneViewDepthCopyPass);
            }
#endif
        }

        public override void SetupLights(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            _forwardLights.Setup(context, ref renderingData);
        }

        public override void SetupCullingParameters(ref ScriptableCullingParameters cullingParameters, ref CameraData cameraData)
        {
            bool isShadowCastingDisabled = !UniversalRenderPipeline.asset.supportsMainLightShadows && !UniversalRenderPipeline.asset.supportsAdditionalLightShadows;
            bool isShadowDistanceZero = Mathf.Approximately(cameraData.maxShadowDistance, 0.0f);
            if (isShadowCastingDisabled || isShadowDistanceZero)
            {
                cullingParameters.cullingOptions &= ~CullingOptions.ShadowCasters;
            }

            cullingParameters.maximumVisibleLights = UniversalRenderPipeline.maxVisibleAdditionalLights + 1;
            cullingParameters.shadowDistance = Mathf.Min(HizPass.GetAverageLinearDepth(ref cameraData.camera), cameraData.maxShadowDistance);
        }

        public override void FinishRendering(CommandBuffer cmd)
        {
            if (_activeCameraColorAttachment != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_activeCameraColorAttachment.id);
                _activeCameraColorAttachment = RenderTargetHandle.CameraTarget;
            }

            if (_activeCameraDepthAttachment != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_activeCameraDepthAttachment.id);
                _activeCameraDepthAttachment = RenderTargetHandle.CameraTarget;
            }

            if (_activeCameraDepthTexture != RenderTargetHandle.CameraTarget)
			{
                cmd.ReleaseTemporaryRT(_activeCameraDepthTexture.id);
                _activeCameraDepthTexture = RenderTargetHandle.CameraTarget;
            }
        }

        void CreateCameraRenderTarget(ScriptableRenderContext context, ref CameraData cameraData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(k_CreateCameraTextures);

            var descriptor = cameraData.cameraTargetDescriptor;
            int msaaSamples = descriptor.msaaSamples;

            if (_activeCameraColorAttachment != RenderTargetHandle.CameraTarget)
            {
                bool useDepthRenderBuffer = _activeCameraDepthAttachment == RenderTargetHandle.CameraTarget;
                var colorDescriptor = descriptor;
                colorDescriptor.useMipMap = false;
                colorDescriptor.autoGenerateMips = false;
                colorDescriptor.depthBufferBits = (useDepthRenderBuffer) ? k_DepthStencilBufferBits : 0;
                cmd.GetTemporaryRT(_activeCameraColorAttachment.id, colorDescriptor, FilterMode.Bilinear);
            }

            if (_activeCameraDepthAttachment != RenderTargetHandle.CameraTarget)
            {
                var depthDescriptor = descriptor;
                depthDescriptor.useMipMap = false;
                depthDescriptor.autoGenerateMips = false;
                depthDescriptor.colorFormat = RenderTextureFormat.Depth;
                depthDescriptor.depthBufferBits = k_DepthStencilBufferBits;
                cmd.GetTemporaryRT(_activeCameraDepthAttachment.id, depthDescriptor, FilterMode.Point);
            }

            if (_activeCameraDepthTexture != RenderTargetHandle.CameraTarget)
			{
                var colorDepthDescriptor = descriptor;
                colorDepthDescriptor.colorFormat = RenderTextureFormat.Depth;
                colorDepthDescriptor.depthBufferBits = k_DepthStencilBufferBits;
                colorDepthDescriptor.msaaSamples = 1;
                cmd.GetTemporaryRT(_activeCameraDepthTexture.id, colorDepthDescriptor, FilterMode.Point);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void SetupBackbufferFormat(int msaaSamples, bool stereo)
        {
#if ENABLE_VR && ENABLE_VR_MODULE
            if (!stereo)
                return;

            bool msaaSampleCountHasChanged = false;
            int currentQualitySettingsSampleCount = QualitySettings.antiAliasing;
            if (currentQualitySettingsSampleCount != msaaSamples &&
                !(currentQualitySettingsSampleCount == 0 && msaaSamples == 1))
            {
                msaaSampleCountHasChanged = true;
            }

            if (msaaSampleCountHasChanged)
            {
                QualitySettings.antiAliasing = msaaSamples;
                XR.XRDevice.UpdateEyeTextureMSAASetting();
            }
#endif
        }

        bool PlatformRequiresExplicitMsaaResolve()
        {
            return !SystemInfo.supportsMultisampleAutoResolve && !(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal && Application.isMobilePlatform);
        }

        bool RequiresIntermediateColorTexture(ref CameraData cameraData)
        {
            if (cameraData.renderType == CameraRenderType.Base && !cameraData.resolveFinalTarget)
                return true;

            bool isSceneViewCamera = cameraData.isSceneViewCamera;
            var cameraTargetDescriptor = cameraData.cameraTargetDescriptor;
            int msaaSamples = cameraTargetDescriptor.msaaSamples;
            bool isStereoEnabled = cameraData.isStereoEnabled;
            bool isScaledRender = !Mathf.Approximately(cameraData.renderScale, 1.0f) && !cameraData.isStereoEnabled;
            bool isCompatibleBackbufferTextureDimension = cameraTargetDescriptor.dimension == TextureDimension.Tex2D;
            bool requiresExplicitMsaaResolve = msaaSamples > 1 && !SystemInfo.supportsMultisampleAutoResolve;
            bool isOffscreenRender = cameraData.targetTexture != null && !isSceneViewCamera;
            bool isCapturing = cameraData.captureActions != null;

#if ENABLE_VR && ENABLE_VR_MODULE
            if (isStereoEnabled)
                isCompatibleBackbufferTextureDimension = UnityEngine.XR.XRSettings.deviceEyeTextureDimension == cameraTargetDescriptor.dimension;
#endif

            bool requiresBlitForOffscreenCamera = cameraData.postProcessEnabled || cameraData.requiresOpaqueTexture || requiresExplicitMsaaResolve || !cameraData.isDefaultViewport;
            if (isOffscreenRender)
                return requiresBlitForOffscreenCamera;

            return requiresBlitForOffscreenCamera || isSceneViewCamera || isScaledRender || cameraData.isHdrEnabled ||
                   !isCompatibleBackbufferTextureDimension || isCapturing ||
                   (Display.main.requiresBlitToBackbuffer && !isStereoEnabled);
        }

        bool CanCopyDepth(ref CameraData cameraData)
        {
            bool msaaEnabledForCamera = cameraData.cameraTargetDescriptor.msaaSamples > 1;
            bool supportsTextureCopy = SystemInfo.copyTextureSupport != CopyTextureSupport.None;
            bool supportsDepthTarget = RenderingUtils.SupportsRenderTextureFormat(RenderTextureFormat.Depth);
            bool supportsDepthCopy = !msaaEnabledForCamera && (supportsDepthTarget || supportsTextureCopy);

            bool msaaDepthResolve = false;
            return supportsDepthCopy || msaaDepthResolve;
        }
    }
}