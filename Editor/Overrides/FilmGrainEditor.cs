using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(FilmGrain))]
    sealed class FilmGrainEditor : VolumeComponentEditor
    {
        SerializedDataParameter _type;
        SerializedDataParameter _intensity;
        SerializedDataParameter _response;
        SerializedDataParameter _texture;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<FilmGrain>(serializedObject);

            _type = Unpack(o.Find(x => x.type));
            _intensity = Unpack(o.Find(x => x.intensity));
            _response = Unpack(o.Find(x => x.response));
            _texture = Unpack(o.Find(x => x.texture));
        }

        public override void OnInspectorGUI()
        {
            if (UniversalRenderPipeline.asset != null && UniversalRenderPipeline.asset.postProcessingFeatureSet == PostProcessingFeatureSet.PostProcessingV2)
            {
                EditorGUILayout.HelpBox(UniversalRenderPipelineAssetEditor.Styles.postProcessingGlobalWarning, MessageType.Warning);
                return;
            }

            PropertyField(_type);

            if (_type.value.intValue == (int)FilmGrainLookup.Custom)
            {
                PropertyField(_texture);

                var texture = (target as FilmGrain).texture.value;

                if (texture != null)
                {
                    var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;

                    // Fails when using an internal texture as you can't change import settings on
                    // builtin resources, thus the check for null
                    if (importer != null)
                    {
                        bool valid = importer.mipmapEnabled == false
                            && importer.alphaSource == TextureImporterAlphaSource.FromGrayScale
                            && importer.filterMode == FilterMode.Point
                            && importer.textureCompression == TextureImporterCompression.Uncompressed
                            && importer.textureType == TextureImporterType.SingleChannel;

                        if (!valid)
                            CoreEditorUtils.DrawFixMeBox("Invalid texture import settings.", () => SetTextureImportSettings(importer));
                    }
                }
            }

            PropertyField(_intensity);
            PropertyField(_response);
        }

        static void SetTextureImportSettings(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.SingleChannel;
            importer.alphaSource = TextureImporterAlphaSource.FromGrayScale;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            AssetDatabase.Refresh();
        }
    }
}
