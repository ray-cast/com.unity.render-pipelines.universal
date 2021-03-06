using UnityEditor;
using UnityEditor.ProjectWindowCallback;

namespace UnityEngine.Rendering.Universal
{
    [CustomEditor(typeof(PostProcessData), true)]
    public class PostProcessDataEditor : Editor
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812")]
        internal class CreatePostProcessDataAsset : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                var instance = CreateInstance<PostProcessData>();
                AssetDatabase.CreateAsset(instance, pathName);
                ResourceReloader.ReloadAllNullIn(instance, UniversalRenderPipelineAsset.packagePath);
                Selection.activeObject = instance;
            }
        }

        [MenuItem("Assets/Create/Rendering/Universal Render Pipeline/Post-process Data", priority = CoreUtils.assetCreateMenuPriority3)]
        static void CreatePostProcessData()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, CreateInstance<CreatePostProcessDataAsset>(), "CustomPostProcessData.asset", null, null);
        }

        SerializedProperty _shaders;
        SerializedProperty _textures;

        private void OnEnable()
        {
            _shaders = serializedObject.FindProperty("shaders");
            _textures = serializedObject.FindProperty("textures");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (EditorPrefs.GetBool("DeveloperMode"))
            {
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(_shaders, true);
                EditorGUILayout.PropertyField(_textures, true);

                if (GUILayout.Button("Reload All"))
                {
                    var resources = target as PostProcessData;
                    resources.shaders = null;
                    resources.textures = null;
                    ResourceReloader.ReloadAllNullIn(target, UniversalRenderPipelineAsset.packagePath);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
