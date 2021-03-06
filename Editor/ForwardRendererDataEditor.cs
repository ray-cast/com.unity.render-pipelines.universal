using UnityEditor;
using UnityEditor.ProjectWindowCallback;

namespace UnityEngine.Rendering.Universal
{
    [CustomEditor(typeof(ForwardRendererData), true)]
    public class ForwardRendererDataEditor : ScriptableRendererDataEditor
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812")]
        internal class CreateForwardRendererAsset : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                var instance = CreateInstance<ForwardRendererData>();
                AssetDatabase.CreateAsset(instance, pathName);
                ResourceReloader.ReloadAllNullIn(instance, UniversalRenderPipelineAsset.packagePath);
                Selection.activeObject = instance;
            }
        }

        [MenuItem("Assets/Create/Rendering/Universal Render Pipeline/Forward Renderer", priority = CoreUtils.assetCreateMenuPriority2)]
        static void CreateForwardRendererData()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, CreateInstance<CreateForwardRendererAsset>(), "CustomForwardRendererData.asset", null, null);
        }


        private static class Styles
        {
            public static readonly GUIContent RendererTitle = new GUIContent("Forward Renderer", "Custom Forward Renderer for Universal RP.");
            public static readonly GUIContent PostProcessLabel = new GUIContent("Post Process Data", "The asset containing references to shaders and Textures that the Renderer uses for post-processing.");
            public static readonly GUIContent FilteringLabel = new GUIContent("Filtering", "Controls filter rendering settings for this renderer.");
            public static readonly GUIContent OpaqueMask = new GUIContent("Opaque Layer Mask", "Controls which opaque layers this renderer draws.");
            public static readonly GUIContent TransparentMask = new GUIContent("Transparent Layer Mask", "Controls which transparent layers this renderer draws.");
            public static readonly GUIContent defaultStencilStateLabel = EditorGUIUtility.TrTextContent("Default Stencil State", "Configure stencil state for the opaque and transparent render passes.");
            public static readonly GUIContent shadowTransparentReceiveLabel = EditorGUIUtility.TrTextContent("Transparent Receive Shadows", "When disabled, none of the transparent objects will receive shadows.");
        }

        SerializedProperty _opaqueLayerMask;
        SerializedProperty _transparentLayerMask;
        SerializedProperty _defaultStencilState;
        SerializedProperty _postProcessData;
        SerializedProperty _shaders;
        SerializedProperty _shadowTransparentReceiveProp;

        private void OnEnable()
        {
            _opaqueLayerMask = serializedObject.FindProperty(nameof(ForwardRendererData._opaqueLayerMask));
            _transparentLayerMask = serializedObject.FindProperty(nameof(ForwardRendererData._transparentLayerMask));
            _defaultStencilState = serializedObject.FindProperty(nameof(ForwardRendererData._defaultStencilState));
            _shadowTransparentReceiveProp = serializedObject.FindProperty(nameof(ForwardRendererData._shadowTransparentReceive));
            _postProcessData = serializedObject.FindProperty(nameof(ForwardRendererData.postProcessData));
            _shaders = serializedObject.FindProperty(nameof(ForwardRendererData.shaders));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(Styles.RendererTitle, EditorStyles.boldLabel); // Title
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_postProcessData, Styles.PostProcessLabel);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(Styles.FilteringLabel, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_opaqueLayerMask, Styles.OpaqueMask);
            EditorGUILayout.PropertyField(_transparentLayerMask, Styles.TransparentMask);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Shadows", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_shadowTransparentReceiveProp, Styles.shadowTransparentReceiveLabel);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Overrides", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_defaultStencilState, Styles.defaultStencilStateLabel, true);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();

            base.OnInspectorGUI();

            if (EditorPrefs.GetBool("DeveloperMode"))
            {
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(_shaders, true);

                if (GUILayout.Button("Reload All"))
                {
                    var resources = target as ForwardRendererData;
                    resources.shaders = null;
                    ResourceReloader.ReloadAllNullIn(target, UniversalRenderPipelineAsset.packagePath);
                }
            }
        }
    }
}
