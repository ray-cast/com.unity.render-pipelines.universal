Shader "Hidden/Universal Render Pipeline/Vignette Lookup"
{
    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

    TEXTURE2D(_SceneLut);
    TEXTURE2D(_CameraColorTexture);

    float4 _userLutParams;
    half4 _Vignette_Params1;
    float4 _Vignette_Params2;
    half4 _Vignette_Params3;

    half _AdditionalLookupCount;
    float4 _AdditionalLookupPosition[MAX_VISIBLE_LIGHTS];

    #define VignetteColor           _Vignette_Params1.xyz
    #define VignetteCenter          _Vignette_Params2.xy
    #define VignetteRadius          _Vignette_Params2.z
    #define VignetteSmoothness      _Vignette_Params2.w
    #define VignetteRoundness       _Vignette_Params1.w

    Varyings VertRender(uint id : SV_VERTEXID)
    {
        Varyings o;
        o.uv = GetFullScreenTriangleTexCoord(id);
        o.positionCS = GetFullScreenTriangleVertexPosition(id);

		return o;
    }

    half3 ApplyMaskVignette(float2 uv, half2 center, half radius, half roundness, half smoothness, half3 color)
    {
        real2 dist = abs(uv - lerp(center, real2(0.5, 0.5), radius));
        dist.x *= roundness;

        real len = length(dist);
        real range = radius * 0.5;
        if (len < range)
            return 1;

        real maxLen = range + range * smoothness * 4;
        real diff = maxLen - len;

        return pow(saturate(diff / (maxLen - range)), 1);
    }

    float4 FragRender(Varyings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float deviceDepth = SampleSceneDepth(input.uv.xy);
#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
        deviceDepth = deviceDepth * 2 - 1;
#endif

        float3 worldPosition = ComputeWorldSpacePosition(input.uv.xy, deviceDepth, unity_MatrixInvVP);

        float4 color = SAMPLE_TEXTURE2D_LOD(_CameraColorTexture, sampler_PointClamp, input.uv.xy, 0);
        float3 inputLutSpace = saturate(LinearToLogC(color.xyz));
        float3 outLut = ApplyLut2D(TEXTURE2D_ARGS(_SceneLut, sampler_LinearClamp), inputLutSpace, _userLutParams.xyz);

        real3 vignette = 1;

        for (uint i = 0; i < uint(_AdditionalLookupCount); i++)
        {
            float4 anchor = _AdditionalLookupPosition[i];
            real3 atten = distance(worldPosition, anchor.xyz) / anchor.w;
            vignette = min(vignette, saturate(pow(atten, _Vignette_Params3.x)));
        }

        UNITY_FLATTEN
        if (VignetteRadius > 0)
        {
            vignette *= ApplyMaskVignette(input.uv.xy, VignetteCenter, VignetteRadius, VignetteRoundness, VignetteSmoothness, 1 - VignetteColor);
        }
        else
        {
            vignette = 0;
        }

        return float4(lerp(color, outLut, vignette * _userLutParams.w), color.a);
    }

    ENDHLSL

    SubShader
    {
        Pass
        {
            ZTest Off ZWrite Off
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma target 3.5
                #pragma editor_sync_compilation

                #pragma vertex VertRender
                #pragma fragment FragRender
            ENDHLSL

        }
    }
    Fallback Off
}