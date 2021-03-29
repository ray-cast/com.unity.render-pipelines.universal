using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UnityEngine.Rendering.Universal
{
    public class FutureTerrainWindow : EditorWindow
    {
         [MenuItem("Window/Future Terrain")]
        static void Open()
        {
            var window = (FutureTerrainWindow)EditorWindow.GetWindowWithRect(typeof(FutureTerrainWindow), new Rect(0, 0, 386, 320), false, "Paint Detail");
            window.Show();
        }
        #region static
        static public Transform CurrentSelect;
        static public int T4MMenuToolbar = 0;
        static public int brushSize = 16;
        static public float grassSensity = 0.5f;
        static public Projector T4MPreview;
        static public int layerMask = 0;
        #endregion

        #region member
        string T4MEditorFolder = "Assets/FutureTerrain/Editor/";
        UnityEngine.Object AddObject;
        GUIContent[] MenuIcon = new GUIContent[1];
        Texture[] TexBrush;
        int selBrush = 0;
        #endregion
        void OnInspectorUpdate()
        {
            Repaint();
        }

        void OnGUI()
        {
            CurrentSelect = Selection.activeTransform;

            GUILayout.BeginHorizontal();

            GUILayout.BeginArea(new Rect(0, 0, 363, 585));

            EditorGUILayout.Space();
            GUILayout.BeginHorizontal("box");
            MenuIcon[0] = new GUIContent(AssetDatabase.LoadAssetAtPath(T4MEditorFolder + "Icons/paint.png", typeof(Texture2D)) as Texture);
            T4MMenuToolbar = (int)GUILayout.Toolbar(T4MMenuToolbar, MenuIcon, "gridlist", GUILayout.Width(172), GUILayout.Height(18));
            GUILayout.EndHorizontal();

            GUILayout.Label(AssetDatabase.LoadAssetAtPath(T4MEditorFolder + "Img/separator.png", typeof(Texture)) as Texture);

            PainterMenu();
            GUILayout.EndArea();


            GUILayout.EndHorizontal();
        }
        void PainterMenu()
        {
            IniBrush();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(AssetDatabase.LoadAssetAtPath(T4MEditorFolder + "Img/brushes.jpg", typeof(Texture)) as Texture, "label");
            GUILayout.BeginHorizontal("box", GUILayout.Width(318));
            GUILayout.FlexibleSpace();
            selBrush = GUILayout.SelectionGrid(selBrush, TexBrush, 9, "gridlist", GUILayout.Width(290), GUILayout.Height(70));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical("box", GUILayout.Width(347));
            brushSize = (int)EditorGUILayout.Slider("Brush Size", brushSize, 1, 100);
            grassSensity = EditorGUILayout.Slider("Sensity", grassSensity, 0.5f, 1f);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void IniBrush()
        {
            ArrayList BrushList = new ArrayList();
            Texture BrushesTL;
            int BrushNum = 0;
            do
            {
                BrushesTL = (Texture)AssetDatabase.LoadAssetAtPath(T4MEditorFolder + "Brushes/Brush" + BrushNum + ".png", typeof(Texture));
                if (BrushesTL)
                {
                    BrushList.Add(BrushesTL);
                }
                BrushNum++;
            } while (BrushesTL);
            TexBrush = BrushList.ToArray(typeof(Texture)) as Texture[];
        }
    }
}