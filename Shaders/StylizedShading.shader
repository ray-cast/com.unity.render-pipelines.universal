Shader "Universal Render Pipeline/Stylized Lit"
{
    Properties
    {
        [Foldout(1, 2, 0, 0)]_AlbedoFoldout ("反照率贴图_Foldout", Float) = 1
        [Tex(_BaseColor)]_BaseMap ("基本贴图", 2D) = "white" { }
        [HideInInspector]_BaseColor ("基本颜色", Color) = (1, 1, 1, 1)

        [Foldout(1, 2, 0, 0)]_AlphaFoldout ("透明度_Foldout", Float) = 1
        [Queue] _Surface("渲染队列", Float) = 0
        [BlendSwitcher]_Blend("透明混合模式", Float) = 0
        [ShowIf(_Surface)] _AlphaScale("透明程度", Range(0.0, 1.0)) = 1
        [HideInInspector][TogglePass(TransparentDepthPrepass)]_TransparentPreDepth("启用提前透明深度写入", Float) = 0
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("启用透明度剔除", Float) = 0
        [ShowIf(_AlphaClip)] _Cutoff("透明度剔除阈值", Range(0.0, 1.0)) = 0.5

        [Foldout(1, 2, 0, 0)]_NormalFoldout ("法线贴图_Foldout", Float) = 1
        [KeywordEnum(Tangent, World)]
        _NormalModel("法线贴图模式（切线空间，世界空间）", Float) = 0
        [TexToggle(_NORMALMAP)][NoScaleOffset]_NormalMap("法线贴图", 2D) = "grey" {}
        [EqualIf(_NormalModel, 0)]_NormalScale("法线强度", Range(-10, 10)) = 1.0
        [Toggle(_USE_COLOR_NORMAL_ON)] _UseColorNormal("启用顶点颜色法线", Float) = 0

        [Foldout(1, 2, 0, 0)]_MutilMaterialsFoldout("复合贴图_Foldout", Float) = 1
        [TexToggle(_METALLICSPECGLOSSMAP)]_MetallicGlossMap("R=金属 G=AO B=自发光区域 A=光滑", 2D) = "white" {}
        [TexToggle(_HAIRCLOTHMAP)]_Mask2("R=各向异性偏移贴图 G=曲率贴图, B=清漆程度, A=厚度贴图", 2D) = "black" {}

        [Foldout(1, 2, 0, 0)]_DisplacmentFoldout("位移贴图_Foldout", Float) = 1
        [TexToggle(_DISPLACMENTMAP)][NoScaleOffset]_DisplacmentMap("位移贴图", 2D) = "grey" {}
        _DisplacmentStrength("位移程度", Range(-1.0, 1.0)) = 0.0

        [Foldout(1, 2, 0, 0)]_BrushFoldout("漫反射笔刷_Foldout", Float) = 1
        _BaseLayerColor("基础笔刷颜色", Color) = (1, 1, 1)
        _FirstLayerColor("亮调笔刷颜色", Color) = (0.8901, 0.4784, 0.3647, 0.25)
        _SecondLayerColor("中调高光颜色", Color) = (0.7725, 0.3843, 0.4078, 0.8)
        _ThirdLayerColor("中调阴影颜色", Color) = (0.6117, 0.2862, 0.3450, 0.9)
        _FourLayerColor("暗调笔刷颜色", Color) = (0.6117, 0.2862, 0.3450, 0.9)
        _DiffuseShadowStrength("阴影笔刷程度", Color) = (0, 0, 0, 0)
        _FirstLayerOffset("亮调偏移程度", Range(-1.0, 1.0)) = 0
        _SecondLayerOffset("中调亮部偏移", Range(-1.0, 1.0)) = 0
        _ThirdLayerOffset("中调暗部偏移", Range(-1.0, 1.0)) = 0
        _FourLayerOffset("暗调偏移程度", Range(-1.0, 1.0)) = 0
        _DiffuseBrushStrength("笔刷影响程度", Range(0.0, 1.0)) = 1.0
        _Softness("笔刷柔和程度", Range(0.0, 1.0)) = 1.0
        _LightOcclusionStrength("笔刷环境遮蔽程度", Range(0.0, 1.0)) = 1.0
        [Toggle(_VIEWBRUSH_ON)] _UseViewBrush ("启用视线方向照明", Float) = 0

        [Foldout(1, 2, 1, 0)]_Subsurface("次表面散射_Foldout", Float) = 1
        _SubsurfaceStrength("次表面散射程度", Range(0, 1)) = 0.0
        _SubsurfaceBlurStrength("次表面模糊程度", Range(0, 1)) = 0.0

        [Foldout(1, 2, 1, 0)]_Translucency("次表面透射_Foldout", Float) = 1
        _TranslucencyColor("次表面透射颜色", Color) = (1,0,0,1)
        _TranslucencyStrength("次表面透射程度", Range(0, 1)) = 0.0
        _TranslucencyScale("次表面透射强度", Range(0.0, 10)) = 1.0
        _TranslucencyAmbient("全局透射程度", Range(0.0, 1)) = 0.0
        _TranslucencyDistortion("次表面投射密度", Range(0, 1)) = 0.1
        _TranslucencyPower("次表面衰减速率", Range(0.01, 10)) = 4

        [Foldout(1, 2, 1, 0)]_SpecularHighlights ("高光反射_Foldout", Float) = 1
        [KeywordEnum(Standard, Anisotropy, KajiyaKay, Skin)]
        _LightModel("反射模型（标准，物理各向异性，快速各向异性）", Float) = 0
        _SpecularColor("反射颜色", Color) = (1,1,1,1)
        _Specular("反射程度", Range(0.0, 1.0)) = 0.5
        _Smoothness("光滑度", Range(0.0, 1.0)) = 0.5
        _Metallic("金属程度", Range(0.0, 1.0)) = 0.0
        [EqualIf(_LightModel, 1)]_Anisotropy("各向异性程度", Range(0.0, 1.0)) = 0.0
        [EqualIf(_LightModel, 1, 2)]_AnisotropyOffset("各向异性偏移程度", Range(-2.0, 2.0)) = 0.0
        [EqualIf(_LightModel, 1, 2)]_AnisotropyShift("各向异性扰动程度", Range(0.0, 1.0)) = 1.0

        [Foldout(1, 2, 1, 0)]_ClearCoatHighlights("高光反射（双层高光、清漆、布料）_Foldout", Float) = 0
        [KeywordEnum(GGX, Anisotropy, KajiyaKay, Skin, Cloth)]
        _ClearCoatModel("反射模型（标准，物理各向异性，快速各向异性，皮肤，布料）", Float) = 0
        _ClearCoatColor("反射颜色", Color) = (1,1,1,1)
        _ClearCoatSpecular("反射程度", Range(0.0, 1.0)) = 0.5
        _ClearCoatSmoothness("光滑度", Range(0.0, 1.0)) = 0.5
        _ClearCoat("清漆厚度", Range(0.0, 1.0)) = 0.0
        _ClearCoatBumpStrength("清漆法线强度", Range(0.0, 1.0)) = 1.0
        [EqualIf(_ClearCoatModel, 1)]_ClearCoatAnisotropy("各向异性程度", Range(0.0, 1.0)) = 0.0
        [EqualIf(_ClearCoatModel, 1, 2)]_ClearCoatAnisotropyOffset("各向异性偏移程度", Range(-2.0, 2.0)) = 0.0
        [EqualIf(_ClearCoatModel, 1, 2)]_ClearCoatAnisotropyShift("各向异性扰动程度", Range(0.0, 1.0)) = 0.0

        [Foldout(1, 2, 1, 0)]_Matcap("材质光照（MatCap）_Foldout", Float) = 0
        [Tex][NoScaleOffset]_MatcapMap("光照贴图", 2D) = "black" {}

        [Foldout(1, 2, 1, 0)]_BackLight("背光_Foldout", Float) = 0
        [HDR]_RimColor("背光颜色", Color) = (1,1,1,1)
        [Tex][NoScaleOffset]_RimLightLookup("背光查找表", 2D) = "black" {}
        [Tex][NoScaleOffset]_RimLightMask("背光遮罩", 2D) = "black" {}

        [Foldout(1, 2, 1, 0)]_EnvironmentLight("环境光_Foldout", Float) = 1
        [ToggleOff(_ENVIRONMENTREFLECTIONS_OFF)] _EnvironmentReflections("启用环境反射", Float) = 1.0
        _OcclusionLimit("环境光最低亮度", Range(0.0, 2.0)) = 0.5
        _OcclusionStrength("环境光遮蔽程度", Range(0.0, 1.0)) = 1.0
        _AmbientDecoloration("环境光去色程度", Range(0.0, 1.0)) = 1

        [Foldout(1, 2, 0, 0)]_EmissiveFoldout("自发光_Foldout", Float) = 1
        [KeywordEnum(None, Color, Albedo, Texture)]
        _EmissionMode("自发光模式（无，颜色，主纹理，自定义纹理）", Float) = 0
        _EmissionColor("自发光颜色", Color) = (1,1,1)
        _EmissionIntensity("自发光强度", Float) = 100.0
        [EqualIf(_EmissionMode, 3)][NoScaleOffset]_EmissionMap("自定义发光贴图", 2D) = "white" {}

        [Foldout(1, 2, 0, 0)]_OutGlowFoldout("外发光_Foldout", Float) = 1
        [TogglePass(OutGlow)]_OutGlowEnable("启用外发光", Float) = 0
        [KeywordEnum(Color, Albedo, Texture)]
        _OutGlowMode("外发光模式（无，颜色，自定义纹理）", Float) = 0
        _OutGlowColor("外发光颜色", Color) = (1,1,1)
        _OutGlowIntensity("外发光强度", Float) = 100.0
        [EqualIf(_OutGlowMode, 2)][NoScaleOffset]_OutGlowMap("自定义外发光贴图", 2D) = "white" {}

        [Foldout(1, 2, 0, 0)]_RimFoldout("边缘光_Foldout", Float) = 1
        [KeywordEnum(None, View, LightDir, Custom)]_RimMode("边缘光模式（无，视线方向，主光源方向，自定义方向）", Float) = 0
        [HDR]_RimLightColor ("边缘颜色，程序会动态控制", Color) = (0,0,0.7,1)
        [HDR]_RimLightColor2("附加外发光颜色", Color) = (0,0,0,0)
        _Rim_Cover ("边缘光环境遮蔽程度", Range(0, 1)) = 1
        _RimLight_Pow ("边缘衰减速率", Range(0.01, 10)) = 4
        [Toggle(_RIM_COLOR_NORMAL_ON)] _RimUseColorNormal("使用顶点颜色法线", Float) = 0
        [Toggle(_RIM_BUMPMAP_ON)] _Caustics("使用贴图法线（不勾选使用顶点法线）", Float) = 0
        [EqualIf(_RimMode, 3)]_Rim_Dir ("自定义遮蔽方向", Vector) = (-1.25,1.25,0,0)

        [Foldout(1, 2, 2, 0)]_Outline("勾边_Foldout", Float) = 1
        _OutlineStrength("勾边宽度", Range(0.0, 1.0)) = 0.1
        [HDR]_OutlineColor("勾边颜色", Color) = (0, 0, 0)
        _OutlineLineStrength("顶点颜色宽度影响", Range(0.0, 1.0)) = 0
        _OutlineNormalStrength("顶点颜色法线影响", Range(0.0, 1.0)) = 0

        [Foldout(1, 2, 0, 0)]_ShadowFoldout("阴影_Foldout", Float) = 1
        [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows("接收阴影", Float) = 1.0
        [HideInInspector][Toggle(_MAIN_LIGHT_EXPONENTIAL_SHADOWS)] _EnableExponentialShadows("使用指数阴影", Float) = 0.0
        _ShadowDepthBias("阴影深度偏移", Range(0.0, 10.0)) = 1.0
        _ShadowNormalBias("阴影法线偏移", Range(0.0, 10.0)) = 1.0
      
        [Foldout(1, 2, 1, 0)]_SpecularAntiAliasing("镜面抗锯齿_Foldout", Float) = 1
        _SpecularAntiAliasingThreshold("镜面抗锯齿阈值", Range(0.0, 10.0)) = 0.2
        _SpecularAntiAliasingVariance("镜面抗锯齿方差", Range(0.0, 10.0)) = 0.15

        [Foldout(1, 2, 1, 0)]_ViewAlpha("视线透明衰减_Foldout", Float) = 0
        [Toggle(_ALPHA_ADDITIONAL_NORMAL_ON)]_UseAdditionalNormal("使用附加颜色法线", Float) = 1
        _AlphaViewBase("视线基本衰减", Range(0.0, 1.0)) = 0
        _AlphaViewPower("视线衰减速率", Range(0.0, 4.0)) = 1

        [Foldout(1, 2, 1, 0)]_StippleTest("点阵像素剔除_Foldout", Float) = 0
        _StippleAlpha("点阵化透明程度", Range(0.0, 1.0)) = 1
        [ToggleOff(_STIPPLETEST_VIEW_OFF)] _ViewStippleCutoff("相机范围剔除", Float) = 0
        _CameraRangeCutoff("相机剔除范围", Range(0.01, 10.0)) = 1
        [ToggleOff(_STIPPLETEST_TARGET_OFF)] _TargetStippleCutoff("目标范围剔除", Float) = 0
        _TargetRangeCutoff("目标剔除范围", Range(0.01, 10.0)) = 1
        _TargetPosition("目标世界位置", Vector) = (0, 0, 0)

        // OVERLAY ,溶解
        [Foldout(1, 2, 0, 0)]_DissolveFoldout("溶解_Foldout", Float) = 1
        [Toggle(_OVERLAY_DISSOLVE)]_UseDissolve("启用溶解", Float) = 0
        _DissolveShowStep("溶解进度", Range(0,1)) = 1.0 //出生进度
        _OverlayExponential("特效进度指数衰减", Float) = 3
        _OffsetY("OffsetY中心位置Y方向偏移量", Float) = 0//玩家位置Y轴偏移
        _MaxDistance ("特效中心距离模型表面的最大距离", Float) = 1//骨骼最大距离
        [HDR][Picker]_DissolveColor("溶解颜色", Color) = (0.376470, 0.376470, 0.376470, 0)
        [HDR][Picker]_DissolveEdgeColor("溶解边缘颜色", Color) = (0.8760047, 1.24855843, 1.92318274,0)
        _DissolveThreshold("溶解颜色和原色的阈值", Range(0.0, 1.0)) = 0.0
        _DissolveEdgeThreshold("溶解颜色和边界颜色阈值", Range(0.0, 1.0)) = 0.878
        [NoScaleOffset]_DissolveMap("溶解遮罩图（示例：NoiseTexture）", 2D) = "white" {}

        [Foldout(1, 2, 0, 0)]_PipelineFoldout("管线设置_Foldout", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("剔除模式", Float) = 2.0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("深度测试", Float) = 4
        [Enum(Off, 0, On, 1)] _ZWrite ("深度写入", Float) = 1

        [Foldout(1, 2, 0, 0)]_DebugFoldout("调试_Foldout", Float) = 1
        [KeywordEnum(None, Albedo, Normal, NormalEx, ColorAlpha, Occlusion, Smoothness, Metallic, ClearCoat, Translucency)]
        _DebugMode("调试输出", Float) = 0
        [TexToggle(_FACIALSHADOWMAP)][NoScaleOffset]_FacialShadowMap("光照贴图", 2D) = "white" {}
        _LightShadowStrength("光照贴图暗部程度", Range(0.0, 1.0)) = 0.5
        [Toggle(_FACTATTENUATION_ON)]_UseFaceAttenuation("启用面部衰减", Float) = 0
        _FaceDirection("面部朝向", Vector) = (0, 0, 1, 1)

        //Scratch Cutting 刀痕
        [HideInInspector] _CuttingTex ("Cutting Texture", 2D) = "black" {}
        [HideInInspector] _CuttingTimeFactor("Cutting Time Factor", Float) =  0

        // ObsoleteProperties
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _GlossMapScale("Smoothness", Float) = 0.0
        [HideInInspector] _Glossiness("Smoothness", Float) = 0.0

        // Blending state
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        // Editmode props
        [HideInInspector] _QueueOffset("Queue offset", Float) = 0.0
    }
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BumpMap_TexelSize;
            float4 _Mask2_ST;
            half4 _BaseColor;
            half3 _SpecularColor;
            half  _Specular;
            half  _AlphaScale;
            half  _AlphaViewBase;
            half  _AlphaViewPower;
            half  _Cutoff;
            half  _Smoothness;
            half  _EmissionIntensity;
            half  _Metallic;
            half  _Softness;

            half _SubsurfaceStrength;
            half _SubsurfaceBlurStrength;

            half _Translucency;
            half _TranslucencyScale;
            half _TranslucencyPower;
            half _TranslucencyAmbient;
            half _TranslucencyDistortion;
            half3 _TranslucencyColor;

            half  _Anisotropy;
            half  _AnisotropyOffset;
            half  _AnisotropyShift;

            half  _ClearCoat;
            half  _ClearCoatBumpStrength;
            half  _ClearCoatSpecular;
            half  _ClearCoatSmoothness;
            half  _ClearCoatAnisotropy;
            half  _ClearCoatAnisotropyOffset;
            half  _ClearCoatAnisotropyShift;
            half3 _ClearCoatColor;

            half  _DisplacmentStrength;

            half  _OutlineStrength;
            half  _OutlineLineStrength;
            half  _OutlineNormalStrength;
            half3 _OutlineColor;

            half  _NormalScale;
            half  _OcclusionLimit;
            half  _OcclusionStrength;
            half  _AmbientDecoloration;
            half3 _BaseLayerColor;
            half4 _FirstLayerColor;
            half4 _SecondLayerColor;
            half4 _ThirdLayerColor;
            half4 _FourLayerColor;
            half  _FirstLayerOffset;
            half  _SecondLayerOffset;
            half  _ThirdLayerOffset;
            half  _FourLayerOffset;
            half  _FirstLayerStrength;
            half  _SecondLayerStrength;
            half  _ThirdLayerStrength;
            half  _FirstShadowStrength;
            half  _SecondShadowStrength;
            half  _ThirdShadowStrength;
            half  _DiffuseBrushStrength;
            half4 _DiffuseShadowStrength;
            half  _LightShadowStrength;
            half  _LightOcclusionStrength;

            half3 _RimColor;
            half3 _FaceDirection;

            half  _SpecularAntiAliasingThreshold;
            half  _SpecularAntiAliasingVariance;

            half _StippleAlpha;
            half _CameraRangeCutoff;
            half _TargetRangeCutoff;
            half3 _TargetPosition;

            half3 _EmissionColor;
            half3 _DiffuseColor;

            half3 _RimLightColor;
            half3 _RimLightColor2;
            half _RimLight_Pow;
            half _Rim_Cover;
            half4 _Rim_Dir;

            half _OutGlowIntensity;
            half3 _OutGlowColor;
            half4 _OutGlowMap_ST;

            half _DissolveShowStep;
            half _OffsetY;
            half _MaxDistance;
            half _OverlayExponential;

            half4 _DissolveColor;
            half4 _DissolveEdgeColor;
            half _DissolveThreshold;
            half _DissolveEdgeThreshold;

            half _CuttingTimeFactor;

            half _ShadowDepthBias;
            half _ShadowNormalBias;
        CBUFFER_END

        TEXTURE2D(_NormalMap);          SAMPLER(sampler_NormalMap);
        TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_Mask2);              SAMPLER(sampler_Mask2);
        TEXTURE2D(_FacialShadowMap);    SAMPLER(sampler_FacialShadowMap);
        TEXTURE2D(_SkinRamp);           SAMPLER(sampler_SkinRamp);
        TEXTURE2D(_DissolveMap);        SAMPLER(sampler_DissolveMap);
        TEXTURE2D(_CuttingTex);         SAMPLER(sampler_CuttingTex);
        TEXTURE2D(_OutGlowMap);         SAMPLER(sampler_OutGlowMap);
        TEXTURE2D(_RimLightLookup);     SAMPLER(sampler_RimLightLookup);
        TEXTURE2D(_RimLightMask);       SAMPLER(sampler_RimLightMask);
        TEXTURE2D(_MatcapMap);          SAMPLER(sampler_MatcapMap);
        TEXTURE2D(_DisplacmentMap);     SAMPLER(sampler_DisplacmentMap);
    ENDHLSL
    SubShader
    {
        // Universal Pipeline tag is required. If Universal render pipeline is not set in the graphics settings
        // this Subshader will fail. One can add a subshader below or fallback to Standard built-in to make this
        // material work with both Universal Render Pipeline and Builtin Unity Pipeline
        Tags{"Queue" = "Geometry+100" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
        LOD 300

        // ------------------------------------------------------------------
        //  Forward pass. Shades all light in a single pass. GI + emission + Fog
        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "ForwardLit"
            Tags{"LightMode" = "UniversalForward"}

            Blend[_SrcBlend][_DstBlend]
            ZTest [_ZTest] ZWrite [_ZWrite]
            Cull[_Cull]

            Stencil
            {
                Ref 128
                Comp Always
                Pass Replace 
                ReadMask 128
                WriteMask 128
            }

            HLSLPROGRAM

            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON

            #pragma shader_feature_local _SPECULARHIGHLIGHTS_ON
            #pragma shader_feature_local _FACIALSHADOWMAP
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _VIEWALPHA_ON
            #pragma shader_feature_local _ALPHA_ADDITIONAL_NORMAL_ON
            #pragma shader_feature_local _METALLICSPECGLOSSMAP
            #pragma shader_feature_local _DISPLACMENTMAP
            #pragma shader_feature_local _HAIRCLOTHMAP
            #pragma shader_feature_local _NORMALMODEL_TANGENT _NORMALMODEL_WORLD
            #pragma shader_feature_local _EMISSIONMODE_NONE _EMISSIONMODE_COLOR _EMISSIONMODE_ALBEDO _EMISSIONMODE_TEXTURE
            #pragma shader_feature_local _RIMMODE_NONE _RIMMODE_VIEW _RIMMODE_LIGHTDIR _RIMMODE_CUSTOM
            #pragma shader_feature_local _RIM_BUMPMAP_ON
            #pragma shader_feature_local _RIM_COLOR_NORMAL_ON
            #pragma shader_feature_local _OVERLAY_DISSOLVE
            #pragma shader_feature_local _SCRATCH_CUTTING
            #pragma shader_feature_local _USE_COLOR_NORMAL_ON
            #pragma shader_feature_local _VIEWBRUSH_ON

            #pragma shader_feature_local _MATCAP_ON
            #pragma shader_feature_local _BACKLIGHT_ON
            #pragma shader_feature_local _FACTATTENUATION_ON
            #pragma shader_feature_local _SPECULARANTIALIASING_ON
            #pragma shader_feature_local _SPECULAR_ANTIALIASING
            #pragma shader_feature_local _TRANSLUCENCY_THICKNESS_OFF
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            //#pragma shader_feature_local _MAIN_LIGHT_EXPONENTIAL_SHADOWS

            #pragma shader_feature_local _DEBUGMODE_NONE _DEBUGMODE_ALBEDO _DEBUGMODE_NORMAL _DEBUGMODE_NORMALEX _DEBUGMODE_COLORALPHA _DEBUGMODE_METALLIC _DEBUGMODE_SMOOTHNESS _DEBUGMODE_OCCLUSION _DEBUGMODE_CLEARCOAT _DEBUGMODE_TRANSLUCENCY
            #pragma shader_feature_local _LIGHTMODEL_STANDARD _LIGHTMODEL_ANISOTROPY _LIGHTMODEL_KAJIYAKAY _LIGHTMODEL_SKIN
            #pragma shader_feature_local _CLEARCOATMODEL_GGX _CLEARCOATMODEL_ANISOTROPY _CLEARCOATMODEL_KAJIYAKAY _CLEARCOATMODEL_SKIN _CLEARCOATMODEL_CLOTH

            #pragma multi_compile _ _STIPPLETEST_ON
            #pragma shader_feature _STIPPLETEST_VIEW_OFF
            #pragma shader_feature _STIPPLETEST_TARGET_OFF

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_CLOUD_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE

            #pragma shader_feature _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local _CLEARCOATHIGHLIGHTS_ON
            #pragma shader_feature_local _ENVIRONMENTLIGHT_ON
            #pragma shader_feature_local _TRANSLUCENCY_ON
            #pragma shader_feature _ENVIRONMENTREFLECTIONS_OFF

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            struct Attributes
            {
                float4 positionOS    : POSITION;
                float3 normalOS      : NORMAL;
                float4 tangentOS     : TANGENT;
                float4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float2 lightmapUV    : TEXCOORD1;//用来读取二套UV，刀痕
                float4 directionalSH : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
            #if defined(_SCRATCH_CUTTING)
                float4 uv                       : TEXCOORD0;
            #else
                float2 uv                       : TEXCOORD0;
            #endif

                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 1);

                float3 viewDirWS                : TEXCOORD2;
                float3 normalWS                 : TEXCOORD3;
                float4 tangentWS                : TEXCOORD4; // xyz: tangent, w: viewDir.y              
                float3 positionWS               : TEXCOORD5;
                float4 screenPos                : TEXCOORD6;                

            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord              : TEXCOORD7;
            #endif

                half4 directionalSH             : TEXCOORD8;
                half4 colorNormalWS             : TEXCOORD9;

                float4 positionCS               : SV_POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            struct PhysicalMaterial
            {
                half3 albedo;
                half3 diffuse;
                half3 specular;
                half  metallic;
                half  smoothness;
                half  shift;
                half3 emission;
                half  softness;
                half  occlusion;
                half  anisotropy;
                half  subsurface;
                half  translucency;
                half  translucencyScale;
                half  translucencyPower;
                half  translucencyAmbient;
                half  translucencyDistortion;
                half3 translucencyColor;
                half  alpha;

                half  clearcoat;
                half  clearcoatShift;
                half  clearcoatSmoothness;
                half  clearcoatAnisotropy;
                half  clearcoatBumpStrength;
                half3 clearcoatSpecular;
                
                half3 normalTS;
                half3 normalLowTS;

                half3 baseLayerColor;
                half3 firstLayerColor;
                half3 secondLayerColor;
                half3 thirdLayerColor;
                half3 fourLayerColor;
                half4 diffuseShadowStrength;
                half  diffuseBrushStrength;
                half  lightShadowStrength;
                half  lightOcclusionStrength;

                half2 facialShadow;

                half  ambientDecoloration;
                half  ambientOcclusion;
                half  ambientLuminanceLimit;
                
                half4 mask2;
            };

            struct GeometryContext
            {
                float3  positionWS;
                half3   normalWS;
                half3   normalLowWS;
                half3   colorNormalWS;
                half3   domainNormal;
                half3   vertexNormalWS;
                half3   tangentWS;
                half3   bitangentWS;
                half3   clearcoatNormalWS;
                half3   clearcoatTangentWS;
                half3   clearcoatBitangentWS;
                half3   viewDirectionWS;
                float4  shadowCoord;
                half3   bakedGI;

                half    grazingTerm;

                half    gloss;
                half    roughness;
                half    roughness2;
                half    roughness2MinusOne;
                half    perceptualRoughness;
                half    normalizationTerm;

                half    clearcoatGloss;
                half    clearcoatPerceptualRoughness;
                half    clearcoatRoughness;
                half    clearcoatRoughness2;
                half    clearcoatRoughness2MinusOne;
                half    clearcoatNormalizationTerm;
            };

            float2 SampleDisplacment(TEXTURE2D_PARAM(displacmentMap, sampler_DisplacmentMap), float2 uv, half3 V, half strength) {
                half depth =  SAMPLE_TEXTURE2D(_DisplacmentMap, sampler_DisplacmentMap, uv).r;
                half2 offset = mul((float2x3)unity_WorldToObject, V) * depth;
                //offset.y = -offset.y;
                return uv + offset * strength;
            }

            float2 PBReflection(float2 ouv, half3 V, half depth, half3 frontW, half3 refractedW, half center) {
                float cosAlpha = dot(frontW, -refractedW);
                float dist = depth/cosAlpha;
                float3 offsetW = dist * refractedW;
                float2 offsetL = mul((float2x3)unity_WorldToObject, offsetW);
                offsetL.y = -offsetL.y;
                return ouv + offsetL;
            }

            void InitializePhysicalMaterial(Varyings input, out PhysicalMaterial material)
            {
                half2 uv = input.uv.xy;
            #ifdef _DISPLACMENTMAP
                uv = SampleDisplacment(TEXTURE2D_ARGS(_DisplacmentMap, sampler_DisplacmentMap), uv, normalize(input.viewDirWS.xyz), _DisplacmentStrength);
            #endif

                half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
                material.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);

                half4 specGloss;
            #ifdef _METALLICSPECGLOSSMAP
                specGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);
                specGloss.r *= _Metallic;
                specGloss.a *= _Smoothness;
            #else
                specGloss = half4(_Metallic.r, 1, 0, _Smoothness);
            #endif

                material.albedo = albedoAlpha.rgb * _BaseColor.rgb;
                material.metallic = specGloss.r;
                material.smoothness = specGloss.a;

            #if defined(_NORMALMODEL_WORLD) && defined(_NORMALMAP)
                material.normalTS = reflect(UnpackNormalRGBNoScale(pow(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), 1.0f / 2.2f)), float3(1, 0, 0));
                material.normalLowTS = reflect(UnpackNormalRGBNoScale(SAMPLE_TEXTURE2D_LOD(_NormalMap, sampler_NormalMap, uv, _SubsurfaceBlurStrength * 7)), float3(1, 0, 0));
            #else
                material.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_NormalMap, sampler_NormalMap), _NormalScale);
                material.normalLowTS = SampleNormal(uv, TEXTURE2D_ARGS(_NormalMap, sampler_NormalMap), _NormalScale, _SubsurfaceBlurStrength * 7);
            #endif
                material.softness = _Softness;
                material.occlusion = specGloss.g;
                material.baseLayerColor = _BaseLayerColor;
                material.firstLayerColor = lerp(_BaseLayerColor, _FirstLayerColor.rgb, _FirstLayerColor.a);
                material.secondLayerColor = lerp(_BaseLayerColor, _SecondLayerColor.rgb, _SecondLayerColor.a);
                material.thirdLayerColor = lerp(_BaseLayerColor, _ThirdLayerColor.rgb, _ThirdLayerColor.a);
                material.fourLayerColor = lerp(_BaseLayerColor, _FourLayerColor.rgb, _FourLayerColor.a);
                material.diffuseBrushStrength = _DiffuseBrushStrength;
                material.diffuseShadowStrength = _DiffuseShadowStrength;
                material.lightShadowStrength = _LightShadowStrength;
                material.lightOcclusionStrength = _LightOcclusionStrength;
                material.ambientDecoloration = _AmbientDecoloration;
                material.ambientOcclusion = LerpWhiteTo(specGloss.g, _OcclusionStrength);
                material.ambientLuminanceLimit = _OcclusionLimit;
                material.anisotropy = _Anisotropy;

            #ifdef _FACIALSHADOWMAP
                material.facialShadow.r = Gamma22ToLinear(SAMPLE_TEXTURE2D(_FacialShadowMap, sampler_FacialShadowMap, uv).r);
                material.facialShadow.g = Gamma22ToLinear(SAMPLE_TEXTURE2D(_FacialShadowMap, sampler_FacialShadowMap, float2(1 - uv.x, uv.y)).r);
            #else
                material.facialShadow.r = 0;
                material.facialShadow.g = 0;
            #endif

            #if _EMISSIONMODE_COLOR
                material.emission = _EmissionColor * _EmissionIntensity;
            #elif _EMISSIONMODE_ALBEDO
                material.emission = lerp(0, material.albedo * _EmissionColor * _EmissionIntensity, specGloss.b);
            #elif _EMISSIONMODE_TEXTURE
                material.emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor * _EmissionIntensity;
            #else
                material.emission = 0;
            #endif

            #ifdef _MATCAP_ON
                float2 diffuseUVAndMatCapCoords;
                float3 normalWS = normalize(input.normalWS.xyz);
                diffuseUVAndMatCapCoords.x = dot(normalize(transpose(unity_MatrixInvV)[0].xyz), normalWS);
                diffuseUVAndMatCapCoords.y = dot(normalize(transpose(unity_MatrixInvV)[1].xyz), normalWS);
                material.emission += material.albedo * SAMPLE_TEXTURE2D(_MatcapMap, sampler_MatcapMap, diffuseUVAndMatCapCoords * 0.5 + 0.5);
            #endif

                material.mask2 = 0;
                material.shift = _AnisotropyOffset;

                material.subsurface = _SubsurfaceStrength;

                material.translucency = _Translucency;
                material.translucencyAmbient = _TranslucencyAmbient;
                material.translucencyColor = _TranslucencyColor;
                material.translucencyDistortion = _TranslucencyDistortion;
                material.translucencyPower = _TranslucencyPower;
                material.translucencyScale = _TranslucencyScale;

                material.clearcoat = _ClearCoat;
                material.clearcoatAnisotropy = _ClearCoatAnisotropy;
                material.clearcoatShift = _ClearCoatAnisotropyOffset;
                material.clearcoatSpecular = _ClearCoatColor * _ClearCoatSpecular * _ClearCoatSpecular * 0.16;
                material.clearcoatSmoothness = _ClearCoatSmoothness;
                material.clearcoatBumpStrength = _ClearCoatBumpStrength;

            #if _HAIRCLOTHMAP
                material.mask2 = SAMPLE_TEXTURE2D(_Mask2, sampler_Mask2, uv * _Mask2_ST.xy + _Mask2_ST.zw);
                material.shift = _AnisotropyOffset + lerp(0, material.mask2.r * 2 - 1, _AnisotropyShift);
                material.clearcoat *= material.mask2.b;
                material.clearcoatShift = _ClearCoatAnisotropyOffset + lerp(0, material.mask2.r * 2 - 1, _ClearCoatAnisotropyShift);
                material.subsurface *= material.mask2.g;
                material.translucency *= material.mask2.a;
            #endif

                half oneMinusReflectivity = OneMinusReflectivityMetallic(material.metallic);
                half reflectivity = 1.0 - oneMinusReflectivity;

                material.diffuse = material.albedo * oneMinusReflectivity;
                material.specular = lerp(_SpecularColor * _Specular * _Specular * 0.16, material.albedo, material.metallic);

            #ifdef _ALPHAPREMULTIPLY_ON
                material.diffuse *= alpha;
                alpha = alpha * oneMinusReflectivity + reflectivity;
            #endif
            }

            void InitializeGeometryContext(Varyings input, PhysicalMaterial material, out GeometryContext geometryContext)
            {
            #if _USE_COLOR_NORMAL_ON
                float3 domainNormal = input.colorNormalWS;
            #else
                float3 domainNormal = input.normalWS.xyz;
            #endif

                geometryContext = (GeometryContext)0;
                geometryContext.positionWS = input.positionWS;

            #if defined(_NORMALMODEL_TANGENT) && defined(_NORMALMAP)
                float sgn = input.tangentWS.w;      // should be either +1 or -1
                float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                geometryContext.normalWS = TransformTangentToWorld(material.normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz));
                geometryContext.colorNormalWS = TransformTangentToWorld(material.normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, input.colorNormalWS.xyz));
                geometryContext.domainNormal = TransformTangentToWorld(material.normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, domainNormal.xyz));
                geometryContext.normalLowWS = TransformTangentToWorld(material.normalLowTS, half3x3(input.tangentWS.xyz, bitangent.xyz, domainNormal.xyz));
            #elif defined(_NORMALMODEL_WORLD) && defined(_NORMALMAP)
                float sgn = input.tangentWS.w;      // should be either +1 or -1
                float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                geometryContext.normalWS = material.normalTS;
                geometryContext.colorNormalWS = input.colorNormalWS.xyz;
                geometryContext.domainNormal = material.normalTS;
                geometryContext.normalLowWS = material.normalLowTS;
            #else
                geometryContext.normalWS = input.normalWS.xyz;
                geometryContext.normalLowWS = input.normalWS.xyz;
                geometryContext.colorNormalWS = input.colorNormalWS.xyz;
                geometryContext.domainNormal = domainNormal;
            #endif

                geometryContext.normalWS = NormalizeNormalPerPixel(geometryContext.normalWS);
                geometryContext.normalLowWS = NormalizeNormalPerPixel(geometryContext.normalLowWS);
                geometryContext.domainNormal = NormalizeNormalPerPixel(geometryContext.domainNormal);
                geometryContext.vertexNormalWS = NormalizeNormalPerPixel(input.normalWS.xyz);
                geometryContext.colorNormalWS = NormalizeNormalPerPixel(geometryContext.colorNormalWS);
                geometryContext.bitangentWS = NormalizeNormalPerPixel(cross(geometryContext.normalWS, float3(0, 1, 0)));
                geometryContext.viewDirectionWS = SafeNormalize(input.viewDirWS);

                half3 tangentWS = cross(geometryContext.normalWS, geometryContext.bitangentWS);

            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                geometryContext.shadowCoord = input.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                geometryContext.shadowCoord = TransformWorldToShadowCoord(geometryContext.positionWS);
            #else
                geometryContext.shadowCoord = float4(0, 0, 0, 0);
            #endif

                geometryContext.bakedGI = SampleSH(normalize(lerp(geometryContext.normalWS, geometryContext.viewDirectionWS, 0)));
                geometryContext.bakedGI = lerp(geometryContext.bakedGI, Luminance(geometryContext.bakedGI), material.ambientDecoloration);

            #if _SPECULARANTIALIASING_ON
                material.smoothness = GeometricNormalFiltering(material.smoothness, geometryContext.normalWS, _SpecularAntiAliasingVariance, _SpecularAntiAliasingThreshold);
                material.clearcoatSmoothness = GeometricNormalFiltering(material.clearcoatSmoothness, geometryContext.normalWS, _SpecularAntiAliasingVariance, _SpecularAntiAliasingThreshold);
            #endif

                half oneMinusReflectivity = OneMinusReflectivityMetallic(material.metallic);
                half reflectivity = 1.0 - oneMinusReflectivity;

                geometryContext.grazingTerm = saturate(material.smoothness + reflectivity);

                geometryContext.gloss = exp2(10 * material.smoothness + 1);
                geometryContext.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(material.smoothness);
                geometryContext.roughness = max(PerceptualRoughnessToRoughness(geometryContext.perceptualRoughness), HALF_MIN);
                geometryContext.roughness2 = geometryContext.roughness * geometryContext.roughness;
                geometryContext.normalizationTerm = geometryContext.roughness * 4.0h + 2.0h;
                geometryContext.roughness2MinusOne = geometryContext.roughness2 - 1.0h;
                geometryContext.tangentWS = NormalizeNormalPerPixel(tangentWS + geometryContext.normalWS * material.shift);

                geometryContext.clearcoatGloss = exp2(10 * material.clearcoatSmoothness + 1);
                geometryContext.clearcoatPerceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(material.clearcoatSmoothness);
                geometryContext.clearcoatRoughness = max(PerceptualRoughnessToRoughness(geometryContext.clearcoatPerceptualRoughness), HALF_MIN);
                geometryContext.clearcoatRoughness2 = geometryContext.clearcoatRoughness * geometryContext.clearcoatRoughness;
                geometryContext.clearcoatNormalizationTerm = geometryContext.clearcoatRoughness * 4.0h + 2.0h;
                geometryContext.clearcoatRoughness2MinusOne = geometryContext.clearcoatRoughness2 - 1.0h;
                geometryContext.clearcoatNormalWS = NormalizeNormalPerPixel(lerp(input.normalWS.xyz, geometryContext.normalWS, material.clearcoatBumpStrength));
                geometryContext.clearcoatBitangentWS = NormalizeNormalPerPixel(cross(geometryContext.clearcoatNormalWS, float3(0, 1, 0)));
                geometryContext.clearcoatTangentWS = NormalizeNormalPerPixel(cross(geometryContext.clearcoatNormalWS, geometryContext.clearcoatBitangentWS) + geometryContext.clearcoatNormalWS * material.clearcoatShift);
            }

            half3 ImageBasedGlobalIllumination(GeometryContext geometryContext, PhysicalMaterial material)
            {
            #ifdef _ENVIRONMENTLIGHT_ON
                half3 domainNormal = geometryContext.domainNormal;

            #if _LIGHTMODEL_ANISOTROPY
                half3 ax = cross(-geometryContext.viewDirectionWS, geometryContext.tangentWS);
                half3 ay = cross(ax, geometryContext.tangentWS);
                domainNormal = lerp(domainNormal, normalize(lerp(domainNormal, ay, material.anisotropy * material.anisotropy)), geometryContext.roughness);
            #endif

                half3 bakedGI = RGBToYCoCg(geometryContext.bakedGI);
                bakedGI.x = max(bakedGI.x, material.ambientLuminanceLimit);
                bakedGI = YCoCgToRGB(bakedGI);

                half nv = max(abs(dot(geometryContext.viewDirectionWS, domainNormal)), 1e-5);

                half occlusionFresnel = lerp(1, material.ambientOcclusion, saturate(dot(geometryContext.vertexNormalWS, geometryContext.viewDirectionWS)));
                half3 reflectVector = reflect(-geometryContext.viewDirectionWS, domainNormal);
                half3 indirectDiffuse = bakedGI * GTAOMultiBounce(material.ambientOcclusion, material.albedo);
                half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, geometryContext.perceptualRoughness, material.ambientOcclusion);
                half3 specularTerm = EnvironmentBRDF(domainNormal, geometryContext.viewDirectionWS, indirectDiffuse, indirectSpecular, material.diffuse, material.specular, geometryContext.roughness2, geometryContext.grazingTerm);

            #if defined(_CLEARCOATHIGHLIGHTS_ON)
                #if defined(_CLEARCOATMODEL_CLOTH)
                    half3 clearcoatTerm = EnvironmentBRDF_Sheen(domainNormal, geometryContext.viewDirectionWS, indirectDiffuse, indirectSpecular, material.diffuse, material.specular, geometryContext.clearcoatRoughness);
                #else
                    half3 clearcoatReflect = reflect(-geometryContext.viewDirectionWS, geometryContext.clearcoatNormalWS);
                    half3 clearcoatIndirectSpecular = GlossyEnvironmentReflection(clearcoatReflect, geometryContext.clearcoatPerceptualRoughness, material.ambientOcclusion);
                    half3 clearcoatTerm = EnvironmentBRDF(domainNormal, geometryContext.viewDirectionWS, indirectDiffuse, clearcoatIndirectSpecular, material.diffuse, material.clearcoatSpecular, geometryContext.clearcoatRoughness2, geometryContext.grazingTerm);
                #endif
                
                specularTerm = lerp(specularTerm, clearcoatTerm, lerp(Pow4(nv), 1, material.clearcoat));
            #endif

                return specularTerm;
            #else
                return 0;
            #endif
            }

            float4 EvalH4(float4 sh, float3 dir)
            {
                float4 result;
                result.x = 0.398942f;
                result.y = 0.690988f * dir.y;
                result.z = 1.381976f * dir.z - 1.0f;
                result.w = 0.690988f * dir.x;

                return dot(sh, result);
            }

            float CurvatureFromLight(float3 tangent, float3 bitangent, float3 curvTensor, float3 lightDir)
            {
                float2 lightDirProj = float2(dot(lightDir, tangent), dot(lightDir, bitangent));

                float curvature = curvTensor.x * pow(lightDirProj.x, 2) + 
                                    2.0f * curvTensor.y * lightDirProj.x * lightDirProj.y +
                                    curvTensor.z * pow(lightDirProj.y, 2);

                return curvature;
            }

            half3 SampleCurvatureLut(Light light, GeometryContext geometryContext, PhysicalMaterial material)
            {
                float3 L = light.direction;
                float nl = dot(geometryContext.normalLowWS, light.direction);

                float lookup = nl * 0.5f + 0.5f;
                float3 rgbCurvature = SAMPLE_TEXTURE2D(_SkinRamp, sampler_SkinRamp, float2(lookup, material.translucency)).rgb;
                rgbCurvature = rgbCurvature * 0.5f - 0.2f;
                
                float3 BlurFactor = saturate(1.0f - nl);
                BlurFactor *= BlurFactor;
                float3 gN = normalize(lerp(geometryContext.normalWS, geometryContext.normalLowWS, 0.3f + 0.7f * BlurFactor));
                float3 bN = normalize(lerp(geometryContext.normalWS, geometryContext.normalLowWS, BlurFactor));
                float3 rgbNoL = float3(saturate(nl), saturate(dot(gN, L)),  saturate(dot(bN, L)));

                return saturate(rgbNoL + rgbCurvature);
            }

            half3 SamplePreIntegratedSkin(Light light, PhysicalMaterial material, float3 normalWS, float3 normalLowWS)
            {
                float rNoL = dot(normalLowWS, light.direction);
                float BlurFactor = saturate(1.0f - rNoL);
                BlurFactor *= BlurFactor;

                float3 gN = lerp(normalWS, normalLowWS, BlurFactor * 0.7 + 0.3f);
                float3 bN = lerp(normalWS, normalLowWS, BlurFactor);

                float3 L = light.direction;
                float3 NoL = float3(rNoL, dot(gN, L), dot(bN, L));
                NoL = NoL * (1 - material.softness) + material.softness;

                float3 diffuse;

                float3 lookup = NoL * 0.5f + 0.5f;
                diffuse.r = SAMPLE_TEXTURE2D(_SkinRamp, sampler_SkinRamp, float2(lookup.r, material.subsurface)).r;
                diffuse.g = SAMPLE_TEXTURE2D(_SkinRamp, sampler_SkinRamp, float2(lookup.g, material.subsurface)).g;
                diffuse.b = SAMPLE_TEXTURE2D(_SkinRamp, sampler_SkinRamp, float2(lookup.b, material.subsurface)).b;

                return diffuse;
            }

            real4 Sigmoid(real4 x, real4 offset, real4 sharp, real4 base)
            {
                return 1.0f / (1.0f + PositivePow(base, -3.0f * sharp * (x - offset)));
            }

            real4 DiffuseLookupRamp(real nl, real ir)
            {
                real4 offset;
                offset[0] = _FirstLayerOffset;
                offset[1] = pow(1.0 - ir, 2.0) * lerp(0.3, 1.0, _SecondLayerOffset);
                offset[2] = pow(1.0 - ir, 2.0) * lerp(-0.3, 1.0, _ThirdLayerOffset);
                offset[3] = pow(1.0 - ir, 2.0) * lerp(0.1, 1.0, _FourLayerOffset);

                real4 sharp;
                sharp[0] = 0.3;
                sharp[1] = pow(lerp(1.1, 3.0, PositivePow(ir, 1.6)), 3.0);
                sharp[2] = pow(lerp(1.1, 3.0, PositivePow(ir, 1.6)), 3.0);
                sharp[3] = pow(lerp(0.5, 0.8, ir), 2.0);

                return Sigmoid(nl * 0.8, offset, sharp, real4(2000, 5, 5, 10000000));
            }

            half3 StylizedBasedDirectLighting(Light light, GeometryContext geometryContext, PhysicalMaterial material)
            {
                half3 domainNormal = geometryContext.domainNormal;

                float3 halfDir = SafeNormalize(light.direction + geometryContext.viewDirectionWS);

                half nl = dot(domainNormal, light.direction);
                half vh  = dot(geometryContext.viewDirectionWS, halfDir);
                half nv = max(abs(dot(geometryContext.viewDirectionWS, domainNormal)), 1e-5f);
                half lv = dot(geometryContext.viewDirectionWS, light.direction);
                half fresnel = pow(nv, material.lightOcclusionStrength * 5);

                half shadowAttenuation = light.shadowAttenuation;
                half occlusion = ComputeMicroShadowing(material.occlusion, nl, material.lightOcclusionStrength);

                float3 Front = normalize(_FaceDirection);// TransformObjectToWorldDir(_FaceDirection);

            #ifdef _FACIALSHADOWMAP
                float3 UP = float3(0,1,0);
                float3 Left = cross(UP, Front);
                float3 Right = -cross(UP, Front);
                float FrontL = dot(normalize(Front.xz), normalize(-light.direction.xz));
                float LeftL = dot(normalize(Left.xz), normalize(-light.direction.xz));
                float RightL = dot(normalize(Right.xz), normalize(-light.direction.xz));

                float softness = max((1 - abs(FrontL)) * 0.5, 0.01);
                float leftShadow = smoothstep(LeftL - softness * 2, LeftL - softness, material.facialShadow.r);
                float rightShadow = smoothstep(RightL - softness * 2, RightL - softness, material.facialShadow.g);
                float facialShadowTerm = lerp(material.lightShadowStrength, 1.0, min(leftShadow, rightShadow)) * step(FrontL, 0);

                shadowAttenuation *= saturate(dot(Front, light.direction)) * facialShadowTerm;
            #endif

            #ifdef _VIEWBRUSH_ON
                half4 layers = DiffuseLookupRamp(nv * 2 - 1, 1 - material.softness) * occlusion;
            #else
                half4 layers = DiffuseLookupRamp(nl, 1 - material.softness) * occlusion * lerp(shadowAttenuation, 1, material.diffuseShadowStrength.a);
            #endif

            #ifdef _FACTATTENUATION_ON
                shadowAttenuation *= lerp(0.2, 1.0f, pow(saturate(dot(Front, light.direction)), 0.45));
            #endif

                half3 diffuseTerm = material.baseLayerColor.rgb;
                diffuseTerm = lerp(material.firstLayerColor.rgb, diffuseTerm, layers.r);
                diffuseTerm = lerp(material.secondLayerColor.rgb, diffuseTerm, layers.g);
                diffuseTerm = lerp(material.thirdLayerColor.rgb, diffuseTerm, layers.b);
                diffuseTerm = lerp(material.fourLayerColor.rgb * geometryContext.bakedGI, diffuseTerm, layers.a);
                diffuseTerm = lerp(material.diffuseShadowStrength * diffuseTerm, diffuseTerm, lerp(1, shadowAttenuation, material.diffuseShadowStrength.a));
                diffuseTerm = lerp(saturate(nl) * shadowAttenuation, diffuseTerm, material.diffuseBrushStrength);

            #ifdef _TRANSLUCENCY_ON
                half3 transLightDir = light.direction + geometryContext.normalWS * material.translucencyDistortion;
                half transDot = pow(saturate(dot(geometryContext.viewDirectionWS, -transLightDir)), material.translucencyPower) * material.translucencyScale;
                half translucencyTerm = saturate(1 - nl) * (transDot + material.translucencyAmbient) * material.translucency;
            #endif

            #ifdef _BACKLIGHT_ON
                half falloff = clamp(1.0 - nv, 0.02, 0.98);
                half rimLightDot = saturate(0.5 * (dot(geometryContext.domainNormal, light.direction) + 1.5));
                half rimLight = SAMPLE_TEXTURE2D(_RimLightLookup, sampler_RimLightLookup, float2(rimLightDot * falloff, 0.5)).r;
                half3 rimTerm = _RimColor * rimLight * saturate(1 - vh) * occlusion;
            #endif

            half3 specularTerm = 0;

            #ifdef _SPECULARHIGHLIGHTS_ON
                #ifdef _LIGHTMODEL_STANDARD
                    specularTerm = BRDF_Specular_GGX(light, geometryContext.normalWS, geometryContext.viewDirectionWS, geometryContext.roughness2, geometryContext.roughness2MinusOne, geometryContext.normalizationTerm);
                #elif _LIGHTMODEL_ANISOTROPY
                    specularTerm = BRDF_Specular_Anisotropic_GGX(light, geometryContext.normalWS, geometryContext.bitangentWS, geometryContext.tangentWS, geometryContext.viewDirectionWS, material.anisotropy, geometryContext.roughness);
                #elif _LIGHTMODEL_KAJIYAKAY
                    specularTerm = BRDF_Specular_KajiyaKay(geometryContext.tangentWS, geometryContext.viewDirectionWS, light.direction, geometryContext.gloss, PI);
                #elif _LIGHTMODEL_SKIN
                    specularTerm = BRDF_Specular_Skin(geometryContext.normalWS, light.direction, geometryContext.viewDirectionWS, geometryContext.roughness, 4 * PI);
                #else
                    specularTerm = BRDF_Specular_GGX(light, geometryContext.normalWS, geometryContext.viewDirectionWS, geometryContext.roughness2, geometryContext.roughness2MinusOne, geometryContext.normalizationTerm);
                #endif
            #endif

            #ifdef _CLEARCOATHIGHLIGHTS_ON
                #if _CLEARCOATMODEL_GGX
                    half clearcoatTerm = BRDF_Specular_GGX(light, geometryContext.clearcoatNormalWS, geometryContext.viewDirectionWS, geometryContext.clearcoatRoughness2, geometryContext.clearcoatRoughness2MinusOne, geometryContext.clearcoatNormalizationTerm);
                #elif _CLEARCOATMODEL_ANISOTROPY
                    half clearcoatTerm = BRDF_Specular_Anisotropic_GGX(light, geometryContext.clearcoatNormalWS, geometryContext.clearcoatBitangentWS, geometryContext.clearcoatTangentWS, geometryContext.viewDirectionWS, material.clearcoatAnisotropy, geometryContext.clearcoatRoughness);
                #elif _CLEARCOATMODEL_KAJIYAKAY
                    half clearcoatTerm = BRDF_Specular_KajiyaKay(geometryContext.clearcoatTangentWS, geometryContext.viewDirectionWS, light.direction, geometryContext.clearcoatGloss, PI);
                #elif _CLEARCOATMODEL_SKIN
                    half clearcoatTerm = BRDF_Specular_Skin(geometryContext.clearcoatNormalWS, light.direction, geometryContext.viewDirectionWS, geometryContext.clearcoatRoughness, 4 * PI);
                #elif _CLEARCOATMODEL_CLOTH
                    half clearcoatTerm = BRDF_Specular_Sheen(light, geometryContext.clearcoatNormalWS, geometryContext.viewDirectionWS, geometryContext.clearcoatRoughness);
                #endif

                specularTerm = lerp(material.specular * specularTerm, material.clearcoatSpecular * clearcoatTerm, lerp(F_Schlick(0.04, saturate(vh)), 1, material.clearcoat));
            #else
                specularTerm *= material.specular;
            #endif

                half3 lighting = material.diffuse * diffuseTerm * light.diffuseStrength;
                lighting += specularTerm * light.shadowAttenuation * saturate(nl);
            #ifdef _TRANSLUCENCY_ON
                lighting += material.diffuse * translucencyTerm * material.translucencyColor * light.diffuseStrength;
            #endif
            #ifdef _BACKLIGHT_ON
                lighting += rimTerm * light.shadowAttenuation;
            #endif
                lighting *= light.color * light.distanceAttenuation;

                return lighting;
            }

            half4 StylizedBasedLighting(Varyings input, GeometryContext geometryContext, PhysicalMaterial material)
            {           
            #ifdef _MAIN_LIGHT_EXPONENTIAL_SHADOWS
                Light mainLight = GetMainLight(geometryContext.shadowCoord);
                mainLight.shadowAttenuation *= GetCloudShadow(geometryContext.positionWS);
            #else
                Light mainLight = GetMainLight(geometryContext.shadowCoord);
                #if !defined(_RECEIVE_SHADOWS_OFF)
                    mainLight.shadowAttenuation = SampleScreenSpaceShadowMap(input.screenPos.xy / input.screenPos.w);
                #endif
            #endif
                mainLight.color *= rcp(_MainLightExposure);

                MixRealtimeAndBakedGI(mainLight, geometryContext.normalWS, geometryContext.bakedGI, half4(0, 0, 0, 0));

                half3 color = ImageBasedGlobalIllumination(geometryContext, material);
                color += StylizedBasedDirectLighting(mainLight, geometryContext, material);

            #ifdef _ADDITIONAL_LIGHTS
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, geometryContext.positionWS);
                    color += StylizedBasedDirectLighting(light, geometryContext, material);
                }
            #endif

            #ifdef _MATCAP_ON
                float2 diffuseUVAndMatCapCoords;
                float3 normalWS = TransformWorldToObjectNormal(geometryContext.normalWS);
                diffuseUVAndMatCapCoords.x = dot(normalize(transpose(unity_MatrixInvV)[0].xyz), normalize(normalWS));
                diffuseUVAndMatCapCoords.y = dot(normalize(transpose(unity_MatrixInvV)[1].xyz), normalize(normalWS));
                color += SAMPLE_TEXTURE2D(_MatcapMap, sampler_MatcapMap, diffuseUVAndMatCapCoords * 0.5 + 0.5);
            #endif

                color += material.emission;

                return half4(color, material.alpha);
            }

            #ifndef _RIMMODE_NONE
                half3 ApplyRimLight(Varyings input, GeometryContext geometryContext, PhysicalMaterial material)
                {
                    //--边缘光
                    #if _RIM_BUMPMAP_ON
                        half3 rimNormal= geometryContext.normalWS.xyz;
                    #elif _RIM_COLOR_NORMAL_ON
                        half3 rimNormal= input.colorNormalWS.xyz;
                    #else
                        half3 rimNormal= input.normalWS.xyz;
                    #endif

                    #if _RIMMODE_VIEW
                        half NoV = saturate(dot(rimNormal, geometryContext.viewDirectionWS));
                        half3 rimColor = pow(1-NoV,_RimLight_Pow) * _RimLightColor * LerpWhiteTo(material.occlusion, _Rim_Cover);
                    #elif _RIMMODE_LIGHTDIR
                        half NoV = saturate(dot(rimNormal, geometryContext.viewDirectionWS));
                        half NoL = saturate(dot(rimNormal, _MainLightPosition.xyz));
                        half3 rimColor = pow(1-NoV,_RimLight_Pow) * _RimLightColor * pow(NoL, 1 + _Rim_Cover * 4);
                    #elif _RIMMODE_CUSTOM
                        half3 normalVS = TransformWorldToViewDir(rimNormal);
                        half NoV = saturate(dot(rimNormal, geometryContext.viewDirectionWS));
                        half NoL = saturate(dot(normalVS, normalize(_Rim_Dir.xyz)));
                        half3 rimColor = pow(1-NoV,_RimLight_Pow) * _RimLightColor * pow(NoL, 1 + _Rim_Cover * 4);
                    #endif

                    #if _RIM_COLOR_NORMAL_ON
                        rimColor *= input.colorNormalWS.w;
                    #endif

                    return rimColor;
                }
            #endif

            #ifdef _OVERLAY_DISSOLVE
                half4 ApplyDissolveEffect(Varyings input, float4 color)
                {
                    float atten = saturate(abs(distance(input.positionWS, TransformObjectToWorld(float3(0,0,0)) + float3(0, _OffsetY, 0)) / _MaxDistance));
                    float process = pow(_DissolveShowStep, _OverlayExponential);
                    float newRange = 1.4;
                    process = process*newRange -(newRange-1);

                    float mask = SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, input.uv.xy).r;
                    float factor = saturate(atten - process);//pow(saturate(atten - process), 0.8);

                    if (factor > mask)
                        return 0;

                    float rate = factor / mask;
                    if (rate > _DissolveThreshold)
                    {
                        color.rgb = lerp(color.rgb, _DissolveColor.rgb, rate);
            
                        if (rate > _DissolveEdgeThreshold)
                            color.rgb = lerp(color.rgb, _DissolveEdgeColor.rgb, rate);
                    }

                    return color;
                }
            #endif

            #ifdef _SCRATCH_CUTTING
                half4 ApplyScratchCutting(Varyings input, half4 col)
                {
                    float timefactor = saturate(_CuttingTimeFactor);
                    half4 cutting = SAMPLE_TEXTURE2D(_CuttingTex, sampler_CuttingTex, input.uv.zw);
                    half  alpha = saturate(cutting.w-timefactor);
                    col.xyz = col.xyz*(1-alpha)+ cutting.xyz *alpha;
                    return col;
                }
            #endif

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                half3 viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;

                output.uv.xy = TRANSFORM_TEX(input.texcoord, _BaseMap);

                #if defined(_SCRATCH_CUTTING)
                    output.uv.zw = input.lightmapUV;
                #endif

                output.normalWS = normalInput.normalWS;
                output.tangentWS = half4(normalInput.tangentWS.xyz, input.tangentOS.w * GetOddNegativeScale());
                output.colorNormalWS = half4(TransformObjectToWorldNormal(normalize(input.color * 2 - 1)), input.color.w);

                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS.xyz, output.vertexSH);

                output.positionWS = vertexInput.positionWS;
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);

                output.viewDirWS = viewDirWS;
                output.directionalSH = input.directionalSH;

            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(vertexInput);
            #endif

                output.positionCS = vertexInput.positionCS;

                return output;
            }

            float4 LitPassFragment(Varyings input) : SV_TARGET0
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#ifdef _STIPPLETEST_ON
                float alpha = _StippleAlpha;
#ifndef _STIPPLETEST_TARGET_OFF
                alpha *= saturate(distance(input.positionWS, _TargetPosition) / _TargetRangeCutoff);
#endif
#ifndef _STIPPLETEST_VIEW_OFF
                alpha *= saturate(distance(input.positionWS, GetCameraPositionWS()) / _CameraRangeCutoff);
#endif

                StippleAlpha(alpha, input.positionCS);
#endif

                PhysicalMaterial material;
                InitializePhysicalMaterial(input, material);

                GeometryContext geometryContext;
                InitializeGeometryContext(input, material, geometryContext);

                float4 color = StylizedBasedLighting(input, geometryContext, material);

                #ifndef _RIMMODE_NONE
                    color.xyz += ApplyRimLight(input, geometryContext, material);
                #endif

                #ifdef _SCRATCH_CUTTING
                    color = ApplyScratchCutting(input,color);
                #endif

                #ifdef _OVERLAY_DISSOLVE
                    return ApplyDissolveEffect(input, color);
                #endif

                #ifdef _VIEWALPHA_ON
                    #if _ALPHA_ADDITIONAL_NORMAL_ON
                        half3 alphaNormal = input.colorNormalWS.xyz;
                    #else
                        half3 alphaNormal = input.normalWS.xyz;
                    #endif
                    color.a *= lerp(_AlphaViewBase, 1, PositivePow(1 - abs(dot(alphaNormal, geometryContext.viewDirectionWS)), _AlphaViewPower));
                #endif

                color.a = saturate(color.a * _AlphaScale);

                Light mainLight = GetMainLight();

                #if _DEBUGMODE_ALBEDO
                    color.rgb = material.albedo;
                #elif _DEBUGMODE_NORMAL
                    color.rgb = SRGBToLinear(geometryContext.normalWS * 0.5 + 0.5);
                #elif _DEBUGMODE_NORMALEX
                    color.rgb = SRGBToLinear(geometryContext.colorNormalWS.xyz * 0.5 + 0.5);//EvalH4(input.directionalSH, TransformWorldToObjectDir(mainLight.direction));
                #elif _DEBUGMODE_COLORALPHA
                    color.rgb = input.colorNormalWS.w;
                #elif _DEBUGMODE_SMOOTHNESS
                    color.rgb = material.smoothness;
                #elif _DEBUGMODE_METALLIC
                    color.rgb = material.metallic;
                #elif _DEBUGMODE_OCCLUSION
                    color.rgb = material.occlusion;
                #elif _DEBUGMODE_TRANSLUCENCY
                    color.rgb = material.translucency;
                #elif _DEBUGMODE_CLEARCOAT
                    color.rgb = material.clearcoat;
                #endif

                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "Outline"
            Tags{"LightMode" = "Outline"}

            ZWrite On
            ZTest Less
            Cull Front

            HLSLPROGRAM

            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature_local _OVERLAY_DISSOLVE

            #pragma multi_compile _ _STIPPLETEST_ON
            #pragma shader_feature _STIPPLETEST_VIEW_OFF
            #pragma shader_feature _STIPPLETEST_TARGET_OFF

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float4 colorOS      : COLOR;
                float2 texcoord     : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 color       : TEXCOORD0;
                float2 uv          : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float4 positionCS  : SV_POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            real GetCameraFOV()
            {
                real t = unity_CameraProjection._m11;
                real fov = atan(1.0f / t) * 2.0 * 180 / 3.1415926;
                return fov;
            }

            real GetOutlineCameraFovAndDistanceFixMultiplier(real positionVS_Z)
            {
                real cameraMulFix;

                if (unity_OrthoParams.w == 0)
                {
                    cameraMulFix = abs(positionVS_Z);
                    cameraMulFix = saturate(cameraMulFix);
                    cameraMulFix *= GetCameraFOV();       
                }
                else
                {
                    real orthoSize = abs(unity_OrthoParams.y);
                    orthoSize = saturate(orthoSize);
                    cameraMulFix = orthoSize * 50;
                }

                return cameraMulFix * 0.01;
            }

            #ifdef _OVERLAY_DISSOLVE
                void ApplyDissolveEffect(Varyings input)
                {
                    float atten = saturate(abs(distance(input.positionWS, TransformObjectToWorld(float3(0,0,0)) + float3(0, _OffsetY, 0)) / _MaxDistance));
                    float process = pow(_DissolveShowStep, _OverlayExponential);
                    float newRange = 1.4;
                    process = process*newRange -(newRange-1);

                    float mask = SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, input.uv.xy).r;
                    float factor = saturate(atten - process);//pow(saturate(atten - process), 0.8);

                    if (factor > mask)
                    {
                        clip(-1);
                    }
                }
            #endif

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

                real outlineStrength = _OutlineStrength;
                outlineStrength *= lerp(1, input.colorOS.w, _OutlineLineStrength);
                outlineStrength *= GetOutlineCameraFovAndDistanceFixMultiplier(vertexInput.positionCS.z);

                float3 outlineNormal = normalize(lerp(input.normalOS, input.colorOS.xyz * 2 - 1, _OutlineNormalStrength));
                float3 normalWS = TransformObjectToWorldNormal(outlineNormal);

                real4 projection = mul(unity_CameraInvProjection, real4(1, 1, UNITY_NEAR_CLIP_VALUE, _ProjectionParams.y));
                real aspect = abs(projection.x / projection.y);
                real2 offset = TransformWorldToHClipDir(normalWS).xy;
                offset.y *= aspect;

                output.uv = input.texcoord;
                output.color = half4(_OutlineColor * Luminance(SampleSH(normalWS)), 1);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS + normalWS * outlineStrength);
                output.positionCS.xy += offset * min(0.01f, output.positionCS.w * outlineStrength);

                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            #ifdef _STIPPLETEST_ON
                float alpha = _StippleAlpha;
            #ifndef _STIPPLETEST_TARGET_OFF
                alpha *= saturate(distance(input.positionWS, _TargetPosition) / _TargetRangeCutoff);
            #endif
            #ifndef _STIPPLETEST_VIEW_OFF
                alpha *= saturate(distance(input.positionWS, GetCameraPositionWS()) / _CameraRangeCutoff);
            #endif

                StippleAlpha(alpha, input.positionCS);
            #endif

            #ifdef _OVERLAY_DISSOLVE
                ApplyDissolveEffect(input);
            #endif

            #if _ALPHATEST_ON
                half4 albedoAlpha = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
                Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
            #endif

                return input.color;
            }

            ENDHLSL
        }
        Pass
        {
            Name "OutGlow"
            Tags{"LightMode" = "OutGlow"}

            ZWrite Off
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM

            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local _OUTGLOWMODE_COLOR _OUTGLOWMODE_ALBEDO _OUTGLOWMODE_TEXTURE
            #pragma shader_feature_local _OVERLAY_DISSOLVE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

            struct Attributes
            {
                float4 position     : POSITION;
                float2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
                //#ifdef _OVERLAY_DISSOLVE
                float3 positionWS               : TEXCOORD1;
                //#endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            #ifdef _OVERLAY_DISSOLVE
                half3 ApplyDissolveEffect(Varyings input, float3 color)
                {
                    float atten = saturate(abs(distance(input.positionWS, TransformObjectToWorld(float3(0,0,0)) + float3(0, _OffsetY, 0)) / _MaxDistance));
                    float process = pow(_DissolveShowStep, _OverlayExponential);
                    float newRange = 1.4;
                    process = process*newRange -(newRange-1);

                    float mask = SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, input.uv.xy).r;
                    float factor = saturate(atten - process);//pow(saturate(atten - process), 0.8);

                    if (factor > mask)
                    {
                        clip(-1);
                        return 0;
                    }
                    
                    return color;
                }
            #endif
            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.position.xyz);

                output.uv = TRANSFORM_TEX(input.texcoord, _OutGlowMap);
                output.positionCS = TransformObjectToHClip(input.position.xyz);
                output.positionWS = vertexInput.positionWS;
                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #if _ALPHATEST_ON
                    half4 albedoAlpha = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
                    Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
                #endif

                float3 color = 0;
                #if _OUTGLOWMODE_COLOR
                    color = _OutGlowColor * _OutGlowIntensity;
                #elif _OUTGLOWMODE_ALBEDO
                    color = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).rgb * _OutGlowColor * _OutGlowIntensity;
                #elif _OUTGLOWMODE_TEXTURE
                    color = SAMPLE_TEXTURE2D(_OutGlowMap, sampler_OutGlowMap, input.uv).rgb * _OutGlowColor * _OutGlowIntensity;
                #endif

                #ifdef _OVERLAY_DISSOLVE
                    color= ApplyDissolveEffect(input, color);
                #endif

                return EncodeRGBM(color);
            }

            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON
            #pragma multi_compile_local _SHADOW_CUSTOM_BIAS
            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
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

            #pragma multi_compile _ _STIPPLETEST_ON
            #pragma shader_feature _STIPPLETEST_VIEW_OFF
            #pragma shader_feature _STIPPLETEST_TARGET_OFF

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
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

            #pragma shader_feature _ _STIPPLETEST_ON
            #pragma shader_feature _STIPPLETEST_VIEW_OFF
            #pragma shader_feature _STIPPLETEST_TARGET_OFF

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "TransparentDepthPrepass"
            Tags{"LightMode" = "TransparentDepthPrepass"}

            ZTest [_ZTest] ZWrite [_ZWrite]
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

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.SimpleShaderGUI"
}