using UnityEditor;

namespace UnityEngine.Rendering.Universal
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(UniversalAdditionalCameraData))]
    class UniversalAdditionalCameraDataEditor : Editor
    {
        public override void OnInspectorGUI()
        {
        }

        [MenuItem("CONTEXT/UniversalAdditionalCameraData/Remove Component")]
        static void RemoveComponent(MenuCommand command)
        {
            EditorUtility.DisplayDialog("Component Info", "You cannot delete this component, you will have to remove the camera.", "OK");
        }
    }
}