using System.Collections.Generic;
using System.Linq;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    [CustomEditor(typeof(TerrainRenderer))]
    public class TerrainRendererEditor : Editor
    {
        public class Styles
        {
            public static GUIContent drawSettingsText = EditorGUIUtility.TrTextContent("画刷设置");
            public static GUIContent materialSettingsText = EditorGUIUtility.TrTextContent("材质设置");
            public static GUIContent renderingSettingsText = EditorGUIUtility.TrTextContent("渲染设置");
            public static GUIContent debugSettingsText = EditorGUIUtility.TrTextContent("调试");
        }

        SavedBool _materialSettingsFoldout;
        SavedBool _renderingSettingsFoldout;
        SavedBool _debugViewFoldout;

        TerrainRenderer _renderer { get { return target as TerrainRenderer; } }

        private MaterialEditor _materialEditor;

        public void OnEnable()
        {
            VirtualTextureSystem.beginTileRendering += beginTileRendering;

            _materialSettingsFoldout = new SavedBool($"{target.GetType()}.MaterialSettingsFoldout", true);
            _renderingSettingsFoldout = new SavedBool($"{target.GetType()}.CullingSettingsFoldout", true);
            _debugViewFoldout = new SavedBool($"{target.GetType()}.DebugSettingsFoldout", true);
        }

		public void OnDisable()
		{
            VirtualTextureSystem.beginTileRendering -= beginTileRendering;

            if (_materialEditor != null)
                DestroyImmediate(_materialEditor);
        }

        public void beginTileRendering(RequestPageData request, TiledTexture tileTexture, Vector2Int tile)
		{
            Repaint();
		}

		public override void OnInspectorGUI()
        {
            if (_renderer.instanceMaterial != null)
                _materialEditor = CreateEditor(_renderer.instanceMaterial) as MaterialEditor;

            EditorGUILayout.BeginVertical();

            this.DrawMaterialSettings();
            this.DrawRenderingSettings();
            this.DrawDebugFoldout();

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
                _renderer.terrainData = EditorGUILayout.ObjectField("地形", _renderer.terrainData, typeof(TerrainData), true) as TerrainData;

                EditorGUI.BeginChangeCheck();

                var instanceMaterial = EditorGUILayout.ObjectField("材质", _renderer.instanceMaterial, typeof(Material), true) as Material;

                if (_renderer.instanceMaterial && !AssetDatabase.IsMainAsset(_renderer.instanceMaterial))
				{
                    if (GUILayout.Button("保存材质"))
                    {
                        string path = EditorUtility.SaveFilePanelInProject("Save material", instanceMaterial.name.Substring(instanceMaterial.name.LastIndexOf("/") + 1), "mat", "Please enter a file name to save the material to");
                        if (path.Length != 0)
                        {
                            Undo.RecordObject(_renderer.instanceMaterial, "material");
                            AssetDatabase.CreateAsset(_renderer.instanceMaterial, path);
                            AssetDatabase.Refresh();
                        }
                    }
                }

                if (EditorGUI.EndChangeCheck())
                {
                    if (_renderer.instanceMaterial && instanceMaterial && _renderer.instanceMaterial != instanceMaterial)
                        instanceMaterial.CopyPropertiesFromMaterial(_renderer.instanceMaterial);

                    _renderer.instanceMaterial = instanceMaterial;

                    if (_renderer.instanceMaterial)
					{
                        _renderer.instanceMaterial.SetInt("_InstancingRendering", 1);
                        _renderer.instanceMaterial.EnableKeyword("_INSTANCING_RENDERING_ON");
                    }

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
                GUILayout.Label(string.Format("实例数量:{0}", _renderer.instanceCount));

                _renderer.debugMode = EditorGUILayout.Toggle(string.Format("实例绘制数量:{0}", _renderer.drawInstancedCount), _renderer.debugMode);
                _renderer.shouldOcclusionCulling = EditorGUILayout.Toggle("启用遮挡剔除（GPU Driven）", _renderer.shouldOcclusionCulling);
                _renderer.shouldVirtualTexture = EditorGUILayout.Toggle("启用虚拟纹理（Virtual Texture）", _renderer.shouldVirtualTexture);

                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawDebugFoldout()
        {
            _debugViewFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(_debugViewFoldout.value, Styles.debugSettingsText);
            if (_debugViewFoldout.value)
            {
                if (GUILayout.Button("重建虚拟纹理"))
				{
                    VirtualTextureSystem.instance.Reset();
                }

                var lookupTexture = VirtualTextureSystem.instance.lookupTexture;
                if (lookupTexture != null)
                {
                    EditorGUILayout.LabelField("虚拟纹理查找表:");
                    EditorGUI.DrawPreviewTexture(GUILayoutUtility.GetAspectRect((float)lookupTexture.width / lookupTexture.height), lookupTexture);
                }

                EditorGUILayout.Space();

                var virtualTexture = VirtualTextureSystem.instance.tileTexture;
                if (virtualTexture != null && virtualTexture.tileTextures.Length >= 2)
                {
                    var albedoTexture = virtualTexture.tileTextures[0];
                    var normalTexture = virtualTexture.tileTextures[1];

                    EditorGUILayout.LabelField("虚拟纹理:");

                    EditorGUI.DrawPreviewTexture(GUILayoutUtility.GetAspectRect((float)albedoTexture.width / albedoTexture.height), albedoTexture);
                    EditorGUI.DrawPreviewTexture(GUILayoutUtility.GetAspectRect((float)normalTexture.width / normalTexture.height), normalTexture);
                }

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

                bool isDefaultMaterial = AssetDatabase.GetAssetPath(_renderer.instanceMaterial).StartsWith("Packages/com.unity.render-pipelines.universal");

                using (new EditorGUI.DisabledGroupScope(isDefaultMaterial))
                    _materialEditor.OnInspectorGUI();
            }
        }
    }

    static class TerrainMenuItems
    {
        [MenuItem("GameObject/GPU Driven/Terrain", priority = CoreUtils.gameObjectMenuPriority)]
        static void CreateTerrain(MenuCommand menuCommand)
        {
            var go = CoreEditorUtils.CreateGameObject("GPU Driven Terrain", menuCommand.context);
            var terrainRenderer = go.AddComponent<TerrainRenderer>();

            string resourcePath = AssetDatabase.GUIDToAssetPath(UniversalRenderPipelineAsset.editorResourcesGUID);
            var objs = InternalEditorUtility.LoadSerializedFileAndForget(resourcePath);
            var editorResourcesAsset = objs != null && objs.Length > 0 ? objs.First() as UniversalRenderPipelineEditorResources : null;

            if (editorResourcesAsset)
                terrainRenderer.instanceMaterial = Material.Instantiate(editorResourcesAsset.materials.terrainBatchLit);
        }

        [MenuItem("GameObject/GPU Driven/Combine/Terrain Batch", true)]
        static bool ValidateConvertTerrain()
        {
            return Selection.activeGameObject != null && PrefabUtility.GetPrefabInstanceHandle(Selection.activeGameObject) == null;
        }

        [MenuItem("GameObject/GPU Driven/Combine/Terrain Batch", priority = CoreUtils.gameObjectMenuPriority)]
        static void ConvertTerrain(MenuCommand menuCommand)
        {
            if (Selection.activeGameObject.TryGetComponent<Terrain>(out var terrain))
            {
                var go = CoreEditorUtils.CreateGameObject(Selection.activeGameObject, "GPU Driven Terrain");
                go.transform.position = Selection.activeGameObject.transform.position;
                go.transform.rotation = Selection.activeGameObject.transform.rotation;
                go.transform.localScale = Selection.activeGameObject.transform.localScale;

                var terrainData = terrain.terrainData;
                var collide = go.AddComponent<TerrainCollider>();
                collide.terrainData = terrainData;

                var terrainRenderer = go.AddComponent<TerrainRenderer>();
                terrainRenderer.terrainData = terrainData;

                string resourcePath = AssetDatabase.GUIDToAssetPath(UniversalRenderPipelineAsset.editorResourcesGUID);
                var objs = InternalEditorUtility.LoadSerializedFileAndForget(resourcePath);
                var editorResourcesAsset = objs != null && objs.Length > 0 ? objs.First() as UniversalRenderPipelineEditorResources : null;

                if (editorResourcesAsset)
				{
                    terrainRenderer.instanceMaterial = Material.Instantiate(editorResourcesAsset.materials.terrainBatchLit);
                    terrainRenderer.instanceMaterial.SetTexture("_Control", terrainData.GetAlphamapTexture(0));

                    var size = terrainData.size;

                    for (var i = 0; i < terrainData.terrainLayers.Length && i < 4; i++)
					{
                        var layer = terrainData.terrainLayers[i];

                        terrainRenderer.instanceMaterial.SetTexture("_Splat" + i, layer.diffuseTexture);
                        terrainRenderer.instanceMaterial.SetTextureOffset("_Splat" + i, layer.tileOffset);
                        terrainRenderer.instanceMaterial.SetTextureScale("_Splat" + i, new Vector2(size.x / layer.tileSize.x, size.z / layer.tileSize.y));                        
                        terrainRenderer.instanceMaterial.SetTexture("_Normal" + i, layer.normalMapTexture);
                        terrainRenderer.instanceMaterial.SetFloat("_BumpScale" + i, layer.normalScale);
                        terrainRenderer.instanceMaterial.SetFloat("_Metallic" + i, layer.metallic);
                        terrainRenderer.instanceMaterial.SetFloat("_Smoothness" + i, layer.smoothness);
                    }
                }
            }
            else
            {
                Debug.Log("选中的物件不是地形");
            }
        }
    }
}