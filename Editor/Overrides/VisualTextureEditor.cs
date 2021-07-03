using UnityEditor;
using UnityEditor.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(VirtualTexture))]
    sealed class VirtualTextureEditor : VolumeComponentEditor
    {
        SerializedDataParameter _enable;
        SerializedDataParameter _center;
        SerializedDataParameter _size;
        SerializedDataParameter _regionAdaptation;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<VirtualTexture>(serializedObject);

            _enable = Unpack(o.Find(x => x.enable));
            _center = Unpack(o.Find(x => x.center));
            _size = Unpack(o.Find(x => x.size));
            _regionAdaptation = Unpack(o.Find(x => x.regionAdaptation));
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_enable);

            EditorGUI.BeginChangeCheck();            
            PropertyField(_regionAdaptation);
            if (EditorGUI.EndChangeCheck())
                VirtualTextureSystem.instance.Reset();

            EditorGUI.BeginDisabledGroup((target as VirtualTexture).regionAdaptation.value);
            PropertyField(_center);
            PropertyField(_size);
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(10);

            if (GUILayout.Button("Rebuild"))
            {
                VirtualTextureSystem.instance.Reset();
            }
        }
    }
}
