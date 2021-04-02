using UnityEditor;
using UnityEditor.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(MipFog))]
    sealed class MipFogEditor : VolumeComponentEditor
    {
        SerializedDataParameter _mode;
        SerializedDataParameter _color;
        SerializedDataParameter _density;
        SerializedDataParameter _start;
        SerializedDataParameter _end;
        SerializedDataParameter _skyDensity;
        SerializedDataParameter _skybox;
        SerializedDataParameter _rotation;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<MipFog>(serializedObject);

            _mode = Unpack(o.Find(x => x.mode));
            _color = Unpack(o.Find(x => x.color));
            _density = Unpack(o.Find(x => x.density));
            _start = Unpack(o.Find(x => x.start));
            _end = Unpack(o.Find(x => x.end));
            _skyDensity = Unpack(o.Find(x => x.skyDensity));
            _skybox = Unpack(o.Find(x => x.skybox));
            _rotation = Unpack(o.Find(x => x.rotation));
        }

        public override void OnInspectorGUI()
        {
            if (UniversalRenderPipeline.asset != null && UniversalRenderPipeline.asset.postProcessingFeatureSet == PostProcessingFeatureSet.PostProcessingV2)
            {
                EditorGUILayout.HelpBox(UniversalRenderPipelineAssetEditor.Styles.postProcessingGlobalWarning, MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Fog", EditorStyles.miniLabel);

            PropertyField(_color);
            PropertyField(_mode);

            if ((target as MipFog).mode == MipFogMode.Linear)
            {
                PropertyField(_start);
                PropertyField(_end);
            }
            else
            {
                PropertyField(_density);
            }

            PropertyField(_skyDensity);

            EditorGUILayout.LabelField("Mip Map", EditorStyles.miniLabel);

            PropertyField(_skybox);
            PropertyField(_rotation);
        }
    }
}
