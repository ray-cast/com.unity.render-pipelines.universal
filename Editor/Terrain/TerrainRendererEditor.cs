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
        }

        SavedBool _materialSettingsFoldout;
        SavedBool _renderingSettingsFoldout;

        MaterialEditor _materialEditor;

        TerrainRenderer _renderer { get { return target as TerrainRenderer; } }

        public void OnEnable()
        {
            _materialSettingsFoldout = new SavedBool($"{target.GetType()}.MaterialSettingsFoldout", true);
            _renderingSettingsFoldout = new SavedBool($"{target.GetType()}.CullingSettingsFoldout", true);
        }

		public void OnDisable()
		{
            if (_materialEditor != null)
                DestroyImmediate(_materialEditor);
        }

		public override void OnInspectorGUI()
        {
            if (_renderer.instanceMaterial != null)
                _materialEditor = CreateEditor(_renderer.instanceMaterial) as MaterialEditor;

            EditorGUILayout.BeginVertical();

            this.DrawMaterialSettings();
            this.DrawRenderingSettings();

            EditorGUILayout.EndVertical();

            this.DrawMaterialEditor();

            if (GUI.changed)
            {
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
                _renderer.quality = Mathf.Max(1, EditorGUILayout.IntField(string.Format("细分质量:{0}", _renderer.quality), _renderer.quality));
                _renderer.shouldOcclusionCulling = EditorGUILayout.Toggle("启用遮挡剔除（GPU Driven）", _renderer.shouldOcclusionCulling);
                _renderer.shouldVirtualTexture = EditorGUILayout.Toggle("启用虚拟纹理（Virtual Texture）", _renderer.shouldVirtualTexture);

                if (GUILayout.Button("刷新地形"))
                    _renderer.UploadTerrainData();

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
        private static Texture2D _defaultCheckerGray;

        public static Texture2D defaultCheckerGray
        {
            get
            {
                if (!_defaultCheckerGray)
                    _defaultCheckerGray = FindBuiltinExtraResources<Texture2D>("Default-Checker-Gray");

                return _defaultCheckerGray;
            }
        }

        public static T FindBuiltinExtraResources<T>(string assetName) where T : UnityEngine.Object
        {
            foreach (T asset in AssetDatabase.LoadAllAssetsAtPath("Resources/unity_builtin_extra").Where(o => o is T))
            {
                if (assetName == asset.name)
                    return asset;
            }

            return default(T);
        }

        static void CreateTexture2DArray(Texture2D[] source, TextureFormat format = TextureFormat.RGBA32, int mipCount = 0, bool linear = false)
        {
            Debug.Assert(source.Length > 0);

            Texture2DArray texture2DArray = new Texture2DArray(source[0].width, source[0].height, source.Length, format, mipCount, linear);
            texture2DArray.filterMode = FilterMode.Bilinear;
            texture2DArray.wrapMode = TextureWrapMode.Repeat;

            for (int i = 0; i < source.Length; i++)
            {
                for (int m = 0; m < source[i].mipmapCount; m++)
                {
                    Graphics.CopyTexture(source[i], 0, m, texture2DArray, i, m);
                }
            }

            texture2DArray.Apply(false);

            string path = EditorUtility.SaveFilePanel("Save Texture", "TextureArray", "asset", "Please enter a file name to save the texture to");
            if (path.Length != 0)
            {
                Undo.RecordObject(texture2DArray, "texture");
                AssetDatabase.CreateAsset(texture2DArray, path);
                AssetDatabase.Refresh();
            }
        }

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

                    if (terrainData.alphamapTextureCount > 0)
                        terrainRenderer.instanceMaterial.SetTexture("_Control", terrainData.alphamapTextures[0]);
                    else
                        terrainRenderer.instanceMaterial.SetTexture("_Control", Texture2D.redTexture);

                    if (terrainData.terrainLayers.Length > 0)
					{
                        terrainRenderer.instanceMaterial.SetInt("_UseNormal", 1);
                        terrainRenderer.instanceMaterial.EnableKeyword("_NORMALMAP");
                    }
                    else
					{
                        terrainRenderer.instanceMaterial.SetInt("_UseNormal", 0);
                        terrainRenderer.instanceMaterial.DisableKeyword("_NORMALMAP");
                    }

                    var size = terrainData.size;
                    var extents = terrainData.bounds.extents;

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

                    for (var i = terrainData.terrainLayers.Length; i < 4; i++)
					{
                        terrainRenderer.instanceMaterial.SetTexture("_Splat" + i, defaultCheckerGray);
                        terrainRenderer.instanceMaterial.SetTextureOffset("_Splat" + i, Vector2.zero);
                        terrainRenderer.instanceMaterial.SetTextureScale("_Splat" + i, new Vector2(extents.x, extents.z));
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