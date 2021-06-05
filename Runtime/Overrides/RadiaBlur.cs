using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable, VolumeComponentMenu("Post-processing/RadiaBlur")]
public sealed class RadiaBlur : VolumeComponent, IPostProcessComponent
{
    [Range(0f, 1f), Tooltip("模糊中心点")]
    public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.5f));

    [Range(-1f, 1f), Tooltip("径向模糊强度")]
    public FloatParameter intensity = new FloatParameter(0.0f);

    [Range(0, 10f), Tooltip("径向中心模糊强度")]
    public FloatParameter exponential = new FloatParameter(0.45f);

    public bool IsActive() => (intensity.value != 0);

    public bool IsTileCompatible() => false;
}