using System;
using System.Collections.Generic;

using Unity.Collections;

namespace UnityEngine.Rendering.Universal
{
    public class AdditionalLightsShadowCasterPass : ScriptableRenderPass
    {
        private static class AdditionalShadowsConstantBuffer
        {
            public static int _AdditionalLightsWorldToShadow;
            public static int _AdditionalShadowParams;
            public static int _AdditionalShadowOffset0;
            public static int _AdditionalShadowOffset1;
            public static int _AdditionalShadowOffset2;
            public static int _AdditionalShadowOffset3;
            public static int _AdditionalShadowmapSize;
        }

        public static int _additionalShadowsBufferId;
        public static int _additionalShadowsIndicesId;
        bool _useStructuredBuffer;

        const int k_ShadowmapBufferBits = 16;
        private RenderTargetHandle _additionalLightsShadowmap;
        RenderTexture _additionalLightsShadowmapTexture;

        int _shadowmapWidth;
        int _shadowmapHeight;

        ShadowSliceData[] _additionalLightSlices = null;

        // Shader data for UBO path
        Matrix4x4[] _additionalLightsWorldToShadow = null;
        Vector4[] _additionalLightsShadowParams = null;

        // Shader data for SSBO
        ShaderInput.ShadowData[] _additionalLightsShadowData = null;

        List<int> _additionalShadowCastingLightIndices = new List<int>();
        List<int> _additionalShadowCastingLightIndicesMap = new List<int>();
        bool _supportsBoxFilterForShadows;
        const string _profilerTag = "Render Additional Shadows";
        ProfilingSampler _profilingSampler = new ProfilingSampler(_profilerTag);

        public AdditionalLightsShadowCasterPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;

            AdditionalShadowsConstantBuffer._AdditionalLightsWorldToShadow = Shader.PropertyToID("_AdditionalLightsWorldToShadow");
            AdditionalShadowsConstantBuffer._AdditionalShadowParams = Shader.PropertyToID("_AdditionalShadowParams");
            AdditionalShadowsConstantBuffer._AdditionalShadowOffset0 = Shader.PropertyToID("_AdditionalShadowOffset0");
            AdditionalShadowsConstantBuffer._AdditionalShadowOffset1 = Shader.PropertyToID("_AdditionalShadowOffset1");
            AdditionalShadowsConstantBuffer._AdditionalShadowOffset2 = Shader.PropertyToID("_AdditionalShadowOffset2");
            AdditionalShadowsConstantBuffer._AdditionalShadowOffset3 = Shader.PropertyToID("_AdditionalShadowOffset3");
            AdditionalShadowsConstantBuffer._AdditionalShadowmapSize = Shader.PropertyToID("_AdditionalShadowmapSize");
            _additionalLightsShadowmap.Init("_AdditionalLightsShadowmapTexture");

            _additionalShadowsBufferId = Shader.PropertyToID("_AdditionalShadowsBuffer");
            _additionalShadowsIndicesId = Shader.PropertyToID("_AdditionalShadowsIndices");
            _useStructuredBuffer = RenderingUtils.useStructuredBuffer;
            _supportsBoxFilterForShadows = Application.isMobilePlatform || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Switch;

            if (!_useStructuredBuffer)
            {
                // Preallocated a fixed size. CommandBuffer.SetGlobal* does allow this data to grow.
                int maxLights = UniversalRenderPipeline.maxVisibleAdditionalLights;
                _additionalLightsWorldToShadow = new Matrix4x4[maxLights];
                _additionalLightsShadowParams = new Vector4[maxLights];
            }
        }

        public bool Setup(ref RenderingData renderingData)
        {
            Clear();

            _shadowmapWidth = renderingData.shadowData.additionalLightsShadowmapWidth;
            _shadowmapHeight = renderingData.shadowData.additionalLightsShadowmapHeight;

            var visibleLights = renderingData.lightData.visibleLights;
            int additionalLightsCount = renderingData.lightData.additionalLightsCount;

            if (_additionalLightSlices == null || _additionalLightSlices.Length < additionalLightsCount)
                _additionalLightSlices = new ShadowSliceData[additionalLightsCount];

            if (_additionalLightsShadowData == null || _additionalLightsShadowData.Length < additionalLightsCount)
                _additionalLightsShadowData = new ShaderInput.ShadowData[additionalLightsCount];

            int validShadowCastingLights = 0;
            bool supportsSoftShadows = renderingData.shadowData.supportsSoftShadows;
            for (int i = 0; i < visibleLights.Length && _additionalShadowCastingLightIndices.Count < additionalLightsCount; ++i)
            {
                VisibleLight shadowLight = visibleLights[i];

                // Skip main directional light as it is not packed into the shadow atlas
                if (i == renderingData.lightData.mainLightIndex)
                    continue;

                int shadowCastingLightIndex = _additionalShadowCastingLightIndices.Count;
                bool isValidShadowSlice = false;
                if (renderingData.cullResults.GetShadowCasterBounds(i, out var bounds))
                {
                    // We need to iterate the lights even though additional lights are disabled because
                    // cullResults.GetShadowCasterBounds() does the fence sync for the shadow culling jobs.
                    if (!renderingData.shadowData.supportsAdditionalLightShadows)
                    {
                        continue;
                    }

                    if (IsValidShadowCastingLight(ref renderingData.lightData, i))
                    {
                        bool success = ShadowUtils.ExtractSpotLightMatrix(ref renderingData.cullResults,
                            ref renderingData.shadowData,
                            i,
                            out var shadowTransform,
                            out _additionalLightSlices[shadowCastingLightIndex].viewMatrix,
                            out _additionalLightSlices[shadowCastingLightIndex].projectionMatrix);

                        if (success)
                        {
                            _additionalShadowCastingLightIndices.Add(i);
                            var light = shadowLight.light;
                            float shadowStrength = light.shadowStrength;
                            float softShadows = (supportsSoftShadows && light.shadows == LightShadows.Soft) ? 1.0f : 0.0f;
                            Vector4 shadowParams = new Vector4(shadowStrength, softShadows, 0.0f, 0.0f);
                            if (_useStructuredBuffer)
                            {
                                _additionalLightsShadowData[shadowCastingLightIndex].worldToShadowMatrix = shadowTransform;
                                _additionalLightsShadowData[shadowCastingLightIndex].shadowParams = shadowParams;
                            }
                            else
                            {
                                _additionalLightsWorldToShadow[shadowCastingLightIndex] = shadowTransform;
                                _additionalLightsShadowParams[shadowCastingLightIndex] = shadowParams;
                            }
                            isValidShadowSlice = true;
                            validShadowCastingLights++;
                        }
                    }
                }

                if (_useStructuredBuffer)
                {
                    // When using StructuredBuffers all the valid shadow casting slices data
                    // are stored in a the ShadowData buffer and then we setup a index map to
                    // map from light indices to shadow buffer index. A index map of -1 means
                    // the light is not a valid shadow casting light and there's no data for it
                    // in the shadow buffer.
                    int indexMap = (isValidShadowSlice) ? shadowCastingLightIndex : -1;
                    _additionalShadowCastingLightIndicesMap.Add(indexMap);
                }
                else if (!isValidShadowSlice)
                {
                    // When NOT using structured buffers we have no performant way to sample the
                    // index map as int[]. Unity shader compiler converts int[] to float4[] to force memory alignment.
                    // This makes indexing int[] arrays very slow. So, in order to avoid indexing shadow lights we
                    // setup slice data and reserve shadow map space even for invalid shadow slices.
                    // The data is setup with zero shadow strength. This has the same visual effect of no shadow
                    // attenuation contribution from this light.
                    // This makes sampling shadow faster but introduces waste in shadow map atlas.
                    // The waste increases with the amount of additional lights to shade.
                    // Therefore Universal RP try to keep the limit at sane levels when using uniform buffers.
                    Matrix4x4 identity = Matrix4x4.identity;
                    _additionalShadowCastingLightIndices.Add(i);
                    _additionalLightsWorldToShadow[shadowCastingLightIndex] = identity;
                    _additionalLightsShadowParams[shadowCastingLightIndex] = Vector4.zero;
                    _additionalLightSlices[shadowCastingLightIndex].viewMatrix = identity;
                    _additionalLightSlices[shadowCastingLightIndex].projectionMatrix = identity;
                }
            }

            // Lights that need to be rendered in the shadow map atlas
            if (validShadowCastingLights == 0)
                return false;

            int atlasWidth = renderingData.shadowData.additionalLightsShadowmapWidth;
            int atlasHeight = renderingData.shadowData.additionalLightsShadowmapHeight;
            int sliceResolution = ShadowUtils.GetMaxTileResolutionInAtlas(atlasWidth, atlasHeight, validShadowCastingLights);

            // In the UI we only allow for square shadow map atlas. Here we check if we can fit
            // all shadow slices into half resolution of the atlas and adjust height to have tighter packing.
            int maximumSlices = (_shadowmapWidth / sliceResolution) * (_shadowmapHeight / sliceResolution);
            if (validShadowCastingLights <= (maximumSlices / 2))
                _shadowmapHeight /= 2;

            int shadowSlicesPerRow = (atlasWidth / sliceResolution);
            float oneOverAtlasWidth = 1.0f / _shadowmapWidth;
            float oneOverAtlasHeight = 1.0f / _shadowmapHeight;

            int sliceIndex = 0;
            int shadowCastingLightsBufferCount = _additionalShadowCastingLightIndices.Count;
            Matrix4x4 sliceTransform = Matrix4x4.identity;
            sliceTransform.m00 = sliceResolution * oneOverAtlasWidth;
            sliceTransform.m11 = sliceResolution * oneOverAtlasHeight;

            for (int i = 0; i < shadowCastingLightsBufferCount; ++i)
            {
                // we can skip the slice if strength is zero. Some slices with zero
                // strength exists when using uniform array path.
                if (!_useStructuredBuffer && Mathf.Approximately(_additionalLightsShadowParams[i].x, 0.0f))
                    continue;

                _additionalLightSlices[i].offsetX = (sliceIndex % shadowSlicesPerRow) * sliceResolution;
                _additionalLightSlices[i].offsetY = (sliceIndex / shadowSlicesPerRow) * sliceResolution;
                _additionalLightSlices[i].resolution = sliceResolution;

                sliceTransform.m03 = _additionalLightSlices[i].offsetX * oneOverAtlasWidth;
                sliceTransform.m13 = _additionalLightSlices[i].offsetY * oneOverAtlasHeight;

                // We bake scale and bias to each shadow map in the atlas in the matrix.
                // saves some instructions in shader.
                if (_useStructuredBuffer)
                    _additionalLightsShadowData[i].worldToShadowMatrix = sliceTransform * _additionalLightsShadowData[i].worldToShadowMatrix;
                else
                    _additionalLightsWorldToShadow[i] = sliceTransform * _additionalLightsWorldToShadow[i];
                sliceIndex++;
            }

            return true;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            _additionalLightsShadowmapTexture = ShadowUtils.GetTemporaryShadowTexture(_shadowmapWidth, _shadowmapHeight, k_ShadowmapBufferBits);
            ConfigureTarget(new RenderTargetIdentifier(_additionalLightsShadowmapTexture));
            ConfigureClear(ClearFlag.All, Color.black);
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderingData.shadowData.supportsAdditionalLightShadows)
                RenderAdditionalShadowmapAtlas(ref context, ref renderingData.cullResults, ref renderingData.lightData, ref renderingData.shadowData);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");

            if (_additionalLightsShadowmapTexture)
            {
                RenderTexture.ReleaseTemporary(_additionalLightsShadowmapTexture);
                _additionalLightsShadowmapTexture = null;
            }
        }

        void Clear()
        {
            _additionalShadowCastingLightIndices.Clear();
            _additionalShadowCastingLightIndicesMap.Clear();
            _additionalLightsShadowmapTexture = null;
        }

        void RenderAdditionalShadowmapAtlas(ref ScriptableRenderContext context, ref CullingResults cullResults, ref LightData lightData, ref ShadowData shadowData)
        {
            NativeArray<VisibleLight> visibleLights = lightData.visibleLights;

            bool additionalLightHasSoftShadows = false;
            CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                bool anyShadowSliceRenderer = false;
                int shadowSlicesCount = _additionalShadowCastingLightIndices.Count;
                for (int i = 0; i < shadowSlicesCount; ++i)
                {
                    // we do the shadow strength check here again here because when using
                    // the uniform array path we might have zero strength shadow lights.
                    // In that case we need the shadow data buffer but we can skip
                    // rendering them to shadowmap.
                    if (!_useStructuredBuffer && Mathf.Approximately(_additionalLightsShadowParams[i].x, 0.0f))
                        continue;

                    // Index of the VisibleLight
                    int shadowLightIndex = _additionalShadowCastingLightIndices[i];
                    VisibleLight shadowLight = visibleLights[shadowLightIndex];

                    ShadowSliceData shadowSliceData = _additionalLightSlices[i];

                    var settings = new ShadowDrawingSettings(cullResults, shadowLightIndex);
                    Vector4 shadowBias = ShadowUtils.GetShadowBias(ref shadowLight, shadowLightIndex,
                        ref shadowData, shadowSliceData.projectionMatrix, shadowSliceData.resolution);
                    ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref shadowLight, shadowBias);
                    ShadowUtils.RenderShadowSlice(cmd, ref context, ref shadowSliceData, ref settings);
                    additionalLightHasSoftShadows |= shadowLight.light.shadows == LightShadows.Soft;
                    anyShadowSliceRenderer = true;
                }

                // We share soft shadow settings for main light and additional lights to save keywords.
                // So we check here if pipeline supports soft shadows and either main light or any additional light has soft shadows
                // to enable the keyword.
                // TODO: In PC and Consoles we can upload shadow data per light and branch on shader. That will be more likely way faster.
                bool mainLightHasSoftShadows = shadowData.supportsMainLightShadows &&
                                               lightData.mainLightIndex != -1 &&
                                               visibleLights[lightData.mainLightIndex].light.shadows ==
                                               LightShadows.Soft;

                bool softShadows = shadowData.supportsSoftShadows &&
                                   (mainLightHasSoftShadows || additionalLightHasSoftShadows);

                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.AdditionalLightShadows, anyShadowSliceRenderer);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadows, softShadows);

                if (anyShadowSliceRenderer)
                    SetupAdditionalLightsShadowReceiverConstants(cmd, ref shadowData, softShadows);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void SetupAdditionalLightsShadowReceiverConstants(CommandBuffer cmd, ref ShadowData shadowData, bool softShadows)
        {
            int shadowLightsCount = _additionalShadowCastingLightIndices.Count;

            float invShadowAtlasWidth = 1.0f / shadowData.additionalLightsShadowmapWidth;
            float invShadowAtlasHeight = 1.0f / shadowData.additionalLightsShadowmapHeight;
            float invHalfShadowAtlasWidth = 0.5f * invShadowAtlasWidth;
            float invHalfShadowAtlasHeight = 0.5f * invShadowAtlasHeight;

            cmd.SetGlobalTexture(_additionalLightsShadowmap.id, _additionalLightsShadowmapTexture);

            if (_useStructuredBuffer)
            {
                NativeArray<ShaderInput.ShadowData> shadowBufferData = new NativeArray<ShaderInput.ShadowData>(shadowLightsCount, Allocator.Temp);
                for (int i = 0; i < shadowLightsCount; ++i)
                {
                    ShaderInput.ShadowData data;
                    data.worldToShadowMatrix = _additionalLightsShadowData[i].worldToShadowMatrix;
                    data.shadowParams = _additionalLightsShadowData[i].shadowParams;
                    shadowBufferData[i] = data;
                }

                var shadowBuffer = ShaderData.instance.GetShadowDataBuffer(shadowLightsCount);
                shadowBuffer.SetData(shadowBufferData);

                var shadowIndicesMapBuffer = ShaderData.instance.GetShadowIndicesBuffer(_additionalShadowCastingLightIndicesMap.Count);
                shadowIndicesMapBuffer.SetData(_additionalShadowCastingLightIndicesMap, 0, 0,
                    _additionalShadowCastingLightIndicesMap.Count);

                cmd.SetGlobalBuffer(_additionalShadowsBufferId, shadowBuffer);
                cmd.SetGlobalBuffer(_additionalShadowsIndicesId, shadowIndicesMapBuffer);
                shadowBufferData.Dispose();
            }
            else
            {
                cmd.SetGlobalMatrixArray(AdditionalShadowsConstantBuffer._AdditionalLightsWorldToShadow, _additionalLightsWorldToShadow);
                cmd.SetGlobalVectorArray(AdditionalShadowsConstantBuffer._AdditionalShadowParams, _additionalLightsShadowParams);
            }

            if (softShadows)
            {
                if (_supportsBoxFilterForShadows)
                {
                    cmd.SetGlobalVector(AdditionalShadowsConstantBuffer._AdditionalShadowOffset0,
                        new Vector4(-invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight, 0.0f, 0.0f));
                    cmd.SetGlobalVector(AdditionalShadowsConstantBuffer._AdditionalShadowOffset1,
                        new Vector4(invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight, 0.0f, 0.0f));
                    cmd.SetGlobalVector(AdditionalShadowsConstantBuffer._AdditionalShadowOffset2,
                        new Vector4(-invHalfShadowAtlasWidth, invHalfShadowAtlasHeight, 0.0f, 0.0f));
                    cmd.SetGlobalVector(AdditionalShadowsConstantBuffer._AdditionalShadowOffset3,
                        new Vector4(invHalfShadowAtlasWidth, invHalfShadowAtlasHeight, 0.0f, 0.0f));
                }

                // Currently only used when !SHADER_API_MOBILE but risky to not set them as it's generic
                // enough so custom shaders might use it.
                cmd.SetGlobalVector(AdditionalShadowsConstantBuffer._AdditionalShadowmapSize, new Vector4(invShadowAtlasWidth, invShadowAtlasHeight,
                    shadowData.additionalLightsShadowmapWidth, shadowData.additionalLightsShadowmapHeight));
            }
        }

        bool IsValidShadowCastingLight(ref LightData lightData, int i)
        {
            if (i == lightData.mainLightIndex)
                return false;

            VisibleLight shadowLight = lightData.visibleLights[i];

            // Directional and Point light shadows are not supported in the shadow map atlas
            if (shadowLight.lightType == LightType.Point || shadowLight.lightType == LightType.Directional)
                return false;

            Light light = shadowLight.light;
            return light != null && light.shadows != LightShadows.None && !Mathf.Approximately(light.shadowStrength, 0.0f);
        }
    }
}