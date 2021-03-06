using UnityEditor;
using UnityEditor.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(MotionBlur))]
    sealed class MotionBlurEditor : VolumeComponentEditor
    {
        //SerializedDataParameter m_Mode;
        SerializedDataParameter _quality;
        SerializedDataParameter _intensity;
        SerializedDataParameter _clamp;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<MotionBlur>(serializedObject);

            //m_Mode = Unpack(o.Find(x => x.mode));
            _quality = Unpack(o.Find(x => x.quality));
            _intensity = Unpack(o.Find(x => x.intensity));
            _clamp = Unpack(o.Find(x => x.clamp));
        }

        public override void OnInspectorGUI()
        {
            if (UniversalRenderPipeline.asset != null && UniversalRenderPipeline.asset.postProcessingFeatureSet == PostProcessingFeatureSet.PostProcessingV2)
            {
                EditorGUILayout.HelpBox(UniversalRenderPipelineAssetEditor.Styles.postProcessingGlobalWarning, MessageType.Warning);
                return;
            }

            //PropertyField(m_Mode);

            //if (m_Mode.value.intValue == (int)MotionBlurMode.CameraOnly)
            //{
            PropertyField(_quality);
                PropertyField(_intensity);
                PropertyField(_clamp);
            //}
            //else
            //{
            //    EditorGUILayout.HelpBox("Object motion blur is not supported on the Universal Render Pipeline yet.", MessageType.Info);
            //}
        }
    }
}
