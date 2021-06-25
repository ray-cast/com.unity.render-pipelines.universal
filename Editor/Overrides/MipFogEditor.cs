using UnityEditor;
using UnityEditor.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(MipFog))]
    sealed class MipFogEditor : VolumeComponentEditor
    {
        SerializedDataParameter _mode;
        SerializedDataParameter _color;
        SerializedDataParameter _tint;
        SerializedDataParameter _density;
        SerializedDataParameter _start;
        SerializedDataParameter _end;
        SerializedDataParameter _skyDensity;
        SerializedDataParameter _colorMode;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<MipFog>(serializedObject);

            _mode = Unpack(o.Find(x => x.mode));
            _color = Unpack(o.Find(x => x.color));
            _tint = Unpack(o.Find(x => x.tint));
            _density = Unpack(o.Find(x => x.density));
            _start = Unpack(o.Find(x => x.start));
            _end = Unpack(o.Find(x => x.end));
            _skyDensity = Unpack(o.Find(x => x.skyDensity));
            _colorMode = Unpack(o.Find(x => x.colorMode));
        }

        public override void OnInspectorGUI()
        {
            if (UniversalRenderPipeline.asset != null && UniversalRenderPipeline.asset.postProcessingFeatureSet == PostProcessingFeatureSet.PostProcessingV2)
            {
                EditorGUILayout.HelpBox(UniversalRenderPipelineAssetEditor.Styles.postProcessingGlobalWarning, MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Fog", EditorStyles.miniLabel);

            PropertyField(_mode);

            PropertyField(_colorMode);
            PropertyField((target as MipFog).colorMode == FogColorMode.SkyColor ? _tint : _color);

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
        }
    }
}
