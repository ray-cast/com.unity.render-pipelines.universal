using UnityEditor;
using UnityEditor.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(VignetteLookup))]
    sealed class VignetteLookupEditor : VolumeComponentEditor
    {
        SerializedDataParameter _color;
        SerializedDataParameter _center;
        SerializedDataParameter _radius;
        SerializedDataParameter _smoothness;
        SerializedDataParameter _rounded;

        SerializedDataParameter _texture;
        SerializedDataParameter _contribution;

        SerializedDataParameter _strength;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<VignetteLookup>(serializedObject);

            _color = Unpack(o.Find(x => x.color));
            _center = Unpack(o.Find(x => x.center));
            _radius = Unpack(o.Find(x => x.radius));
            _smoothness = Unpack(o.Find(x => x.smoothness));
            _rounded = Unpack(o.Find(x => x.rounded));

            _texture = Unpack(o.Find(x => x.texture));
            _contribution = Unpack(o.Find(x => x.contribution));

            _strength = Unpack(o.Find(x => x.strength));
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Lookup", EditorStyles.miniLabel);

            PropertyField(_texture, EditorGUIUtility.TrTextContent("Lookup Texture"));

            var lut = _texture.value.objectReferenceValue;
            if (lut != null && !((VignetteLookup)target).ValidateLUT())
                EditorGUILayout.HelpBox("Invalid lookup texture. It must be a half floating-point 2D texture or render texture with the same size.", MessageType.Warning);

            PropertyField(_contribution, EditorGUIUtility.TrTextContent("Contribution"));

            EditorGUILayout.LabelField("Vignette", EditorStyles.miniLabel);

            PropertyField(_color);
            PropertyField(_center);
            PropertyField(_radius);
            PropertyField(_smoothness);
            PropertyField(_rounded);

            EditorGUILayout.LabelField("Anchor", EditorStyles.miniLabel);

            PropertyField(_strength, EditorGUIUtility.TrTextContent("Strength"));
        }
    }
}
