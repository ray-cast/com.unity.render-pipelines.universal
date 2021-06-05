namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Default renderer for Universal RP.
    /// This renderer is supported on all Universal RP supported platforms.
    /// It uses a classic forward rendering strategy with per-object light culling.
    /// </summary>
    public sealed class ForwardRenderer : ScriptableRenderer
    {
        const int k_DepthStencilBufferBits = 32;
        const string k_CreateCameraTextures = "Create Camera Texture";

        ColorGradingLutPass _colorGradingLutPass;
        DepthOnlyPass _depthPrepass;
        MainLightShadowCasterPass _mainLightShadowCasterPass;
        AdditionalLightsShadowCasterPass _additionalLightsShadowCasterPass;
        DrawObjectsPass _renderOpaqueForwardPass;
        DrawSkyboxPass _drawSkyboxPass;
        CopyDepthPass _copyDepthPass;
        CopyColorPass _copyColorPass;
        TransparentSettingsPass _transparentSettingsPass;
        DrawObjectsPass _renderTransparentForwardPass;
        InvokeOnRenderObjectCallbackPass _onRenderObjectCallbackPass;
        PostProcessPass _postProcessPass;
        PostProcessPass _finalPostProcessPass;
        FinalBlitPass _finalBlitPass;
        CapturePass _capturePass;

#if POST_PROCESSING_STACK_2_0_0_OR_NEWER
        PostProcessPassCompat m_OpaquePostProcessPassCompat;
        PostProcessPassCompat m_PostProcessPassCompat;
#endif

#if UNITY_EDITOR
        SceneViewDepthCopyPass _sceneViewDepthCopyPass;
#endif

        RenderTargetHandle _activeCameraColorAttachment;
        RenderTargetHandle _activeCameraDepthAttachment;
        RenderTargetHandle _cameraColorAttachment;
        RenderTargetHandle _cameraDepthAttachment;
        RenderTargetHandle _depthTexture;
        RenderTargetHandle _opaqueColor;
        RenderTargetHandle _afterPostProcessColor;
        RenderTargetHandle _colorGradingLut;

        ForwardLights _forwardLights;
        StencilState _defaultStencilState;

        Material _blitMaterial;
        Material _copyDepthMaterial;
        Material _samplingMaterial;
        Material _screenspaceShadowsMaterial;

        public ForwardRenderer(ForwardRendererData data) : base(data)
        {
            _blitMaterial = CoreUtils.CreateEngineMaterial(data.shaders.blitPS);
            _copyDepthMaterial = CoreUtils.CreateEngineMaterial(data.shaders.copyDepthPS);
            _samplingMaterial = CoreUtils.CreateEngineMaterial(data.shaders.samplingPS);
            _screenspaceShadowsMaterial = CoreUtils.CreateEngineMaterial(data.shaders.screenSpaceShadowPS);

            StencilStateData stencilData = data.defaultStencilState;
            _defaultStencilState = StencilState.defaultValue;
            _defaultStencilState.enabled = stencilData.overrideStencilState;
            _defaultStencilState.SetCompareFunction(stencilData.stencilCompareFunction);
            _defaultStencilState.SetPassOperation(stencilData.passOperation);
            _defaultStencilState.SetFailOperation(stencilData.failOperation);
            _defaultStencilState.SetZFailOperation(stencilData.zFailOperation);

            // Note: Since all custom render passes inject first and we have stable sort,
            // we inject the builtin passes in the before events.
            _mainLightShadowCasterPass = new MainLightShadowCasterPass(RenderPassEvent.BeforeRenderingShadows);
            _additionalLightsShadowCasterPass = new AdditionalLightsShadowCasterPass(RenderPassEvent.BeforeRenderingShadows);
            _depthPrepass = new DepthOnlyPass(RenderPassEvent.BeforeRenderingPrepasses, RenderQueueRange.opaque, data.opaqueLayerMask);
            _colorGradingLutPass = new ColorGradingLutPass(RenderPassEvent.BeforeRenderingPrepasses, data.postProcessData);
            _renderOpaqueForwardPass = new DrawObjectsPass("Render Opaques", true, RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, data.opaqueLayerMask, _defaultStencilState, stencilData.stencilReference);
            _copyDepthPass = new CopyDepthPass(RenderPassEvent.AfterRenderingSkybox, _copyDepthMaterial);
            _drawSkyboxPass = new DrawSkyboxPass(RenderPassEvent.BeforeRenderingSkybox);
            _copyColorPass = new CopyColorPass(RenderPassEvent.AfterRenderingSkybox, _samplingMaterial);
#if ADAPTIVE_PERFORMANCE_2_1_0_OR_NEWER
            if (!UniversalRenderPipeline.asset.useAdaptivePerformance || AdaptivePerformance.AdaptivePerformanceRenderSettings.SkipTransparentObjects == false)
#endif
            {
                _transparentSettingsPass = new TransparentSettingsPass(RenderPassEvent.BeforeRenderingTransparents, data.shadowTransparentReceive);
                _renderTransparentForwardPass = new DrawObjectsPass("Render Transparents", false, RenderPassEvent.BeforeRenderingTransparents, RenderQueueRange.transparent, data.transparentLayerMask, _defaultStencilState, stencilData.stencilReference);
            }

            _onRenderObjectCallbackPass = new InvokeOnRenderObjectCallbackPass(RenderPassEvent.BeforeRenderingPostProcessing);
            _postProcessPass = new PostProcessPass(RenderPassEvent.BeforeRenderingPostProcessing, data.postProcessData, _blitMaterial);
            _finalPostProcessPass = new PostProcessPass(RenderPassEvent.AfterRendering + 1, data.postProcessData, _blitMaterial);
            _capturePass = new CapturePass(RenderPassEvent.AfterRendering);
            _finalBlitPass = new FinalBlitPass(RenderPassEvent.AfterRendering + 1, _blitMaterial);

#if POST_PROCESSING_STACK_2_0_0_OR_NEWER
            m_OpaquePostProcessPassCompat = new PostProcessPassCompat(RenderPassEvent.BeforeRenderingOpaques, true);
            m_PostProcessPassCompat = new PostProcessPassCompat(RenderPassEvent.BeforeRenderingPostProcessing);
#endif

#if UNITY_EDITOR
            _sceneViewDepthCopyPass = new SceneViewDepthCopyPass(RenderPassEvent.AfterRendering + 9, _copyDepthMaterial);
#endif

            // RenderTexture format depends on camera and pipeline (HDR, non HDR, etc)
            // Samples (MSAA) depend on camera and pipeline
            _cameraColorAttachment.Init("_CameraColorTexture");
            _cameraDepthAttachment.Init("_CameraDepthAttachment");
            _depthTexture.Init("_CameraDepthTexture");
            _opaqueColor.Init("_CameraOpaqueTexture");
            _afterPostProcessColor.Init("_AfterPostProcessTexture");
            _colorGradingLut.Init("_InternalGradingLut");
            _forwardLights = new ForwardLights();

            supportedRenderingFeatures = new RenderingFeatures()
            {
                cameraStacking = true,
            };
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            // always dispose unmanaged resources
            _postProcessPass.Cleanup();
            _finalPostProcessPass.Cleanup();
            _colorGradingLutPass.Cleanup();

            CoreUtils.Destroy(_blitMaterial);
            CoreUtils.Destroy(_copyDepthMaterial);
            CoreUtils.Destroy(_samplingMaterial);
            CoreUtils.Destroy(_screenspaceShadowsMaterial);
        }

        /// <inheritdoc />
        public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
        {
#if ADAPTIVE_PERFORMANCE_2_1_0_OR_NEWER
            bool needTransparencyPass = !UniversalRenderPipeline.asset.useAdaptivePerformance || !AdaptivePerformance.AdaptivePerformanceRenderSettings.SkipTransparentObjects;
#endif
            Camera camera = renderingData.cameraData.camera;
            ref CameraData cameraData = ref renderingData.cameraData;
            RenderTextureDescriptor cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            // Special path for depth only offscreen cameras. Only write opaques + transparents.
            bool isOffscreenDepthTexture = cameraData.targetTexture != null && cameraData.targetTexture.format == RenderTextureFormat.Depth;
            if (isOffscreenDepthTexture)
            {
                ConfigureCameraTarget(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.CameraTarget);

                for (int i = 0; i < rendererFeatures.Count; ++i)
                {
                    if (rendererFeatures[i].isActive)
                        rendererFeatures[i].AddRenderPasses(this, ref renderingData);
                }

                EnqueuePass(_renderOpaqueForwardPass);
                EnqueuePass(_drawSkyboxPass);
#if ADAPTIVE_PERFORMANCE_2_1_0_OR_NEWER
                if (!needTransparencyPass)
                    return;
#endif
                EnqueuePass(_renderTransparentForwardPass);
                return;
            }

            // Should apply post-processing after rendering this camera?
            bool applyPostProcessing = cameraData.postProcessEnabled;
            // There's at least a camera in the camera stack that applies post-processing
            bool anyPostProcessing = renderingData.postProcessingEnabled;

            var postProcessFeatureSet = UniversalRenderPipeline.asset.postProcessingFeatureSet;

            // We generate color LUT in the base camera only. This allows us to not break render pass execution for overlay cameras.
            bool generateColorGradingLUT = cameraData.postProcessEnabled;
#if POST_PROCESSING_STACK_2_0_0_OR_NEWER
            // PPv2 doesn't need to generate color grading LUT.
            if (postProcessFeatureSet == PostProcessingFeatureSet.PostProcessingV2)
                generateColorGradingLUT = false;
#endif

            bool isSceneViewCamera = cameraData.isSceneViewCamera;
            bool isPreviewCamera = cameraData.isPreviewCamera;
            bool requiresDepthTexture = cameraData.requiresDepthTexture;
            bool isStereoEnabled = cameraData.isStereoEnabled;

            bool mainLightShadows = _mainLightShadowCasterPass.Setup(ref renderingData, cameraData.camera.farClipPlane);
            bool additionalLightShadows = _additionalLightsShadowCasterPass.Setup(ref renderingData);
            bool transparentsNeedSettingsPass = _transparentSettingsPass.Setup(ref renderingData);

            // Depth prepass is generated in the following cases:
            // - If game or offscreen camera requires it we check if we can copy the depth from the rendering opaques pass and use that instead.
            // - Scene or preview cameras always require a depth texture. We do a depth pre-pass to simplify it and it shouldn't matter much for editor.
            bool requiresDepthPrepass = requiresDepthTexture && !CanCopyDepth(ref renderingData.cameraData);
            requiresDepthPrepass |= isSceneViewCamera;
            requiresDepthPrepass |= isPreviewCamera;

            // The copying of depth should normally happen after rendering opaques.
            // But if we only require it for post processing or the scene camera then we do it after rendering transparent objects
            _copyDepthPass.renderPassEvent = (!requiresDepthTexture && (applyPostProcessing || isSceneViewCamera)) ? RenderPassEvent.AfterRenderingTransparents : RenderPassEvent.AfterRenderingOpaques;

            // TODO: CopyDepth pass is disabled in XR due to required work to handle camera matrices in URP.
            // IF this condition is removed make sure the CopyDepthPass.cs is working properly on all XR modes. This requires PureXR SDK integration.
            if (isStereoEnabled && requiresDepthTexture)
                requiresDepthPrepass = true;

            bool isRunningHololens = false;
#if ENABLE_VR && ENABLE_VR_MODULE
            isRunningHololens = UniversalRenderPipeline.IsRunningHololens(camera);
#endif
            bool createColorTexture = RequiresIntermediateColorTexture(ref cameraData);
            createColorTexture |= (rendererFeatures.Count != 0 && !isRunningHololens);
            createColorTexture &= !isPreviewCamera;

            // If camera requires depth and there's no depth pre-pass we create a depth texture that can be read later by effect requiring it.
            bool createDepthTexture = cameraData.requiresDepthTexture && !requiresDepthPrepass;
            createDepthTexture |= (cameraData.renderType == CameraRenderType.Base && !cameraData.resolveFinalTarget);

#if UNITY_ANDROID || UNITY_WEBGL
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan)
            {
                // GLES can not use render texture's depth buffer with the color buffer of the backbuffer
                // in such case we create a color texture for it too.
                createColorTexture |= createDepthTexture;
            }
#endif

            // Configure all settings require to start a new camera stack (base camera only)
            if (cameraData.renderType == CameraRenderType.Base)
            {
                _activeCameraColorAttachment = (createColorTexture) ? _cameraColorAttachment : RenderTargetHandle.CameraTarget;
                _activeCameraDepthAttachment = (createDepthTexture) ? _cameraDepthAttachment : RenderTargetHandle.CameraTarget;

                bool intermediateRenderTexture = createColorTexture || createDepthTexture;

                // Doesn't create texture for Overlay cameras as they are already overlaying on top of created textures.
                bool createTextures = intermediateRenderTexture;
                if (createTextures)
                    CreateCameraRenderTarget(context, ref renderingData.cameraData);

                // if rendering to intermediate render texture we don't have to create msaa backbuffer
                int backbufferMsaaSamples = (intermediateRenderTexture) ? 1 : cameraTargetDescriptor.msaaSamples;

                if (Camera.main == camera && camera.cameraType == CameraType.Game && cameraData.targetTexture == null)
                    SetupBackbufferFormat(backbufferMsaaSamples, isStereoEnabled);
            }
            else
            {
                _activeCameraColorAttachment = _cameraColorAttachment;
                _activeCameraDepthAttachment = _cameraDepthAttachment;
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
            bool hasPassesAfterPostProcessing = activeRenderPassQueue.Find(x => x.renderPassEvent == RenderPassEvent.AfterRendering) != null;

            if (mainLightShadows)
                EnqueuePass(_mainLightShadowCasterPass);

            if (additionalLightShadows)
                EnqueuePass(_additionalLightsShadowCasterPass);

            if (requiresDepthPrepass)
            {
                _depthPrepass.Setup(cameraTargetDescriptor, _depthTexture);
                EnqueuePass(_depthPrepass);
            }

            if (generateColorGradingLUT)
            {
                _colorGradingLutPass.Setup(_colorGradingLut);
                EnqueuePass(_colorGradingLutPass);
            }

            EnqueuePass(_renderOpaqueForwardPass);

#if POST_PROCESSING_STACK_2_0_0_OR_NEWER
#pragma warning disable 0618 // Obsolete
            bool hasOpaquePostProcessCompat = applyPostProcessing &&
                postProcessFeatureSet == PostProcessingFeatureSet.PostProcessingV2 &&
                renderingData.cameraData.postProcessLayer.HasOpaqueOnlyEffects(RenderingUtils.postProcessRenderContext);

            if (hasOpaquePostProcessCompat)
            {
                m_OpaquePostProcessPassCompat.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, m_ActiveCameraColorAttachment);
                EnqueuePass(m_OpaquePostProcessPassCompat);
            }
#pragma warning restore 0618
#endif

            bool isOverlayCamera = cameraData.renderType == CameraRenderType.Overlay;
            if (camera.clearFlags == CameraClearFlags.Skybox && !isOverlayCamera)
            {
                bool isRequireSkybox = RenderSettings.skybox != null;
                if (!isRequireSkybox)
                {
                    if (cameraData.camera.TryGetComponent<Skybox>(out var cameraSkybox))
                        isRequireSkybox |= cameraSkybox.material != null;
                }

                if (isRequireSkybox)
                    EnqueuePass(_drawSkyboxPass);
            }

            // If a depth texture was created we necessarily need to copy it, otherwise we could have render it to a renderbuffer
            if (!requiresDepthPrepass && renderingData.cameraData.requiresDepthTexture && createDepthTexture)
            {
                _copyDepthPass.Setup(_activeCameraDepthAttachment, _depthTexture);
                EnqueuePass(_copyDepthPass);
            }

            if (renderingData.cameraData.requiresOpaqueTexture)
            {
                // TODO: Downsampling method should be store in the renderer instead of in the asset.
                // We need to migrate this data to renderer. For now, we query the method in the active asset.
                Downsampling downsamplingMethod = UniversalRenderPipeline.asset.opaqueDownsampling;
                _copyColorPass.Setup(_activeCameraColorAttachment.Identifier(), _opaqueColor, downsamplingMethod);
                EnqueuePass(_copyColorPass);
            }

#if ADAPTIVE_PERFORMANCE_2_1_0_OR_NEWER
            if (needTransparencyPass)
#endif
            {
                if (transparentsNeedSettingsPass)
                {
                    EnqueuePass(_transparentSettingsPass);
                }

                EnqueuePass(_renderTransparentForwardPass);
            }
            EnqueuePass(_onRenderObjectCallbackPass);

            bool lastCameraInTheStack = cameraData.resolveFinalTarget;
            bool hasCaptureActions = renderingData.cameraData.captureActions != null && lastCameraInTheStack;
            bool applyFinalPostProcessing = anyPostProcessing && lastCameraInTheStack &&
                                     renderingData.cameraData.antialiasing == AntialiasingMode.FastApproximateAntialiasing;

            // When post-processing is enabled we can use the stack to resolve rendering to camera target (screen or RT).
            // However when there are render passes executing after post we avoid resolving to screen so rendering continues (before sRGBConvertion etc)
            bool resolvePostProcessingToCameraTarget = !hasCaptureActions && !hasPassesAfterPostProcessing && !applyFinalPostProcessing;

            #region Post-processing v2 support
#if POST_PROCESSING_STACK_2_0_0_OR_NEWER
            // To keep things clean we'll separate the logic from builtin PP and PPv2 - expect some copy/pasting
            if (postProcessFeatureSet == PostProcessingFeatureSet.PostProcessingV2)
            {
                // if we have additional filters
                // we need to stay in a RT
                if (hasPassesAfterPostProcessing)
                {
                    // perform post with src / dest the same
                    if (applyPostProcessing)
                    {
                        m_PostProcessPassCompat.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, m_ActiveCameraColorAttachment);
                        EnqueuePass(m_PostProcessPassCompat);
                    }

                    //now blit into the final target
                    if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
                    {
                        if (renderingData.cameraData.captureActions != null)
                        {
                            m_CapturePass.Setup(m_ActiveCameraColorAttachment);
                            EnqueuePass(m_CapturePass);
                        }

                        m_FinalBlitPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment);
                        EnqueuePass(m_FinalBlitPass);
                    }
                }
                else
                {
                    if (applyPostProcessing)
                    {
                        m_PostProcessPassCompat.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, RenderTargetHandle.CameraTarget);
                        EnqueuePass(m_PostProcessPassCompat);
                    }
                    else if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
                    {
                        m_FinalBlitPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment);
                        EnqueuePass(m_FinalBlitPass);
                    }
                }
            }
            else
#endif
            #endregion
            {

                if (lastCameraInTheStack)
                {
                    // Post-processing will resolve to final target. No need for final blit pass.
                    if (applyPostProcessing)
                    {
                        var destination = resolvePostProcessingToCameraTarget ? RenderTargetHandle.CameraTarget : _afterPostProcessColor;

                        // if resolving to screen we need to be able to perform sRGBConvertion in post-processing if necessary
                        bool doSRGBConvertion = resolvePostProcessingToCameraTarget;
                        _postProcessPass.Setup(cameraTargetDescriptor, _activeCameraColorAttachment, destination, _activeCameraDepthAttachment, _colorGradingLut, applyFinalPostProcessing, doSRGBConvertion);
                        EnqueuePass(_postProcessPass);
                    }

                    if (renderingData.cameraData.captureActions != null)
                    {
                        _capturePass.Setup(_activeCameraColorAttachment);
                        EnqueuePass(_capturePass);
                    }

                    // if we applied post-processing for this camera it means current active texture is m_AfterPostProcessColor
                    var sourceForFinalPass = (applyPostProcessing) ? _afterPostProcessColor : _activeCameraColorAttachment;

                    // Do FXAA or any other final post-processing effect that might need to run after AA.
                    if (applyFinalPostProcessing)
                    {
                        _finalPostProcessPass.SetupFinalPass(sourceForFinalPass);
                        EnqueuePass(_finalPostProcessPass);
                    }

                    // if post-processing then we already resolved to camera target while doing post.
                    // Also only do final blit if camera is not rendering to RT.
                    bool cameraTargetResolved =
                        // final PP always blit to camera target
                        applyFinalPostProcessing ||
                        // no final PP but we have PP stack. In that case it blit unless there are render pass after PP
                        (applyPostProcessing && !hasPassesAfterPostProcessing) ||
                        // offscreen camera rendering to a texture, we don't need a blit pass to resolve to screen
                        _activeCameraColorAttachment == RenderTargetHandle.CameraTarget;

                    // We need final blit to resolve to screen
                    if (!cameraTargetResolved)
                    {
                        _finalBlitPass.Setup(cameraTargetDescriptor, sourceForFinalPass);
                        EnqueuePass(_finalBlitPass);
                    }
                }

                // stay in RT so we resume rendering on stack after post-processing
                else if (applyPostProcessing)
                {
                    _postProcessPass.Setup(cameraTargetDescriptor, _activeCameraColorAttachment, _afterPostProcessColor, _activeCameraDepthAttachment, _colorGradingLut, false, false);
                    EnqueuePass(_postProcessPass);
                }

            }

#if UNITY_EDITOR
            if (isSceneViewCamera)
            {
				// Scene view camera should always resolve target (not stacked)
				UnityEngine.Assertions.Assert.IsTrue(lastCameraInTheStack, "Editor camera must resolve target upon finish rendering.");
                _sceneViewDepthCopyPass.Setup(_depthTexture);
                EnqueuePass(_sceneViewDepthCopyPass);
            }
#endif
        }

        /// <inheritdoc />
        public override void SetupLights(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            _forwardLights.Setup(context, ref renderingData);
        }

        /// <inheritdoc />
        public override void SetupCullingParameters(ref ScriptableCullingParameters cullingParameters,
            ref CameraData cameraData)
        {
            // TODO: PerObjectCulling also affect reflection probes. Enabling it for now.
            // if (asset.additionalLightsRenderingMode == LightRenderingMode.Disabled ||
            //     asset.maxAdditionalLightsCount == 0)
            // {
            //     cullingParameters.cullingOptions |= CullingOptions.DisablePerObjectCulling;
            // }

            // We disable shadow casters if both shadow casting modes are turned off
            // or the shadow distance has been turned down to zero
            bool isShadowCastingDisabled = !UniversalRenderPipeline.asset.supportsMainLightShadows && !UniversalRenderPipeline.asset.supportsAdditionalLightShadows;
            bool isShadowDistanceZero = Mathf.Approximately(cameraData.maxShadowDistance, 0.0f);
            if (isShadowCastingDisabled || isShadowDistanceZero)
            {
                cullingParameters.cullingOptions &= ~CullingOptions.ShadowCasters;
            }

            // We set the number of maximum visible lights allowed and we add one for the mainlight...
            cullingParameters.maximumVisibleLights = UniversalRenderPipeline.maxVisibleAdditionalLights + 1;
            cullingParameters.shadowDistance = cameraData.maxShadowDistance;
        }

        /// <inheritdoc />
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

            if (_depthTexture != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(_depthTexture.id);
                _depthTexture = RenderTargetHandle.CameraTarget;
            }
        }

        void CreateCameraRenderTarget(ScriptableRenderContext context, ref CameraData cameraData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(k_CreateCameraTextures);
            var descriptor = cameraData.cameraTargetDescriptor;
            int msaaSamples = descriptor.msaaSamples;

            var colorDepthDescriptor = descriptor;
            colorDepthDescriptor.colorFormat = RenderTextureFormat.Depth;
            colorDepthDescriptor.depthBufferBits = k_DepthStencilBufferBits;
            colorDepthDescriptor.msaaSamples = 1;
            cmd.GetTemporaryRT(_depthTexture.id, colorDepthDescriptor, FilterMode.Point);

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

            // There's no exposed API to control how a backbuffer is created with MSAA
            // By settings antiAliasing we match what the amount of samples in camera data with backbuffer
            // We only do this for the main camera and this only takes effect in the beginning of next frame.
            // This settings should not be changed on a frame basis so that's fine.
            if (msaaSampleCountHasChanged)
            {
                QualitySettings.antiAliasing = msaaSamples;
                XR.XRDevice.UpdateEyeTextureMSAASetting();
            }
#endif
        }

        bool PlatformRequiresExplicitMsaaResolve()
        {
            // On Metal/iOS the MSAA resolve is done implicitly as part of the renderpass, so we do not need an extra intermediate pass for the explicit autoresolve.
            // TODO: should also be valid on Metal MacOS/Editor, but currently not working as expected. Remove the "mobile only" requirement once trunk has a fix.

            return !SystemInfo.supportsMultisampleAutoResolve &&
                   !(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal && Application.isMobilePlatform);
        }

        /// <summary>
        /// Checks if the pipeline needs to create a intermediate render texture.
        /// </summary>
        /// <param name="cameraData">CameraData contains all relevant render target information for the camera.</param>
        /// <seealso cref="CameraData"/>
        /// <returns>Return true if pipeline needs to render to a intermediate render texture.</returns>
        bool RequiresIntermediateColorTexture(ref CameraData cameraData)
        {
            // When rendering a camera stack we always create an intermediate render texture to composite camera results.
            // We create it upon rendering the Base camera.
            if (cameraData.renderType == CameraRenderType.Base && !cameraData.resolveFinalTarget)
                return true;

            bool isSceneViewCamera = cameraData.isSceneViewCamera;
            var cameraTargetDescriptor = cameraData.cameraTargetDescriptor;
            int msaaSamples = cameraTargetDescriptor.msaaSamples;
            bool isStereoEnabled = cameraData.isStereoEnabled;
            bool isScaledRender = !Mathf.Approximately(cameraData.renderScale, 1.0f) && !cameraData.isStereoEnabled;
            bool isCompatibleBackbufferTextureDimension = cameraTargetDescriptor.dimension == TextureDimension.Tex2D;
            bool requiresExplicitMsaaResolve = msaaSamples > 1 && PlatformRequiresExplicitMsaaResolve();
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

            // TODO:  We don't have support to highp Texture2DMS currently and this breaks depth precision.
            // currently disabling it until shader changes kick in.
            //bool msaaDepthResolve = msaaEnabledForCamera && SystemInfo.supportsMultisampledTextures != 0;
            bool msaaDepthResolve = false;
            return supportsDepthCopy || msaaDepthResolve;
        }
    }
}