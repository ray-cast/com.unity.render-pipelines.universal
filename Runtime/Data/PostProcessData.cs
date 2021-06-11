using System;

namespace UnityEngine.Rendering.Universal
{
    [Serializable]
    public class PostProcessData : ScriptableObject
    {
        [Serializable, ReloadGroup]
        public sealed class ShaderResources
        {
            [Reload("Shaders/PostProcessing/StopNaN.shader")]
            public Shader stopNanPS;

            [Reload("Shaders/PostProcessing/SubpixelMorphologicalAntialiasing.shader")]
            public Shader subpixelMorphologicalAntialiasingPS;

            [Reload("Shaders/PostProcessing/GaussianDepthOfField.shader")]
            public Shader gaussianDepthOfFieldPS;

            [Reload("Shaders/PostProcessing/BokehDepthOfField.shader")]
            public Shader bokehDepthOfFieldPS;

            [Reload("Shaders/PostProcessing/CameraMotionBlur.shader")]
            public Shader cameraMotionBlurPS;

            [Reload("Shaders/PostProcessing/PaniniProjection.shader")]
            public Shader paniniProjectionPS;

            [Reload("Shaders/PostProcessing/SceneLookup.shader")]
            public Shader sceneLookupPS;

            [Reload("Shaders/PostProcessing/LutBuilderLdr.shader")]
            public Shader lutBuilderLdrPS;

            [Reload("Shaders/PostProcessing/LutBuilderHdr.shader")]
            public Shader lutBuilderHdrPS;

            [Reload("Shaders/PostProcessing/Bloom.shader")]
            public Shader bloomPS;

            [Reload("Shaders/PostProcessing/KawaseBloom.shader")]
            public Shader kawaseBloomPS;

            [Reload("Shaders/PostProcessing/UberPost.shader")]
            public Shader uberPostPS;

            [Reload("Shaders/PostProcessing/FinalPost.shader")]
            public Shader finalPostPassPS;
        }

        [Serializable, ReloadGroup]
        public sealed class TextureResources
        {
            // Pre-baked noise
            [Reload("Textures/BlueNoise16/L/LDR_LLL1_{0}.png", 0, 32)]
            public Texture2D[] blueNoise16LTex;

            // Post-processing
            [Reload(new[]
            {
                "Textures/FilmGrain/Thin01.png",
                "Textures/FilmGrain/Thin02.png",
                "Textures/FilmGrain/Medium01.png",
                "Textures/FilmGrain/Medium02.png",
                "Textures/FilmGrain/Medium03.png",
                "Textures/FilmGrain/Medium04.png",
                "Textures/FilmGrain/Medium05.png",
                "Textures/FilmGrain/Medium06.png",
                "Textures/FilmGrain/Large01.png",
                "Textures/FilmGrain/Large02.png"
            })]
            public Texture2D[] filmGrainTex;

            [Reload("Textures/SMAA/AreaTex.tga")]
            public Texture2D smaaAreaTex;

            [Reload("Textures/SMAA/SearchTex.tga")]
            public Texture2D smaaSearchTex;

            [Reload("Textures/Skin/shadowLUT.bmp")]
            public Texture2D shadowTex;
        }

        public ShaderResources shaders;
        public TextureResources textures;
    }
}