using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Setup a specific render pipeline on scene loading.
/// </summary>
[ExecuteAlways]
public class SwitchRenderPipeline : MonoBehaviour
{
    /// <summary>
    /// Scriptable Render Pipeline Asset to setup on scene load.
    /// </summary>
    public RenderPipelineAsset enterPipelineAsset;
    public RenderPipelineAsset leavePipelineAsset;

    void OnEnable()
    {
        GraphicsSettings.renderPipelineAsset = enterPipelineAsset;
    }

    void OnDisable()
    {
        GraphicsSettings.renderPipelineAsset = leavePipelineAsset;
    }
}