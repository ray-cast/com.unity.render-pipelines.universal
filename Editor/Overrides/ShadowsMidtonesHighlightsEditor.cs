using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
    // TODO: handle retina / EditorGUIUtility.pixelsPerPoint
    [VolumeComponentEditor(typeof(ShadowsMidtonesHighlights))]
    sealed class ShadowsMidtonesHighlightsEditor : VolumeComponentEditor
    {
        SerializedDataParameter _shadows;
        SerializedDataParameter _midtones;
        SerializedDataParameter _highlights;
        SerializedDataParameter _shadowsStart;
        SerializedDataParameter _shadowsEnd;
        SerializedDataParameter _highlightsStart;
        SerializedDataParameter _highlightsEnd;

        readonly TrackballUIDrawer _trackballUIDrawer = new TrackballUIDrawer();
        
        // Curve drawing utilities
        Rect _curveRect;
        Material _material;
        RenderTexture _curveTex;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<ShadowsMidtonesHighlights>(serializedObject);
            
            _shadows         = Unpack(o.Find(x => x.shadows));
            _midtones        = Unpack(o.Find(x => x.midtones));
            _highlights      = Unpack(o.Find(x => x.highlights));
            _shadowsStart    = Unpack(o.Find(x => x.shadowsStart));
            _shadowsEnd      = Unpack(o.Find(x => x.shadowsEnd));
            _highlightsStart = Unpack(o.Find(x => x.highlightsStart));
            _highlightsEnd   = Unpack(o.Find(x => x.highlightsEnd));

            _material = new Material(Shader.Find("Hidden/Universal Render Pipeline/Editor/Shadows Midtones Highlights Curve"));
        }

        public override void OnInspectorGUI()
        {
            if (UniversalRenderPipeline.asset != null && UniversalRenderPipeline.asset.postProcessingFeatureSet == PostProcessingFeatureSet.PostProcessingV2)
            {
                EditorGUILayout.HelpBox(UniversalRenderPipelineAssetEditor.Styles.postProcessingGlobalWarning, MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _trackballUIDrawer.OnGUI(_shadows.value, _shadows.overrideState, EditorGUIUtility.TrTextContent("Shadows"), GetWheelValue);
                GUILayout.Space(4f);
                _trackballUIDrawer.OnGUI(_midtones.value, _midtones.overrideState, EditorGUIUtility.TrTextContent("Midtones"), GetWheelValue);
                GUILayout.Space(4f);
                _trackballUIDrawer.OnGUI(_highlights.value, _highlights.overrideState, EditorGUIUtility.TrTextContent("Highlights"), GetWheelValue);
            }
            EditorGUILayout.Space();

            // Reserve GUI space
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                _curveRect = GUILayoutUtility.GetRect(128, 80);
            }

            if (Event.current.type == EventType.Repaint)
            {
                float alpha = GUI.enabled ? 1f : 0.4f;
                var limits = new Vector4(_shadowsStart.value.floatValue, _shadowsEnd.value.floatValue, _highlightsStart.value.floatValue, _highlightsEnd.value.floatValue);

                _material.SetVector("_ShaHiLimits", limits);
                _material.SetVector("_Variants", new Vector4(alpha, Mathf.Max(_highlightsEnd.value.floatValue, 1f), 0f, 0f));

                CheckCurveRT((int)_curveRect.width, (int)_curveRect.height);

                var oldRt = RenderTexture.active;
                Graphics.Blit(null, _curveTex, _material, EditorGUIUtility.isProSkin ? 0 : 1);
                RenderTexture.active = oldRt;

                GUI.DrawTexture(_curveRect, _curveTex);

                Handles.DrawSolidRectangleWithOutline(_curveRect, Color.clear, Color.white * 0.4f);
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Shadow Limits", EditorStyles.miniLabel);
            PropertyField(_shadowsStart, EditorGUIUtility.TrTextContent("Start"));
            _shadowsStart.value.floatValue = Mathf.Min(_shadowsStart.value.floatValue, _shadowsEnd.value.floatValue);
            PropertyField(_shadowsEnd, EditorGUIUtility.TrTextContent("End"));
            _shadowsEnd.value.floatValue = Mathf.Max(_shadowsStart.value.floatValue, _shadowsEnd.value.floatValue);

            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Highlight Limits", EditorStyles.miniLabel);
            PropertyField(_highlightsStart, EditorGUIUtility.TrTextContent("Start"));
            _highlightsStart.value.floatValue = Mathf.Min(_highlightsStart.value.floatValue, _highlightsEnd.value.floatValue);
            PropertyField(_highlightsEnd, EditorGUIUtility.TrTextContent("End"));
            _highlightsEnd.value.floatValue = Mathf.Max(_highlightsStart.value.floatValue, _highlightsEnd.value.floatValue);
        }

        void CheckCurveRT(int width, int height)
        {
            if (_curveTex == null || !_curveTex.IsCreated() || _curveTex.width != width || _curveTex.height != height)
            {
                CoreUtils.Destroy(_curveTex);
                _curveTex = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
                _curveTex.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        static Vector3 GetWheelValue(Vector4 v)
        {
            float w = v.w * (Mathf.Sign(v.w) < 0f ? 1f : 4f);
            return new Vector3(
                Mathf.Max(v.x + w, 0f),
                Mathf.Max(v.y + w, 0f),
                Mathf.Max(v.z + w, 0f)
            );
        }
    }
}
