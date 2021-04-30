using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    [CustomEditor(typeof(MeshBatchRenderer))]
    public class MeshBatchRendererEditor : Editor
    {
        public class Styles
        {
            public static GUIContent drawSettingsText = EditorGUIUtility.TrTextContent("画刷设置");
            public static GUIContent materialSettingsText = EditorGUIUtility.TrTextContent("材质设置");
            public static GUIContent renderingSettingsText = EditorGUIUtility.TrTextContent("渲染设置");
        }

        SavedBool _drawSettingsFoldout;
        SavedBool _materialSettingsFoldout;
        SavedBool _renderingSettingsFoldout;

        bool _isEnableBrush = false;
        int layerMask = int.MaxValue;
        float _eraseRadius = 0.3f;
        MeshBatchRenderer _renderer { get { return target as MeshBatchRenderer; } }
        MeshBatchData _batchData;
        int randomCount = 1024;

        private MaterialEditor _materialEditor;

        public void OnEnable()
        {
            _materialSettingsFoldout = new SavedBool($"{target.GetType()}.MaterialSettingsFoldout", true);
            _renderingSettingsFoldout = new SavedBool($"{target.GetType()}.CullingSettingsFoldout", true);
            _drawSettingsFoldout = new SavedBool($"{target.GetType()}.DrawSettingsFoldout", false);
        }

        public override void OnInspectorGUI()
        {
            if (_renderer.instanceMaterial != null)
                _materialEditor = CreateEditor(_renderer.instanceMaterial) as MaterialEditor;

            _batchData = _renderer.instanceBatchData;

            EditorGUILayout.BeginVertical();

            this.DrawMaterialSettings();
            this.DrawRenderingSettings();
            this.DrawPainterSetting();

            EditorGUILayout.EndVertical();

            this.DrawMaterialEditor();

            if (GUI.changed)
            {
                Debug.LogFormat("GUI.changed={0}", GUI.changed);
                EditorUtility.SetDirty(target);
            }
        }

        void DrawMaterialSettings()
        {
            _materialSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(_materialSettingsFoldout.value, Styles.materialSettingsText);
            if (_materialSettingsFoldout.value)
            {
                 EditorGUI.BeginChangeCheck();

                var instanceMaterial = (Material)EditorGUILayout.ObjectField("材质", _renderer.instanceMaterial, typeof(Material), true);

                if (EditorGUI.EndChangeCheck())
                {
                    if (_renderer.instanceMaterial && instanceMaterial)
                        instanceMaterial.CopyPropertiesFromMaterial(_renderer.instanceMaterial);

                    _renderer.instanceMaterial = instanceMaterial;

                    serializedObject.ApplyModifiedProperties();

                    if (_materialEditor != null)
                        DestroyImmediate(_materialEditor);

                    if (_renderer.instanceMaterial != null)
                        _materialEditor = CreateEditor(_renderer.instanceMaterial) as MaterialEditor;
                }

                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawRenderingSettings()
        {
            _renderingSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(_renderingSettingsFoldout.value, Styles.renderingSettingsText);
            if (_renderingSettingsFoldout.value)
            {
                GUILayout.Label(string.Format("实例数量:{0}", _batchData.instanceData.Count));
                _renderer.debugMode = EditorGUILayout.Toggle(string.Format("实例绘制数量:{0}", _renderer.drawInstancedCount), _renderer.debugMode);

                EditorGUILayout.Space();

                _renderer.sensity = EditorGUILayout.Slider("全局密度", _renderer.sensity, 0.0f, 1.0f);
                _renderer.maxDrawDistance = EditorGUILayout.Slider("最大可视距离", _renderer.maxDrawDistance, 1.0f, 150f);
                _renderer.distanceCulling = EditorGUILayout.Slider("可视距离剔除权重", _renderer.distanceCulling, 0f, 1f);
                _renderer.isCpuCulling = EditorGUILayout.Toggle("启用区域剔除（CPU）", _renderer.isCpuCulling);
                _renderer.isGpuCulling = EditorGUILayout.Toggle("启用遮挡剔除（GPU Driver）", _renderer.isGpuCulling);

                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawPainterSetting()
        {
            _drawSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(_drawSettingsFoldout.value, Styles.drawSettingsText);
            if (_drawSettingsFoldout.value)
            {
                _isEnableBrush = EditorGUILayout.Toggle("启用绘制", _isEnableBrush);
                _batchData.brushSensity = EditorGUILayout.IntSlider("密度(厘米)", _batchData.brushSensity, 1, 50);
                _eraseRadius = EditorGUILayout.Slider("清除半径(厘米)", _eraseRadius, 0.1f, 5f);

                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                randomCount = EditorGUILayout.IntField("随机数量", randomCount);
                if (GUILayout.Button("随机树"))
                    _batchData.RandomGroupBySensity(_renderer.transform, randomCount);
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("清除所有树"))
                    _batchData.Clear();

                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawMaterialEditor()
        {
            if (_materialEditor != null)
            {
                _materialEditor.DrawHeader();

                bool isDefaultMaterial = AssetDatabase.GetAssetPath(_renderer.instanceMaterial).StartsWith("Assets");

                using (new EditorGUI.DisabledGroupScope(isDefaultMaterial))
                    _materialEditor.OnInspectorGUI();
            }
        }

        void OnSceneGUI()
        {
            if (!EditorApplication.isPlaying && _isEnableBrush)
			{
                Painter();

                if (UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                    SceneView.RepaintAll();
            }
        }

        void Painter()
        {
            Event e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            HandleUtility.AddDefaultControl(0);

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.KeypadPlus)
            {
                FutureTerrainWindow.brushSize += 1;
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.KeypadMinus)
            {
                FutureTerrainWindow.brushSize -= 1;
            }

            if (Physics.Raycast(ray, out var raycastHit, Mathf.Infinity, layerMask))
            {
                if (!e.shift)
                {
                    Handles.color = Color.green;
                    Handles.DrawLine(raycastHit.point + new Vector3(0, 0.5f, 0), raycastHit.point);
                }
                else
                {
                    Handles.color = Color.red;
                    Handles.DrawWireDisc(raycastHit.point, raycastHit.normal, _eraseRadius);
                }

                if (e.isMouse && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                {
                    Debug.LogFormat("hit mesh orgin {0} dir {1} hit {2}", ray.origin, ray.direction, raycastHit.point);
                    if (!e.shift)
                    {
                        _batchData.Append(raycastHit.point, Vector3.one);
                        _batchData.UploadMeshData();
                        EditorUtility.SetDirty(target);
                    }
                    else
                    {
                        _batchData.Remove(raycastHit.point, _eraseRadius);
                        _batchData.UploadMeshData();
                        EditorUtility.SetDirty(target);
                    }
                }
            }
        }
    }

    static class TreeMenuItems
    {
        [MenuItem("GameObject/GPU Driven/Batch Renderer", priority = CoreUtils.gameObjectMenuPriority)]
        static void CreateBatchRenderer(MenuCommand menuCommand)
        {
            Vector3[] verts = new Vector3[3];
            verts[0] = new Vector3(-0.01f, 0);
            verts[1] = new Vector3(+0.01f, 0);
            verts[2] = new Vector3(-0.0f, 0.3f);
            int[] trinagles = new int[3] { 2, 1, 0, };

            var go = CoreEditorUtils.CreateGameObject("GPU Driven Batch", menuCommand.context);
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(trinagles, 0);

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshBatchRenderer>();
        }

        [MenuItem("GameObject/GPU Driven/Combine/Tree Batch", true)]
        static bool ValidateConvertTree()
        {
            return Selection.activeGameObject != null && PrefabUtility.GetPrefabInstanceHandle(Selection.activeGameObject) == null;
        }

        [MenuItem("GameObject/GPU Driven/Combine/Tree Batch", priority = CoreUtils.gameObjectMenuPriority)]
        static void ConvertTree(MenuCommand menuCommand)
        {
            Transform[] transforms = Selection.GetTransforms(SelectionMode.Deep | SelectionMode.ExcludePrefab | SelectionMode.Editable);

            Undo.RecordObjects(transforms, "Place Selection On Surface");

            Dictionary<Mesh, List<Transform>> meshClassify = new Dictionary<Mesh, List<Transform>>();

            foreach (Transform t in transforms)
			{
                if (t.TryGetComponent<MeshFilter>(out var filter))
				{
                    if (meshClassify.TryGetValue(filter.sharedMesh, out var value))
                        value.Add(t);
                    else
					{
                        var list = new List<Transform>();
                        list.Add(t);
                        meshClassify.Add(filter.sharedMesh, list);
                    }
                }
            }

            foreach (var group in meshClassify)
			{
                var go = CoreEditorUtils.CreateGameObject(Selection.activeGameObject, "GPU Driven Tree");

                var meshFilter = go.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = group.Key;

                var batchRenderer = go.AddComponent<MeshBatchRenderer>();
                batchRenderer.instanceMaterial = new Material(Shader.Find("Universal Render Pipeline/Tree Lit"));
                batchRenderer.instanceMaterial.SetInt("_InstancingRendering", 1);
                batchRenderer.instanceMaterial.EnableKeyword("_INSTANCING_RENDERING_ON");

                if (group.Value[0].TryGetComponent<MeshRenderer>(out var renderer))
				{
                    var material = renderer.sharedMaterial;
                    if (material)
					{
                        batchRenderer.instanceMaterial.color = material.color;
                        batchRenderer.instanceMaterial.mainTexture = material.mainTexture;
                        batchRenderer.instanceMaterial.mainTextureOffset = material.mainTextureOffset;
                        batchRenderer.instanceMaterial.mainTextureScale = material.mainTextureScale;
                    }
                }

                foreach (var item in group.Value)
                    batchRenderer.instanceBatchData.Append(item.position, item.lossyScale);

                batchRenderer.instanceBatchData.UploadMeshData();
            }
        }

        [MenuItem("GameObject/GPU Driven/Combine/Grass Batch", true)]
        static bool ValidateConvertGrass()
        {
            return Selection.activeGameObject != null && PrefabUtility.GetPrefabInstanceHandle(Selection.activeGameObject) == null;
        }

        [MenuItem("GameObject/GPU Driven/Combine/Grass Batch", priority = CoreUtils.gameObjectMenuPriority)]
        static void ConvertGrass(MenuCommand menuCommand)
        {
            Transform[] transforms = Selection.GetTransforms(SelectionMode.Deep | SelectionMode.ExcludePrefab | SelectionMode.Editable);

            Undo.RecordObjects(transforms, "Place Selection On Surface");

            Dictionary<Mesh, List<Transform>> meshClassify = new Dictionary<Mesh, List<Transform>>();

            foreach (Transform t in transforms)
            {
                if (t.TryGetComponent<MeshFilter>(out var filter))
                {
                    if (meshClassify.TryGetValue(filter.sharedMesh, out var value))
                        value.Add(t);
                    else
                    {
                        var list = new List<Transform>();
                        list.Add(t);
                        meshClassify.Add(filter.sharedMesh, list);
                    }
                }
            }

            foreach (var group in meshClassify)
            {
                var go = CoreEditorUtils.CreateGameObject(Selection.activeGameObject, "GPU Driven Grass");

                var meshFilter = go.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = group.Key;

                var batchRenderer = go.AddComponent<MeshBatchRenderer>();
                batchRenderer.instanceMaterial = new Material(Shader.Find("Universal Render Pipeline/Grass Lit"));

                if (group.Value[0].TryGetComponent<MeshRenderer>(out var renderer))
                {
                    var material = renderer.sharedMaterial;
                    if (material)
                    {
                        batchRenderer.instanceMaterial.color = material.color;
                        batchRenderer.instanceMaterial.mainTexture = material.mainTexture;
                        batchRenderer.instanceMaterial.mainTextureOffset = material.mainTextureOffset;
                        batchRenderer.instanceMaterial.mainTextureScale = material.mainTextureScale;
                        batchRenderer.instanceMaterial.CopyPropertiesFromMaterial(material);
                    }
                }

                batchRenderer.instanceMaterial.SetInt("_InstancingRendering", 1);
                batchRenderer.instanceMaterial.EnableKeyword("_INSTANCING_RENDERING_ON");

                foreach (var item in group.Value)
                    batchRenderer.instanceBatchData.Append(item.position, item.lossyScale);

                batchRenderer.instanceBatchData.UploadMeshData();
            }
        }
    }
}