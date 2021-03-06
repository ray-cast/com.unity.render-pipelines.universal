using System;

namespace UnityEngine.Rendering.Universal
{
    public class CopyColorPass : ScriptableRenderPass
    {
        private int _sampleOffsetShaderHandle;
        private Material _samplingMaterial;
        private Downsampling _downsamplingMethod;

        private RenderTargetIdentifier source { get; set; }
        private RenderTargetHandle destination { get; set; }

        private const string _profilerTag = "Copy Color";

        /// <summary>
        /// Create the CopyColorPass
        /// </summary>
        public CopyColorPass(RenderPassEvent evt, Material samplingMaterial)
        {
            _samplingMaterial = samplingMaterial;
            _sampleOffsetShaderHandle = Shader.PropertyToID("_SampleOffset");
            renderPassEvent = evt;
            _downsamplingMethod = Downsampling.None;
        }

        /// <summary>
        /// Configure the pass with the source and destination to execute on.
        /// </summary>
        /// <param name="source">Source Render Target</param>
        /// <param name="destination">Destination Render Target</param>
        public void Setup(RenderTargetIdentifier source, RenderTargetHandle destination, Downsampling downsampling)
        {
            this.source = source;
            this.destination = destination;
            this._downsamplingMethod = downsampling;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescripor)
        {
            RenderTextureDescriptor descriptor = cameraTextureDescripor;
            descriptor.msaaSamples = 1;
            descriptor.depthBufferBits = 0;
            if (_downsamplingMethod == Downsampling._2xBilinear)
            {
                descriptor.width /= 2;
                descriptor.height /= 2;
            }
            else if (_downsamplingMethod == Downsampling._4xBox || _downsamplingMethod == Downsampling._4xBilinear)
            {
                descriptor.width /= 4;
                descriptor.height /= 4;
            }

            cmd.GetTemporaryRT(destination.id, descriptor, _downsamplingMethod == Downsampling.None ? FilterMode.Point : FilterMode.Bilinear);
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_samplingMaterial == null)
            {
                Debug.LogErrorFormat("Missing {0}. {1} render pass will not execute. Check for missing reference in the renderer resources.", _samplingMaterial, GetType().Name);
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);

            switch (_downsamplingMethod)
            {
                case Downsampling.None:
                    Blit(cmd, source, destination.Identifier());
                    break;
                case Downsampling._2xBilinear:
                    Blit(cmd, source, destination.Identifier());
                    break;
                case Downsampling._4xBox:
                    _samplingMaterial.SetFloat(_sampleOffsetShaderHandle, 2);
                    Blit(cmd, source, destination.Identifier(), _samplingMaterial);
                    break;
                case Downsampling._4xBilinear:
                    Blit(cmd, source, destination.Identifier());
                    break;
            }

            context.ExecuteCommandBuffer(cmd);

            CommandBufferPool.Release(cmd);
        }

        /// <inheritdoc/>
        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");

            if (destination != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(destination.id);
                destination = RenderTargetHandle.CameraTarget;
            }
        }
    }
}