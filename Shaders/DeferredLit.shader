Shader "Universal Render Pipeline/Deferred Lit"
{
    Properties
    {
        [MainColor] _BaseColor("基本颜色", Color) = (1,1,1,1)
        [MainTexture] _BaseMap("基本贴图", 2D) = "white" {}

        [Space(20)]
        [Toggle(_ALPHATEST_ON)]_UseAlphaCutoff("启用透明度剔除", int) = 0
        _Cutoff("透明度剔除阈值", Range(0.0, 1.0)) = 0.5

        [Space(20)]
        [Toggle(_STIPPLETEST_ON)]_UseStippleCutoff("启用点阵像素剔除", int) = 0
        _StippleAlpha("点阵化透明程度", Range(0.0, 1.0)) = 1
        [ToggleOff(_STIPPLETEST_VIEW_OFF)] _ViewStippleCutoff("相机范围剔除", Float) = 1.0
        _CameraRangeCutoff("相机剔除范围", Range(0.01, 10.0)) = 1
        [ToggleOff(_STIPPLETEST_TARGET_OFF)] _TargetStippleCutoff("目标范围剔除", Float) = 1.0
        _TargetRangeCutoff("目标剔除范围", Range(0.01, 10.0)) = 2
        _TargetPosition("目标世界位置", Vector) = (0, 0, 0)

        [Space(20)]
        _BumpScale("法线强度", Float) = 1.0
        [NoScaleOffset][TexToggle(_NORMALMAP)]_BumpMap("法线贴图", 2D) = "bump" {}

        [Space(20)]
        _Metallic("金属程度", Range(0.0, 1.0)) = 0.0
        _Smoothness("光滑度", Range(0.0, 1.0)) = 0.5
        _Translucency("透射程度", Range(0.0, 1.0)) = 0.0
        _SpecColor("镜面颜色", Color) = (0.2, 0.2, 0.2)
        _OcclusionStrength("遮蔽强度", Range(0.0, 1.0)) = 1.0
        [NoScaleOffset][TexToggle(_METALLICSPECGLOSSMAP)]_MetallicGlossMap("材质复合贴图", 2D) = "white" {}

        [Space(20)]
        [KeywordEnum(None, Color, Albedo, Texture)]
        _EmissionMode("自发光模式（无，颜色，主纹理，自定义纹理）", Float) = 0
        _EmissionColor("自发光颜色", Color) = (1,1,1)
        _EmissionIntensity("自发光强度", Float) = 100.0
        [NoScaleOffset]_EmissionMap("自定义发光贴图", 2D) = "white" {}

        [Space(20)]
        [TexToggle(_WETNESS_ON)]_WetnessMap("积水贴图", 2D) = "black" {}

        [Space(20)]
        [TexToggle(_DETAILMAP)]_DetailMap("细节贴图", 2D) = "black" {}
        [TexToggle(_DETAILBUMPMAP)]_DetailBumpMap("细节法线贴图", 2D) = "bump" {}
        _DetialBumpMapScale("细节法线强度", Range(0.0, 5.0)) = 1

        [Space(20)]
        [Toggle(_SPECULAR_ANTIALIASING)] _UseSpecularHighlights("启用镜面抗锯齿", Float) = 0
        _SpecularAntiAliasingThreshold("镜面抗锯齿程度", Range(0.0, 10.0)) = 0.2
        _SpecularAntiAliasingVariance("镜面抗锯齿方差", Range(0.0, 10.0)) = 0.15

        [Space(20)]
        [Toggle(_VIRTUAL_BLEND_ON)]_UseVirtualBlend("启用环境混合", int) = 0
        _VirtualBlendMaterial("环境纹理混合高度(米)", Range(0, 10)) = 0.1
        _VirtualBlendNormal("环境法线混合高度(米)", Range(0, 10)) = 0.1

        [Space(20)]
        [Toggle(_WIND_ON)]_UseWind("启用风场", int) = 0
        _WindWeight("风场影响程度", Range(0.0, 1.0)) = 1.0
        _WindStormWeight("风浪影响程度", Range(0.0, 1.0)) = 1.0

        [Space(20)]
        [Toggle]_DepthPrepass("启用深度预渲染", Float) = 0

        [Space(20)]
        _ShadowDepthBias("阴影深度偏移", Range(0.0, 10.0)) = 1.0
        _ShadowNormalBias("阴影法线偏移", Range(0.0, 10.0)) = 1.0

        [Space(20)]
        [ToggleOff(_SPECULARHIGHLIGHTS_OFF)] _SpecularHighlights("启用高光反射", Float) = 1.0
        [ToggleOff(_ENVIRONMENTREFLECTIONS_OFF)] _EnvironmentReflections("启用环境反射", Float) = 1.0

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

            Blend Off
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
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _METALLICSPECGLOSSMAP
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local _EMISSIONMODE_NONE _EMISSIONMODE_COLOR _EMISSIONMODE_ALBEDO _EMISSIONMODE_TEXTURE
            #pragma shader_feature_local _OCCLUSIONMAP
            #pragma shader_feature_local _WETNESS_ON
            #pragma shader_feature_local _DETAILMAP
            #pragma shader_feature_local _DETAILBUMPMAP

            #pragma shader_feature _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature _RECEIVE_SHADOWS_OFF

            #pragma shader_feature_local _WIND_ON
            #pragma shader_feature_local _VIRTUAL_BLEND_ON
            #pragma shader_feature_local _SPECULAR_ANTIALIASING

            #pragma multi_compile_local _ _STIPPLETEST_ON

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
            #pragma multi_compile _ _VIRTUAL_TEXTURE_HQ

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "DeferredLitInput.hlsl"
            #include "DeferredLitGbufferPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature_local _WIND_ON
            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma vertex ShadowBiasPassVertex
            #pragma fragment ShadowPassFragment

            #include "DeferredLitInput.hlsl"
            #include "DeferredLitGbufferPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "PrepassDepth"
            Tags{"LightMode" = "PrepassDepth"}

            ZWrite On
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local _WIND_ON

            #pragma multi_compile_local _ _STIPPLETEST_ON

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "DeferredLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local _WIND_ON

            #pragma multi_compile_local _ _STIPPLETEST_ON

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "DeferredLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
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

            #include "DeferredLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"

            ENDHLSL
        }
        Pass
        {
            Name "Universal2D"
            Tags{ "LightMode" = "Universal2D" }

            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _ALPHAPREMULTIPLY_ON

            #include "DeferredLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Universal2D.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.DeferredLitShader"
}