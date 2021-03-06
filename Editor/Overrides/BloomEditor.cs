using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(Bloom))]
    sealed class BloomEditor : VolumeComponentEditor
    {
        SerializedDataParameter _threshold;
        SerializedDataParameter _intensity;
        SerializedDataParameter _scatter;
        SerializedDataParameter _clamp;
        SerializedDataParameter _tint;
        SerializedDataParameter _highQualityFiltering;
        SerializedDataParameter _dirtTexture;
        SerializedDataParameter _dirtIntensity;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<Bloom>(serializedObject);

            _threshold = Unpack(o.Find(x => x.threshold));
            _intensity = Unpack(o.Find(x => x.intensity));
            _scatter = Unpack(o.Find(x => x.scatter));
            _clamp = Unpack(o.Find(x => x.clamp));
            _tint = Unpack(o.Find(x => x.tint));
            _highQualityFiltering = Unpack(o.Find(x => x.highQualityFiltering));
            _dirtTexture = Unpack(o.Find(x => x.dirtTexture));
            _dirtIntensity = Unpack(o.Find(x => x.dirtIntensity));
        }

        public override void OnInspectorGUI()
        {
            if (UniversalRenderPipeline.asset != null && UniversalRenderPipeline.asset.postProcessingFeatureSet == PostProcessingFeatureSet.PostProcessingV2)
            {
                EditorGUILayout.HelpBox(UniversalRenderPipelineAssetEditor.Styles.postProcessingGlobalWarning, MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Bloom", EditorStyles.miniLabel);

            PropertyField(_threshold);
            PropertyField(_intensity);
            PropertyField(_scatter);
            PropertyField(_tint);
            PropertyField(_clamp);
            PropertyField(_highQualityFiltering);

            if (_highQualityFiltering.overrideState.boolValue && _highQualityFiltering.value.boolValue && CoreEditorUtils.buildTargets.Contains(GraphicsDeviceType.OpenGLES2))
                EditorGUILayout.HelpBox("High Quality Bloom isn't supported on GLES2 platforms.", MessageType.Warning);

            EditorGUILayout.LabelField("Lens Dirt", EditorStyles.miniLabel);

            PropertyField(_dirtTexture);
            PropertyField(_dirtIntensity);
        }
    }
}
