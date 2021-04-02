using System.Collections.Generic;
using System;
using UnityEditor;

namespace UnityEngine.Rendering.Universal
{
    public delegate void GrassGroupChange();
    [Serializable]
    public class GrassGroup
    {
        public const int k_DefaultCount = 64;
        public const int k_MaxColorsLimits = 16;
        public const int k_MaxScalesLimits = 16;

        public bool isCpuCulling = true;
        public bool isGpuCulling = true;

        public float brushSensity = 0.5f;

        public float sensity = 1.0f;
        public float distanceCulling = 1.0f;
        public float maxDrawDistance = 125;//this setting will affect performance a lot!

        public Material instanceMaterial;

        [Range(1, 10000)]
        public int instanceCount = k_DefaultCount;

        InstancedIndirectGrassRenderer _renderer;

        [SerializeField]
        float _windIntensity;

        [SerializeField]
        float _windFrequency;

        [SerializeField]
        float _windRange = 20;

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

        public List<GrassPrototype> grasses = new List<GrassPrototype>();        
        public List<GrassColor> allColors = new List<GrassColor>();
        public List<Vector3> allScales = new List<Vector3>();

        public uint usingScaleIndex;

        public static int maxColorLimits
        {
            get
            {
                return k_MaxColorsLimits;
            }
        }

        public static int maxScaleLimits
        {
            get
            {
                return k_MaxScalesLimits;
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

        public float windRange
        {
            get { return _windRange; }
            set
            {
                if (_windRange != value)
                {
#if UNITY_EDITOR
                    Undo.RecordObjects(new UnityEngine.Object[2] { _renderer, instanceMaterial }, "");
#endif
                    _windRange = value;
                    instanceMaterial.SetFloat("_WindRange", _windRange);
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
        public float bendStrength
        {
            get { return instanceMaterial.GetFloat("_BendStrength"); }
            set
            {
#if UNITY_EDITOR
                Undo.RecordObject(instanceMaterial, "");
#endif
                instanceMaterial.SetFloat("_BendStrength", value);
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

        public Mesh cachedGrassMesh
        {
            get
            {
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

        public uint usingColorIndex 
        {
            get
            {
                for (int i = 0; i < allColors.Count; i++)
                    if (allColors[i].isUsing)
                        return (uint)i;
                return 0;
            }
            set
            {
                for (int i = 0; i < allColors.Count; i++)
                    allColors[i].isUsing = (i == value ? true : false);
            }
        }

        public void SetDryColor(Color c, int index)
        {
            if (allColors[index].dryColor != c)
            {
                allColors[index].dryColor = c;
                if (onColorChange != null)
                    onColorChange();
            }
        }
        public void SetHealthyColor(Color c, int index)
        {
            if (allColors[index].healthyColor != c)
            {
                allColors[index].healthyColor = c;
                if (onColorChange != null)
                    onColorChange();
            }
        }

        public bool AddColor()
        {
            if (allColors.Count < GrassGroup.maxColorLimits)
			{
                allColors.Add(new GrassColor());
                return true;
            }

            return false;
        }

        public void RemoveColor(int index)
        {
            if (allColors.Count > 1)
            {
                if (usingColorIndex == index)//如果移除的是当前正在使用的，则选择第一个做为当前使用
                    usingColorIndex = 0;
                allColors.RemoveAt(index);
                //已经使用此颜色的需要更新成使用第一个
                for (int i = 0; i < grasses.Count; i++)
                {
                    GrassPrototype gp = grasses[i];
                    if (gp.colorIndex == index)
                        gp.colorIndex = usingColorIndex;
                }
                if (onChange != null)
                    onChange();
            }
        }

        public void SetScale(Vector3 newScale, int index)
        {
            Vector3 scale = allScales[index];
            if (scale != newScale)
            {
                allScales[index] = newScale;
                if (onScaleChange != null)
                    onScaleChange();
            }
        }

        public bool AddScale()
        {
            if (allScales.Count < GrassGroup.k_MaxScalesLimits)
			{
                allScales.Add(Vector3.one);
                return true;
            }

            return false;
        }

        public void RemoveScale(int index)
        {
            if (allScales.Count > 1)
            {
                if (usingScaleIndex == index)//如果移除的是当前正在使用的，则选择第一个做为当前使用
                    usingScaleIndex = 0;
                allScales.RemoveAt(index);
                //已经使用此模板的需要更新成使用第一个
                for (int i = 0; i < grasses.Count; i++)
                {
                    GrassPrototype gp = grasses[i];
                    if (gp.scaleIndex == index)
                        gp.scaleIndex = usingScaleIndex;
                }
                if (onChange != null)
                    onChange();
            }
        }

        public event GrassGroupChange onChange;
        public event GrassGroupChange onColorChange;
        public event GrassGroupChange onScaleChange;

#if UNITY_EDITOR//空间划分，提高刷草性能，刷草时会检测密度，划分了空间可以减少检测的草数量
        Dictionary<long, List<GrassPrototype>> _gridDic;
        void AddGrass2Grid(GrassPrototype grass)
        {
            long key = GetGridKey(grass.worldPos.x, grass.worldPos.z);
            List<GrassPrototype> list;
            if (!_gridDic.TryGetValue(key, out list))
                list = _gridDic[key] = new List<GrassPrototype>();
            list.Add(grass);
        }
        long GetGridKey(float x, float z)
        {
            int xGrid = Mathf.FloorToInt(x / 1);
            int zGrid = Mathf.FloorToInt(z / 1);
            return xGrid << 32 + zGrid;
        }
#endif

        public void Init(InstancedIndirectGrassRenderer renderer)
        {
            _renderer = renderer;

            //UnityException: Load is not allowed to be called during serialization, call it from OnEnable instead. Called from ScriptableObject 'TerrainData'.
            if (instanceMaterial == null)
            {
                Material srcMat = Resources.Load<Material>("InstancedIndirectGrass");
                instanceMaterial = new Material(srcMat);
                //_baseColor = instanceMaterial.GetColor("_BaseColor");
                //_groundColor = instanceMaterial.GetColor("_GroundColor");
                _windIntensity = instanceMaterial.GetFloat("_WindAIntensity");
                _windFrequency = instanceMaterial.GetFloat("_WindAFrequency");
                _windTiling = instanceMaterial.GetVector("_WindATiling");
                _windWrap = instanceMaterial.GetVector("_WindAWrap");
                _windNoise = instanceMaterial.GetTexture("_WindNoiseMap");
                _windHightlightSpeed = instanceMaterial.GetFloat("_WindHightlightSpeed");
                _windScatter = instanceMaterial.GetVector("_WindScatter");
                _windDirection = instanceMaterial.GetVector("_WindDirection");
            }

            if (allColors.Count == 0)
                allColors.Add(new GrassColor());

            int usingIndex = -1;
            for (int i = 0; i < allColors.Count; i++)
                if (allColors[i].isUsing)
                {
                    usingIndex = i;
                    break;
                }
            if (usingIndex == -1)
                allColors[0].isUsing = true;
            if (allScales.Count == 0)
                allScales.Add(new Vector3(1, 1.5f, 1));
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                _gridDic = new Dictionary<long, List<GrassPrototype>>();
                for (int i = 0; i < grasses.Count; i++)
                    AddGrass2Grid(grasses[i]);
            }
#endif
        }
#if UNITY_EDITOR
        public void AddGrass(Vector3 pos)
        {
            pos.x = Mathf.Round(pos.x * 100) / 100;
            pos.y = Mathf.Round(pos.y * 100) / 100;
            pos.z = Mathf.Round(pos.z * 100) / 100;
            bool isAlreadyExist = false;
            List<GrassPrototype> gridGrasses;
            long key = GetGridKey(pos.x, pos.z);
            if (_gridDic.TryGetValue(key, out gridGrasses))
            {
                for (int i = 0; i < gridGrasses.Count; i++)
                {
                    if (Vector3.Distance(gridGrasses[i].worldPos, pos) < brushSensity)
                    {
                        isAlreadyExist = true;
                        break;
                    }
                }
            }
            if (!isAlreadyExist)
            {
                GrassPrototype gp = new GrassPrototype() {
                    worldPos = pos,
                    colorIndex = usingColorIndex,
                    scaleIndex = usingScaleIndex
                };
                grasses.Add(gp);
                AddGrass2Grid(gp);
                if (onChange != null)
                    onChange();
            }
        }
        //圆范围内
        public void RemoveGrass(Vector3 center, float radius)
        {
            for (int i = grasses.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(grasses[i].worldPos, center) < radius)
                {
                    grasses.RemoveAt(i);
                }
            }
        }
        public void ClearAllGrass()
        {
            grasses.Clear();
            if (onChange != null)
                onChange();
        }
        public void RandomGroup(Transform transform, int instanceCount)
        {
            grasses.Clear();
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

                GrassPrototype gp = new GrassPrototype()
                {
                    worldPos = new Vector3(pos.x, pos.y, pos.z),
                    colorIndex = usingColorIndex,
                    scaleIndex = usingScaleIndex
                };
                grasses.Add(gp);
                AddGrass2Grid(gp);
            }
            if (onChange != null)
                onChange();
        }
        public void RandomGroupBySensity(Transform transform, int instanceCount)
        {
            grasses.Clear();

            //auto keep density the same
            int wide = Mathf.RoundToInt(Mathf.Sqrt(instanceCount));
            int count = 0;
            Vector3 offset = transform.position + new Vector3(-wide / 2f * brushSensity, 0, -wide / 2f * brushSensity);
            for (int i = 0; i < wide && count < instanceCount; i++)
            {
                for (int j = 0; j < wide && count < instanceCount; j++)
                {
                    Vector3 pos = new Vector3(j * brushSensity, 0, i * brushSensity);
                    pos += offset;
                    GrassPrototype gp = new GrassPrototype()
                    {
                        worldPos = new Vector3(pos.x, pos.y, pos.z),
                        colorIndex = usingColorIndex,
                        scaleIndex = usingScaleIndex
                    };
                    grasses.Add(gp);
                    AddGrass2Grid(gp);
                    count++;
                }
            }
            if (onChange != null)
                onChange();
        }
#endif
        public void UpdateGrass()
        {
            if (onChange != null)
                onChange();
        }
    }
}

