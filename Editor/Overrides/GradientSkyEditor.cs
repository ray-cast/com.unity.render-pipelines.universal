using UnityEditor;
using UnityEditor.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(GradientSky))]
    sealed class GradientSkyEditor : VolumeComponentEditor
    {
        SerializedDataParameter _top;
        SerializedDataParameter _middle;
        SerializedDataParameter _bottom;
        SerializedDataParameter _diffusion;
        SerializedDataParameter _exposure;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<GradientSky>(serializedObject);

            _top = Unpack(o.Find(x => x.top));
            _middle = Unpack(o.Find(x => x.middle));
            _bottom = Unpack(o.Find(x => x.bottom));
            _diffusion = Unpack(o.Find(x => x.gradientDiffusion));
            _exposure = Unpack(o.Find(x => x.exposure));
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_top);
            PropertyField(_middle);
            PropertyField(_bottom);
            PropertyField(_diffusion);
            PropertyField(_exposure);
        }
    }
}
