using UnityEditor;
using UnityEditor.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(Tonemapping))]
    sealed class TonemappingEditor : VolumeComponentEditor
    {
        SerializedDataParameter _mode;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<Tonemapping>(serializedObject);

            _mode = Unpack(o.Find(x => x.mode));
        }

        public override void OnInspectorGUI()
        {
            if (UniversalRenderPipeline.asset != null && UniversalRenderPipeline.asset.postProcessingFeatureSet == PostProcessingFeatureSet.PostProcessingV2)
            {
                EditorGUILayout.HelpBox(UniversalRenderPipelineAssetEditor.Styles.postProcessingGlobalWarning, MessageType.Warning);
                return;
            }

            PropertyField(_mode);

            // Display a warning if the user is trying to use a tonemap while rendering in LDR
            if (UniversalRenderPipeline.asset)
			{
                if (UniversalRenderPipeline.asset.supportsHDR == false)
                    EditorGUILayout.HelpBox("Tonemapping should only be used when working in HDR.", MessageType.Warning);
            }
        }
    }
}
