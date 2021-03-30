using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public class HizPass : ScriptableRenderPass
    {
        const int k_MaxPyramidSize = 8;

        public static Matrix4x4 _hizLastCameraView = Matrix4x4.identity;
        public static Matrix4x4 _hizLastCameraProjection = Matrix4x4.identity;
        public static RenderTexture _hizRenderTarget = null;

        public Vector2Int _hizSize = Vector2Int.zero;
        public ProfilingSampler _profilingSampler;

        private ComputeShader _hizCS;

        private RenderTargetHandle _depthTextureHandle { get; set; }

        public HizPass(RenderPassEvent evt, ComputeShader hizCS)
        {
            renderPassEvent = evt;

            _hizCS = hizCS;
            _profilingSampler = new ProfilingSampler(ShaderConstants._profilerTag);

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

            if (_hizSize.x != cameraTextureDescriptor.width || _hizSize.y != cameraTextureDescriptor.height || _hizRenderTarget == null)
			{
                _hizRenderTarget = new RenderTexture(width, height, 0, GraphicsFormat.R32_SFloat, 7)
                {
                    enableRandomWrite = true,
                    useMipMap = true,
                    autoGenerateMips = false,
                };

                _hizSize.Set(cameraTextureDescriptor.width, cameraTextureDescriptor.height);

                cmd.SetGlobalTexture("_CameraHizTexture", _hizRenderTarget);
            }

            var desc = GetStereoCompatibleDescriptor(cameraTextureDescriptor, width, height, _hizRenderTarget.graphicsFormat);

            for (int i = 1; i < _hizRenderTarget.mipmapCount; ++i)
            {
                desc.width = Mathf.Max(1, width >> i);
                desc.height = Mathf.Max(1, height >> i);

                cmd.GetTemporaryRT(ShaderConstants._HizMipDown[i], desc, _hizRenderTarget.filterMode);
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._profilerTag);

            _hizLastCameraView = renderingData.cameraData.camera.worldToCameraMatrix;
            _hizLastCameraProjection = renderingData.cameraData.camera.projectionMatrix;

            using (new ProfilingScope(cmd, _profilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                int width = _hizRenderTarget.width;
                int height = _hizRenderTarget.height;

                for (int i = 0; i < _hizRenderTarget.mipmapCount; i++)
                {
                    if (i % 2 == 1)
                    {
                        cmd.SetComputeVectorParam(_hizCS, ShaderConstants._DepthTex_TexelSize, new Vector4(1.0f / width, 1.0f / height, 0, 0));
                        cmd.SetComputeIntParam(_hizCS, ShaderConstants._MipScale, 2 << (i - 1));
                        cmd.SetComputeTextureParam(_hizCS, 0, ShaderConstants._DepthTexture, _hizRenderTarget, i - 1);
                        cmd.SetComputeTextureParam(_hizCS, 0, ShaderConstants._RWHizTexture, ShaderConstants._HizMipDown[i]);
                        cmd.DispatchCompute(_hizCS, 0, Mathf.CeilToInt(width / 16f), Mathf.CeilToInt(height / 8f), 1);
                        cmd.CopyTexture(ShaderConstants._HizMipDown[i], 0, 0, _hizRenderTarget, 0, i);
                    }
                    else
                    {
                        cmd.SetComputeVectorParam(_hizCS, ShaderConstants._DepthTex_TexelSize, new Vector4(1.0f / width, 1.0f / height, 0, 0));
                        cmd.SetComputeIntParam(_hizCS, ShaderConstants._MipScale, 2);
                        cmd.SetComputeTextureParam(_hizCS, 0, ShaderConstants._DepthTexture, i == 0 ? _depthTextureHandle.Identifier() : ShaderConstants._HizMipDown[i - 1]);
                        cmd.SetComputeTextureParam(_hizCS, 0, ShaderConstants._RWHizTexture, _hizRenderTarget, i);
                        cmd.DispatchCompute(_hizCS, 0, Mathf.CeilToInt(width / 16f), Mathf.CeilToInt(height / 8f), 1);
                    }

                    width = width >> 1;
                    height = height >> 1;
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (_hizRenderTarget != null)
			{
                for (int i = 1; i < _hizRenderTarget.mipmapCount; i++)
                    cmd.ReleaseTemporaryRT(ShaderConstants._HizMipDown[i]);
            }
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