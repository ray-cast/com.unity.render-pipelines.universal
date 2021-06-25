using UnityEditor;
using UnityEditor.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(HeightFog))]
    sealed class HeightFogEditor : VolumeComponentEditor
    {
        SerializedDataParameter _enable;
        SerializedDataParameter _tint;
        SerializedDataParameter _fogAttenuationDistance;
        SerializedDataParameter _baseHeight;
        SerializedDataParameter _maximumHeight;
        SerializedDataParameter _relativeRendering;
        SerializedDataParameter _density;
        SerializedDataParameter _colorMode;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<HeightFog>(serializedObject);

            _enable = Unpack(o.Find(x => x.enable));
            _tint = Unpack(o.Find(x => x.tint));
            _fogAttenuationDistance = Unpack(o.Find(x => x.fogAttenuationDistance));
            _density = Unpack(o.Find(x => x.heightDensity));
            _baseHeight = Unpack(o.Find(x => x.baseHeight));
            _maximumHeight = Unpack(o.Find(x => x.maximumHeight));
            _relativeRendering = Unpack(o.Find(x => x.relativeRendering));
            _colorMode = Unpack(o.Find(x => x.colorMode));
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Fog", EditorStyles.miniLabel);

            PropertyField(_enable);
            PropertyField(_fogAttenuationDistance);

            PropertyField(_colorMode);
            PropertyField(_tint);

            PropertyField(_baseHeight);
            PropertyField(_maximumHeight);
            PropertyField(_density);
            PropertyField(_relativeRendering);
        }
    }
}
