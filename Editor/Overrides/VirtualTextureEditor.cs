using UnityEditor;
using UnityEditor.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(VirtualTexture))]
    sealed class VirtualTextureEditor : VolumeComponentEditor
    {
        SerializedDataParameter _enable;
        SerializedDataParameter _center;
        SerializedDataParameter _size;
        SerializedDataParameter _regionAdaptation;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<VirtualTexture>(serializedObject);

            _enable = Unpack(o.Find(x => x.enable));
            _center = Unpack(o.Find(x => x.center));
            _size = Unpack(o.Find(x => x.size));
            _regionAdaptation = Unpack(o.Find(x => x.regionAdaptation));

            VirtualTextureSystem.beginTileRendering += beginTileRendering;
        }

        public override void OnDisable()
        {
            VirtualTextureSystem.beginTileRendering -= beginTileRendering;
        }

        public void beginTileRendering(RequestPageData request, TiledTexture tileTexture, Vector2Int tile)
        {
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_enable);

            EditorGUI.BeginChangeCheck();            
            PropertyField(_regionAdaptation);
            if (EditorGUI.EndChangeCheck())
                VirtualTextureSystem.instance.Reset();

            EditorGUI.BeginDisabledGroup((target as VirtualTexture).regionAdaptation.value);
            PropertyField(_center);
            PropertyField(_size);
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(10);

            if (GUILayout.Button("Rebuild"))
            {
                VirtualTextureSystem.instance.Reset();
            }

            var lookupTexture = VirtualTextureSystem.instance.lookupTexture;
            if (lookupTexture != null)
            {
                EditorGUILayout.LabelField("–Èƒ‚Œ∆¿Ì≤È’“±Ì:");
                EditorGUI.DrawPreviewTexture(GUILayoutUtility.GetAspectRect((float)lookupTexture.width / lookupTexture.height), lookupTexture);
            }

            EditorGUILayout.Space();

            var virtualTexture = VirtualTextureSystem.instance.tileTexture;
            if (virtualTexture != null && virtualTexture.tileTextures.Length >= 2)
            {
                var albedoTexture = virtualTexture.tileTextures[0];
                var normalTexture = virtualTexture.tileTextures[1];

                EditorGUILayout.LabelField("–Èƒ‚Œ∆¿Ì:");

                EditorGUI.DrawPreviewTexture(GUILayoutUtility.GetAspectRect((float)albedoTexture.width / albedoTexture.height), albedoTexture);
                EditorGUI.DrawPreviewTexture(GUILayoutUtility.GetAspectRect((float)normalTexture.width / normalTexture.height), normalTexture);
            }
        }
    }
}
