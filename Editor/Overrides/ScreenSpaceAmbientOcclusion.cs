using UnityEditor;
using UnityEditor.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(ScreenSpaceAmbientOcclusion))]
    sealed class ScreenSpaceAmbientOcclusionEditor : VolumeComponentEditor
    {
        SerializedDataParameter _quality;
        SerializedDataParameter _radius;
        SerializedDataParameter _strength;
        SerializedDataParameter _fullRes;
        SerializedDataParameter _sharpness;
        SerializedDataParameter _bias;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<ScreenSpaceAmbientOcclusion>(serializedObject);

            _quality = Unpack(o.Find(x => x.quality));
            _radius = Unpack(o.Find(x => x.radius));
            _strength = Unpack(o.Find(x => x.strength));
            _fullRes = Unpack(o.Find(x => x._fullRes));
            _sharpness = Unpack(o.Find(x => x.sharpness));
            _bias = Unpack(o.Find(x => x.bias));
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_quality);

            EditorGUI.BeginDisabledGroup((target as ScreenSpaceAmbientOcclusion).quality.value != ScalableSettingLevelParameter.Level.Custom);
            PropertyField(_fullRes, new GUIContent("Full Resolution"));
            EditorGUI.EndDisabledGroup();

            PropertyField(_radius);
            PropertyField(_strength);
            PropertyField(_sharpness);
            PropertyField(_bias);
        }
    }
}
