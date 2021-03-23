using UnityEditor;

namespace UnityEngine.Rendering.Universal
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(UniversalAdditionalLightData))]
    public class UniversalAdditionLightDataEditor : Editor
    {
        public override void OnInspectorGUI()
        {
        }

        [MenuItem("CONTEXT/UniversalAdditionLightData/Remove Component")]
        static void RemoveComponent(MenuCommand command)
        {
            if (EditorUtility.DisplayDialog("Remove Component?", "Are you sure you want to remove this component? If you do, you will lose some settings.", "Remove", "Cancel"))
            {
                Undo.DestroyObjectImmediate(command.context);
            }
        }
    }
}