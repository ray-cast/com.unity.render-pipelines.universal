Shader "Hidden/Universal Render Pipeline/Sky/HDRi Sky"
{
    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    float3 _Tint;
    float4 _SkyParam; // x exposure, y multiplier, zw rotation (cosPhi and sinPhi)

    #define _Intensity          _SkyParam.x
    #define _CosPhi             _SkyParam.z
    #define _SinPhi             _SkyParam.w
    #define _CosSinPhi          _SkyParam.zw

    TEXTURECUBE(_Cubemap);
    SAMPLER(sampler_Cubemap);

    float3 RotationUp(float3 p, float2 cos_sin)
    {
        float3 rotDirX = float3(cos_sin.x, 0, -cos_sin.y);
        float3 rotDirY = float3(cos_sin.y, 0,  cos_sin.x);

        return float3(dot(rotDirX, p), p.y, dot(rotDirY, p));
    }

    float3 GetSkyColor(float3 dir)
    {
        return SAMPLE_TEXTURECUBE_LOD(_Cubemap, sampler_Cubemap, dir, 0).rgb;
    }

    float4 GetColorWithRotation(float3 dir, float exposure, float2 cos_sin)
    {
        dir = RotationUp(dir, cos_sin);

        float3 skyColor = GetSkyColor(dir) * _Intensity * exposure;

        return float4(skyColor, 1.0);
    }

    struct Attributes
    {
        float4 positionOS   : POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float4 viewdir  : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings VertRender(uint id : SV_VERTEXID)
    {
		Varyings o;
		o.positionCS = GetFullScreenTriangleVertexPosition(id, UNITY_RAW_FAR_CLIP_VALUE);		
		o.viewdir = mul(unity_MatrixInvVP, ComputeClipSpacePosition(GetFullScreenTriangleTexCoord(id), 1));

		return o;
    }

    float4 FragRender(Varyings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float3 viewDirWS = -normalize(GetCameraPositionWS() - input.viewdir.xyz / input.viewdir.w);

        return float4(_Tint * GetColorWithRotation(viewDirWS, 2, _CosSinPhi).xyz, 1.0);
    }

    ENDHLSL

    SubShader
    {
        ZTest LEqual ZWrite Off
        Blend Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
                #pragma target 3.5
                #pragma prefer_hlslcc gles
                #pragma exclude_renderers d3d11_9x
                #pragma editor_sync_compilation

                #pragma vertex VertRender
                #pragma fragment FragRender
            ENDHLSL

        }
    }
    Fallback Off
}