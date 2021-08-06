using UnityEditor;
using UnityEditor.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(AntiAliasing))]
    sealed class AntiAliasingEditor : VolumeComponentEditor
    {
        SerializedDataParameter _mode;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<AntiAliasing>(serializedObject);

            _mode = Unpack(o.Find(x => x.mode));
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_mode);

            if ((target as AntiAliasing).mode == AntialiasingMode.FastApproximateAntialiasing)
			{
                EditorGUILayout.HelpBox("目前暂不支持FXAA", MessageType.Info);
            }
        }
    }
}
