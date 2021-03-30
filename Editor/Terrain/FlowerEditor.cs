using UnityEditor;

namespace UnityEngine.Rendering.Universal
{
    [CustomEditor(typeof(InstancedIndirectFlowerRenderer))]
    public class FlowerEditor : Editor
    {
        int layerMask = int.MaxValue;
        float _eraseRadius = 0.3f;
        InstancedIndirectFlowerRenderer _flowerRender { get { return target as InstancedIndirectFlowerRenderer; } }
        FlowerGroup _flowerGroup;
        int randomCount = 1024;

        public override void OnInspectorGUI()
        {
            //base.OnInspectorGUI();

            FlowerSetting();
        }
        void FlowerSetting()
        {
            _flowerGroup = _flowerRender.flowerGroup;
            EditorGUILayout.BeginVertical();
            EditorGUILayout.HelpBox("花设置", MessageType.None);
            GUILayout.Label(string.Format("花数量:{0}", _flowerGroup.floweres.Count));
            GUILayout.Label(string.Format("花绘制数量:{0}", _flowerRender.drawInstancedCount));

            _flowerGroup.sensity = EditorGUILayout.IntSlider("密度(厘米)", _flowerGroup.sensity, 1, 50);
            _eraseRadius = EditorGUILayout.Slider("清除半径(厘米)", _eraseRadius, 0.1f, 5f);
            _flowerGroup.grassWidth = EditorGUILayout.FloatField("花宽度缩放", _flowerGroup.grassWidth);
            _flowerGroup.grassHeight = EditorGUILayout.FloatField("花高度缩放", _flowerGroup.grassHeight);
            _flowerGroup.windDirection = EditorGUILayout.Vector3Field("风的朝向", _flowerGroup.windDirection);
            _flowerGroup.windIntensity = EditorGUILayout.FloatField("风的强度", _flowerGroup.windIntensity);
            _flowerGroup.windFrequency = EditorGUILayout.FloatField("风的频率", _flowerGroup.windFrequency);
            _flowerGroup.windTiling = EditorGUILayout.Vector2Field("风的持续", _flowerGroup.windTiling);
            _flowerGroup.windWrap = EditorGUILayout.Vector2Field("风带来的弯曲", _flowerGroup.windWrap);
            _flowerGroup.windHightlightSpeed = EditorGUILayout.FloatField("风场高光速率", _flowerGroup.windHightlightSpeed);
            _flowerGroup.windScatter = EditorGUILayout.Vector2Field("风场的纹理缩放", _flowerGroup.windScatter);
            _flowerGroup.windNoise = (Texture)EditorGUILayout.ObjectField("风场遮罩图 （示例：gradient_beam_007）", _flowerGroup.windNoise, typeof(Texture), true);
            _flowerGroup.bendStrength = EditorGUILayout.Slider("弯曲程度", _flowerGroup.bendStrength, 0, 1);
            EditorGUILayout.BeginHorizontal();
            _flowerGroup.texture = (Texture)EditorGUILayout.ObjectField("花的纹理", _flowerGroup.texture, typeof(Texture), true);
            _flowerGroup.color = EditorGUILayout.ColorField(_flowerGroup.color, new[] { GUILayout.Width(80) });
            EditorGUILayout.EndHorizontal();
            _flowerGroup.cutoff = EditorGUILayout.Slider("Alpha Clipping", _flowerGroup.cutoff, 0, 1);
            _flowerGroup.cachedGrassMesh = (Mesh)EditorGUILayout.ObjectField("花的网格", _flowerGroup.cachedGrassMesh, typeof(Mesh), true);

            EditorGUILayout.HelpBox("调试", MessageType.None);
            bool clearAllGrass = GUILayout.Button("清除所有花");
            if (clearAllGrass)
                _flowerGroup.ClearAllGrass();
            EditorGUILayout.BeginHorizontal();
            bool randomFlower = GUILayout.Button("随机花");
            randomCount = EditorGUILayout.IntField("随机数量", randomCount);
            if (randomFlower)
                _flowerGroup.RandomGroupBySensity(_flowerRender.transform, randomCount);
            EditorGUILayout.EndHorizontal();

            _flowerGroup.isCpuCulling = EditorGUILayout.Toggle("IsCpuCulling", _flowerGroup.isCpuCulling);
            _flowerGroup.isGpuCulling = EditorGUILayout.Toggle("IsGpuCulling", _flowerGroup.isGpuCulling);
            _flowerGroup.instanceMaterial = EditorGUILayout.ObjectField("材质", _flowerGroup.instanceMaterial, typeof(Material), true) as Material;

            EditorGUILayout.EndVertical();
            if (GUI.changed)
            {
                Debug.LogFormat("GUI.changed={0}", GUI.changed);
                EditorUtility.SetDirty(target);
            }
        }
        #region OnSceneGUI
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
            //if (e.shift)
            {
                //Handles.color = Color.red;
                //Handles.DrawSolidDisc(ray.origin, Vector3.up, _eraseRadius);
                //areaOfEffect = Handles.ScaleValueHandle(areaOfEffect,
                //_terrain.transform.position + new Vector3(areaOfEffect, 0, 0),
                //Quaternion.identity,
                //2,
                //Handles.CylinderCap,
                //2);
            }
            RaycastHit raycastHit = new RaycastHit();
            bool isHit = Physics.Raycast(ray, out raycastHit, Mathf.Infinity, layerMask);
            if (isHit)
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
                //Debug.LogFormat("type={0} button={1} isMouse={2}", e.type, e.button, e.isMouse); 
                if (e.isMouse && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                {
                    Debug.LogFormat("hit mesh orgin {0} dir {1} hit {2}", ray.origin, ray.direction, raycastHit.point);
                    if (!e.shift)//加草
                    {
                        _flowerGroup.AddFlower(raycastHit.point);
                        EditorUtility.SetDirty(target);
                    }
                    else//删草
                    {
                        _flowerGroup.RemoveFlower(raycastHit.point, _eraseRadius);
                        EditorUtility.SetDirty(target);
                    }
                }
                else
                {
                    //Debug.LogFormat("not hit mesh orgin {0} dir {1}", ray.origin, ray.direction);
                }
            }
        }
        #endregion
    }
}