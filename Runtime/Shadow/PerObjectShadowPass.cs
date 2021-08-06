using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    public class PerObjectShadowPass : ScriptableRenderPass
    {
        int _shadowmapWidth;
        int _shadowmapHeight;

        private RenderTargetHandle _perObjectShadowTexture;
        private bool _supportsBoxFilterForShadows;

        public struct ShadowData
        {
            public int index;
            public int offsetX;
            public int offsetY;
            public int resolution;

            public Vector4 shadowBias;
            public Vector4 shadowParams;

            public Matrix4x4 viewMatrix;
            public Matrix4x4 projectionMatrix;
            public Matrix4x4 shadowTransform;
        }

        private ShadowData[] _perObjectShadowData = null;
        private Matrix4x4[] _perObjectWorldToShadow = null;
        private Vector4[] _perObjectShadowClip = null;
        private List<int> _perObjectCastingShadowIndices = new List<int>();

        private readonly bool _forceShadowPointSampling;

        public PerObjectShadowPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;

            _perObjectShadowTexture.Init("_PerObjectShadowTexture");
            _perObjectShadowData = new ShadowData[UniversalRenderPipeline.maxPerObjectShadows];
            _perObjectShadowClip = new Vector4[UniversalRenderPipeline.maxPerObjectShadows];
            _perObjectWorldToShadow = new Matrix4x4[UniversalRenderPipeline.maxPerObjectShadows];

            _forceShadowPointSampling = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal && GraphicsSettings.HasShaderDefine(Graphics.activeTier, BuiltinShaderDefine.UNITY_METAL_SHADOWS_USE_POINT_FILTERING);
            _supportsBoxFilterForShadows = Application.isMobilePlatform || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Switch;
        }

        public bool Setup(ref RenderingData renderingData)
        {
            if (!renderingData.shadowData.supportsPerObjectShadows || renderingData.lightData.mainLightIndex < 0)
                return false;
            
            var shadowIndex = renderingData.lightData.mainLightIndex;
            var shadowLight = renderingData.lightData.visibleLights[shadowIndex];
            var shadows = CharacterShadowManager.instance._perObjectShadows;
            var shadowCastingLights = CharacterShadowManager.instance._perObjectShadows.Count;

            _shadowmapWidth = renderingData.shadowData.perObjectShadowmapWidth;
            _shadowmapHeight = renderingData.shadowData.perObjectShadowmapHeight;

            _perObjectCastingShadowIndices.Clear();

            for (int i = 0; i < shadowCastingLights && _perObjectCastingShadowIndices.Count < renderingData.shadowData.perObjectShadowLimit; i++)
            {
                var boundingBox = shadows[i].worldBoundingBox;
                if (boundingBox.size != Vector3.zero)
                {
                    var shadowCastingLightIndex = _perObjectCastingShadowIndices.Count;

                    ShadowUtils.ExtractDirectionalLightMatrix(shadowLight, boundingBox, out var viewMatrix, out var projectionMatrix);

                    _perObjectShadowData[shadowCastingLightIndex].index = i;
                    _perObjectShadowData[shadowCastingLightIndex].offsetX = 0;
                    _perObjectShadowData[shadowCastingLightIndex].offsetY = 0;
                    _perObjectShadowData[shadowCastingLightIndex].resolution = _shadowmapWidth;
                    _perObjectShadowData[shadowCastingLightIndex].viewMatrix = viewMatrix;
                    _perObjectShadowData[shadowCastingLightIndex].projectionMatrix = projectionMatrix;
                    _perObjectShadowData[shadowCastingLightIndex].shadowBias = ShadowUtils.GetShadowBias(ref shadowLight, shadowIndex, ref renderingData.shadowData, projectionMatrix, _shadowmapWidth);

                    _perObjectShadowClip[i] = new Vector4(0, 0, 1, 1);

                    _perObjectWorldToShadow[shadowCastingLightIndex] = ShadowUtils.GetShadowTransform(projectionMatrix, viewMatrix);

                    _perObjectCastingShadowIndices.Add(shadowCastingLightIndex);
                }
            }

            if (_perObjectCastingShadowIndices.Count > 1)
            {
                int sliceResolution = ShadowUtils.GetMaxTileResolutionInAtlas(_shadowmapWidth, _shadowmapHeight, _perObjectCastingShadowIndices.Count);
                int maximumSlices = (_shadowmapWidth / sliceResolution) * (_shadowmapHeight / sliceResolution);
                if (_perObjectCastingShadowIndices.Count <= (maximumSlices / 2))
                    _shadowmapHeight /= 2;

                int shadowSlicesPerRow = (_shadowmapWidth / sliceResolution);
                float oneOverAtlasWidth = 1.0f / _shadowmapWidth;
                float oneOverAtlasHeight = 1.0f / _shadowmapHeight;

                int sliceIndex = 0;
                int shadowCastingLightsBufferCount = _perObjectCastingShadowIndices.Count;
                Matrix4x4 sliceTransform = Matrix4x4.identity;
                sliceTransform.m00 = sliceResolution * oneOverAtlasWidth;
                sliceTransform.m11 = sliceResolution * oneOverAtlasHeight;

                for (int i = 0; i < shadowCastingLightsBufferCount; ++i)
                {
                    _perObjectShadowData[i].offsetX = (sliceIndex % shadowSlicesPerRow) * sliceResolution;
                    _perObjectShadowData[i].offsetY = (sliceIndex / shadowSlicesPerRow) * sliceResolution;
                    _perObjectShadowData[i].resolution = sliceResolution;

                    _perObjectShadowClip[i].x = _perObjectShadowData[i].offsetX / (float)_shadowmapWidth;
                    _perObjectShadowClip[i].y = _perObjectShadowData[i].offsetY / (float)_shadowmapHeight;
                    _perObjectShadowClip[i].z = _perObjectShadowClip[i].x + _perObjectShadowData[i].resolution / (float)_shadowmapWidth;
                    _perObjectShadowClip[i].w = _perObjectShadowClip[i].y + _perObjectShadowData[i].resolution / (float)_shadowmapHeight;

                    sliceTransform.m03 = _perObjectShadowData[i].offsetX * oneOverAtlasWidth;
                    sliceTransform.m13 = _perObjectShadowData[i].offsetY * oneOverAtlasHeight;

                    _perObjectWorldToShadow[i] = sliceTransform * _perObjectWorldToShadow[i];

                    sliceIndex++;
                }
            }

            return true;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var baseDescriptor = cameraTextureDescriptor;
            baseDescriptor.width = _shadowmapWidth;
            baseDescriptor.height = _shadowmapHeight;
            baseDescriptor.colorFormat = RenderTextureFormat.Depth;
            baseDescriptor.depthBufferBits = baseDescriptor.depthBufferBits;
            
            cmd.GetTemporaryRT(_perObjectShadowTexture.id, baseDescriptor, _forceShadowPointSampling ? FilterMode.Point : FilterMode.Bilinear);

            ConfigureTarget(_perObjectShadowTexture.Identifier());
            ConfigureClear(ClearFlag.All, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._profilerTag);
            cmd.SetGlobalVector(ShaderConstants._PerObjectShadowsCount, new Vector4(_perObjectCastingShadowIndices.Count, 0, 0, 0));
            cmd.SetGlobalMatrixArray(ShaderConstants._PerObjectWorldToShadow, _perObjectWorldToShadow);
            cmd.SetGlobalVectorArray(ShaderConstants._PerObjectShadowsClip, _perObjectShadowClip);
            cmd.EnableShaderKeyword(ShaderKeywordStrings.MainLightPerObjectShadow);

            ref CameraData cameraData = ref renderingData.cameraData;
            if (cameraData.isStereoEnabled)
                context.StartMultiEye(cameraData.camera);

            VisibleLight shadowLight = renderingData.lightData.visibleLights[renderingData.lightData.mainLightIndex];

            bool softShadows = renderingData.shadowData.supportsSoftShadows;
            if (softShadows)
            {
                float invShadowAtlasWidth = 1.0f / _shadowmapWidth;
                float invShadowAtlasHeight = 1.0f / _shadowmapHeight;
                float invHalfShadowAtlasWidth = 0.5f * invShadowAtlasWidth;
                float invHalfShadowAtlasHeight = 0.5f * invShadowAtlasHeight;

                if (_supportsBoxFilterForShadows)
                {
                    cmd.SetGlobalVector(ShaderConstants._PerObjectShadowOffset0, new Vector4(-invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight, 0.0f, 0.0f));
                    cmd.SetGlobalVector(ShaderConstants._PerObjectShadowOffset1, new Vector4(invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight, 0.0f, 0.0f));
                    cmd.SetGlobalVector(ShaderConstants._PerObjectShadowOffset2, new Vector4(-invHalfShadowAtlasWidth, invHalfShadowAtlasHeight, 0.0f, 0.0f));
                    cmd.SetGlobalVector(ShaderConstants._PerObjectShadowOffset3, new Vector4(invHalfShadowAtlasWidth, invHalfShadowAtlasHeight, 0.0f, 0.0f));
                }

                cmd.SetGlobalVector(ShaderConstants._PerObjectShadowmapSize, new Vector4(invShadowAtlasWidth, invShadowAtlasHeight, _shadowmapWidth, _shadowmapHeight));
            }

            for (int i = 0; i < _perObjectCastingShadowIndices.Count; i++)
            {
                ref var shadowSliceData = ref _perObjectShadowData[i];

                RenderingUtils.SetViewAndProjectionMatrices(cmd, shadowSliceData.viewMatrix, GL.GetGPUProjectionMatrix(shadowSliceData.projectionMatrix, true), false);
                ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref shadowLight, shadowSliceData.shadowBias);

                cmd.SetViewport(new Rect(shadowSliceData.offsetX, shadowSliceData.offsetY, shadowSliceData.resolution, shadowSliceData.resolution));
                cmd.EnableScissorRect(new Rect(shadowSliceData.offsetX, shadowSliceData.offsetY, shadowSliceData.resolution, shadowSliceData.resolution));

                var renderable = CharacterShadowManager.instance._perObjectShadows[shadowSliceData.index]._renderable;
                foreach (var renderer in renderable)
                {
                    for (int submesh = 0; submesh < renderer.mesh.subMeshCount; submesh++)
                        cmd.DrawMesh(renderer.mesh, renderer.localToWorldMatrix, renderer.material, submesh, renderer.shadowPass);
                }

                cmd.DisableScissorRect();
            }

            RenderingUtils.SetViewAndProjectionMatrices(cmd, cameraData.GetViewMatrix(), cameraData.GetGPUProjectionMatrix(), false);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_perObjectShadowTexture.id);
        }

        static class ShaderConstants
        {
            public static readonly string _profilerTag = "Per Object Shadow";
            public static readonly string _shaderTagId = "ShadowCaster";

            public static readonly int _PerObjectShadowsClip = Shader.PropertyToID("_PerObjectShadowsClip");
            public static readonly int _PerObjectShadowsCount = Shader.PropertyToID("_PerObjectShadowsCount");
            public static readonly int _PerObjectWorldToShadow = Shader.PropertyToID("_PerObjectWorldToShadow");

            public static readonly int _PerObjectShadowOffset0 = Shader.PropertyToID("_PerObjectShadowOffset0");
            public static readonly int _PerObjectShadowOffset1 = Shader.PropertyToID("_PerObjectShadowOffset1");
            public static readonly int _PerObjectShadowOffset2 = Shader.PropertyToID("_PerObjectShadowOffset2");
            public static readonly int _PerObjectShadowOffset3 = Shader.PropertyToID("_PerObjectShadowOffset3");

            public static readonly int _PerObjectShadowmapSize = Shader.PropertyToID("_PerObjectShadowmapSize");
        }
    }
}