using UnityEditor;
using UnityEditor.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(LensDistortion))]
    sealed class LensDistortionEditor : VolumeComponentEditor
    {
        public override void OnInspectorGUI()
        {
            if (UniversalRenderPipeline.asset != null && UniversalRenderPipeline.asset.postProcessingFeatureSet == PostProcessingFeatureSet.PostProcessingV2)
            {
                EditorGUILayout.HelpBox(UniversalRenderPipelineAssetEditor.Styles.postProcessingGlobalWarning, MessageType.Warning);
                return;
            }

            base.OnInspectorGUI();
        }
    }
}
