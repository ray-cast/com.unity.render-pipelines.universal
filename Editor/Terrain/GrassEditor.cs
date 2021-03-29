using UnityEditor;

namespace UnityEngine.Rendering.Universal
{
    [CustomEditor(typeof(InstancedIndirectGrassRenderer))]
    public class GrassEditor : Editor
    {
        int layerMask = int.MaxValue;
        InstancedIndirectGrassRenderer _grassRender { get { return target as InstancedIndirectGrassRenderer; } }
        GrassGroup _grassGroup;
        Texture[] TexBrush;
        float _eraseRadius = 0.3f;
        float _brushRadius = 0.3f;
        int _randomCount = 1024;
        bool _isUsingCicleBrush = false;

        public override void OnInspectorGUI()
        {
            //base.OnInspectorGUI();
            GrassSetting();
        }
        void GrassSetting()
        {
            _grassGroup = _grassRender.grassGroup;

            EditorGUILayout.BeginVertical();
            GUILayout.Label(string.Format("草数量:{0}", _grassGroup.grasses.Count));
            GUILayout.Label(string.Format("草绘制数量:{0}", _grassRender.drawInstancedCount));

            /*bool isUpdateGrass = GUILayout.Button("手动刷新草");
            if (isUpdateGrass)
                _grassGroup.UpdateGrass();*/

            _isUsingCicleBrush = EditorGUILayout.Toggle("使用圆形画刷", _isUsingCicleBrush);
            if (_isUsingCicleBrush)
                _brushRadius = EditorGUILayout.Slider("圆形画刷半径", _brushRadius, 0.1f, 5f);
            _grassGroup.sensity = EditorGUILayout.Slider("密度", _grassGroup.sensity, 0.01f, 0.5f);
            _eraseRadius = EditorGUILayout.Slider("清除半径", _eraseRadius, 0.1f, 5f);
            _grassGroup.maxDrawDistance = EditorGUILayout.Slider("最大可视距离", _grassGroup.maxDrawDistance, 1.0f, 150f);
            //_grassGroup.baseColor = EditorGUILayout.ColorField("BaseColor", _grassGroup.baseColor);
            //_grassGroup.groundColor = EditorGUILayout.ColorField("GroundColor", _grassGroup.groundColor);
            _grassGroup.windDirection = EditorGUILayout.Vector3Field("风的朝向", _grassGroup.windDirection);
            _grassGroup.windIntensity = EditorGUILayout.FloatField("风的强度", _grassGroup.windIntensity);
            _grassGroup.windFrequency = EditorGUILayout.FloatField("风的频率", _grassGroup.windFrequency);
            _grassGroup.windTiling = EditorGUILayout.Vector2Field("风的持续", _grassGroup.windTiling);
            _grassGroup.windWrap = EditorGUILayout.Vector2Field("风带来的弯曲", _grassGroup.windWrap);
            _grassGroup.windHightlightSpeed = EditorGUILayout.FloatField("风场高光速率", _grassGroup.windHightlightSpeed);
            _grassGroup.windScatter = EditorGUILayout.Vector2Field("风场的纹理缩放", _grassGroup.windScatter);
            _grassGroup.windNoise = (Texture)EditorGUILayout.ObjectField("风场遮罩图 （示例：gradient_beam_007）", _grassGroup.windNoise, typeof(Texture), true);
            _grassGroup.bendStrength = EditorGUILayout.Slider("挤压带来的弯曲", _grassGroup.bendStrength, 0, 1);
            _grassGroup.cachedGrassMesh = (Mesh)EditorGUILayout.ObjectField("草的网格", _grassGroup.cachedGrassMesh, typeof(Mesh), true);

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
            bool isAddColor = GUILayout.Button("添加颜色");
            if (isAddColor)
            {
                if (!_grassGroup.AddColor())
                    Debug.LogError("已达到最大添加上限 : " + GrassGroup.maxColorLimits);
            }

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

            bool isAddScale = GUILayout.Button("添加缩放");
            if (isAddScale)
            {
                if (!_grassGroup.AddScale())
                    Debug.LogError("已达到最大添加上限 : " + GrassGroup.maxScaleLimits);
            }

            EditorGUILayout.HelpBox("调试", MessageType.None);
            bool clearAllGrass = GUILayout.Button("清除所有草");
            if (clearAllGrass)
                _grassGroup.ClearAllGrass();
            EditorGUILayout.BeginHorizontal();
            bool randomGrass = GUILayout.Button("随机草");
            _randomCount = EditorGUILayout.IntField("随机数量", _randomCount);
            if (randomGrass)
                _grassGroup.RandomGroupBySensity(_grassRender.transform, _randomCount);
            EditorGUILayout.EndHorizontal();

            _grassGroup.isCpuCulling = EditorGUILayout.Toggle("IsCpuCulling", _grassGroup.isCpuCulling);
            _grassGroup.isGpuCulling = EditorGUILayout.Toggle("IsGpuCulling", _grassGroup.isGpuCulling);
            _grassGroup.instanceMaterial = (Material)EditorGUILayout.ObjectField("InstanceMaterial", _grassGroup.instanceMaterial, typeof(Material), true);

            EditorGUILayout.EndVertical();
            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }
        }

        void OnSceneGUI()
        {
            if (EditorApplication.isPlaying)
                return;
            Painter();
            if (UnityEditorInternal.InternalEditorUtility.isApplicationActive)//unity激活下才repaint
                SceneView.RepaintAll();
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

            RaycastHit raycastHit = new RaycastHit();
            bool isHit = Physics.Raycast(ray, out raycastHit, Mathf.Infinity, layerMask);
            if (isHit)
            {
                if (!e.shift)
                {
                    if (_isUsingCicleBrush)//圆形画刷
                    {
                        Handles.color = Color.green;
                        Handles.DrawWireDisc(raycastHit.point, Vector3.up, _brushRadius);
                    }
                    else
                    {
                        Handles.color = Color.green;
                        Handles.DrawLine(raycastHit.point + new Vector3(0, 0.5f, 0), raycastHit.point);
                    }
                }
                else//按住shift表示删草
                {
                    Handles.color = Color.red;
                    Handles.DrawWireDisc(raycastHit.point, raycastHit.normal, _eraseRadius);
                }
                //Debug.LogFormat("type={0} button={1} isMouse={2}", e.type, e.button, e.isMouse); 
                if (e.isMouse && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                {
                    Debug.LogFormat("hit mesh orgin {0} dir {1} hit {2}", ray.origin, ray.direction, raycastHit.point);
                    if (!e.shift)//加草
                    {
                        if (_isUsingCicleBrush)
                        {
                            int needPaintCount = Mathf.RoundToInt(_brushRadius * _brushRadius / (_grassGroup.sensity * _grassGroup.sensity));
                            for (int i = 0; i < needPaintCount; i++)
                            {
                                float len = Random.Range(0, _brushRadius);
                                float angle = Random.Range(0, 360);
                                float xPos = raycastHit.point.x + Mathf.Cos(angle) * len;
                                float zPos = raycastHit.point.z + Mathf.Sin(angle) * len;
                                Vector3 orgin = new Vector3(xPos, raycastHit.point.y + 0.5f, zPos);
                                RaycastHit hit;
                                bool isHitGround = Physics.Raycast(orgin, Vector3.down, out hit, Mathf.Infinity, layerMask);
                                if (isHitGround)
                                    _grassGroup.AddGrass(new Vector3(xPos, hit.point.y, zPos));
                            }
                            EditorUtility.SetDirty(target);
                        }
                        else
                        {
                            _grassGroup.AddGrass(raycastHit.point);
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