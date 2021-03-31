using System.Collections.Generic;
using System;
using UnityEngine.SocialPlatforms;
using UnityEditor;

namespace UnityEngine.Rendering.Universal
{
    public delegate void FlowerGroupChange();
    [Serializable]
    public class FlowerGroup
    {
        public const int DEFAULT_COUNT = 64;

        [SerializeField]
        float _grassWidth;

        [SerializeField]
        float _grassHeight;

        [SerializeField]
        float _windIntensity;

        [SerializeField]
        float _windFrequency;

        [SerializeField]
        Vector2 _windTiling;

        [SerializeField]
        Vector2 _windWrap;

        [SerializeField]
        float _windHightlightSpeed;

        [SerializeField]
        Vector2 _windScatter;

        [SerializeField]
        Vector3 _windDirection;

        [SerializeField]
        Texture _windNoise;

        [SerializeField]
        Mesh _cachedGrassMesh;

        public Material instanceMaterial;

        public bool isCpuCulling = true;
        public bool isGpuCulling = true;
        public int brushSensity = 2;

        public float sensity = 1.0f;
        public float maxDrawDistance = 100;//this setting will affect performance a lot!

        float _cutoff = 0.5f;

        public List<FlowerPrototype> floweres = new List<FlowerPrototype>();

        [Range(1, 10000)]
        public int instanceCount = DEFAULT_COUNT;

        public event GrassGroupChange onChange;

        public InstancedIndirectFlowerRenderer _renderer;

        public Texture texture
        {
            get { return instanceMaterial.GetTexture("_MainTex"); }
            set { instanceMaterial.SetTexture("_MainTex", value); }
        }
        public Color color
        {
            get { return instanceMaterial.GetColor("_MainColor"); }
            set {
#if UNITY_EDITOR
                Undo.RecordObject(instanceMaterial, "");
#endif
                instanceMaterial.SetColor("_MainColor", value);
            }
        }
        public float cutoff
        {
            get { return _cutoff; }
            set
            {
                if (_cutoff != value)
                {
#if UNITY_EDITOR
                    Undo.RecordObjects(new UnityEngine.Object[2] { _renderer, instanceMaterial }, "");
#endif
                    _cutoff = value;
                    instanceMaterial.SetFloat("_Cutoff", _cutoff);
                }
            }
        }

        public float grassWidth
        {
            get { return _grassWidth; }
            set
            {
                if (_grassWidth != value)
                {
#if UNITY_EDITOR
                    Undo.RecordObjects(new UnityEngine.Object[2] { _renderer, instanceMaterial }, "");
#endif
                    _grassWidth = value;
                    instanceMaterial.SetFloat("_GrassWidth", _grassWidth);
                }
            }
        }

        public float grassHeight
        {
            get { return _grassHeight; }
            set
            {
                if (_grassHeight != value)
                {
#if UNITY_EDITOR
                    Undo.RecordObjects(new UnityEngine.Object[2] { _renderer, instanceMaterial }, "");
#endif
                    _grassHeight = value;
                    instanceMaterial.SetFloat("_GrassHeight", _grassHeight);
                }
            }
        }

        public float windIntensity
        {
            get { return _windIntensity; }
            set
            {
                if (_windIntensity != value)
                {
#if UNITY_EDITOR
                    Undo.RecordObjects(new UnityEngine.Object[2] { _renderer, instanceMaterial }, "");
#endif
                    _windIntensity = value;
                    instanceMaterial.SetFloat("_WindAIntensity", _windIntensity);
                }
            }
        }
        public float windFrequency
        {
            get { return _windFrequency; }
            set
            {
                if (_windFrequency != value)
                {
#if UNITY_EDITOR
                    Undo.RecordObjects(new UnityEngine.Object[2] { _renderer, instanceMaterial }, "");
#endif
                    _windFrequency = value;
                    instanceMaterial.SetFloat("_WindAFrequency", _windFrequency);
                }
            }
        }

        public Vector2 windTiling
        {
            get { return _windTiling; }
            set
            {
                if (_windTiling != value)
                {
#if UNITY_EDITOR
                    Undo.RecordObjects(new UnityEngine.Object[2] { _renderer, instanceMaterial }, "");
#endif
                    _windTiling = value;
                    instanceMaterial.SetVector("_WindATiling", _windTiling);
                }
            }
        }
        public Vector2 windWrap
        {
            get { return _windWrap; }
            set
            {
                if (_windWrap != value)
                {
#if UNITY_EDITOR
                    Undo.RecordObjects(new UnityEngine.Object[2] { _renderer, instanceMaterial }, "");
#endif
                    _windWrap = value;
                    instanceMaterial.SetVector("_WindAWrap", _windWrap);
                }
            }
        }

        public float windHightlightSpeed
        {
            get { return _windHightlightSpeed; }
            set
            {
                if (_windHightlightSpeed != value)
                {
#if UNITY_EDITOR
                    Undo.RecordObjects(new UnityEngine.Object[2] { _renderer, instanceMaterial }, "");
#endif
                    _windHightlightSpeed = value;
                    instanceMaterial.SetFloat("_WindHightlightSpeed", _windHightlightSpeed);
                }
            }
        }

        public Vector2 windScatter
        {
            get { return _windScatter; }
            set
            {
                if (_windScatter != value)
                {
#if UNITY_EDITOR
                    Undo.RecordObjects(new UnityEngine.Object[2] { _renderer, instanceMaterial }, "");
#endif
                    _windScatter = value;
                    instanceMaterial.SetVector("_WindScatter", _windScatter);
                }
            }
        }

        public Vector3 windDirection
        {
            get { return _windDirection; }
            set
            {
                if (_windDirection != value)
                {
#if UNITY_EDITOR
                    Undo.RecordObjects(new UnityEngine.Object[2] { _renderer, instanceMaterial }, "");
#endif
                    _windDirection = value;
                    instanceMaterial.SetVector("_WindDirection", _windDirection);
                }
            }
        }

        public Texture windNoise
        {
            get { return _windNoise; }
            set
            {
                if (windNoise != value)
                {
#if UNITY_EDITOR
                    Undo.RecordObjects(new UnityEngine.Object[2] { _renderer, instanceMaterial }, "");
#endif
                    _windNoise = value;
                    instanceMaterial.SetTexture("_WindNoiseMap", _windNoise);
                }
            }
        }

        public float bendStrength
        {
            get { return instanceMaterial.GetFloat("_BendStrength"); }
            set
            {
#if UNITY_EDITOR
                Undo.RecordObjects(new UnityEngine.Object[2] { _renderer, instanceMaterial }, "");
#endif
                instanceMaterial.SetFloat("_BendStrength", value);
            }
        }

        public Mesh cachedGrassMesh
        {
            get {
                if (_cachedGrassMesh == null)
                {
                    //if not exist, create a 3 vertices hardcode triangle grass mesh
                    _cachedGrassMesh = new Mesh();

                    //single grass (vertices)
                    Vector3[] verts = new Vector3[3];
                    verts[0] = new Vector3(-0.01f, 0);
                    verts[1] = new Vector3(+0.01f, 0);
                    verts[2] = new Vector3(-0.0f, 0.3f);
                    //single grass (Triangle index)
                    int[] trinagles = new int[3] { 2, 1, 0, }; //order to fit Cull Back in grass shader

                    _cachedGrassMesh.SetVertices(verts);
                    _cachedGrassMesh.SetTriangles(trinagles, 0);
                }
                return _cachedGrassMesh;
            }
            set
            {
                if (_cachedGrassMesh != value)
                {
                    _cachedGrassMesh = value;
                    if (onChange != null)
                        onChange();
                }
            }
        }

        public void Init(InstancedIndirectFlowerRenderer renderer)
        {
            _renderer = renderer;
            //UnityException: Load is not allowed to be called during serialization, call it from OnEnable instead. Called from ScriptableObject 'TerrainData'.
            if (instanceMaterial == null)
            {
                Material srcMat = Resources.Load<Material>("InstancedIndirectFlower");
                instanceMaterial = new Material(srcMat);
                _grassWidth = instanceMaterial.GetFloat("_GrassWidth");
                _grassHeight = instanceMaterial.GetFloat("_GrassHeight");
                _windIntensity = instanceMaterial.GetFloat("_WindAIntensity");
                _windFrequency = instanceMaterial.GetFloat("_WindAFrequency");
                _windTiling = instanceMaterial.GetVector("_WindATiling");
                _windWrap = instanceMaterial.GetVector("_WindAWrap");
                _windNoise = instanceMaterial.GetTexture("_WindNoiseMap");
                _windHightlightSpeed = instanceMaterial.GetFloat("_WindHightlightSpeed");
                _windScatter = instanceMaterial.GetVector("_WindScatter");
                _windDirection = instanceMaterial.GetVector("_WindDirection");
            }
        }

        public void AddFlower(Vector3 worldPos)
        {
            worldPos.x = Mathf.Round(worldPos.x * 100) / 100;
            worldPos.y = Mathf.Round(worldPos.y * 100) / 100;
            worldPos.z = Mathf.Round(worldPos.z * 100) / 100;
            bool isAlreadyExist = false;
            for (int i = 0; i < floweres.Count; i++)
            {
                if (Vector3.Distance(floweres[i].worldPos, worldPos) * 100 < brushSensity)
                {
                    isAlreadyExist = true;
                    break;
                }
            }
            if (!isAlreadyExist)
            {
                FlowerPrototype fp = new FlowerPrototype() { worldPos = worldPos};
                floweres.Add(fp);
                if (onChange != null)
                    onChange();
            }
        }
        //圆范围内
        public void RemoveFlower(Vector3 center, float radius)
        {
            for (int i = floweres.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(floweres[i].worldPos, center) < radius)
                {
                    floweres.RemoveAt(i);
                }
            }
        }
        public void ClearAllGrass()
        {
            floweres.Clear();
            if (onChange != null)
                onChange();
        }
        public void UpdateGrass()
        {
            if (onChange != null)
                onChange();
        }
        public void RandomGroup(Transform transform, int instanceCount)
        {
            floweres.Clear();
            //same seed to keep grass visual the same
            UnityEngine.Random.InitState(123);

            //auto keep density the same
            float scale = Mathf.Sqrt(instanceCount) / 40f;
            transform.localScale = new Vector3(scale, transform.localScale.y, scale);

            //////////////////////////////////////////////////////////////////////////
            //can define any posWS in this section, random is just an example
            //////////////////////////////////////////////////////////////////////////
            for (int i = 0; i < instanceCount; i++)
            {
                Vector3 pos = Vector3.zero;

                pos.x = UnityEngine.Random.Range(-1f, 1f) * transform.lossyScale.x;
                pos.z = UnityEngine.Random.Range(-1f, 1f) * transform.lossyScale.z;

                //transform to posWS in C#
                pos += transform.position;

                floweres.Add(new FlowerPrototype() { worldPos = new Vector3(pos.x, pos.y, pos.z)});
            }
            if (onChange != null)
                onChange();
        }
        public void RandomGroupBySensity(Transform transform, int instanceCount)
        {
            floweres.Clear();

            //auto keep density the same
            int wide = Mathf.RoundToInt(Mathf.Sqrt(instanceCount));
            int count = 0;
            float ss = brushSensity / 100f;
            Vector3 offset = transform.position + new Vector3(-wide / 2f * ss, 0, -wide / 2f * ss);
            for (int i = 0; i < wide && count < instanceCount; i++)
            {
                for (int j = 0; j < wide && count < instanceCount; j++)
                {
                    Vector3 pos = new Vector3(j* ss, 0, i* ss);
                    pos += offset;
                    floweres.Add(new FlowerPrototype() { worldPos = new Vector3(pos.x, pos.y, pos.z) });
                    count++;
                }
            }
            if (onChange != null)
                onChange();
        }
    }
}

