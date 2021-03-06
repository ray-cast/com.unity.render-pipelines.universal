using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(LiftGammaGain))]
    sealed class LiftGammaGainEditor : VolumeComponentEditor
    {
        SerializedDataParameter _lift;
        SerializedDataParameter _gamma;
        SerializedDataParameter _gain;

        readonly TrackballUIDrawer _trackballUIDrawer = new TrackballUIDrawer();

        public override void OnEnable()
        {
            var o = new PropertyFetcher<LiftGammaGain>(serializedObject);

            _lift = Unpack(o.Find(x => x.lift));
            _gamma = Unpack(o.Find(x => x.gamma));
            _gain = Unpack(o.Find(x => x.gain));
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
                _trackballUIDrawer.OnGUI(_lift.value, _lift.overrideState, EditorGUIUtility.TrTextContent("Lift"), GetLiftValue);
                GUILayout.Space(4f);
                _trackballUIDrawer.OnGUI(_gamma.value, _gamma.overrideState, EditorGUIUtility.TrTextContent("Gamma"), GetLiftValue);
                GUILayout.Space(4f);
                _trackballUIDrawer.OnGUI(_gain.value, _gain.overrideState, EditorGUIUtility.TrTextContent("Gain"), GetLiftValue);
            }
        }

        static Vector3 GetLiftValue(Vector4 x) => new Vector3(x.x + x.w, x.y + x.w, x.z + x.w);
    }
}
