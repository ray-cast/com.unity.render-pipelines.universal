using System.IO;
using UnityEditor;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    // Note: this pass can't be done at the same time as post-processing as it needs to be done in
    // advance in case we're doing on-tile color grading.
    /// <summary>
    /// Renders a color grading LUT texture.
    /// </summary>
    public class ColorGradingLutPass : ScriptableRenderPass
    {
        const string k_ProfilerTag = "Color Grading LUT";

        readonly Material _lutBuilderLdr;
        readonly Material _lutBuilderHdr;
        readonly GraphicsFormat _hdrLutFormat;
        readonly GraphicsFormat _ldrLutFormat;

        RenderTargetHandle _internalLut;

        public ColorGradingLutPass(RenderPassEvent evt, PostProcessData data)
        {
            renderPassEvent = evt;
            overrideCameraTarget = true;

            Material Load(Shader shader)
            {
                if (shader == null)
                {
                    Debug.LogError($"Missing shader. {GetType().DeclaringType.Name} render pass will not execute. Check for missing reference in the renderer resources.");
                    return null;
                }

                return CoreUtils.CreateEngineMaterial(shader);
            }

            _lutBuilderLdr = Load(data.shaders.lutBuilderLdrPS);
            _lutBuilderHdr = Load(data.shaders.lutBuilderHdrPS);

            // Warm up lut format as IsFormatSupported adds GC pressure...
            const FormatUsage kFlags = FormatUsage.Linear | FormatUsage.Render;
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, kFlags))
                _hdrLutFormat = GraphicsFormat.R16G16B16A16_SFloat;
            else if (SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, kFlags))
                _hdrLutFormat = GraphicsFormat.B10G11R11_UFloatPack32;
            else
                // Obviously using this for log lut encoding is a very bad idea for precision but we
                // need it for compatibility reasons and avoid black screens on platforms that don't
                // support floating point formats. Expect banding and posterization artifact if this
                // ends up being used.
                _hdrLutFormat = GraphicsFormat.R8G8B8A8_UNorm;

            _ldrLutFormat = GraphicsFormat.R8G8B8A8_UNorm;

#if UNITY_EDITOR
            var asset = UniversalRenderPipeline.asset;
            if (asset)
                asset.colorLookupBake += Bake;
#endif
        }

        public void Setup(in RenderTargetHandle internalLut)
        {
            _internalLut = internalLut;
        }

#if UNITY_EDITOR
        public void Bake()
		{
            var asset = UniversalRenderPipeline.asset;

            var isHdr = asset.colorGradingMode == ColorGradingMode.HighDynamicRange;

            var lutHeight = asset.colorGradingLutSize;
            var lutWidth = lutHeight * lutHeight;
            var format = isHdr ? _hdrLutFormat : _ldrLutFormat;
            var material = isHdr ? _lutBuilderHdr : _lutBuilderLdr;

            var assetsLut = new RenderTexture(lutWidth, lutHeight, 0, format, 0)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Graphics.Blit(assetsLut, assetsLut, material, 1);

            Texture2D tex = new Texture2D(lutWidth, lutHeight, format, 0, TextureCreationFlags.None);
            RenderTexture.active = assetsLut;
            tex.ReadPixels(new Rect(0, 0, lutWidth, lutHeight), 0, 0);
            tex.Apply();

            if (isHdr)
			{
				string path = EditorUtility.SaveFilePanelInProject("Save Texture", "Lut", "asset", "Please enter a file name to save the texture to");
				if (path.Length != 0)
				{
					AssetDatabase.CreateAsset(tex, path);
					AssetDatabase.Refresh();
				}
			}
			else
			{
                string path = EditorUtility.SaveFilePanelInProject("Save Image", "Lut", "png", "Please enter a file name to save the image to");
                if (path.Length != 0)
                {
                    byte[] pngData = tex.EncodeToPNG();
                    if (pngData != null)
                    {
                        File.WriteAllBytes(path, pngData);
                        AssetDatabase.Refresh();
                    }
                }
            }
        }
#endif
        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get(k_ProfilerTag);

            // Fetch all color grading settings
            var stack = VolumeManager.instance.stack;
            var channelMixer = stack.GetComponent<ChannelMixer>();
            var colorAdjustments = stack.GetComponent<ColorAdjustments>();
            var curves = stack.GetComponent<ColorCurves>();
            var liftGammaGain = stack.GetComponent<LiftGammaGain>();
            var shadowsMidtonesHighlights = stack.GetComponent<ShadowsMidtonesHighlights>();
            var splitToning = stack.GetComponent<SplitToning>();
            var tonemapping = stack.GetComponent<Tonemapping>();
            var whiteBalance = stack.GetComponent<WhiteBalance>();

            ref var postProcessingData = ref renderingData.postProcessingData;
            bool hdr = postProcessingData.gradingMode == ColorGradingMode.HighDynamicRange;
            
            // Prepare texture & material
            int lutHeight = postProcessingData.lutSize;
            int lutWidth = lutHeight * lutHeight;
            var format = hdr ? _hdrLutFormat : _ldrLutFormat;
            var material = hdr ? _lutBuilderHdr : _lutBuilderLdr;
            var desc = new RenderTextureDescriptor(lutWidth, lutHeight, format, 0);
            desc.vrUsage = VRTextureUsage.None; // We only need one for both eyes in VR
            cmd.GetTemporaryRT(_internalLut.id, desc, FilterMode.Bilinear);

            // Prepare data
            var lmsColorBalance = ColorUtils.ColorBalanceToLMSCoeffs(whiteBalance.temperature.value, whiteBalance.tint.value);
            var hueSatCon = new Vector4(colorAdjustments.hueShift.value / 360f, colorAdjustments.saturation.value / 100f + 1f, colorAdjustments.contrast.value / 100f + 1f, 0f);
            var channelMixerR = new Vector4(channelMixer.redOutRedIn.value / 100f, channelMixer.redOutGreenIn.value / 100f, channelMixer.redOutBlueIn.value / 100f, 0f);
            var channelMixerG = new Vector4(channelMixer.greenOutRedIn.value / 100f, channelMixer.greenOutGreenIn.value / 100f, channelMixer.greenOutBlueIn.value / 100f, 0f);
            var channelMixerB = new Vector4(channelMixer.blueOutRedIn.value / 100f, channelMixer.blueOutGreenIn.value / 100f, channelMixer.blueOutBlueIn.value / 100f, 0f);

            var shadowsHighlightsLimits = new Vector4(
                shadowsMidtonesHighlights.shadowsStart.value,
                shadowsMidtonesHighlights.shadowsEnd.value,
                shadowsMidtonesHighlights.highlightsStart.value,
                shadowsMidtonesHighlights.highlightsEnd.value
            );

            var (shadows, midtones, highlights) = ColorUtils.PrepareShadowsMidtonesHighlights(
                shadowsMidtonesHighlights.shadows.value,
                shadowsMidtonesHighlights.midtones.value,
                shadowsMidtonesHighlights.highlights.value
            );

            var (lift, gamma, gain) = ColorUtils.PrepareLiftGammaGain(
                liftGammaGain.lift.value,
                liftGammaGain.gamma.value,
                liftGammaGain.gain.value
            );

            var (splitShadows, splitHighlights) = ColorUtils.PrepareSplitToning(
                splitToning.shadows.value,
                splitToning.highlights.value,
                splitToning.balance.value
            );

            var lutParameters = new Vector4(lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f));

            // Fill in constants
            material.SetVector(ShaderConstants._Lut_Params, lutParameters);
            material.SetVector(ShaderConstants._ColorBalance, lmsColorBalance);
            material.SetVector(ShaderConstants._ColorFilter, colorAdjustments.colorFilter.value.linear);
            material.SetVector(ShaderConstants._ChannelMixerRed, channelMixerR);
            material.SetVector(ShaderConstants._ChannelMixerGreen, channelMixerG);
            material.SetVector(ShaderConstants._ChannelMixerBlue, channelMixerB);
            material.SetVector(ShaderConstants._HueSatCon, hueSatCon);
            material.SetVector(ShaderConstants._Lift, lift);
            material.SetVector(ShaderConstants._Gamma, gamma);
            material.SetVector(ShaderConstants._Gain, gain);
            material.SetVector(ShaderConstants._Shadows, shadows);
            material.SetVector(ShaderConstants._Midtones, midtones);
            material.SetVector(ShaderConstants._Highlights, highlights);
            material.SetVector(ShaderConstants._ShaHiLimits, shadowsHighlightsLimits);
            material.SetVector(ShaderConstants._SplitShadows, splitShadows);
            material.SetVector(ShaderConstants._SplitHighlights, splitHighlights);

            // YRGB curves
            material.SetTexture(ShaderConstants._CurveMaster, curves.master.value.GetTexture());
            material.SetTexture(ShaderConstants._CurveRed, curves.red.value.GetTexture());
            material.SetTexture(ShaderConstants._CurveGreen, curves.green.value.GetTexture());
            material.SetTexture(ShaderConstants._CurveBlue, curves.blue.value.GetTexture());

            // Secondary curves
            material.SetTexture(ShaderConstants._CurveHueVsHue, curves.hueVsHue.value.GetTexture());
            material.SetTexture(ShaderConstants._CurveHueVsSat, curves.hueVsSat.value.GetTexture());
            material.SetTexture(ShaderConstants._CurveLumVsSat, curves.lumVsSat.value.GetTexture());
            material.SetTexture(ShaderConstants._CurveSatVsSat, curves.satVsSat.value.GetTexture());

            // Tonemapping (baked into the lut for HDR)
            if (hdr)
            {
                material.shaderKeywords = null;

                switch (tonemapping.mode.value)
                {
                    case TonemappingMode.Neutral: material.EnableKeyword(ShaderKeywordStrings.TonemapNeutral); break;
                    case TonemappingMode.ACES: material.EnableKeyword(ShaderKeywordStrings.TonemapACES); break;
                    case TonemappingMode.GranTurismo: material.EnableKeyword(ShaderKeywordStrings.TonemapGranTurismo); break;
                    default: break; // None
                }
            }

            // Render the lut
            Blit(cmd, _internalLut.id, _internalLut.id, material, 0);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// <inheritdoc/>
        internal override void OnFinishCameraStackRendering(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_internalLut.id);
        }

        /// <inheritdoc/>
        public override void FrameCleanup(CommandBuffer cmd)
        {
        }

        internal void Cleanup()
        {
#if UNITY_EDITOR
            var asset = UniversalRenderPipeline.asset;
            if (asset)
                asset.colorLookupBake -= Bake;
#endif

            CoreUtils.Destroy(_lutBuilderLdr);
            CoreUtils.Destroy(_lutBuilderHdr);
        }

        // Precomputed shader ids to same some CPU cycles (mostly affects mobile)
        static class ShaderConstants
        {
            public static readonly int _Lut_Params = Shader.PropertyToID("_Lut_Params");
            public static readonly int _ColorBalance = Shader.PropertyToID("_ColorBalance");
            public static readonly int _ColorFilter = Shader.PropertyToID("_ColorFilter");
            public static readonly int _ChannelMixerRed = Shader.PropertyToID("_ChannelMixerRed");
            public static readonly int _ChannelMixerGreen = Shader.PropertyToID("_ChannelMixerGreen");
            public static readonly int _ChannelMixerBlue = Shader.PropertyToID("_ChannelMixerBlue");
            public static readonly int _HueSatCon = Shader.PropertyToID("_HueSatCon");
            public static readonly int _Lift = Shader.PropertyToID("_Lift");
            public static readonly int _Gamma = Shader.PropertyToID("_Gamma");
            public static readonly int _Gain = Shader.PropertyToID("_Gain");
            public static readonly int _Shadows = Shader.PropertyToID("_Shadows");
            public static readonly int _Midtones = Shader.PropertyToID("_Midtones");
            public static readonly int _Highlights = Shader.PropertyToID("_Highlights");
            public static readonly int _ShaHiLimits = Shader.PropertyToID("_ShaHiLimits");
            public static readonly int _SplitShadows = Shader.PropertyToID("_SplitShadows");
            public static readonly int _SplitHighlights = Shader.PropertyToID("_SplitHighlights");
            public static readonly int _CurveMaster = Shader.PropertyToID("_CurveMaster");
            public static readonly int _CurveRed = Shader.PropertyToID("_CurveRed");
            public static readonly int _CurveGreen = Shader.PropertyToID("_CurveGreen");
            public static readonly int _CurveBlue = Shader.PropertyToID("_CurveBlue");
            public static readonly int _CurveHueVsHue = Shader.PropertyToID("_CurveHueVsHue");
            public static readonly int _CurveHueVsSat = Shader.PropertyToID("_CurveHueVsSat");
            public static readonly int _CurveLumVsSat = Shader.PropertyToID("_CurveLumVsSat");
            public static readonly int _CurveSatVsSat = Shader.PropertyToID("_CurveSatVsSat");
        }
    }
}