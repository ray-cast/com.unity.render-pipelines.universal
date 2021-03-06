using UnityEditor;
using UnityEditor.ProjectWindowCallback;

namespace UnityEngine.Rendering.Universal
{
    [CustomEditor(typeof(ClusterBasedDeferredRendererData), true)]
    public class ClusterBasedDeferredRendererDataEditor : ScriptableRendererDataEditor
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812")]
        internal class CreateClusterBasedDeferredRendererAsset : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                var instance = CreateInstance<ClusterBasedDeferredRendererData>();
                AssetDatabase.CreateAsset(instance, pathName);
                ResourceReloader.ReloadAllNullIn(instance, UniversalRenderPipelineAsset.packagePath);
                Selection.activeObject = instance;
            }
        }

        [MenuItem("Assets/Create/Rendering/Universal Render Pipeline/Cluster Based Deferred Renderer", priority = CoreUtils.assetCreateMenuPriority2)]
        static void CreateClusterBasedDeferredRendererData()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, CreateInstance<CreateClusterBasedDeferredRendererAsset>(), "ClusterBasedDeferredRendererData.asset", null, null);
        }

        private static class Styles
        {
            public static readonly GUIContent rendererTitle = new GUIContent("后处理", "Custom Forward Renderer for Universal RP.");
            public static readonly GUIContent postProcessLabel = new GUIContent("后处理数据", "The asset containing references to shaders and Textures that the Renderer uses for post-processing.");
            public static readonly GUIContent filteringLabel = new GUIContent("物件过滤", "Controls filter rendering settings for this renderer.");
            public static readonly GUIContent opaqueMask = new GUIContent("不透明层遮罩", "Controls which opaque layers this renderer draws.");
            public static readonly GUIContent transparentMask = new GUIContent("透明层遮罩", "Controls which transparent layers this renderer draws.");
            public static readonly GUIContent defaultStencilStateLabel = EditorGUIUtility.TrTextContent("默认模板设置", "Configure stencil state for the opaque and transparent render passes.");
            public static readonly GUIContent shadowTransparentReceiveLabel = EditorGUIUtility.TrTextContent("透明物件产生阴影", "When disabled, none of the transparent objects will receive shadows.");
        }

        SerializedProperty _opaqueLayerMask;
        SerializedProperty _transparentLayerMask;
        SerializedProperty _defaultStencilState;
        SerializedProperty _postProcessData;
        SerializedProperty _shaders;
        SerializedProperty _shadowTransparentReceiveProp;

        private void OnEnable()
        {
            _opaqueLayerMask = serializedObject.FindProperty(nameof(ClusterBasedDeferredRendererData._opaqueLayerMask));
            _transparentLayerMask = serializedObject.FindProperty(nameof(ClusterBasedDeferredRendererData._transparentLayerMask));
            _defaultStencilState = serializedObject.FindProperty(nameof(ClusterBasedDeferredRendererData._defaultStencilState));
            _shadowTransparentReceiveProp = serializedObject.FindProperty(nameof(ClusterBasedDeferredRendererData._shadowTransparentReceive));
            _postProcessData = serializedObject.FindProperty(nameof(ClusterBasedDeferredRendererData.postProcessData));
            _shaders = serializedObject.FindProperty(nameof(ClusterBasedDeferredRendererData.shaders));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(Styles.rendererTitle, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_postProcessData, Styles.postProcessLabel);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(Styles.filteringLabel, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_opaqueLayerMask, Styles.opaqueMask);
            EditorGUILayout.PropertyField(_transparentLayerMask, Styles.transparentMask);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("阴影", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_shadowTransparentReceiveProp, Styles.shadowTransparentReceiveLabel);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("模板测试", EditorStyles.boldLabel);
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