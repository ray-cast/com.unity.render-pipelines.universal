#ifndef UNITY_INSTANCING_RENDERING_INCLUDED
#define UNITY_INSTANCING_RENDERING_INCLUDED

struct BatchData
{
    float4 position;
    float4 scale;
};

StructuredBuffer<BatchData> _AllInstancesTransformBuffer;
StructuredBuffer<uint> _AllVisibleInstancesIndexBuffer;

static float unity_InstanceBakeGI;

void SetupInstancing()
{
#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
    uint index = _AllVisibleInstancesIndexBuffer[unity_InstanceID];
    float4 position = _AllInstancesTransformBuffer[index].position;
    float3 scale = _AllInstancesTransformBuffer[index].scale.xyz;
    UNITY_MATRIX_M = float4x4(
            scale.x,0,0, position.x,
            0,scale.y,0, position.y,
            0,0,scale.z, position.z,
            0,0,0,1
        );
    unity_InstanceBakeGI = position.w;
#endif
}

#endif