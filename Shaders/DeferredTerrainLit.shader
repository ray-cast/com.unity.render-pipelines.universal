Shader "Universal Render Pipeline/Terrain/Deferred Lit"
{
    Properties
    {
        _ControlInt("材质ID : 0-普通 1-沙漠 2-草 3-雪 4-水", Int) = 0 //注意跟EnumGroundType对应
        _Control("ID贴图", 2D) = "white" {}
        _Splat0("基本颜色 1", 2D) = "white" {}
        _Splat1("基本颜色 2", 2D) = "white" {}
        _Splat2("基本颜色 3", 2D) = "white" {}
        _Splat3("基本颜色 4", 2D) = "white" {}
        [Toggle(_NORMALMAP)]_UseNormal("启用法线", int) = 1
        [NoScaleOffset]_Normal0("法线贴图 1", 2D) = "bump" {}
        _BumpScale0("法线强度 1", Range(0, 10)) = 1.0
        [NoScaleOffset]_Normal1("法线贴图 2", 2D) = "bump" {}
        _BumpScale1("法线强度 2", Range(0, 10)) = 1.0
        [NoScaleOffset]_Normal2("法线贴图 3", 2D) = "bump" {}
        _BumpScale2("法线强度 3", Range(0, 10)) = 1.0
        [NoScaleOffset]_Normal3("法线贴图 4", 2D) = "bump" {}
        _BumpScale3("法线强度 4", Range(0, 10)) = 1.0
        [TexToggle(_USE_WETNESSMAP1)]_WetnessMap0("积水贴图 1", 2D) = "black" {}
        [TexToggle(_USE_WETNESSMAP2)]_WetnessMap1("积水贴图 2", 2D) = "black" {}
        [TexToggle(_USE_WETNESSMAP3)]_WetnessMap2("积水贴图 3", 2D) = "black" {}
        [TexToggle(_USE_WETNESSMAP4)]_WetnessMap3("积水贴图 4", 2D) = "black" {}
        _Metallic0("金属程度 1", Range(0, 1)) = 0
        _Metallic1("金属程度 2", Range(0, 1)) = 0
        _Metallic2("金属程度 3", Range(0, 1)) = 0
        _Metallic3("金属程度 4", Range(0, 1)) = 0
        _Smoothness0("光滑度 1", Range(0, 1)) = 0.5
        _Smoothness1("光滑度 2", Range(0, 1)) = 0.5
        _Smoothness2("光滑度 3", Range(0, 1)) = 0.5
        _Smoothness3("光滑度 4", Range(0, 1)) = 0.5

        [Space(20)]
        [Toggle(_SPECULAR_ANTIALIASING)] _UseSpecularHighlights("启用镜面抗锯齿", Float) = 0
        [PowerSlider(2.0)] _specularAntiAliasingThreshold("镜面抗锯齿程度", Range(0.0, 10.0)) = 1

        [Space(20)]
        _ShadowDepthBias("阴影深度偏移", Range(0.0, 10.0)) = 1.0
        _ShadowNormalBias("阴影法线偏移", Range(0.0, 10.0)) = 1.0

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
            Name "DeferredLit"
            Tags{"LightMode" = "Deferred"}

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
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _USE_WETNESSMAP1
            #pragma shader_feature_local _USE_WETNESSMAP2
            #pragma shader_feature_local _USE_WETNESSMAP3
            #pragma shader_feature_local _USE_WETNESSMAP4
            #pragma shader_feature_local _SPECULAR_ANTIALIASING

            #pragma shader_feature _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature _RECEIVE_SHADOWS_OFF

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
            #pragma multi_compile _ PROCEDURAL_INSTANCING_ON
            #pragma instancing_options procedural:SetupTerrainInstancing

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

            #pragma multi_compile _ PROCEDURAL_INSTANCING_ON
            #pragma instancing_options procedural:SetupTerrainInstancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            #include "DeferredTerrainLitInput.hlsl"
            #include "DeferredTerrainLitPasses.hlsl"
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

            #pragma multi_compile _ PROCEDURAL_INSTANCING_ON
            #pragma instancing_options procedural:SetupTerrainInstancing
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

            #pragma shader_feature _EMISSION
            #pragma shader_feature _METALLICSPECGLOSSMAP
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma shader_feature _SPECGLOSSMAP

            #include "DeferredTerrainLitInput.hlsl"
            #include "DeferredTerrainLitPasses.hlsl"

            ENDHLSL
        }
        Pass
        {
            Name "Feedback"
            Tags{"LightMode" = "Feedback"}

            ZWrite On

            HLSLPROGRAM

            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex FeedbackVertex
            #pragma fragment FeedbackFragment

            #pragma multi_compile PROCEDURAL_INSTANCING_ON

            #pragma instancing_options procedural:SetupTerrainInstancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            #include "DeferredTerrainLitInput.hlsl"
            #include "DeferredTerrainLitPasses.hlsl"

            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.DeferredTerrainLitShader"
}