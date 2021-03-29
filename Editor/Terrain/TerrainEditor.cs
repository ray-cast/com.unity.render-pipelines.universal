using UnityEditor;
using System.Collections;

namespace UnityEngine.Rendering.Universal
{
    [CustomEditor(typeof(Terrain))]
    public class TerrainEditor : Editor
    {
        int State;
        int layerMask = int.MaxValue;
        static public int T4MMenuToolbar = 0;
        GUIContent[] MenuIcon = new GUIContent[2];
        int menuToolbarSelected;
        float _eraseRadius = 0.3f;
        Projector T4MPreview;
        Terrain _terrain { get { return target as Terrain; } }

        void InitPreview()
        {
            //var ProjectorB = new GameObject("PreviewT4M");
            //ProjectorB.AddComponent(typeof(Projector));
            //ProjectorB.hideFlags = HideFlags.HideInHierarchy;
            //T4MPreview = ProjectorB.GetComponent(typeof(Projector)) as Projector;
            //T4MPreview.nearClipPlane = -20;
            //T4MPreview.farClipPlane = 20;
            //T4MPreview.orthographic = true;
            //T4MPreview.orthographicSize = brushSize;
            //T4MPreview.ignoreLayers = ~layerMask;
            //T4MPreview.transform.Rotate(90, -90, 0);
            //Material NewPMat = new Material("Shader \"Hidden/PreviewT4M\" { \n	Properties {\n _Transp (\"Transparency\", Range(0,1)) = 1 \n  _MainTex (\"Texture\", 2D) = \"\" { }\n	_MaskTex (\"Mask (RGB) Trans (A)\", 2D) = \"\" { TexGen ObjectLinear }\n	}\nSubShader {\n Pass {\nBlend SrcAlpha OneMinusSrcAlpha  \n SetTexture [_MainTex]  \n SetTexture [_MaskTex] {\n constantColor (1,1,1,[_Transp]) \n	combine previous , texture* constant\n	Matrix [_Projector]\n	}\n}\n}\n}");
            //T4MPreview.material = NewPMat;
            //T4MPreview.material.SetTexture("_MainTex", TexTexture[T4MselTexture]);
            //T4MPreview.material.SetTexture("_MaskTex", TexBrush[selBrush]);
        }

        public override void OnInspectorGUI()
        {
            //base.OnInspectorGUI();
            if (!T4MPreview)
                InitPreview();

            MenuIcon[0] = new GUIContent(AssetDatabase.LoadAssetAtPath(UniversalRenderPipelineAsset.packagePath + "Editor/Terrain/Icons/paint.png", typeof(Texture2D)) as Texture);
            MenuIcon[1] = new GUIContent(AssetDatabase.LoadAssetAtPath(UniversalRenderPipelineAsset.packagePath + "Editor/Terrain/Icons/myt4m.png", typeof(Texture2D)) as Texture);

            EditorGUILayout.BeginHorizontal("box");
            menuToolbarSelected = GUILayout.Toolbar(menuToolbarSelected, MenuIcon);
            EditorGUILayout.EndHorizontal();
            switch (menuToolbarSelected)
            {
                case 0:
                    //GrassSetting();
                    break;
                case 1:
                    TerrainSetting();
                    break;
            }
        }
        void TerrainSetting()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.HelpBox("地形设置", MessageType.None);
            EditorGUILayout.EndVertical();
        }
        #region glass
        GrassGroup _grassGroup;
        Texture[] TexBrush;
        int selBrush = 0;
        int brushSize = 16;
        float grassSensity = 0.5f;
        int randomCount = 1024;
        //void GrassSetting()
        //{
        //    IniBrush();
        //    _grassGroup = _terrain.terrainData.grassGroup;
        //    EditorGUILayout.BeginVertical();
        //    EditorGUILayout.HelpBox("草设置", MessageType.None);
        //    GUILayout.Label(string.Format("草数量:{0}", _grassGroup.grasses.Count));
        //    bool clearAllGrass = GUILayout.Button("清除所有草");
        //    if (clearAllGrass)
        //        _grassGroup.ClearAllGrass();
        //    EditorGUILayout.BeginHorizontal();
        //    bool randomGrass = GUILayout.Button("随机草");
        //    randomCount = EditorGUILayout.IntField("随机数量", randomCount);
        //    if (randomGrass)
        //        _grassGroup.RandomGroup(_terrain.transform, randomCount);
        //    EditorGUILayout.EndHorizontal();
        //    bool isUpdateGrass = GUILayout.Button("手动刷新草");
        //    if (isUpdateGrass)
        //        _grassGroup.UpdateGrass();
        //    _grassGroup.sensity = EditorGUILayout.IntSlider("Sensity", _grassGroup.sensity, 1, 10);
        //    _eraseRadius = EditorGUILayout.Slider("清除半径", _eraseRadius, 0.1f, 5f);
        //    //_grassGroup.baseColor = EditorGUILayout.ColorField("BaseColor", _grassGroup.baseColor);
        //    //_grassGroup.groundColor = EditorGUILayout.ColorField("GroundColor", _grassGroup.groundColor);
        //    _grassGroup.grassWidth = EditorGUILayout.FloatField("草宽度缩放", _grassGroup.grassWidth);
        //    _grassGroup.grassHeight = EditorGUILayout.FloatField("草高度缩放", _grassGroup.grassHeight);
        //    _grassGroup.windIntensity = EditorGUILayout.FloatField("风的强度", _grassGroup.windIntensity);
        //    _grassGroup.windFrequency = EditorGUILayout.FloatField("风的频率", _grassGroup.windFrequency);
        //    _grassGroup.cachedGrassMesh = (Mesh)EditorGUILayout.ObjectField("草的网格", _grassGroup.cachedGrassMesh, typeof(Mesh), true);
        //    EditorGUILayout.HelpBox("草颜色列表", MessageType.None);
        //    for (int i = 0; i < _grassGroup.allColors.Count; i++)
        //    {
        //        GrassColor gc = _grassGroup.allColors[i];
        //        EditorGUILayout.BeginHorizontal();
        //        bool isUsing = EditorGUILayout.Toggle(gc.isUsing, new[]{GUILayout.Width(30)});
        //        if (isUsing)
        //            _grassGroup.usingColorIndex = (uint)i;
        //        GUILayout.FlexibleSpace();
        //        Color c = EditorGUILayout.ColorField(gc.dryColor, new[]{GUILayout.Width(100)});
        //        _grassGroup.SetDryColor(c, i);
        //        GUILayout.FlexibleSpace();
        //        c = EditorGUILayout.ColorField(gc.healthyColor, new[] { GUILayout.Width(100) });
        //        _grassGroup.SetHealthyColor(c, i);
        //        GUILayout.FlexibleSpace();
        //        bool isRemoveColor = GUILayout.Button("删除", new[] { GUILayout.Width(50) });
        //        if (isRemoveColor)
        //            _grassGroup.RemoveColor(i);
        //        EditorGUILayout.EndHorizontal();
        //    }
        //    bool isAddColor = GUILayout.Button("添加颜色");
        //    if (isAddColor)
        //        _grassGroup.AddColor();
        //    EditorGUILayout.HelpBox("调试", MessageType.None);
        //    _grassGroup.isCpuCulling = EditorGUILayout.Toggle("IsCpuCulling", _grassGroup.isCpuCulling);
        //    _grassGroup.isGpuCulling = EditorGUILayout.Toggle("IsGpuCulling", _grassGroup.isGpuCulling);
        //    //EditorGUILayout.ObjectField("材质", _grassGroup.instanceMaterial, typeof(Material), true);
        //    //_grassGroup.instanceMaterial = (Material)EditorGUILayout.ObjectField("InstanceMaterial", _grassGroup.instanceMaterial, typeof(Material), true);
        //    //_grassGroup.cullingComputeShader = (ComputeShader)EditorGUILayout.ObjectField("CullingComputeShader", _grassGroup.cullingComputeShader, typeof(ComputeShader), true);
        //    /*
        //    selBrush = GUILayout.SelectionGrid(selBrush, TexBrush, 9, "gridlist", GUILayout.Width(290), GUILayout.Height(70));
        //    brushSize = (int)EditorGUILayout.Slider("Brush Size", brushSize, 1, 100);
        //    grassSensity = EditorGUILayout.Slider("Sensity", grassSensity, 0.5f, 1f);
        //    */
        //    EditorGUILayout.EndVertical();
        //    if (GUI.changed)
        //    {
        //        Debug.LogFormat("GUI.changed={0}", GUI.changed);
        //        //SceneView.RepaintAll();
        //        EditorUtility.SetDirty(target);
        //    }
        //}
        void IniBrush()
        {
            ArrayList BrushList = new ArrayList();
            Texture BrushesTL;
            int BrushNum = 0;
            do
            {
                BrushesTL = (Texture)AssetDatabase.LoadAssetAtPath(UniversalRenderPipelineAsset.packagePath + "Editor/Terrain/Brushes/Brush" + BrushNum + ".png", typeof(Texture));
                if (BrushesTL)
                {
                    BrushList.Add(BrushesTL);
                }
                BrushNum++;
            } while (BrushesTL);
            TexBrush = BrushList.ToArray(typeof(Texture)) as Texture[];
        }
        #endregion
        #region OnSceneGUI
        void OnSceneGUI()
        {
            if (EditorApplication.isPlaying)
                return;
            switch (menuToolbarSelected)
            {
                case 0:
                    Painter();
                    break;
                case 1:
                    break;
            }
            SceneView.RepaintAll();
        }
        float areaOfEffect = 5;
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
                        _grassGroup.AddGrass(raycastHit.point);
                        EditorUtility.SetDirty(target);
                    }
                    else//删草
                    {
                        _grassGroup.RemoveGrass(raycastHit.point, _eraseRadius);
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