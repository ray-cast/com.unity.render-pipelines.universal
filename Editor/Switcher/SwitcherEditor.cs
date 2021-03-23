using UnityEditor;
using UnityEditor.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [CustomEditor(typeof(Switcher))]
    sealed class SwitcherEditor : Editor
    {
        SerializedProperty m_IsGlobal;
        SerializedProperty m_BlendRadius;
        SerializedProperty m_Weight;
        SerializedProperty m_Priority;

        Switcher actualTarget => target as Switcher;

        readonly GUIContent[] m_Modes = { new GUIContent("Global"), new GUIContent("Local") };

        public void OnEnable()
        {
            var o = new PropertyFetcher<Switcher>(serializedObject);
            m_IsGlobal = o.Find(x => x.isGlobal);
            m_BlendRadius = o.Find(x => x.blendDistance);
            m_Weight = o.Find(x => x.weight);
            m_Priority = o.Find(x => x.priority);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUIContent label = EditorGUIUtility.TrTextContent("Mode", "A global volume is applied to the whole scene.");
            Rect lineRect = EditorGUILayout.GetControlRect();
            int isGlobal = m_IsGlobal.boolValue ? 0 : 1;
            EditorGUI.BeginProperty(lineRect, label, m_IsGlobal);
            {
                EditorGUI.BeginChangeCheck();
                isGlobal = EditorGUI.Popup(lineRect, label, isGlobal, m_Modes);
                if (EditorGUI.EndChangeCheck())
                    m_IsGlobal.boolValue = isGlobal == 0;
            }
            EditorGUI.EndProperty();

            if (isGlobal != 0) // Blend radius is not needed for global volumes
            {
                if (!actualTarget.TryGetComponent<Collider>(out _))
                {
                    EditorGUILayout.HelpBox("Add a Collider to this GameObject to set boundaries for the local Volume.", MessageType.Info);

                    if (GUILayout.Button(EditorGUIUtility.TrTextContent("Add Collider"), EditorStyles.miniButton))
                    {
                        var menu = new GenericMenu();
                        menu.AddItem(EditorGUIUtility.TrTextContent("Box"), false, () => Undo.AddComponent<BoxCollider>(actualTarget.gameObject));
                        menu.AddItem(EditorGUIUtility.TrTextContent("Sphere"), false, () => Undo.AddComponent<SphereCollider>(actualTarget.gameObject));
                        menu.AddItem(EditorGUIUtility.TrTextContent("Capsule"), false, () => Undo.AddComponent<CapsuleCollider>(actualTarget.gameObject));
                        menu.AddItem(EditorGUIUtility.TrTextContent("Mesh"), false, () => Undo.AddComponent<MeshCollider>(actualTarget.gameObject));
                        menu.ShowAsContext();
                    }
                }

                EditorGUILayout.PropertyField(m_BlendRadius);
                m_BlendRadius.floatValue = Mathf.Max(m_BlendRadius.floatValue, 0f);
            }

            EditorGUILayout.PropertyField(m_Weight);
            EditorGUILayout.PropertyField(m_Priority);

            GUILayout.Label("Target");

            for (int i = 0; i < actualTarget.components.Count; i++)
            {
                var source = actualTarget.components[i];

                EditorGUILayout.BeginHorizontal();
                source.light = EditorGUILayout.ObjectField(source.light, typeof(Light), true) as Light;
                source.weight = Mathf.Clamp(EditorGUILayout.FloatField(source.weight, new[] { GUILayout.Width(40) }), 0.0f, 1.0f);

                if (GUILayout.Button("Delete", new[] { GUILayout.Width(50) }))
                    actualTarget.components.RemoveAt(i);

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Light Switcher", EditorStyles.miniButton))
            {
                AddSwitchMenu();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void AddSwitchMenu()
        {
            var switcher = target as Switcher;
            switcher.components.Add(ScriptableObject.CreateInstance<SwitcherComponent>());
        }
    }
}