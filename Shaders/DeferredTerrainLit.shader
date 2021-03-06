Shader "Universal Render Pipeline/Terrain/Deferred Lit"
{
    Properties
    {
        _ControlInt("Control : 0-普通 1-沙漠 2-草 3-雪 4-水", Int) = 0 //注意跟EnumGroundType对应
        _Control("Control", 2D) = "white" {}
        _Splat0("Layer 1", 2D) = "white" {}
        _Splat1("Layer 2", 2D) = "white" {}
        _Splat2("Layer 3", 2D) = "white" {}
        _Splat3("Layer 4", 2D) = "white" {}
        [Toggle(_NORMALMAP)]_UseNormal("Normal Map Enable", int) = 1
        _Normal0("Normalmap 1", 2D) = "bump" {}
        _Normal1("Normalmap 2", 2D) = "bump" {}
        _Normal2("Normalmap 3", 2D) = "bump" {}
        _Normal3("Normalmap 4", 2D) = "bump" {}
        [Toggle(_USE_WETNESSMAP1)]_UseWetnessMap1("Wetness Map 1 Enable", int) = 0
        _WetnessMap0("Wetness Map 1", 2D) = "black" {}
        [Toggle(_USE_WETNESSMAP2)]_UseWetnessMap2("Wetness Map 2 Enable", int) = 0
        _WetnessMap1("Wetness Map 2", 2D) = "black" {}
        [Toggle(_USE_WETNESSMAP3)]_UseWetnessMap3("Wetness Map 3 Enable", int) = 0
        _WetnessMap2("Wetness Map 3", 2D) = "black" {}
        [Toggle(_USE_WETNESSMAP4)]_UseWetnessMap4("Wetness Map 4 Enable", int) = 0
        _WetnessMap3("Wetness Map 4", 2D) = "black" {}
        _Metallic0("Metallic 1", Range(0 , 1)) = 0
        _Metallic1("Metallic 2", Range(0 , 1)) = 0
        _Metallic2("Metallic 3", Range(0 , 1)) = 0
        _Metallic3("Metallic 4", Range(0 , 1)) = 0
        _Smoothness0("Smoothness 1", Range(0 , 1)) = 0.5
        _Smoothness1("Smoothness 2", Range(0 , 1)) = 0.5
        _Smoothness2("Smoothness 3", Range(0 , 1)) = 0.5
        _Smoothness3("Smoothness 4", Range(0 , 1)) = 0.5

        [Space(20)]
        [Toggle(_SPECULAR_ANTIALIASING)] _UseSpecularHighlights("启用镜面抗锯齿", Float) = 0
        [PowerSlider(2.0)] _specularAntiAliasingThreshold("镜面抗锯齿程度", Range(0.0, 10.0)) = 1

        [HideInInspector] [MainColor] _BaseColor("基本颜色", Color) = (1,1,1,1)
        [HideInInspector] [MainTexture] _BaseMap("基本贴图", 2D) = "white" {}
        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        [HideInInspector] [ToggleOff]_ReceiveShadows("接收阴影", Float) = 1.0

        // Blending state
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _Cull("__cull", Float) = 2.0

        // Editmode props
        [HideInInspector] _QueueOffset("Queue offset", Float) = 0.0

        // ObsoleteProperties
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _GlossMapScale("Smoothness", Float) = 0.0
        [HideInInspector] _Glossiness("Smoothness", Float) = 0.0
        [HideInInspector] _GlossyReflections("EnvironmentReflections", Float) = 0.0
    }

    SubShader
    {
        // Universal Pipeline tag is required. If Universal render pipeline is not set in the graphics settings
        // this Subshader will fail. One can add a subshader below or fallback to Standard built-in to make this
        // material work with both Universal Render Pipeline and Builtin Unity Pipeline
        Tags{"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
        LOD 300

        // ------------------------------------------------------------------
        //  Forward pass. Shades all light in a single pass. GI + emission + Fog
        Pass
        {
            // Lightmode matches the ShaderPassName set in StandardRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "ForwardLit"
            Tags{"LightMode" = "Deferred"}

            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard SRP library
            // All shaders must be compiled with HLSLcc and currently only gles is not using HLSLcc by default
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _USE_WETNESSMAP1
            #pragma shader_feature _USE_WETNESSMAP2
            #pragma shader_feature _USE_WETNESSMAP3
            #pragma shader_feature _USE_WETNESSMAP4

            #pragma shader_feature _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature _SPECULAR_SETUP
            #pragma shader_feature _RECEIVE_SHADOWS_OFF
            #pragma shader_feature _SPECULAR_ANTIALIASING

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "DeferredTerrainLitInput.hlsl"
            #include "DeferredTerrainLitPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            #include "DeferredTerrainLitInput.hlsl"
            #include "DeferredTerrainLitPasses.hlsl"
            ENDHLSL
        }

        // This pass it not used during regular rendering, only for lightmap baking.
        Pass
        {
            Name "Meta"
            Tags{"LightMode" = "Meta"}

            Cull Off

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMeta

            #pragma shader_feature _SPECULAR_SETUP
            #pragma shader_feature _EMISSION
            #pragma shader_feature _METALLICSPECGLOSSMAP
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma shader_feature _SPECGLOSSMAP

            #include "DeferredTerrainLitInput.hlsl"
            #include "DeferredTerrainLitPasses.hlsl"

            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}