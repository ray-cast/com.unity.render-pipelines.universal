Shader "Character/Stylized Shading"
{
    Properties
    {
        [Queue] _Surface("渲染队列", Float) = 0

        [Space(20)]
        [MainColor] _BaseColor("基本颜色", Color) = (1,1,1,1)
        [MainTexture] _BaseMap("颜色贴图", 2D) = "white" {}

        [Space(20)]
        [BlendSwitcher(_Surface)]_Blend("透明混合模式", Float) = 0
        [ShowIf(_Surface)] _AlphaScale("透明程度", Range(0.0, 1.0)) = 1
        [HideInInspector][TogglePass(TransparentDepthPrepass)]_TransparentPreDepth("启用提前透明深度写入", Float) = 0

        [Space(20)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("启用透明度剔除", Float) = 4
        [ShowIf(_AlphaClip)] _Cutoff("透明度剔除阈值", Range(0.0, 1.0)) = 0.5

        [Space(20)]
        [KeywordEnum(Tangent, World)]
        _NormalModel("法线贴图模式（切线空间，世界空间）", Float) = 0
        [TexToggle(_NORMALMAP)][NoScaleOffset]_NormalMap("法线贴图", 2D) = "grey" {}
        [EqualIf(_NormalModel, 0)]_NormalScale("法线强度", Range(-10, 10)) = 1.0
        [Toggle(_USE_COLOR_NORMAL_ON)] _UseColorNormal("启用顶点颜色法线", Float) = 0

        [Space(20)]
        [ToggleOff(_SPECULARHIGHLIGHTS_OFF)] _SpecularHighlights("启用高光反射", Float) = 1.0
        [KeywordEnum(Standard, Cloth, Anisotropy, Subsurface, Skin)]
        _LightModel("光照模型（标准，布料，各向异性（头发，金属)，次表面散射）", Float) = 0
        _Smoothness("光滑度", Range(0.0, 1.0)) = 0.5
        _Metallic("金属程度", Range(0.0, 1.0)) = 0.0
        _SpecularColor("反射颜色", Color) = (1,1,1,1)
        _Specular("反射程度", Range(0.0, 1.0)) = 0.5
        [EqualIf(_LightModel, 1)]_Sheen("布料程度", Range(0.0, 1.0)) = 0.0
        [EqualIf(_LightModel, 2)]_Anisotropy("各向异性程度", Range(0.0, 1.0)) = 0.0
        [EqualIf(_LightModel, 2)]_AnisotropyOffset("各向异性偏移程度", Range(-2.0, 2.0)) = 0.0
        [EqualIf(_LightModel, 2)]_AnisotropyShift("各向异性扰动程度", Range(0.0, 1.0)) = 1.0
        [EqualIf(_LightModel, 3)]_Subsurface("次表面散射程度", Range(0, 1)) = 0.0
        [EqualIf(_LightModel, 3)]_SubsurfaceStrength("次表面模糊程度", Range(0, 1)) = 0.0
        [TexToggle(_METALLICSPECGLOSSMAP)]_MetallicGlossMap("R=金属 G=AO B=ALBEDO模式自发光的区域 A=光滑", 2D) = "white" {}
        [TexToggle(_HAIRCLOTHMAP)]_Mask2("R=各向异性偏移贴图 G=曲率贴图, B=布料程度, A=厚度贴图", 2D) = "black" {}

        [Space(20)]
        [Toggle(_SPECULARHIGHLIGHTS2_ON)] _SpecularHighlights2("启用多层高光反射", Float) = 0.0
        _Smoothness2("光滑度2", Range(0.0, 1.0)) = 0.5
        _AnisotropyOffset2("各向异性偏移程度2", Range(-2.0, 2.0)) = 0.0
        _AnisotropyShift2("各向异性扰动程度2", Range(0.0, 1.0)) = 1.0
        _SpecularColor2("反射颜色2", Color) = (1,1,1,1)

        [Space(20)]
        _Translucency("次表面透射程度", Range(0, 1)) = 0.0
        _TranslucencyColor("次表面透射颜色", Color) = (1,0,0,1)
        _TranslucencyScale("次表面透射强度", Range(0.0, 10)) = 1.0
        _TranslucencyAmbient("全局透射程度", Range(0.0, 1)) = 0.0
        _TranslucencyDistortion("次表面投射密度", Range(0, 1)) = 0.1
        _TranslucencyPower("次表面衰减速率", Range(0.01, 10)) = 4

        [HideInInspector] [TexToggle(_FACIALSHADOWMAP)][NoScaleOffset]_FacialShadowMap("光照贴图", 2D) = "white" {}
        [HideInInspector] _LightShadowStrength("光照贴图暗部程度", Range(0.0, 1.0)) = 0.5

        [Space(20)]
        [Toggle(_BACKLIGHT_ON)]_UseBackLight("启用背光", Float) = 0
        [HDR]_RimColor("背光颜色", Color) = (1,1,1,1)
        [NoScaleOffset]_RimLightLookup("背光查找表", 2D) = "black" {}
        [NoScaleOffset]_RimLightMask("背光遮罩", 2D) = "black" {}

        [Space(20)]
        [Toggle(_FACTATTENUATION_ON)]_UseFaceAttenuation("启用面部衰减", Float) = 0
        _FaceDirection("面部朝向", Vector) = (0, 0, 1, 1)

        [Space(20)]
        _BaseLayerColor("基础笔刷颜色", Color) = (1, 1, 1)
        _FirstLayerColor("亮调笔刷颜色", Color) = (0.8901, 0.4784, 0.3647, 0.25)
        _SecondLayerColor("中调笔刷颜色", Color) = (0.7725, 0.3843, 0.4078, 0.8)
        _ThirdLayerColor("中调笔刷颜色", Color) = (0.6117, 0.2862, 0.3450, 0.9)
        _FourLayerColor("暗调笔刷颜色", Color) = (0.6117, 0.2862, 0.3450, 0.9)
        _DiffuseShadowStrength("阴影笔刷颜色", Color) = (0, 0, 0)
        _FirstLayerOffset("亮调偏移程度", Range(-1.0, 1.0)) = 0
        _SecondLayerOffset("中调偏移程度", Range(-1.0, 1.0)) = 0
        _ThirdLayerOffset("暗调偏移程度", Range(-1.0, 1.0)) = 0
        _FourLayerOffset("暗调偏移程度", Range(-1.0, 1.0)) = 0
        _DiffuseBrushStrength("笔刷影响程度", Range(0.0, 1.0)) = 1.0
        _Softness("笔刷柔和程度", Range(0.0, 1.0)) = 1.0
        _LightOcclusionStrength("笔刷环境遮蔽程度", Range(0.0, 1.0)) = 1.0
        [Toggle(_VIEWBRUSH_ON)] _UseViewBrush ("启用视线方向照明", Float) = 0

        [Space(20)]
        [ToggleOff(_ENVIRONMENTREFLECTIONS_OFF)] _EnvironmentReflections("启用环境反射", Float) = 1.0
        _OcclusionLimit("环境光最低亮度", Range(0.0, 2.0)) = 0.5
        _OcclusionStrength("环境光遮蔽程度", Range(0.0, 1.0)) = 1.0
        _AmbientDecoloration("环境光去色程度", Range(0.0, 1.0)) = 1

        [Space(20)]
        [KeywordEnum(None, Color, Albedo, Texture)]
        _EmissionMode("自发光模式（无，颜色，主纹理，自定义纹理）", Float) = 0
        _EmissionColor("自发光颜色", Color) = (1,1,1)
        _EmissionIntensity("自发光强度", Float) = 100.0
        [EqualIf(_EmissionMode, 3)][NoScaleOffset]_EmissionMap("自定义发光贴图", 2D) = "white" {}

        [Space(20)]
        [TogglePass(OutGlow)]_OutGlowEnable("启用外发光", Float) = 0
        [KeywordEnum(Color, Albedo, Texture)]
        _OutGlowMode("外发光模式（无，颜色，自定义纹理）", Float) = 0
        _OutGlowColor("外发光颜色", Color) = (1,1,1)
        _OutGlowIntensity("外发光强度", Float) = 100.0
        [EqualIf(_OutGlowMode, 2)][NoScaleOffset]_OutGlowMap("自定义外发光贴图", 2D) = "white" {}

        [Space(20)]//边缘光
        [KeywordEnum(None, View, LightDir, Custom)]_RimMode("边缘光模式（无，视线方向，主光源方向，自定义方向）", Float) = 0
        [HDR]_RimLightColor ("边缘颜色，程序会动态控制", Color) = (0,0,0.7,1)
        [HDR]_RimLightColor2("附加外发光颜色", Color) = (0,0,0,0)
        _Rim_Cover ("边缘光环境遮蔽程度", Range(0, 1)) = 1
        _RimLight_Pow ("边缘衰减速率", Range(0.01, 10)) = 4
        [Toggle(_RIM_COLOR_NORMAL_ON)] _RimUseColorNormal("使用顶点颜色法线", Float) = 0
        [Toggle(_RIM_BUMPMAP_ON)] _Caustics("使用贴图法线（不勾选使用顶点法线）", Float) = 0
        [EqualIf(_RimMode, 3)]_Rim_Dir ("自定义遮蔽方向", Vector) = (-1.25,1.25,0,0)

        [Space(20)]
        [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows("接收阴影", Float) = 1.0
        [Toggle(_MAIN_LIGHT_EXPONENTIAL_SHADOWS)] _EnableExponentialShadows("使用指数阴影", Float) = 0.0
        [ShowIf(_ReceiveShadows)]_ShadowDepthBias("阴影深度偏移", Range(0.0, 10.0)) = 1.0
      
        [Space(20)]
        [Toggle(_SPECULAR_ANTIALIASING)] _UseSpecularHighlights("启用镜面抗锯齿", Float) = 1
        [ShowIf(_UseSpecularHighlights)]_SpecularAntiAliasingThreshold("镜面抗锯齿阈值", Range(0.0, 10.0)) = 0.2
        [ShowIf(_UseSpecularHighlights)]_SpecularAntiAliasingVariance("镜面抗锯齿方差", Range(0.0, 10.0)) = 0.15

        [Space(20)]
        [Toggle(_ALPHA_VIEWSCALE)] _UseViewAlpha("启用视线透明衰减", Float) = 0
        [Toggle(_ALPHA_ADDITIONAL_NORMAL_ON)]_UseAdditionalNormal("使用附加颜色法线", Float) = 1
        _AlphaViewBase("视线基本衰减", Range(0.0, 1.0)) = 0
        _AlphaViewPower("视线衰减速率", Range(0.0, 4.0)) = 1

        [Space(20)]
        [Toggle(_STIPPLETEST_ON)]_UseStippleCutoff("启用点阵像素剔除", int) = 0
        _StippleAlpha("点阵化透明程度", Range(0.0, 1.0)) = 1
        [ToggleOff(_STIPPLETEST_VIEW_OFF)] _ViewStippleCutoff("相机范围剔除", Float) = 0
        _CameraRangeCutoff("相机剔除范围", Range(0.01, 10.0)) = 1
        [ToggleOff(_STIPPLETEST_TARGET_OFF)] _TargetStippleCutoff("目标范围剔除", Float) = 0
        _TargetRangeCutoff("目标剔除范围", Range(0.01, 10.0)) = 1
        _TargetPosition("目标世界位置", Vector) = (0, 0, 0)

        // OVERLAY ,溶解
        [Space(20)]
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

        [Space(20)]
        [KeywordEnum(None, Albedo, Normal, NormalEx, Occlusion, Smoothness, Metallic, Sheen, Translucency)]
        _DebugMode("调试输出", Float) = 0

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("剔除模式", Float) = 2.0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("深度测试", Float) = 4
        [Enum(Off, 0, On, 1)] _ZWrite ("深度写入", Float) = 0

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

            half  _Smoothness2;
            half  _AnisotropyOffset2;
            half  _AnisotropyShift2;
            half3 _SpecularColor2;

            half  _Anisotropy;
            half  _AnisotropyOffset;
            half  _AnisotropyShift;

            half  _Sheen;
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
            half3 _DiffuseShadowStrength;
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

            half _Subsurface;
            half _SubsurfaceStrength;

            half _Translucency;
            half _TranslucencyScale;
            half _TranslucencyPower;
            half _TranslucencyAmbient;
            half _TranslucencyDistortion;
            half3 _TranslucencyColor;

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

            #pragma shader_feature_local _FACIALSHADOWMAP
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ALPHA_VIEWSCALE
            #pragma shader_feature_local _ALPHA_ADDITIONAL_NORMAL_ON
            #pragma shader_feature_local _METALLICSPECGLOSSMAP
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

            #pragma shader_feature_local _BACKLIGHT_ON
            #pragma shader_feature_local _FACTATTENUATION_ON
            #pragma shader_feature_local _SPECULAR_ANTIALIASING
            #pragma shader_feature_local _TRANSLUCENCY_THICKNESS_OFF
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _MAIN_LIGHT_EXPONENTIAL_SHADOWS

            #pragma shader_feature_local _DEBUGMODE_NONE _DEBUGMODE_ALBEDO _DEBUGMODE_NORMAL _DEBUGMODE_NORMALEX _DEBUGMODE_METALLIC _DEBUGMODE_SMOOTHNESS _DEBUGMODE_OCCLUSION _DEBUGMODE_SHEEN _DEBUGMODE_TRANSLUCENCY
            #pragma shader_feature_local _LIGHTMODEL_STANDARD _LIGHTMODEL_CLOTH _LIGHTMODEL_ANISOTROPY _LIGHTMODEL_SUBSURFACE _LIGHTMODEL_SKIN

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
            #pragma shader_feature_local _SPECULARHIGHLIGHTS2_ON
            #pragma shader_feature _ENVIRONMENTREFLECTIONS_OFF

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 texcoord     : TEXCOORD0;
                float2 lightmapUV   : TEXCOORD1;//用来读取二套UV，刀痕
                float4 color        : COLOR;
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
                float4 normalWS                 : TEXCOORD3;
                float4 tangentWS                : TEXCOORD4; // xyz: tangent, w: viewDir.y              
                float3 positionWS               : TEXCOORD5;
                float4 screenPos                : TEXCOORD6;                

            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord              : TEXCOORD7;
            #endif

                half4 rimMask                   : TEXCOORD8;
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
                half3 normalTS;
                half3 normalLowTS;
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
                half  sheen;
                half3 baseLayerColor;
                half3 firstLayerColor;
                half3 secondLayerColor;
                half3 thirdLayerColor;
                half3 fourLayerColor;
                half3 diffuseShadowStrength;
                half  diffuseBrushStrength;
                half  lightShadowStrength;
                half  lightOcclusionStrength;
                half2 facialShadow;
                half  ambientDecoloration;
                half  ambientOcclusion;
                half  ambientLuminanceLimit;
                half  alpha;
                half  shift;
                half  shift2;
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
                half3   secondTangentWS;
                half3   bitangentWS;
                half3   viewDirectionWS;
                float4  shadowCoord;
                half3   bakedGI;
                half    roughness;
                half    roughness2;
                half    roughness2MinusOne;
                half    perceptualRoughness;

                half    grazingTerm;
                half    normalizationTerm;
            };

            void InitializePhysicalMaterial(float2 uv, out PhysicalMaterial material)
            {
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
                material.specular = lerp(_SpecularColor * _Specular * _Specular * 0.16, material.albedo, material.metallic);
                material.smoothness = specGloss.a;
            #if defined(_NORMALMODEL_WORLD) && defined(_NORMALMAP)
                material.normalTS = reflect(UnpackNormalRGBNoScale(pow(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), 1.0f / 2.2f)), float3(1, 0, 0));
                material.normalLowTS = reflect(UnpackNormalRGBNoScale(SAMPLE_TEXTURE2D_LOD(_NormalMap, sampler_NormalMap, uv, _SubsurfaceStrength * 7)), float3(1, 0, 0));
            #else
                material.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_NormalMap, sampler_NormalMap), _NormalScale);
                material.normalLowTS = SampleNormal(uv, TEXTURE2D_ARGS(_NormalMap, sampler_NormalMap), _NormalScale, _SubsurfaceStrength * 7);
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

                material.mask2 = 0;
                material.sheen = _Sheen;
                material.shift = _AnisotropyOffset;
                material.shift2 = _AnisotropyOffset2;

                material.subsurface = _Subsurface;

                material.translucency = _Translucency;
                material.translucencyAmbient = _TranslucencyAmbient;
                material.translucencyColor = _TranslucencyColor;
                material.translucencyDistortion = _TranslucencyDistortion;
                material.translucencyPower = _TranslucencyPower;
                material.translucencyScale = _TranslucencyScale;

            #if _HAIRCLOTHMAP
                material.mask2 = SAMPLE_TEXTURE2D(_Mask2, sampler_Mask2, uv * _Mask2_ST.xy + _Mask2_ST.zw);
                material.shift = _AnisotropyOffset + lerp(0, material.mask2.r * 2 - 1, _AnisotropyShift);
                material.shift2 = _AnisotropyOffset2 + lerp(0, material.mask2.r * 2 - 1, _AnisotropyShift2);
                material.subsurface *= material.mask2.g;
                material.sheen *= material.mask2.b;
                material.translucency *= material.mask2.a;
            #endif

                half oneMinusReflectivity = OneMinusReflectivityMetallic(material.metallic);
                half reflectivity = 1.0 - oneMinusReflectivity;

                material.diffuse = material.albedo * oneMinusReflectivity;

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
                float3 domainNormal = input.normalWS;
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
                geometryContext.normalWS = input.normalWS;
                geometryContext.normalLowWS = input.normalWS;
                geometryContext.colorNormalWS = input.colorNormalWS.xyz;
                geometryContext.domainNormal = domainNormal;
            #endif

                geometryContext.normalWS = NormalizeNormalPerPixel(geometryContext.normalWS);
                geometryContext.normalLowWS = NormalizeNormalPerPixel(geometryContext.normalLowWS);
                geometryContext.domainNormal = NormalizeNormalPerPixel(geometryContext.domainNormal);
                geometryContext.vertexNormalWS = NormalizeNormalPerPixel(input.normalWS);
                geometryContext.colorNormalWS = NormalizeNormalPerPixel(geometryContext.colorNormalWS);
                geometryContext.bitangentWS = NormalizeNormalPerPixel(cross(geometryContext.normalWS, float3(0, 1, 0)));
                geometryContext.viewDirectionWS = SafeNormalize(input.viewDirWS);

                half3 tangentWS = cross(geometryContext.normalWS, geometryContext.bitangentWS);
                geometryContext.tangentWS = NormalizeNormalPerPixel(tangentWS + geometryContext.normalWS * material.shift);
                geometryContext.secondTangentWS = NormalizeNormalPerPixel(tangentWS + geometryContext.normalWS * material.shift2);

            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                geometryContext.shadowCoord = input.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                geometryContext.shadowCoord = TransformWorldToShadowCoord(geometryContext.positionWS);
            #else
                geometryContext.shadowCoord = float4(0, 0, 0, 0);
            #endif

                geometryContext.bakedGI = SampleSH(normalize(lerp(geometryContext.normalWS, geometryContext.viewDirectionWS, 0)));
                geometryContext.bakedGI = lerp(geometryContext.bakedGI, Luminance(geometryContext.bakedGI), material.ambientDecoloration);

            #if _SPECULAR_ANTIALIASING
                material.smoothness = GeometricNormalFiltering(material.smoothness, geometryContext.normalWS, _SpecularAntiAliasingVariance, _SpecularAntiAliasingThreshold);
            #endif

                half oneMinusReflectivity = OneMinusReflectivityMetallic(material.metallic);
                half reflectivity = 1.0 - oneMinusReflectivity;

                geometryContext.grazingTerm = saturate(material.smoothness + reflectivity);
                geometryContext.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(material.smoothness);
                geometryContext.roughness = max(PerceptualRoughnessToRoughness(geometryContext.perceptualRoughness), HALF_MIN);
                geometryContext.roughness2 = geometryContext.roughness * geometryContext.roughness;

                geometryContext.normalizationTerm = geometryContext.roughness * 4.0h + 2.0h;
                geometryContext.roughness2MinusOne = geometryContext.roughness2 - 1.0h;
            }

            half3 ImageBasedGlobalIllumination(GeometryContext geometryContext, PhysicalMaterial material)
            {
                half3 domainNormal = geometryContext.domainNormal;

            #if _LIGHTMODEL_ANISOTROPY
                half3 ax = cross(-geometryContext.viewDirectionWS, geometryContext.tangentWS);
                half3 ay = cross(ax, geometryContext.tangentWS);
                domainNormal = lerp(domainNormal, normalize(lerp(domainNormal, ay, material.anisotropy * material.anisotropy)), geometryContext.roughness);
            #endif

                half3 bakedGI = RGBToYCoCg(geometryContext.bakedGI);
                bakedGI.x = max(bakedGI.x, material.ambientLuminanceLimit);
                bakedGI = YCoCgToRGB(bakedGI);

                half occlusionFresnel = lerp(1, material.ambientOcclusion, saturate(dot(geometryContext.vertexNormalWS, geometryContext.viewDirectionWS)));
                half3 reflectVector = reflect(-geometryContext.viewDirectionWS, domainNormal);
                half3 indirectDiffuse = bakedGI * GTAOMultiBounce(material.ambientOcclusion, material.albedo);
                half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, geometryContext.perceptualRoughness, material.ambientOcclusion);

            #if _LIGHTMODEL_CLOTH
                half3 specular = EnvironmentBRDF(domainNormal, geometryContext.viewDirectionWS, indirectDiffuse, indirectSpecular, material.diffuse, material.specular, geometryContext.roughness2, geometryContext.grazingTerm);
                half3 cloth = EnvironmentBRDF_Sheen(domainNormal, geometryContext.viewDirectionWS, indirectDiffuse, indirectSpecular, material.diffuse, material.specular, geometryContext.roughness);
                return lerp(specular, cloth, material.sheen);
            #elif _LIGHTMODEL_ANISOTROPY
                return EnvironmentBRDF(domainNormal, geometryContext.viewDirectionWS, indirectDiffuse, indirectSpecular, material.diffuse, material.specular, geometryContext.roughness2, geometryContext.grazingTerm);
            #else
                return EnvironmentBRDF(domainNormal, geometryContext.viewDirectionWS, indirectDiffuse, indirectSpecular, material.diffuse, material.specular, geometryContext.roughness2, geometryContext.grazingTerm);
            #endif
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
                float3 rgbCurvature = SAMPLE_TEXTURE2D(_SkinRamp, sampler_SkinRamp, float2(lookup, material.translucency));
                rgbCurvature = rgbCurvature * 0.5f - 0.2f;
                
                float3 BlurFactor = saturate(1.0f - nl);
                BlurFactor *= BlurFactor;
                float3 gN = normalize(lerp(geometryContext.normalWS, geometryContext.normalLowWS, 0.3f + 0.7f * BlurFactor));
                float3 bN = normalize(lerp(geometryContext.normalWS, geometryContext.normalLowWS, BlurFactor));
                float3 rgbNoL = float3(saturate(nl), saturate(dot(gN, L)),  saturate(dot(bN, L)));

                return saturate(rgbNoL + rgbCurvature);
            }

            float3 SamplePennerSkin(float nl, half ir)
            {
                float pndl = saturate( nl);
                float nndl = saturate(-nl);

                float brdf = pndl;
                float3 sss = float3(1.0,0.1,0.01) * (1.0 - pndl) * (1.0 - pndl) * pow(1.0-nndl,3.0 / (ir + 0.001)) * saturate(ir - 0.04);

                return saturate(brdf + sss);
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

            #if 0
                diffuse.r = SamplePennerSkin(NoL.r, material.subsurface).r;
                diffuse.g = SamplePennerSkin(NoL.g, material.subsurface).g;
                diffuse.b = SamplePennerSkin(NoL.b, material.subsurface).b;
            #else
                float3 lookup = NoL * 0.5f + 0.5f;
                diffuse.r = SAMPLE_TEXTURE2D(_SkinRamp, sampler_SkinRamp, float2(lookup.r, material.subsurface)).r;
                diffuse.g = SAMPLE_TEXTURE2D(_SkinRamp, sampler_SkinRamp, float2(lookup.g, material.subsurface)).g;
                diffuse.b = SAMPLE_TEXTURE2D(_SkinRamp, sampler_SkinRamp, float2(lookup.b, material.subsurface)).b;
            #endif

                return diffuse;
            }

            float sigmoid(float x, float offset, float sharp, float base)
            {
                float s;
                s = 1.0f / (1.0f + pow(base, (-3.0f * sharp * (x - offset))));
                return s;
            }

            real4 DiffuseRamp(real nl, real ir)
            {
                real x = nl * 0.8;

                real firstLayerOffset = _FirstLayerOffset;
                real secondLayerOffset = pow(1.0 - ir, 2.0) * lerp(0.3, 1.0, _SecondLayerOffset);
                real thirdLayerOffset = pow(1.0 - ir, 2.0) * lerp(-0.3, 1.0, _ThirdLayerOffset);
                real fourLayerOffset = pow(1.0 - ir, 2.0) * lerp(0.1, 1.0, _FourLayerOffset);

                real _FirstLayerSharp = 0.3;
                real _SecondLayerSharp = pow(lerp(1.1, 3.0, pow(ir, 1.6)), 3.0);
                real _ThirdLayerSharp = pow(lerp(1.1, 3.0, pow(ir, 1.6)), 3.0);
                real _FourLayerSharp = pow(lerp(0.5, 0.8, ir), 2.0);

                real _FirstLayer = sigmoid(x, firstLayerOffset, _FirstLayerSharp, 2000);
                real _SecondLayer = sigmoid(x, secondLayerOffset, _SecondLayerSharp, 5);
                real _ThirdLayer = sigmoid(x, thirdLayerOffset, _ThirdLayerSharp, 5);
                real _FourLayer = sigmoid(x, fourLayerOffset, _FourLayerSharp, 10000000);

                return real4(_FirstLayer, _SecondLayer, _ThirdLayer, _FourLayer);
            }

            half3 StylizedBasedDirectLighting(Light light, GeometryContext geometryContext, PhysicalMaterial material)
            {
                half3 domainNormal = geometryContext.domainNormal;

                half nl = dot(domainNormal, light.direction);
                half nv = abs(dot(geometryContext.viewDirectionWS, domainNormal));
                half lv = 1 - dot(geometryContext.viewDirectionWS, light.direction);
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
                half4 layers = DiffuseRamp(nv * 2 - 1, 1 - material.softness) * occlusion;
            #else
                half4 layers = DiffuseRamp(nl, 1 - material.softness) * occlusion;
            #endif

            #ifdef _FACTATTENUATION_ON
                shadowAttenuation *= lerp(0.2, 1.0f, pow(saturate(dot(Front, light.direction)), 0.45));
            #endif

                half3 diffuseTerm = material.baseLayerColor.rgb;
                diffuseTerm = lerp(material.firstLayerColor.rgb, diffuseTerm, layers.r);
                diffuseTerm = lerp(material.secondLayerColor.rgb, diffuseTerm, layers.g);
                diffuseTerm = lerp(material.thirdLayerColor.rgb, diffuseTerm, layers.b);
                diffuseTerm = lerp(material.fourLayerColor.rgb, diffuseTerm, layers.a);
                diffuseTerm = lerp(material.diffuseShadowStrength * diffuseTerm, diffuseTerm, shadowAttenuation);
                diffuseTerm = lerp(saturate(nl * (1 - material.softness * 0.5) + material.softness * 0.5) * shadowAttenuation, diffuseTerm, material.diffuseBrushStrength);

                half3 transLightDir = light.direction + geometryContext.normalWS * material.translucencyDistortion;
                half transDot = pow(saturate(dot(geometryContext.viewDirectionWS, -transLightDir)), material.translucencyPower) * material.translucencyScale;
                half translucencyTerm = saturate(1 - nl) * (transDot + material.translucencyAmbient) * material.translucency;

            #ifdef _BACKLIGHT_ON
                half falloff = clamp(1.0 - nv, 0.02, 0.98);
                half rimLightDot = saturate(0.5 * (dot(geometryContext.domainNormal, light.direction) + 1.5));
                half rimLight = SAMPLE_TEXTURE2D(_RimLightLookup, sampler_RimLightLookup, float2(rimLightDot * falloff, 0.5)).r;
                half3 rimTerm = _RimColor * rimLight * saturate(lv);
            #endif

            #ifdef _LIGHTMODEL_CLOTH
                half3 specularTerm = BRDF_Specular_GGX(light, geometryContext.normalWS, geometryContext.viewDirectionWS, geometryContext.roughness2, geometryContext.roughness2MinusOne, geometryContext.normalizationTerm);
                half3 clothD = BRDF_Specular_Sheen(light, geometryContext.normalWS, geometryContext.viewDirectionWS, geometryContext.roughness);
                specularTerm = lerp(specularTerm, clothD, material.sheen);
            #elif _LIGHTMODEL_ANISOTROPY
                half3 X = geometryContext.bitangentWS;
                half3 Y = geometryContext.tangentWS;
                half3 specularTerm = BRDF_Specular_Anisotropic_GGX(light, geometryContext.normalWS, X, Y, geometryContext.viewDirectionWS, material.anisotropy, geometryContext.roughness);
            #elif _LIGHTMODEL_SKIN
                half3 specularTerm = BRDF_Specular_Skin(geometryContext.normalWS, light.direction, geometryContext.viewDirectionWS, geometryContext.roughness, 4 * PI);
            #else
                half3 specularTerm = BRDF_Specular_GGX(light, geometryContext.normalWS, geometryContext.viewDirectionWS, geometryContext.roughness2, geometryContext.roughness2MinusOne, geometryContext.normalizationTerm);
            #endif

            #ifdef _SPECULARHIGHLIGHTS2_ON
                half exponent = exp2(10 * _Smoothness2 + 1);
                specularTerm += _SpecularColor2 * StrandSpecular(geometryContext.secondTangentWS, geometryContext.viewDirectionWS, light.direction, exponent, PI);
            #endif

                half3 lighting = material.diffuse * diffuseTerm;
                lighting += material.specular * lerp(material.diffuseShadowStrength * specularTerm, specularTerm, light.shadowAttenuation) * saturate(nl);
                lighting += material.diffuse * translucencyTerm * material.translucencyColor;
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
                Light mainLight = GetMainLight();
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
                        rimColor *= input.rimMask.w;
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

                output.colorNormalWS = half4(TransformObjectToWorldNormal(normalize(input.color * 2 - 1)), input.color.w);
                output.normalWS = half4(normalInput.normalWS, viewDirWS.x);
                output.tangentWS = half4(normalInput.tangentWS.xyz, input.tangentOS.w * GetOddNegativeScale());

                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS.xyz, output.vertexSH);

                output.positionWS = vertexInput.positionWS;
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);

                output.viewDirWS = viewDirWS;
                output.rimMask.xyz = TransformObjectToWorldNormal(input.color);
                output.rimMask.w = input.color.w;

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
                InitializePhysicalMaterial(input.uv.xy, material);

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

                #ifdef _ALPHA_VIEWSCALE
                    #if _ALPHA_ADDITIONAL_NORMAL_ON
                        half3 alphaNormal = input.colorNormalWS.xyz;
                    #else
                        half3 alphaNormal = input.normalWS.xyz;
                    #endif
                    color.a *= lerp(_AlphaViewBase, 1, pow(1 - abs(dot(alphaNormal, geometryContext.viewDirectionWS)), _AlphaViewPower));
                #endif

                color.a = saturate(color.a * _AlphaScale);

                #if _DEBUGMODE_ALBEDO
                    color.rgb = material.albedo;
                #elif _DEBUGMODE_NORMAL
                    color.rgb = geometryContext.normalWS * 0.5 + 0.5;
                #elif _DEBUGMODE_NORMALEX
                    color.rgb = geometryContext.colorNormalWS * 0.5 + 0.5;
                #elif _DEBUGMODE_SMOOTHNESS
                    color.rgb = material.smoothness;
                #elif _DEBUGMODE_METALLIC
                    color.rgb = material.metallic;
                #elif _DEBUGMODE_OCCLUSION
                    color.rgb = material.occlusion;
                #elif _DEBUGMODE_TRANSLUCENCY
                    color.rgb = material.translucency;
                #elif _DEBUGMODE_SHEEN
                    color.rgb = material.sheen;
                #endif

                return color;
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

            #define _ShadowNormalBias 0

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "PrepassDepth"
            Tags{"LightMode" = "PrepassDepth"}

            ZWrite [_ZWrite]
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
}