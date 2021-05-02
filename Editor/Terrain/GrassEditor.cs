using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    [CustomEditor(typeof(InstancedIndirectGrassRenderer))]
    public class GrassEditor : Editor
    {
        public class Styles
        {
            public static GUIContent drawSettingsText = EditorGUIUtility.TrTextContent("画刷设置");
            public static GUIContent windSettingsText = EditorGUIUtility.TrTextContent("风场设置");
            public static GUIContent grassSettingsText = EditorGUIUtility.TrTextContent("草地设置");
            public static GUIContent cullingSettingsText = EditorGUIUtility.TrTextContent("剔除设置");
        }

        SavedBool _drawSettingsFoldout;
        SavedBool _grassSettingsFoldout;
        SavedBool _windSettingsFoldout;
        SavedBool _cullingSettingsFoldout;

        int layerMask = int.MaxValue;
        GrassGroup _grassGroup;
        float _eraseRadius = 0.3f;
        float _brushRadius = 0.3f;
        int _randomCount = 1024;
        bool _isEnableBrush = false;
        bool _isUsingCicleBrush = false;

        InstancedIndirectGrassRenderer _grassRender { get { return target as InstancedIndirectGrassRenderer; } }

        public void OnEnable()
		{
            _drawSettingsFoldout = new SavedBool($"{target.GetType()}.DrawSettingsFoldout", false);
            _grassSettingsFoldout = new SavedBool($"{target.GetType()}.GrassSettingsFoldout", false);
            _windSettingsFoldout = new SavedBool($"{target.GetType()}.WindSettingsFoldout", false);
            _cullingSettingsFoldout = new SavedBool($"{target.GetType()}.CullingSettingsFoldout", false);
        }

		public override void OnInspectorGUI()
        {
            _grassGroup = _grassRender.grassGroup;

            EditorGUILayout.BeginVertical();
            GUILayout.Label(string.Format("草数量:{0}", _grassGroup.grasses.Count));
            GUILayout.Label(string.Format("草绘制数量:{0}", _grassRender.drawInstancedCount));

            EditorGUILayout.Space();

            this.DrawPaintSettings();
            this.DrawGrassSettings();
            this.DrawWindSettings();
            this.DrawCullingSettings();

            EditorGUILayout.EndVertical();
            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }
        }

        void DrawPaintSettings()
		{
            _drawSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(_drawSettingsFoldout.value, Styles.drawSettingsText);
            if (_drawSettingsFoldout.value)
            {
                _isEnableBrush = EditorGUILayout.Toggle("启用绘制", _isEnableBrush);
                _isUsingCicleBrush = EditorGUILayout.Toggle("使用圆形画刷", _isUsingCicleBrush);
                if (_isUsingCicleBrush)
                    _brushRadius = EditorGUILayout.Slider("圆形画刷半径", _brushRadius, 0.1f, 5f);
                _grassGroup.brushSensity = EditorGUILayout.Slider("密度", _grassGroup.brushSensity, 0.01f, 0.5f);
                _eraseRadius = EditorGUILayout.Slider("清除半径", _eraseRadius, 0.1f, 5f);

                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("草颜色列表", MessageType.None);

                for (int i = 0; i < _grassGroup.allColors.Count; i++)
                {
                    GrassColor gc = _grassGroup.allColors[i];
                    EditorGUILayout.BeginHorizontal();
                    bool isUsing = EditorGUILayout.Toggle(gc.isUsing, new[] { GUILayout.Width(30) });
                    if (isUsing)
                        _grassGroup.usingColorIndex = (uint)i;
                    GUILayout.FlexibleSpace();
                    Color c = EditorGUILayout.ColorField(gc.dryColor, new[] { GUILayout.Width(100) });
                    _grassGroup.SetDryColor(c, i);
                    GUILayout.FlexibleSpace();
                    c = EditorGUILayout.ColorField(gc.healthyColor, new[] { GUILayout.Width(100) });
                    _grassGroup.SetHealthyColor(c, i);
                    GUILayout.FlexibleSpace();
                    bool isRemoveColor = GUILayout.Button("删除", new[] { GUILayout.Width(50) });
                    if (isRemoveColor)
                        _grassGroup.RemoveColor(i);
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("添加颜色", EditorStyles.miniButton))
                {
                    if (!_grassGroup.AddColor())
                        Debug.LogError("已达到最大添加上限 : " + GrassGroup.maxColorLimits);
                }

                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("草缩放列表", MessageType.None);

                for (int i = 0; i < _grassGroup.allScales.Count; i++)
                {
                    Vector3 scale = _grassGroup.allScales[i];
                    EditorGUILayout.BeginHorizontal();
                    bool isUsing = EditorGUILayout.Toggle(i == _grassGroup.usingScaleIndex, new[] { GUILayout.Width(30) });
                    if (isUsing)
                        _grassGroup.usingScaleIndex = (uint)i;
                    GUILayout.FlexibleSpace();
                    _grassGroup.SetScale(EditorGUILayout.Vector3Field("", scale), i);
                    GUILayout.FlexibleSpace();
                    bool isRemoveScale = GUILayout.Button("删除", new[] { GUILayout.Width(50) });
                    if (isRemoveScale)
                        _grassGroup.RemoveScale(i);
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("添加缩放", EditorStyles.miniButton))
                {
                    if (!_grassGroup.AddScale())
                        Debug.LogError("已达到最大添加上限 : " + GrassGroup.maxScaleLimits);
                }

                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                _randomCount = EditorGUILayout.IntField("随机数量", _randomCount);
                if (GUILayout.Button("随机草", EditorStyles.miniButton))
                    _grassGroup.RandomGroupBySensity(_grassRender.transform, _randomCount);
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("清除所有草", EditorStyles.miniButton))
                    _grassGroup.ClearAllGrass();

                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawGrassSettings()
        {
            _grassSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(_grassSettingsFoldout.value, Styles.grassSettingsText);
            if (_grassSettingsFoldout.value)
            {
                _grassGroup.cachedGrassMesh = (Mesh)EditorGUILayout.ObjectField("草的网格", _grassGroup.cachedGrassMesh, typeof(Mesh), true);
                _grassGroup.instanceMaterial = (Material)EditorGUILayout.ObjectField("草的材质", _grassGroup.instanceMaterial, typeof(Material), true);
                _grassGroup.bendStrength = EditorGUILayout.Slider("挤压带来的弯曲", _grassGroup.bendStrength, 0, 1);

                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawWindSettings()
		{
            _windSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(_windSettingsFoldout.value, Styles.windSettingsText);
            if (_windSettingsFoldout.value)
            {
                _grassGroup.windDirection = EditorGUILayout.Vector3Field("风的朝向", _grassGroup.windDirection);
                _grassGroup.windIntensity = EditorGUILayout.FloatField("风的强度", _grassGroup.windIntensity);
                _grassGroup.windRange = EditorGUILayout.FloatField("风的运动范围", _grassGroup.windRange);
                _grassGroup.windFrequency = EditorGUILayout.FloatField("风的频率", _grassGroup.windFrequency);
                _grassGroup.windTiling = EditorGUILayout.Vector2Field("风的持续", _grassGroup.windTiling);
                _grassGroup.windWrap = EditorGUILayout.Vector2Field("风带来的弯曲", _grassGroup.windWrap);
                _grassGroup.windHightlightSpeed = EditorGUILayout.FloatField("风场高光速率", _grassGroup.windHightlightSpeed);
                _grassGroup.windScatter = EditorGUILayout.Vector2Field("风场的纹理缩放", _grassGroup.windScatter);
                _grassGroup.windNoise = (Texture)EditorGUILayout.ObjectField("风场遮罩图 （示例：gradient_beam_007）", _grassGroup.windNoise, typeof(Texture), true);

                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawCullingSettings()
        {
            _cullingSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(_cullingSettingsFoldout.value, Styles.cullingSettingsText);
            if (_cullingSettingsFoldout.value)
            {
                _grassGroup.sensity = EditorGUILayout.Slider("全局密度", _grassGroup.sensity, 0.0f, 1.0f);
                _grassGroup.maxDrawDistance = EditorGUILayout.Slider("最大可视距离", _grassGroup.maxDrawDistance, 1.0f, 150f);
                _grassGroup.distanceCulling = EditorGUILayout.Slider("可视距离剔除权重", _grassGroup.distanceCulling, 0f, 1f);
                _grassGroup.isCpuCulling = EditorGUILayout.Toggle("启用区域剔除（CPU）", _grassGroup.isCpuCulling);
                _grassGroup.isGpuCulling = EditorGUILayout.Toggle("启用遮挡剔除（GPU Driver）", _grassGroup.isGpuCulling);
                _grassRender.debugMode = EditorGUILayout.Toggle("启用调试", _grassRender.debugMode);

                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
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

            HandleUtility.AddDefaultControl(0);

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.KeypadPlus)
                FutureTerrainWindow.brushSize += 1;
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.KeypadMinus)
                FutureTerrainWindow.brushSize -= 1;

            bool isHit = Physics.Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition), out var raycastHit, Mathf.Infinity, layerMask);
            if (isHit)
            {
                if (!e.shift)
                {
                    if (_isUsingCicleBrush)
                    {
                        // 圆形画刷
                        Handles.color = Color.green;
                        Handles.DrawWireDisc(raycastHit.point, Vector3.up, _brushRadius);
                    }
                    else
                    {
                        Handles.color = Color.green;
                        Handles.DrawLine(raycastHit.point + new Vector3(0, 0.5f, 0), raycastHit.point);
                    }
                }
                else
                {
                    // 按住shift表示删草
                    Handles.color = Color.red;
                    Handles.DrawWireDisc(raycastHit.point, raycastHit.normal, _eraseRadius);
                }

                if (e.isMouse && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                {
                    if (!e.shift)//加草
                    {
                        if (_isUsingCicleBrush)
                        {
                            bool shouledUpdateData = false;
                            int needPaintCount = Mathf.RoundToInt(_brushRadius * _brushRadius / (_grassGroup.brushSensity * _grassGroup.brushSensity));

                            for (int i = 0; i < needPaintCount; i++)
                            {
                                float len = Random.Range(0, _brushRadius);
                                float angle = Random.Range(0, 360);
                                float xPos = raycastHit.point.x + Mathf.Cos(angle) * len;
                                float zPos = raycastHit.point.z + Mathf.Sin(angle) * len;
                                Vector3 orgin = new Vector3(xPos, raycastHit.point.y + 0.5f, zPos);

                                bool isHitGround = Physics.Raycast(orgin, Vector3.down, out var hit, Mathf.Infinity, layerMask);
                                if (isHitGround)
                                    shouledUpdateData |= _grassGroup.AddGrass(new Vector3(xPos, hit.point.y, zPos));
                            }

                            if (shouledUpdateData)
                                _grassGroup.UpdateGrass();

                            EditorUtility.SetDirty(target);
                        }
                        else
                        {
                            if (_grassGroup.AddGrass(raycastHit.point))
                                _grassGroup.UpdateGrass();

                            EditorUtility.SetDirty(target);
                        }
                    }
                    else//删草
                    {
                        _grassGroup.RemoveGrass(raycastHit.point, _eraseRadius);
                        EditorUtility.SetDirty(target);
                    }
                }
            }
        }
    }
}