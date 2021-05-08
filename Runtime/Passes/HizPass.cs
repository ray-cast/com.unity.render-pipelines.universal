using System.Collections;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public class HizPass : ScriptableRenderPass
    {
        const int k_MaxPyramidSize = 7;

        private ComputeShader _hizCS;

        private RenderTextureDescriptor _cameraDescriptor { get; set; }

        private RenderTargetHandle _depthTextureHandle { get; set; }

        private static Dictionary<Camera, RenderTexture> _hizRenderTarget = new Dictionary<Camera, RenderTexture>();

        public static RenderTexture GetHizTexture(ref Camera camera)
		{
            _hizRenderTarget.TryGetValue(camera, out var value);
            return value;
        }

        public HizPass(RenderPassEvent evt, ComputeShader hizCS)
        {
            renderPassEvent = evt;

            _hizCS = hizCS;
            
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

            var halfWidth = Mathf.CeilToInt(camera.pixelWidth / 2f);
            var halfHeight = Mathf.CeilToInt(camera.pixelHeight / 2f);
            var hizRenderTarget = GetHizTexture(ref camera);

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

                hizRenderTarget = RenderTexture.GetTemporary(hizDesc);

                if (_hizRenderTarget.ContainsKey(camera))
                    _hizRenderTarget[camera] = hizRenderTarget;
                else
                    _hizRenderTarget.Add(camera, hizRenderTarget);
            }

            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._profilerTag);

            cmd.SetGlobalTexture("_CameraHizTexture", hizRenderTarget);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            int width = hizRenderTarget.width;
            int height = hizRenderTarget.height;

            for (int i = 0; i < hizRenderTarget.mipmapCount; i++)
            {
                if (i % 2 == 1)
                {
                    cmd.SetComputeVectorParam(_hizCS, ShaderConstants._DepthTex_TexelSize, new Vector4(1.0f / width, 1.0f / height, 0, 0));
                    cmd.SetComputeIntParam(_hizCS, ShaderConstants._MipScale, 2 << (i - 1));
                    cmd.SetComputeTextureParam(_hizCS, 0, ShaderConstants._DepthTexture, hizRenderTarget, i - 1);
                    cmd.SetComputeTextureParam(_hizCS, 0, ShaderConstants._RWHizTexture, ShaderConstants._HizMipDown[i]);
                    cmd.DispatchCompute(_hizCS, 0, Mathf.CeilToInt(width / 16f), Mathf.CeilToInt(height / 8f), 1);
                    cmd.CopyTexture(ShaderConstants._HizMipDown[i], 0, 0, hizRenderTarget, 0, i);
                }
                else
                {
                    cmd.SetComputeVectorParam(_hizCS, ShaderConstants._DepthTex_TexelSize, new Vector4(1.0f / width, 1.0f / height, 0, 0));
                    cmd.SetComputeIntParam(_hizCS, ShaderConstants._MipScale, 2);
                    cmd.SetComputeTextureParam(_hizCS, 0, ShaderConstants._DepthTexture, i == 0 ? _depthTextureHandle.Identifier() : ShaderConstants._HizMipDown[i - 1]);
                    cmd.SetComputeTextureParam(_hizCS, 0, ShaderConstants._RWHizTexture, hizRenderTarget, i);
                    cmd.DispatchCompute(_hizCS, 0, Mathf.CeilToInt(width / 16f), Mathf.CeilToInt(height / 8f), 1);
                }

                width = Mathf.Max(1, width >> 1);
                height = Mathf.Max(1, height >> 1);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            foreach (var item in _hizRenderTarget)
			{
                if (item.Key == null && item.Value != null)
                    RenderTexture.ReleaseTemporary(item.Value);
            }

            for (int i = 1; i < k_MaxPyramidSize; i++)
                cmd.ReleaseTemporaryRT(ShaderConstants._HizMipDown[i]);
        }

        static class ShaderConstants
        {
            public const string _profilerTag = "Hiz Pass";

            public static readonly int _MipScale = Shader.PropertyToID("_MipScale");
            public static readonly int _DepthTex_TexelSize = Shader.PropertyToID("_DepthTex_TexelSize");
            public static readonly int _DepthTexture = Shader.PropertyToID("_DepthTexture");
            public static readonly int _RWHizTexture = Shader.PropertyToID("_RWHizTexture");

            public static int[] _HizMipDown;
        }
    }
}