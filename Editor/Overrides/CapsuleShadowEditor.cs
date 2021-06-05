using UnityEditor;
using UnityEditor.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(CapsuleShadow))]
    sealed class CapsuleShadowEditor : VolumeComponentEditor
    {
        SerializedDataParameter _quality;
        SerializedDataParameter _angle;
        SerializedDataParameter _strength;
        SerializedDataParameter _fullRes;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<CapsuleShadow>(serializedObject);

            _quality = Unpack(o.Find(x => x.quality));
            _angle = Unpack(o.Find(x => x.angle));
            _strength = Unpack(o.Find(x => x.strength));
            _fullRes = Unpack(o.Find(x => x._fullRes));
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_quality);

            EditorGUI.BeginDisabledGroup((target as CapsuleShadow).quality.value != ScalableSettingLevelParameter.Level.Custom);
            PropertyField(_fullRes, new GUIContent("Full Resolution"));
            EditorGUI.EndDisabledGroup();

            PropertyField(_angle);
            PropertyField(_strength);
        }
    }
}
