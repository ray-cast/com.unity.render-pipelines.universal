using UnityEditor;
using UnityEditor.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(VisualEnvironment))]
    sealed class VisualEnvironmentEditor : VolumeComponentEditor
    {
        SerializedDataParameter _sky;
        SerializedDataParameter _ambient;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<VisualEnvironment>(serializedObject);

            _sky = Unpack(o.Find(x => x.sky));
            _ambient = Unpack(o.Find(x => x.ambient));
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_sky);
            PropertyField(_ambient);
        }
    }
}
