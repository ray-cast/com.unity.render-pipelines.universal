using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(Bloom))]
    sealed class BloomEditor : VolumeComponentEditor
    {
        SerializedDataParameter _mode;
        SerializedDataParameter _threshold;
        SerializedDataParameter _iteration;
        SerializedDataParameter _radius;
        SerializedDataParameter _intensity;
        SerializedDataParameter _scatter;
        SerializedDataParameter _clamp;
        SerializedDataParameter _tint;
        SerializedDataParameter _tonemapping;
        SerializedDataParameter _glowFiltering;
        SerializedDataParameter _highQualityFiltering;
        SerializedDataParameter _dirtTexture;
        SerializedDataParameter _dirtIntensity;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<Bloom>(serializedObject);

            _mode = Unpack(o.Find(x => x.mode));
            _threshold = Unpack(o.Find(x => x.threshold));
            _iteration = Unpack(o.Find(x => x.iteration));
            _radius = Unpack(o.Find(x => x.radius));
            _intensity = Unpack(o.Find(x => x.intensity));
            _scatter = Unpack(o.Find(x => x.scatter));
            _clamp = Unpack(o.Find(x => x.clamp));
            _tint = Unpack(o.Find(x => x.tint));
            _tonemapping = Unpack(o.Find(x => x.tonemapping));
            _glowFiltering = Unpack(o.Find(x => x.glowFiltering));
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

            EditorGUILayout.LabelField("Blur", EditorStyles.miniLabel);

            PropertyField(_mode);

            if ((target as Bloom).mode == BloomMode.Kawase)
			{
                PropertyField(_iteration);
                PropertyField(_radius);
            }

            EditorGUILayout.LabelField("Bloom", EditorStyles.miniLabel);

            PropertyField(_threshold);
            PropertyField(_intensity);
            PropertyField(_scatter);
            PropertyField(_tint);
            PropertyField(_clamp);
            PropertyField(_tonemapping);
            PropertyField(_glowFiltering);
            PropertyField(_highQualityFiltering);

            EditorGUILayout.LabelField("Lens Dirt", EditorStyles.miniLabel);

            PropertyField(_dirtTexture);
            PropertyField(_dirtIntensity);
        }
    }
}
