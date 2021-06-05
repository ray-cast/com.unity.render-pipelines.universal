using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public class HizPass : ScriptableRenderPass
    {
        const int k_MaxPyramidSize = 7;
        const int k_MaxDepthCache = 6;

        private ComputeShader _hizCS;

        private int _hzbBuildKernel;
        private int _hzbMipBuildKernel;

        private bool _isRequestComplete = true;

        private Camera _currenCamera;

        private float _maxLinearDepthThreadhold;

        private RenderTextureDescriptor _cameraDescriptor { get; set; }

        private RenderTargetHandle _depthTextureHandle { get; set; }

        public struct DepthEstimation
		{
            public int currenDepthIndex;
            public float adaptedLinearDepth;
            public float[] linearDepthArrays;
            public RenderTexture hizTexture;
            public Matrix4x4 viewMatrix;
            public Matrix4x4 viewProjectionMatrix;
        }

        private static Dictionary<Camera, DepthEstimation> _depthEstimation = new Dictionary<Camera, DepthEstimation>();

        public static float GetAverageLinearDepth(ref Camera camera)
		{
            if (_depthEstimation.TryGetValue(camera, out var value))
                return value.adaptedLinearDepth;
            else
                return float.MaxValue;
		}

        public static RenderTexture GetHizTexture(ref Camera camera)
		{
            _depthEstimation.TryGetValue(camera, out var value);
            return value.hizTexture;
        }

        public static DepthEstimation GetDepthEstimation(ref Camera camera)
        {
            _depthEstimation.TryGetValue(camera, out var value);
            return value;
        }

        public HizPass(RenderPassEvent evt, ComputeShader hizCS)
        {
            renderPassEvent = evt;

            _hizCS = hizCS;
            _hzbBuildKernel = _hizCS.FindKernel("HzbBuild");
            _hzbMipBuildKernel = _hizCS.FindKernel("HzbMipBuild");

            ShaderConstants._HizMipDown = new int[k_MaxPyramidSize];

            for (int i = 1; i < k_MaxPyramidSize; i++)
                ShaderConstants._HizMipDown[i] = Shader.PropertyToID("_HizMipDown" + i);
        }

        public void Setup(RenderTargetHandle depthTextureHandle)
        {
            this._depthTextureHandle = depthTextureHandle;
        }

        RenderTextureDescriptor GetStereoCompatibleDescriptor(RenderTextureDescriptor cameraTextureDescriptor, int width, int height, GraphicsFormat format, int depthBufferBits = 0)
        {
            var desc = cameraTextureDescriptor;
            desc.depthBufferBits = depthBufferBits;
            desc.msaaSamples = 1;
            desc.width = width;
            desc.height = height;
            desc.graphicsFormat = format;
            desc.enableRandomWrite = true;

            return desc;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            int width = Mathf.CeilToInt(cameraTextureDescriptor.width / 2f);
            int height = Mathf.CeilToInt(cameraTextureDescriptor.height / 2f);

            var desc = GetStereoCompatibleDescriptor(cameraTextureDescriptor, width, height, GraphicsFormat.R32_SFloat);

            for (int i = 1; i < k_MaxPyramidSize; ++i)
            {
                desc.width = Mathf.Max(1, width >> i);
                desc.height = Mathf.Max(1, height >> i);

                cmd.GetTemporaryRT(ShaderConstants._HizMipDown[i], desc, FilterMode.Point);
            }

            this._cameraDescriptor = cameraTextureDescriptor;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ref var camera = ref renderingData.cameraData.camera;

            if (!_depthEstimation.TryGetValue(camera, out var depthEstimation))
			{
                depthEstimation.currenDepthIndex = 0;
                depthEstimation.adaptedLinearDepth = renderingData.cameraData.maxShadowDistance;
                depthEstimation.linearDepthArrays = new float[k_MaxDepthCache];
                depthEstimation.hizTexture = null;
                depthEstimation.viewMatrix = camera.worldToCameraMatrix;
                depthEstimation.viewProjectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix;

                for (var i = 0; i < k_MaxDepthCache; i++)
                    depthEstimation.linearDepthArrays[i] = renderingData.cameraData.maxShadowDistance;

                _depthEstimation.Add(camera, depthEstimation);
            }
            else
			{
                depthEstimation.viewMatrix = camera.worldToCameraMatrix;
                depthEstimation.viewProjectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix;
                _depthEstimation[camera] = depthEstimation;
            }

            var halfWidth = Mathf.CeilToInt(camera.pixelWidth / 2f);
            var halfHeight = Mathf.CeilToInt(camera.pixelHeight / 2f);
            ref var hizRenderTarget = ref depthEstimation.hizTexture;

            if (hizRenderTarget == null || hizRenderTarget.width != halfWidth || hizRenderTarget.height != halfHeight)
            {
                var hizDesc = GetStereoCompatibleDescriptor(_cameraDescriptor, halfWidth, halfHeight, GraphicsFormat.R32_SFloat);
                hizDesc.mipCount = k_MaxPyramidSize;
                hizDesc.enableRandomWrite = true;
                hizDesc.useMipMap = true;
                hizDesc.autoGenerateMips = false;

                if (hizRenderTarget != null)
                {
                    RenderTexture.ReleaseTemporary(hizRenderTarget);
                }
                
                depthEstimation.hizTexture = RenderTexture.GetTemporary(hizDesc);

                _depthEstimation[camera] = depthEstimation;
            }

            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._profilerTag);

            cmd.SetGlobalTexture("_CameraHizTexture", hizRenderTarget);

            context.ExecuteCommandBuffer(cmd);

            int width = hizRenderTarget.width;
            int height = hizRenderTarget.height;

            for (int i = 0; i < hizRenderTarget.mipmapCount; i++)
            {
                cmd.SetComputeIntParam(_hizCS, ShaderConstants._MipScale, 2);
                cmd.SetComputeIntParam(_hizCS, ShaderConstants._MipLevel, Mathf.Max(0, i - 1));
                cmd.SetComputeVectorParam(_hizCS, ShaderConstants._DepthTex_TexelSize, new Vector4(1.0f / width, 1.0f / height, 0, 0));

                if (i % 2 == 1)
                {
                    cmd.SetComputeTextureParam(_hizCS, _hzbMipBuildKernel, ShaderConstants._DepthTexture, hizRenderTarget);
                    cmd.SetComputeTextureParam(_hizCS, _hzbMipBuildKernel, ShaderConstants._RWHizTexture, ShaderConstants._HizMipDown[i]);
                    cmd.DispatchCompute(_hizCS, _hzbMipBuildKernel, Mathf.CeilToInt(width / 16f), Mathf.CeilToInt(height / 8f), 1);
                    cmd.CopyTexture(ShaderConstants._HizMipDown[i], 0, 0, hizRenderTarget, 0, i);
                }
                else
                {
                    cmd.SetComputeTextureParam(_hizCS, _hzbBuildKernel, ShaderConstants._DepthTexture, i == 0 ? _depthTextureHandle.Identifier() : ShaderConstants._HizMipDown[i - 1]);
                    cmd.SetComputeTextureParam(_hizCS, _hzbBuildKernel, ShaderConstants._RWHizTexture, hizRenderTarget, i);
                    cmd.DispatchCompute(_hizCS, _hzbBuildKernel, Mathf.CeilToInt(width / 16f), Mathf.CeilToInt(height / 8f), 1);
                }

                width = Mathf.Max(1, width >> 1);
                height = Mathf.Max(1, height >> 1);
            }

            if (_isRequestComplete)
			{
                _currenCamera = camera;
                _maxLinearDepthThreadhold = renderingData.cameraData.maxShadowDistance;

                /*if (camera.cameraType == CameraType.Game)
				{
                    _isRequestComplete = false;
                    cmd.RequestAsyncReadback(hizRenderTarget, 6, GraphicsFormat.R32_SFloat, OnAsyncGPUReadbackRequest);
                }*/
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private float LinearEyeDepth(float z, Vector4 _ZBufferParams)
        {
            return 1.0f / (_ZBufferParams.z * z + _ZBufferParams.w);
        }

        private float CalcAdaptedDepth(float adaptedDepth, float depth, float delta)
        {
            return Mathf.Max(0.0f, adaptedDepth + (depth - adaptedDepth) * (1.0f - Mathf.Pow(2, -25f * delta)));
        }

        private void OnAsyncGPUReadbackRequest(AsyncGPUReadbackRequest req)
        {
            if (_currenCamera)
			{
                float near = _currenCamera.nearClipPlane;
                float far = _currenCamera.farClipPlane;
                float invNear = Mathf.Approximately(near, 0.0f) ? 0.0f : 1.0f / near;
                float invFar = Mathf.Approximately(far, 0.0f) ? 0.0f : 1.0f / far;
                float zc0 = 1.0f - far * invNear;
                float zc1 = far * invNear;

                Vector4 zBufferParams = new Vector4(zc0, zc1, zc0 * invFar, zc1 * invFar);

                if (SystemInfo.usesReversedZBuffer)
                {
                    zBufferParams.y += zBufferParams.x;
                    zBufferParams.x = -zBufferParams.x;
                    zBufferParams.w += zBufferParams.z;
                    zBufferParams.z = -zBufferParams.z;
                }

                if (_depthEstimation.TryGetValue(_currenCamera, out var depthEstimation))
                {
                    var maxDepth = 0.0f;
                    var data = req.GetData<float>(0);

                    for (var i = 0; i < data.Length; i++)
                    {
                        if (data[i] > 0)
                        {
                            float linearDepth = LinearEyeDepth(data[i], zBufferParams);
                            if (linearDepth < _maxLinearDepthThreadhold)
                                maxDepth = Mathf.Max(maxDepth, linearDepth);
                        }
                    }

                    var curLinearDepth = maxDepth > 0 ? Mathf.CeilToInt(maxDepth * (_currenCamera.sensorSize.x / _currenCamera.sensorSize.y)) : _maxLinearDepthThreadhold;
                    depthEstimation.linearDepthArrays[depthEstimation.currenDepthIndex] = curLinearDepth;
                    depthEstimation.adaptedLinearDepth = curLinearDepth < depthEstimation.adaptedLinearDepth ? CalcAdaptedDepth(depthEstimation.adaptedLinearDepth, curLinearDepth, Time.deltaTime) : curLinearDepth;

                    var accumulateDepth = 0.0f;
                    for (int i = 0; i <= depthEstimation.currenDepthIndex; i++)
                        accumulateDepth += depthEstimation.linearDepthArrays[i];

                    if (depthEstimation.currenDepthIndex < (k_MaxDepthCache - 1))
                        depthEstimation.currenDepthIndex++;                        
                    else
                        depthEstimation.currenDepthIndex = 0;

                    _depthEstimation[_currenCamera] = depthEstimation;
                }
            }

            _isRequestComplete = true;
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            foreach (var item in _depthEstimation)
			{
                if (item.Key == null && item.Value.hizTexture != null)
                    RenderTexture.ReleaseTemporary(item.Value.hizTexture);
            }

            for (int i = 1; i < k_MaxPyramidSize; i++)
                cmd.ReleaseTemporaryRT(ShaderConstants._HizMipDown[i]);
        }

        static class ShaderConstants
        {
            public const string _profilerTag = "Hiz Pass";

            public static readonly int _MipScale = Shader.PropertyToID("_MipScale");
            public static readonly int _MipLevel = Shader.PropertyToID("_MipLevel");
            public static readonly int _DepthTex_TexelSize = Shader.PropertyToID("_DepthTex_TexelSize");
            public static readonly int _DepthTexture = Shader.PropertyToID("_DepthTexture");
            public static readonly int _RWHizTexture = Shader.PropertyToID("_RWHizTexture");

            public static int[] _HizMipDown;
        }
    }
}