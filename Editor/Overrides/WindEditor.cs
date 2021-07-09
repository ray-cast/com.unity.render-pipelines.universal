using UnityEditor;
using UnityEditor.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [VolumeComponentEditor(typeof(Wind))]
    sealed class WindEditor : VolumeComponentEditor
    {
        SerializedDataParameter _direction;
        SerializedDataParameter _range;

        SerializedDataParameter _mode;
        SerializedDataParameter _level;

        SerializedDataParameter _speed;
        SerializedDataParameter _loads;

        SerializedDataParameter _frequency;
        SerializedDataParameter _bending;
        SerializedDataParameter _lean;
        SerializedDataParameter _random;

        SerializedDataParameter _tiling;
        SerializedDataParameter _angle;
        SerializedDataParameter _noise;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<Wind>(serializedObject);

            _direction = Unpack(o.Find(x => x.direction));
            _range = Unpack(o.Find(x => x.range));

            _mode = Unpack(o.Find(x => x.mode));
            _level = Unpack(o.Find(x => x.scale));

            _loads = Unpack(o.Find(x => x.load));
            _speed = Unpack(o.Find(x => x.speed));

            _frequency = Unpack(o.Find(x => x.frequency));
            _bending = Unpack(o.Find(x => x.bending));
            _lean = Unpack(o.Find(x => x.lean));
            _random = Unpack(o.Find(x => x.random));

            _tiling = Unpack(o.Find(x => x.tiling));
            _angle = Unpack(o.Find(x => x.angle));
            _noise = Unpack(o.Find(x => x.noise));
        }

        public override void OnInspectorGUI()
        {
            var wind = target as Wind;

            EditorGUILayout.LabelField("Common", EditorStyles.miniLabel);

            PropertyField(_mode);

            if (wind.mode.value == WindMode.Physics)
			{
                PropertyField(_level);

                PropertyField(_direction);
                PropertyField(_range, EditorGUIUtility.TrTextContent("Range (m)"));

                PropertyField(_speed, EditorGUIUtility.TrTextContent("Speed (m/s)"));
                PropertyField(_loads, EditorGUIUtility.TrTextContent("Load (mmAq)"));

                var V = wind.speed.value;
                var r = 1.21f;
                var g = Mathf.Abs(Vector3.Dot(Vector3.one, Physics.gravity));

                switch (wind.scale.value)
                {
                    case WindLevel.Level0: V = 0.0f; break;
                    case WindLevel.Level1: V = 1.5f; break;
                    case WindLevel.Level2: V = 3.3f; break;
                    case WindLevel.Level3: V = 5.4f; break;
                    case WindLevel.Level4: V = 7.9f; break;
                    case WindLevel.Level5: V = 10.7f; break;
                    case WindLevel.Level6: V = 13.8f; break;
                    case WindLevel.Level7: V = 17.1f; break;
                    case WindLevel.Level8: V = 20.7f; break;
                    case WindLevel.Level9: V = 24.4f; break;
                    case WindLevel.Level10: V = 28.4f; break;
                }

                wind.speed.Override(V);
                wind.load.Override((V * V) / (2f * g) * r);
            }
			else
			{
                PropertyField(_direction);
                PropertyField(_range, EditorGUIUtility.TrTextContent("Range (m)"));

                PropertyField(_speed, EditorGUIUtility.TrTextContent("Speed (m/s)"));
                PropertyField(_loads, EditorGUIUtility.TrTextContent("Load (mmAq)"));

                if (GUILayout.Button("Calculate the Wind Loads from Speed"))
                    wind.load.Override(Mathf.Pow(wind.speed.value / 4.04f, 2));
            }

            EditorGUILayout.LabelField("Swing", EditorStyles.miniLabel);

            PropertyField(_frequency);
            PropertyField(_bending);
            PropertyField(_lean);
            PropertyField(_angle);
            PropertyField(_random);

            EditorGUILayout.LabelField("Storm", EditorStyles.miniLabel);

            PropertyField(_tiling);
            PropertyField(_noise);
        }
    }
}
