using System;

namespace UnityEngine.Rendering.Universal
{
    public class MainLightShadowCasterPass : ScriptableRenderPass
    {
        private static class MainLightShadowConstantBuffer
        {
            public static int _WorldToShadow;
            public static int _ShadowParams;
            public static int _CascadeShadowSplitSpheres0;
            public static int _CascadeShadowSplitSpheres1;
            public static int _CascadeShadowSplitSpheres2;
            public static int _CascadeShadowSplitSpheres3;
            public static int _CascadeShadowSplitSphereRadii;
            public static int _ShadowOffset0;
            public static int _ShadowOffset1;
            public static int _ShadowOffset2;
            public static int _ShadowOffset3;
            public static int _ShadowmapSize;
        }

        const int k_MaxCascades = 4;
        const int k_ShadowmapBufferBits = 16;
        int _shadowmapWidth;
        int _shadowmapHeight;
        int _shadowCasterCascadesCount;
        bool _supportsBoxFilterForShadows;

        RenderTargetHandle _mainLightShadowmap;
        RenderTexture _mainLightShadowmapTexture;

        Matrix4x4[] _mainLightShadowMatrices;
        ShadowSliceData[] _cascadeSlices;
        Vector4[] _cascadeSplitDistances;
        CullingResults _shadowCullResults;

        const string _profilerTag = "Render Main Shadowmap";
        ProfilingSampler _profilingSampler = new ProfilingSampler(_profilerTag);

        public MainLightShadowCasterPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;

            _mainLightShadowMatrices = new Matrix4x4[k_MaxCascades + 1];
            _cascadeSlices = new ShadowSliceData[k_MaxCascades];
            _cascadeSplitDistances = new Vector4[k_MaxCascades];

            MainLightShadowConstantBuffer._WorldToShadow = Shader.PropertyToID("_MainLightWorldToShadow");
            MainLightShadowConstantBuffer._ShadowParams = Shader.PropertyToID("_MainLightShadowParams");
            MainLightShadowConstantBuffer._CascadeShadowSplitSpheres0 = Shader.PropertyToID("_CascadeShadowSplitSpheres0");
            MainLightShadowConstantBuffer._CascadeShadowSplitSpheres1 = Shader.PropertyToID("_CascadeShadowSplitSpheres1");
            MainLightShadowConstantBuffer._CascadeShadowSplitSpheres2 = Shader.PropertyToID("_CascadeShadowSplitSpheres2");
            MainLightShadowConstantBuffer._CascadeShadowSplitSpheres3 = Shader.PropertyToID("_CascadeShadowSplitSpheres3");
            MainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii = Shader.PropertyToID("_CascadeShadowSplitSphereRadii");
            MainLightShadowConstantBuffer._ShadowOffset0 = Shader.PropertyToID("_MainLightShadowOffset0");
            MainLightShadowConstantBuffer._ShadowOffset1 = Shader.PropertyToID("_MainLightShadowOffset1");
            MainLightShadowConstantBuffer._ShadowOffset2 = Shader.PropertyToID("_MainLightShadowOffset2");
            MainLightShadowConstantBuffer._ShadowOffset3 = Shader.PropertyToID("_MainLightShadowOffset3");
            MainLightShadowConstantBuffer._ShadowmapSize = Shader.PropertyToID("_MainLightShadowmapSize");

            _mainLightShadowmap.Init("_MainLightShadowmapTexture");
            _supportsBoxFilterForShadows = Application.isMobilePlatform || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Switch;
        }

        public void SetupShadowCullingParameters(ref RenderingData renderingData, ref Bounds bounds, ref ScriptableCullingParameters cullingParameters)
        {
            cullingParameters.cullingOptions &= ~CullingOptions.NeedsReflectionProbes;
            cullingParameters.maximumVisibleLights = UniversalRenderPipeline.maxVisibleAdditionalLights + 1;
            cullingParameters.shadowDistance = Mathf.Min(renderingData.cameraData.maxShadowDistance, Mathf.Max(
                Vector3.Distance(renderingData.cameraData.camera.transform.position, bounds.min),
                Vector3.Distance(renderingData.cameraData.camera.transform.position, bounds.max)
            ));
        }

        public bool Setup(ref ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!renderingData.shadowData.supportsMainLightShadows)
                return false;

            Clear();
            int shadowLightIndex = renderingData.lightData.mainLightIndex;
            if (shadowLightIndex == -1)
                return false;

            VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];
            Light light = shadowLight.light;
            if (light.shadows == LightShadows.None)
                return false;

            if (shadowLight.lightType != LightType.Directional)
            {
                Debug.LogWarning("Only directional lights are supported as main light.");
            }

            Bounds bounds;
            if (!renderingData.cullResults.GetShadowCasterBounds(shadowLightIndex, out bounds))
                return false;

            var camera = renderingData.cameraData.camera;
            if (!camera.TryGetCullingParameters(UniversalRenderPipeline.IsStereoEnabled(camera), out var cullingParameters))
                return false;

            SetupShadowCullingParameters(ref renderingData, ref bounds, ref cullingParameters);

            _shadowCasterCascadesCount = renderingData.shadowData.mainLightShadowCascadesCount;
            _shadowmapWidth = renderingData.shadowData.mainLightShadowmapWidth;
            _shadowmapHeight = (_shadowCasterCascadesCount == 2) ? renderingData.shadowData.mainLightShadowmapHeight >> 1 : renderingData.shadowData.mainLightShadowmapHeight;
            _shadowCullResults = context.Cull(ref cullingParameters);

            var shadowResolution = ShadowUtils.GetMaxTileResolutionInAtlas(renderingData.shadowData.mainLightShadowmapWidth, renderingData.shadowData.mainLightShadowmapHeight, _shadowCasterCascadesCount);

            for (int cascadeIndex = 0; cascadeIndex < _shadowCasterCascadesCount; ++cascadeIndex)
            {
                bool success = ShadowUtils.ExtractDirectionalLightMatrix(ref _shadowCullResults, ref renderingData.shadowData,
                    shadowLightIndex, cascadeIndex, _shadowmapWidth, _shadowmapHeight, shadowResolution, light.shadowNearPlane,
                    out _cascadeSplitDistances[cascadeIndex],
                    out _cascadeSlices[cascadeIndex],
                    out _cascadeSlices[cascadeIndex].viewMatrix,
                    out _cascadeSlices[cascadeIndex].projectionMatrix
                );

                if (!success)
                    return false;
            }

            return true;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            _mainLightShadowmapTexture = ShadowUtils.GetTemporaryShadowTexture(_shadowmapWidth,
                    _shadowmapHeight, k_ShadowmapBufferBits);
            ConfigureTarget(new RenderTargetIdentifier(_mainLightShadowmapTexture));
            ConfigureClear(ClearFlag.All, Color.black);
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            RenderMainLightCascadeShadowmap(ref context, ref _shadowCullResults, ref renderingData.lightData, ref renderingData.shadowData);
        }

        /// <inheritdoc/>
        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");

            if (_mainLightShadowmapTexture)
            {
                RenderTexture.ReleaseTemporary(_mainLightShadowmapTexture);
                _mainLightShadowmapTexture = null;
            }
        }

        void Clear()
        {
            _mainLightShadowmapTexture = null;

            for (int i = 0; i < _mainLightShadowMatrices.Length; ++i)
                _mainLightShadowMatrices[i] = Matrix4x4.identity;

            for (int i = 0; i < _cascadeSplitDistances.Length; ++i)
                _cascadeSplitDistances[i] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

            for (int i = 0; i < _cascadeSlices.Length; ++i)
                _cascadeSlices[i].Clear();
        }

        void RenderMainLightCascadeShadowmap(ref ScriptableRenderContext context, ref CullingResults cullResults, ref LightData lightData, ref ShadowData shadowData)
        {
            int shadowLightIndex = lightData.mainLightIndex;
            if (shadowLightIndex == -1)
                return;

            VisibleLight shadowLight = lightData.visibleLights[shadowLightIndex];

            CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                var settings = new ShadowDrawingSettings(cullResults, shadowLightIndex);

                for (int cascadeIndex = 0; cascadeIndex < _shadowCasterCascadesCount; ++cascadeIndex)
                {
                    var splitData = settings.splitData;
                    splitData.cullingSphere = _cascadeSplitDistances[cascadeIndex];
                    settings.splitData = splitData;
                    Vector4 shadowBias = ShadowUtils.GetShadowBias(ref shadowLight, shadowLightIndex, ref shadowData, _cascadeSlices[cascadeIndex].projectionMatrix, _cascadeSlices[cascadeIndex].resolution);
                    ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref shadowLight, shadowBias);
                    ShadowUtils.RenderShadowSlice(cmd, ref context, ref _cascadeSlices[cascadeIndex],
                        ref settings, _cascadeSlices[cascadeIndex].projectionMatrix, _cascadeSlices[cascadeIndex].viewMatrix);
                }

                bool softShadows = shadowLight.light.shadows == LightShadows.Soft && shadowData.supportsSoftShadows;
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, true);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowCascades, shadowData.mainLightShadowCascadesCount > 1);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadows, softShadows);

                SetupMainLightShadowReceiverConstants(cmd, shadowLight, softShadows);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void SetupMainLightShadowReceiverConstants(CommandBuffer cmd, VisibleLight shadowLight, bool softShadows)
        {
            Light light = shadowLight.light;

            int cascadeCount = _shadowCasterCascadesCount;
            for (int i = 0; i < cascadeCount; ++i)
                _mainLightShadowMatrices[i] = _cascadeSlices[i].shadowTransform;

            Matrix4x4 noOpShadowMatrix = Matrix4x4.zero;
            noOpShadowMatrix.m22 = (SystemInfo.usesReversedZBuffer) ? 1.0f : 0.0f;
            for (int i = cascadeCount; i <= k_MaxCascades; ++i)
                _mainLightShadowMatrices[i] = noOpShadowMatrix;

            float invShadowAtlasWidth = 1.0f / _shadowmapWidth;
            float invShadowAtlasHeight = 1.0f / _shadowmapHeight;
            float invHalfShadowAtlasWidth = 0.5f * invShadowAtlasWidth;
            float invHalfShadowAtlasHeight = 0.5f * invShadowAtlasHeight;
            float softShadowsProp = softShadows ? 1.0f : 0.0f;
            cmd.SetGlobalTexture(_mainLightShadowmap.id, _mainLightShadowmapTexture);
            cmd.SetGlobalMatrixArray(MainLightShadowConstantBuffer._WorldToShadow, _mainLightShadowMatrices);
            cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowParams, new Vector4(light.shadowStrength, softShadowsProp, 0.0f, 0.0f));

            if (_shadowCasterCascadesCount > 1)
            {
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres0, _cascadeSplitDistances[0]);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres1, _cascadeSplitDistances[1]);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres2, _cascadeSplitDistances[2]);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres3, _cascadeSplitDistances[3]);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii, new Vector4(
                    _cascadeSplitDistances[0].w * _cascadeSplitDistances[0].w,
                    _cascadeSplitDistances[1].w * _cascadeSplitDistances[1].w,
                    _cascadeSplitDistances[2].w * _cascadeSplitDistances[2].w,
                    _cascadeSplitDistances[3].w * _cascadeSplitDistances[3].w));
            }

            if (softShadows)
            {
                if (_supportsBoxFilterForShadows)
                {
                    cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowOffset0, new Vector4(-invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight, 0.0f, 0.0f));
                    cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowOffset1, new Vector4(invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight, 0.0f, 0.0f));
                    cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowOffset2, new Vector4(-invHalfShadowAtlasWidth, invHalfShadowAtlasHeight, 0.0f, 0.0f));
                    cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowOffset3, new Vector4(invHalfShadowAtlasWidth, invHalfShadowAtlasHeight, 0.0f, 0.0f));
                }

                cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowmapSize, new Vector4(invShadowAtlasWidth, invShadowAtlasHeight, _shadowmapWidth, _shadowmapHeight));
            }
        }
    }
}