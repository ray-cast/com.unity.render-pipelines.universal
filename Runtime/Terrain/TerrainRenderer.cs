using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;

namespace UnityEngine.Rendering.Universal
{
    internal class bool4
    {
        public bool x;
        public bool y;
        public bool z;
        public bool w;

        public bool4(bool v1, bool v2, bool v3, bool v4)
        {
            this.x = v1;
            this.y = v2;
            this.z = v3;
            this.w = v4;
        }
    }

    [ExecuteAlways]
    public class TerrainRenderer : MonoBehaviour
    {
        public bool shouldOcclusionCulling = true;

        public Material instanceMaterial;
        public TerrainData terrainData;
        public TiledTexture virtualTexture;

        private Texture2D _normalMap;
        private Texture2D _lightMap;
        private RenderTexture _heightMap;

        private Bounds _boundingBox = new Bounds();
        private Plane[] _cameraFrustumPlanes = new Plane[6];
        private Vector4[] _cameraFrustumData = new Vector4[6];
        private Bounds _worldBoundingBox;
        private Matrix4x4 _localToWorldMatrix = Matrix4x4.zero;

        private Mesh _tileQuadMesh;
        private Material _tileMaterial;

        private Mesh _instancePatchMesh;

        private ComputeShader _computeShader;
        private ComputeBuffer _allInstancesPatchBuffer;
        private ComputeBuffer _visibleInstancesIndexBuffer;
        private ComputeBuffer _visibleShadowIndexBuffer;
        private ComputeBuffer _argsBuffer;
        private ComputeBuffer _shadowBuffer;

        private int _clearUniqueCounterKernel;
        private int _computeOcclusionCullingKernel;
        private int _computeFrustumCullingKernel;
        private int _maxComputeWorkGroupSize = 64;

        private bool _shouldUpdateTerrain;
        private bool _shouldUpdateTerrainPatches;
        private bool _shouldUpdateBoudingBox;

        private int _sectorID = int.MaxValue;
        private int _instanceCount;

        private TerrainTree _terrainTree;
        private TerrainData _terrainDataCache;
        private Dictionary<int, TerrainPatch[]> _terrainPatchesCaches;

#if UNITY_EDITOR
        public bool debugMode = false;
        public int drawInstancedCount = 0;
#endif

        public int instanceCount { get { return _instanceCount; } }

        void OnEnable()
        {
            _shouldUpdateTerrain = true;
            
            _instancePatchMesh = Resources.Load<Mesh>("Quad");

            _computeShader = Resources.Load<ComputeShader>("TerrainCulling");
            _computeFrustumCullingKernel = this._computeShader.FindKernel("ComputeFrustumCulling");
            _computeOcclusionCullingKernel = this._computeShader.FindKernel("ComputeOcclusionCulling");
            _clearUniqueCounterKernel = this._computeShader.FindKernel("ClearIndirectArgument");

            _sectorID = int.MaxValue;
            _terrainDataCache = terrainData;
            _terrainPatchesCaches = new Dictionary<int, TerrainPatch[]>();

            uint[] args = new uint[5];
            args[0] = (uint)_instancePatchMesh.GetIndexCount(0);
            args[1] = (uint)0;
            args[2] = (uint)_instancePatchMesh.GetIndexStart(0);
            args[3] = (uint)_instancePatchMesh.GetBaseVertex(0);
            args[4] = 0;

            _argsBuffer?.Dispose();
            _argsBuffer = new ComputeBuffer(args.Length, sizeof(uint), ComputeBufferType.IndirectArguments);
            _argsBuffer.SetData(args);

            _shadowBuffer?.Dispose();
            _shadowBuffer = new ComputeBuffer(args.Length, sizeof(uint), ComputeBufferType.IndirectArguments);
            _shadowBuffer.SetData(args);

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.beginFrameRendering += OnBeginFrameRendering;

            VirtualTextureSystem.beginTileRendering += OnBeginTileRendering;

            InitializeQuadMesh();

#if UNITY_EDITOR
            Lightmapping.bakeCompleted += OnBakeCompleted;
#endif
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginFrameRendering -= OnBeginFrameRendering;

            VirtualTextureSystem.beginTileRendering -= OnBeginTileRendering;

#if UNITY_EDITOR
            UnityEditor.Lightmapping.bakeCompleted -= OnBakeCompleted;
#endif

            _allInstancesPatchBuffer?.Dispose();
            _visibleInstancesIndexBuffer?.Dispose();
            _argsBuffer?.Dispose();
            _shadowBuffer?.Dispose();

            if (_normalMap != null)
                DestroyImmediate(_normalMap);

            if (_lightMap != null)
                DestroyImmediate(_lightMap);

            _allInstancesPatchBuffer = null;
            _visibleInstancesIndexBuffer = null;
            _argsBuffer = null;
            _shadowBuffer = null;
            _normalMap = null;
            _lightMap = null;
        }

        public void InitializeQuadMesh()
        {
            List<Vector3> quadVertexList = new List<Vector3>();
            List<int> quadTriangleList = new List<int>();
            List<Vector2> quadUVList = new List<Vector2>();

            quadVertexList.Add(new Vector3(0, 1, 0.1f));
            quadVertexList.Add(new Vector3(0, 0, 0.1f));
            quadVertexList.Add(new Vector3(1, 0, 0.1f));
            quadVertexList.Add(new Vector3(1, 1, 0.1f));

            quadUVList.Add(new Vector2(0, 0));
            quadUVList.Add(new Vector2(0, 1));
            quadUVList.Add(new Vector2(1, 1));
            quadUVList.Add(new Vector2(1, 0));

            quadTriangleList.Add(0);
            quadTriangleList.Add(1);
            quadTriangleList.Add(2);

            quadTriangleList.Add(2);
            quadTriangleList.Add(3);
            quadTriangleList.Add(0);

            _tileQuadMesh = new Mesh();
            _tileQuadMesh.SetVertices(quadVertexList);
            _tileQuadMesh.SetUVs(0, quadUVList);
            _tileQuadMesh.SetTriangles(quadTriangleList, 0);

            _tileMaterial = new Material(Shader.Find("VirtualTexture/DrawTexture"));
        }

        public void InitializeWorldBoundingBox()
        {
            if (transform.localToWorldMatrix != _localToWorldMatrix || _shouldUpdateBoudingBox)
            {
                _shouldUpdateBoudingBox = false;
                _localToWorldMatrix = transform.localToWorldMatrix;

                var m = _localToWorldMatrix.transpose;
                _worldBoundingBox.SetMinMax(new Vector3(m.m30, m.m31, m.m32), new Vector3(m.m30, m.m31, m.m32));

                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        float e = m[j * 4 + i] * _boundingBox.min[j];
                        float f = m[j * 4 + i] * _boundingBox.max[j];

                        var min = _worldBoundingBox.min;
                        var max = _worldBoundingBox.max;

                        if (e < f)
                        {
                            min[i] += e;
                            max[i] += f;
                        }
                        else
                        {
                            min[i] += f;
                            max[i] += e;
                        }

                        _worldBoundingBox.min = min;
                        _worldBoundingBox.max = max;
                    }
                }
            }
        }

        void InitializeNormalMap()
		{
            if (terrainData)
			{
                _heightMap = terrainData.heightmapTexture;

                var colors = new Color32[_heightMap.width * _heightMap.height];
                int index = 0;

                for (int j = 0; j < _heightMap.height; j++) 
                {
                    for (int i = 0; i < _heightMap.width; i++)
                    {
                        var normal = terrainData.GetInterpolatedNormal((float)i / (_heightMap.width - 1), (float)j / (_heightMap.height - 1));
                        colors[index++] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f);
                    }
                }

                _normalMap = new Texture2D(_heightMap.width, _heightMap.height, TextureFormat.RGB24, -1, true);
                _normalMap.SetPixels32(colors);
                _normalMap.Apply();
            }
			else
			{
                Texture2D.DestroyImmediate(_normalMap);
                _normalMap = null;
            }
        }

        void InitializeLightMap()
        {
            if (terrainData)
            {
                _heightMap = terrainData.heightmapTexture;

                var position = new Vector3[_heightMap.width * _heightMap.height];

                for (int j = 0; j < _heightMap.height; j++)
                {
                    var index = _heightMap.width * j;
                    var v = (float)j / (_heightMap.height - 1);
                    var z = v * terrainData.size.z;

                    for (int i = 0; i < _heightMap.width; i++)
                    {
                        var u = (float)i / (_heightMap.width - 1);
                        var x = u * terrainData.size.x;
                        var height = terrainData.GetInterpolatedHeight(u, v);

                        position[index + i] = transform.localToWorldMatrix.MultiplyPoint(new Vector3(x, height, z));
                    }
                }

                var colors = new Color32[_heightMap.width * _heightMap.height];
                var lightprobes = new SphericalHarmonicsL2[position.Length];
                LightProbes.CalculateInterpolatedLightAndOcclusionProbes(position, lightprobes, null);

                Color[] results = new Color[1];
                Vector3[] dirs = new Vector3[1];
                dirs[0].x = 0;
                dirs[0].y = 1;
                dirs[0].z = 0;

                for (int j = 0; j < _heightMap.height; j++)
                {
                    var index = _heightMap.width * j;

                    for (int i = 0; i < _heightMap.width; i++)
                    {
                        lightprobes[index + i].Evaluate(dirs, results);
                        colors[index + i] = results[0];
                    }
                }

                _lightMap = new Texture2D(_heightMap.width, _heightMap.height, TextureFormat.RGBA32, -1, true);
                _lightMap.SetPixels32(colors);
                _lightMap.Apply();
            }
            else
            {
                Texture2D.DestroyImmediate(_lightMap);
                _lightMap = null;
            }
        }

        void InitializeTerrainTree()
        {
			if (terrainData)
			{
				var rect = new Rect(0, 0, terrainData.size.x, terrainData.size.z);

                var written = 0;

                var chunkX = Mathf.CeilToInt(rect.width / ShaderConstants.chunkSize);
				var chunkY = Mathf.CeilToInt(rect.height / ShaderConstants.chunkSize);
				var children = new TerrainTree[chunkX * chunkY];
                
                for (var i = 0; i < rect.width; i += ShaderConstants.chunkSize)
				{
					for (var j = 0; j < rect.height; j += ShaderConstants.chunkSize)
					{
						children[written++] = new TerrainTree(new Rect(i, j, ShaderConstants.chunkSize, ShaderConstants.chunkSize), 3);
					}
				}

                _boundingBox.min = new Vector3(rect.min.x, 0, rect.min.y);
				_boundingBox.max = new Vector3(rect.max.x, terrainData.size.y, rect.max.y);

				_terrainTree = new TerrainTree(rect);
				_terrainTree.children = children;
                _terrainPatchesCaches.Clear();
            }
			else
			{
                _terrainPatchesCaches.Clear();
            }
        }

        void InitializeTerrainData()
        {
            if (_terrainDataCache != terrainData)
			{
                _shouldUpdateTerrain = true;
                _terrainDataCache = terrainData;
            }

            if (_shouldUpdateTerrain)
            {
                _shouldUpdateTerrain = false;
                _shouldUpdateBoudingBox = true;
                _shouldUpdateTerrainPatches = true;

                this.InitializeLightMap();
                this.InitializeNormalMap();
                this.InitializeTerrainTree();
            }
        }

        public struct NeighborJob : IJob
        {
            public int patchSize;
            public int patchX;
            public int patchY;
            public NativeArray<TerrainPatch> patches;
            public NativeArray<int> flags;

            public void Execute()
            {
                for (var i = 0; i < patches.Length; i++)
                {
                    var patch = patches[i];

                    int u = ((int)patch.rect.x) / patchSize;
                    int v = ((int)patch.rect.y) / patchSize;

                    int w = u + ((int)patch.rect.z) / patchSize;
                    int h = v + ((int)patch.rect.w) / patchSize;

                    for (int y = v; y < h; y++)
                    {
                        for (int x = u; x < w; x++)
                        {
                            flags[y * patchY + x] = patch.mip;
                        }
                    }
                }

                for (var i = 0; i < patches.Length; i++)
				{
                    var patch = patches[i];
                    int x = Mathf.FloorToInt(patch.rect.x + patch.rect.z / 2) / patchSize;
                    int y = Mathf.FloorToInt(patch.rect.y + patch.rect.w / 2) / patchSize;

                    int w = Mathf.FloorToInt(patch.rect.z) / patchSize;
                    int h = Mathf.FloorToInt(patch.rect.w) / patchSize;

                    var top = (y + h) * patchY + x;
                    var bottom = (y - h) * patchY + x;
                    var left = y * patchY + x - w;
                    var right = y * patchY + x + w;

                    var topNode = top > 0 && top < flags.Length ? flags[top] : 0;
                    var bottomNode = bottom > 0 && bottom < flags.Length ? flags[bottom] : 0;
                    var leftNode = left > 0 && left < flags.Length ? flags[left] : 0;
                    var rightNode = right > 0 && right < flags.Length ? flags[right] : 0;
                    var nei = new bool4(topNode > patch.mip, bottomNode > patch.mip, leftNode > patch.mip, rightNode > patch.mip);

                    patch.neighbor = (nei.x ? 1 : 0) + ((1 << 1) * (nei.y ? 1 : 0)) + ((1 << 2) * (nei.z ? 1 : 0)) + ((1 << 3) * (nei.w ? 1 : 0));

                    patches[i] = patch;
                }
            }
        }

        void InitializeTerrainPatches(Vector3 camPos)
        {
            if (terrainData == null) return;

            var chunkX = Mathf.CeilToInt(terrainData.size.x / ShaderConstants.chunkSize);
            var chunkZ = Mathf.CeilToInt(terrainData.size.z / ShaderConstants.chunkSize);
            var chunkPos = transform.worldToLocalMatrix.MultiplyPoint(camPos);

            int sectorX = Mathf.FloorToInt(chunkPos.x / ShaderConstants.chunkSize);
            int sectorZ = Mathf.FloorToInt(chunkPos.z / ShaderConstants.chunkSize);
            int sectorID = Mathf.Clamp(chunkZ * sectorZ + sectorX, 0, chunkZ * chunkX);

            if (_sectorID != sectorID)
			{
                _sectorID = sectorID;
                _shouldUpdateTerrainPatches = true;
			}
            
            if (!_terrainPatchesCaches.ContainsKey(_sectorID))
			{
                var patchSize = ShaderConstants.chunkSize >> 3;
                var patchX = ShaderConstants.chunkSize / patchSize * chunkX;
                var patchY = ShaderConstants.chunkSize / patchSize * chunkZ;
                var patches = new List<TerrainPatch>();
                var pathchFlag = new NativeArray<int>(patchX * patchY, Allocator.Temp);

                _terrainTree.CollectNodeInfo(new Vector2(chunkPos.x, chunkPos.z), ShaderConstants.chunkSize, patches);

                for (var i = 0; i < patches.Count; i++)
                {
                    var patch = patches[i];

                    int u = ((int)patch.rect.x) / patchSize;
                    int v = ((int)patch.rect.y) / patchSize;

                    int w = u + ((int)patch.rect.z) / patchSize;
                    int h = v + ((int)patch.rect.w) / patchSize;

                    for (int y = v; y < h; y++)
                    {
                        for (int x = u; x < w; x++)
                            pathchFlag[y * patchY + x] = patch.mip;
                    }
                }

                for (var i = 0; i < patches.Count; i++)
                {
                    var patch = patches[i];
                    int x = Mathf.FloorToInt(patch.rect.x + patch.rect.z / 2) / patchSize;
                    int y = Mathf.FloorToInt(patch.rect.y + patch.rect.w / 2) / patchSize;

                    int w = Mathf.FloorToInt(patch.rect.z) / patchSize;
                    int h = Mathf.FloorToInt(patch.rect.w) / patchSize;

                    var top = (y + h) * patchY + x;
                    var bottom = (y - h) * patchY + x;
                    var left = y * patchY + x - w;
                    var right = y * patchY + x + w;

                    var topNode = top > 0 && top < pathchFlag.Length ? pathchFlag[top] : 0;
                    var bottomNode = bottom > 0 && bottom < pathchFlag.Length ? pathchFlag[bottom] : 0;
                    var leftNode = left > 0 && left < pathchFlag.Length ? pathchFlag[left] : 0;
                    var rightNode = right > 0 && right < pathchFlag.Length ? pathchFlag[right] : 0;
                    var nei = new bool4(topNode > patch.mip, bottomNode > patch.mip, leftNode > patch.mip, rightNode > patch.mip);

                    patch.neighbor = (nei.x ? 1 : 0) + ((1 << 1) * (nei.y ? 1 : 0)) + ((1 << 2) * (nei.z ? 1 : 0)) + ((1 << 3) * (nei.w ? 1 : 0));

                    patches[i] = patch;
                }

                _terrainPatchesCaches.Add(_sectorID, patches.ToArray());

                pathchFlag.Dispose();
            }
        }

        void UpdateTerrainPatches()
        {
            if (_shouldUpdateTerrainPatches)
            {
                if (_terrainPatchesCaches.TryGetValue(_sectorID, out var instancePatches))
				{
                    _instanceCount = instancePatches.Length;

                    if (_visibleInstancesIndexBuffer == null || _visibleInstancesIndexBuffer != null && _visibleInstancesIndexBuffer.count < instancePatches.Length)
                    {
                        _visibleInstancesIndexBuffer?.Dispose();
                        _visibleInstancesIndexBuffer = new ComputeBuffer(Mathf.CeilToInt(instancePatches.Length / (float)_maxComputeWorkGroupSize) * _maxComputeWorkGroupSize, sizeof(uint));

                        if (instanceMaterial)
                            instanceMaterial.SetBuffer(ShaderConstants._VisibleInstancesIndexBuffer, _visibleInstancesIndexBuffer);
                    }

                    if (_allInstancesPatchBuffer == null || _allInstancesPatchBuffer != null && _allInstancesPatchBuffer.count < instancePatches.Length)
                    {
                        _allInstancesPatchBuffer?.Dispose();
                        _allInstancesPatchBuffer = new ComputeBuffer(Mathf.CeilToInt(instancePatches.Length / (float)_maxComputeWorkGroupSize) * _maxComputeWorkGroupSize, Marshal.SizeOf<TerrainPatch>());

                        if (instanceMaterial)
                            instanceMaterial.SetBuffer(ShaderConstants._AllInstancesPatchBuffer, _allInstancesPatchBuffer);
                    }

                    _allInstancesPatchBuffer.SetData(instancePatches);

                    if (_visibleShadowIndexBuffer == null || _visibleShadowIndexBuffer != null && _visibleShadowIndexBuffer.count < instancePatches.Length)
                    {
                        _visibleShadowIndexBuffer?.Dispose();
                        _visibleShadowIndexBuffer = new ComputeBuffer(Mathf.CeilToInt(instancePatches.Length / (float)_maxComputeWorkGroupSize) * _maxComputeWorkGroupSize, sizeof(uint));

                        if (instanceMaterial)
                            instanceMaterial.SetBuffer(ShaderConstants._VisibleShadowIndexBuffer, _visibleShadowIndexBuffer);
                    }

                    uint[] shadowIndex = new uint[instancePatches.Length];
                    for (uint i = 0; i < instancePatches.Length; i++)
                        shadowIndex[i] = i;

                    _visibleShadowIndexBuffer.SetData(shadowIndex);

                    uint[] args = new uint[5];
                    args[0] = (uint)_instancePatchMesh.GetIndexCount(0);
                    args[1] = (uint)_instanceCount;
                    args[2] = (uint)_instancePatchMesh.GetIndexStart(0);
                    args[3] = (uint)_instancePatchMesh.GetBaseVertex(0);
                    args[4] = 0;

                    _shadowBuffer?.Dispose();
                    _shadowBuffer = new ComputeBuffer(args.Length, sizeof(uint), ComputeBufferType.IndirectArguments);
                    _shadowBuffer.SetData(args);
                }
                else
				{
                    _instanceCount = 0;
                }

                _shouldUpdateTerrainPatches = false;
            }
        }

        private void OnBeginTileRendering(RequestPageData request, TiledTexture tileTexture, Vector2Int tile)
		{
            int x = request.pageX;
            int y = request.pageY;
            int perSize = (int)Mathf.Pow(2, request.mipLevel);

            x = x - x % perSize;
            y = y - y % perSize;

            this.virtualTexture = tileTexture;

            var tileRect = tileTexture.TileToRect(tile);

            var pageSize = VirtualTextureSystem.instance.pageSize;
            var RealTotalRect = new Rect(_worldBoundingBox.min.x, _worldBoundingBox.min.z, _worldBoundingBox.size.x, _worldBoundingBox.size.z);
            var paddingEffect = tileTexture.paddingSize * perSize * (RealTotalRect.width / pageSize) / tileTexture.tileSize;
            var realRect = new Rect(RealTotalRect.xMin + (float)x / pageSize * RealTotalRect.width - paddingEffect,
                                     RealTotalRect.yMin + (float)y / pageSize * RealTotalRect.height - paddingEffect,
                                     RealTotalRect.width / pageSize * perSize + 2f * paddingEffect,
                                     RealTotalRect.width / pageSize * perSize + 2f * paddingEffect);
            var terRect = Rect.zero;
            terRect.xMin = transform.position.x;
            terRect.yMin = transform.position.z;
            terRect.width = terrainData.size.x;
            terRect.height = terrainData.size.z;

            var needDrawRect = realRect;
            needDrawRect.xMin = Mathf.Max(realRect.xMin, terRect.xMin);
            needDrawRect.yMin = Mathf.Max(realRect.yMin, terRect.yMin);
            needDrawRect.xMax = Mathf.Min(realRect.xMax, terRect.xMax);
            needDrawRect.yMax = Mathf.Min(realRect.yMax, terRect.yMax);
            var scaleFactor = tileRect.width / realRect.width;

            var position = new Rect(tileRect.x + (needDrawRect.xMin - realRect.xMin) * scaleFactor,
                                    tileRect.y + (needDrawRect.yMin - realRect.yMin) * scaleFactor,
                                    needDrawRect.width * scaleFactor,
                                    needDrawRect.height * scaleFactor);

            var scaleOffset = new Vector4(
                        needDrawRect.width / terRect.width,
                        needDrawRect.height / terRect.height,
                        (needDrawRect.xMin - terRect.xMin) / terRect.width,
                        (needDrawRect.yMin - terRect.yMin) / terRect.height);

            float l = position.x * 2.0f / tileTexture.width - 1;
            float b = position.y * 2.0f / tileTexture.height - 1;
            float r = (position.x + position.width) * 2.0f / tileTexture.width - 1;
            float t = (position.y + position.height) * 2.0f / tileTexture.height - 1;
            /*var mat = new Matrix4x4();
            mat.m00 = r - l;
            mat.m03 = l;
            mat.m11 = t - b;
            mat.m13 = b;
            mat.m23 = -1;
            mat.m33 = 1;*/

            var tileX = tileRect.x / (float)tileTexture.width * 2 - 1;
            var tileY = 1 - (tileRect.y + tileRect.height) / (float)tileTexture.height * 2;

            var width = tileRect.width * 2 / (float)tileTexture.width;
            var height = tileRect.height * 2 / (float)tileTexture.height;

            var mat = Matrix4x4.TRS(new Vector3(tileX, tileY, 0), Quaternion.identity, new Vector3(width, height, 0));
            //var mat2 = Matrix4x4.Ortho(35, tileTexture.regionSize.x, 35, tileTexture.regionSize.y, 0.01f, 1000);

            var _Control_ST = instanceMaterial.GetVector("_Control_ST");
            var _Splat0_ST = instanceMaterial.GetVector("_Splat0_ST");
            var _Splat1_ST = instanceMaterial.GetVector("_Splat1_ST");
            var _Splat2_ST = instanceMaterial.GetVector("_Splat2_ST");
            var _Splat3_ST = instanceMaterial.GetVector("_Splat3_ST");

            _tileMaterial.SetMatrix("_ImageMVP", GL.GetGPUProjectionMatrix(mat, false));

            _tileMaterial.SetTexture("_Control", instanceMaterial.GetTexture("_Control"));
            _tileMaterial.SetVector("_Control_ST", new Vector4(_Control_ST.x * scaleOffset.x, _Control_ST.y * scaleOffset.y, _Control_ST.x * scaleOffset.z, _Control_ST.y * scaleOffset.w));

            _tileMaterial.SetTexture("_Splat0", instanceMaterial.GetTexture("_Splat0"));
            _tileMaterial.SetTexture("_Splat1", instanceMaterial.GetTexture("_Splat1"));
            _tileMaterial.SetTexture("_Splat2", instanceMaterial.GetTexture("_Splat2"));
            _tileMaterial.SetTexture("_Splat3", instanceMaterial.GetTexture("_Splat3"));
            _tileMaterial.SetVector("_Splat0_ST", new Vector4(_Splat0_ST.x * scaleOffset.x, _Splat0_ST.y * scaleOffset.y, _Splat0_ST.x * scaleOffset.z, _Splat0_ST.y * scaleOffset.w));
            _tileMaterial.SetVector("_Splat1_ST", new Vector4(_Splat1_ST.x * scaleOffset.x, _Splat1_ST.y * scaleOffset.y, _Splat1_ST.x * scaleOffset.z, _Splat1_ST.y * scaleOffset.w));
            _tileMaterial.SetVector("_Splat2_ST", new Vector4(_Splat2_ST.x * scaleOffset.x, _Splat2_ST.y * scaleOffset.y, _Splat2_ST.x * scaleOffset.z, _Splat2_ST.y * scaleOffset.w));
            _tileMaterial.SetVector("_Splat3_ST", new Vector4(_Splat3_ST.x * scaleOffset.x, _Splat0_ST.y * scaleOffset.y, _Splat3_ST.x * scaleOffset.z, _Splat3_ST.y * scaleOffset.w));

            if (instanceMaterial.IsKeywordEnabled("_NORMALMAP"))
            {
                _tileMaterial.EnableKeyword("_NORMALMAP");

                _tileMaterial.SetTexture("_Normal0", instanceMaterial.GetTexture("_Normal0"));
                _tileMaterial.SetTexture("_Normal1", instanceMaterial.GetTexture("_Normal1"));
                _tileMaterial.SetTexture("_Normal2", instanceMaterial.GetTexture("_Normal2"));
                _tileMaterial.SetTexture("_Normal3", instanceMaterial.GetTexture("_Normal3"));

                _tileMaterial.SetFloat("_BumpScale0", instanceMaterial.GetFloat("_BumpScale0"));
                _tileMaterial.SetFloat("_BumpScale1", instanceMaterial.GetFloat("_BumpScale1"));
                _tileMaterial.SetFloat("_BumpScale2", instanceMaterial.GetFloat("_BumpScale2"));
                _tileMaterial.SetFloat("_BumpScale3", instanceMaterial.GetFloat("_BumpScale3"));
            }

            var cmd = CommandBufferPool.Get();
            cmd.SetRenderTarget(tileTexture.tileBuffers, tileTexture.tileDepthBuffer);
            cmd.DrawMesh(_tileQuadMesh, Matrix4x4.identity, _tileMaterial, 0);

            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void OnBeginFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            this.InitializeTerrainData();
            this.InitializeWorldBoundingBox();
            
            if (_instancePatchMesh && terrainData && instanceMaterial)
            {
                instanceMaterial.EnableKeyword("PROCEDURAL_INSTANCING_ON");
                instanceMaterial.SetInt(ShaderConstants._unity_BaseInstanceID, 0);
                instanceMaterial.SetMatrix(ShaderConstants._PivotMatrixWS, transform.localToWorldMatrix);

#if UNITY_EDITOR
                instanceMaterial.SetVector(ShaderConstants._TerrainSize, terrainData.size);
                instanceMaterial.SetVector(ShaderConstants._TerrainHeightMap_TexelSize, new Vector4(_heightMap.texelSize.x, _heightMap.texelSize.y, _heightMap.width, _heightMap.height));

                instanceMaterial.SetTexture(ShaderConstants._TerrainLightMap, _lightMap);
                instanceMaterial.SetTexture(ShaderConstants._TerrainNormalMap, _normalMap);
                instanceMaterial.SetTexture(ShaderConstants._TerrainHeightMap, _heightMap);

                instanceMaterial.SetBuffer(ShaderConstants._AllInstancesPatchBuffer, _allInstancesPatchBuffer);
                instanceMaterial.SetBuffer(ShaderConstants._VisibleInstancesIndexBuffer, _visibleInstancesIndexBuffer);
                instanceMaterial.SetBuffer(ShaderConstants._VisibleShadowIndexBuffer, _visibleShadowIndexBuffer);
#endif

                foreach (var camera in cameras)
                {
                    if (camera.cameraType == CameraType.Preview)
                        return;

                    int mask = camera.cullingMask & (1 << gameObject.layer);
                    if (mask == 0)
                        return;

                    Graphics.DrawMeshInstancedIndirect(
                                _instancePatchMesh,
                                0,
                                instanceMaterial,
                                _worldBoundingBox,
                                _shadowBuffer,
                                0,
                                null,
                                ShadowCastingMode.ShadowsOnly,
                                false,
                                this.gameObject.layer,
                                camera,
                                LightProbeUsage.Off
                        );

                    Graphics.DrawMeshInstancedIndirect(
                                _instancePatchMesh,
                                0,
                                instanceMaterial,
                                _worldBoundingBox,
                                _argsBuffer,
                                0,
                                null,
                                ShadowCastingMode.Off,
                                false,
                                this.gameObject.layer,
                                camera,
                                LightProbeUsage.Off
                        );
                }
            }
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
#if UNITY_EDITOR
            bool componentExists = camera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData);
            if (EditorApplication.isPlaying && (camera.cameraType != CameraType.Game || !componentExists) || camera.cameraType == CameraType.Preview)
                return;
#endif

            int mask = camera.cullingMask & (1 << gameObject.layer);
            if (terrainData == null || mask == 0)
                return;

            var cullingMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix * transform.localToWorldMatrix;
            GeometryUtility.CalculateFrustumPlanes(cullingMatrix, _cameraFrustumPlanes);

            if (!GeometryUtility.TestPlanesAABB(_cameraFrustumPlanes, _boundingBox))
                return;

            if (camera)
            {
                this.InitializeTerrainPatches(camera.transform.position);
                this.UpdateTerrainPatches();
            }

            if (_instanceCount == 0)
                return;

            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._configureTag);
            {
                float near = camera.nearClipPlane;
                float far = camera.farClipPlane;
                float invNear = Mathf.Approximately(near, 0.0f) ? 0.0f : 1.0f / near;
                float invFar = Mathf.Approximately(far, 0.0f) ? 0.0f : 1.0f / far;
                float isOrthographic = camera.orthographic ? 1.0f : 0.0f;
                float zc0 = 1.0f - far * invNear;
                float zc1 = far * invNear;

                Vector4 zBufferParams = new Vector4(zc0, zc1, zc0 * invFar, zc1 * invFar);

                if (SystemInfo.usesReversedZBuffer)
                {
                    zBufferParams.y += zBufferParams.x;
                    zBufferParams.x = -zBufferParams.x;
                    zBufferParams.w += zBufferParams.z;
                    zBufferParams.z = -zBufferParams.z;
                }

                for (int i = 0; i < _cameraFrustumPlanes.Length; i++)
                {
                    ref var plane = ref _cameraFrustumPlanes[i];
                    _cameraFrustumData[i].x = plane.normal.x;
                    _cameraFrustumData[i].y = plane.normal.y;
                    _cameraFrustumData[i].z = plane.normal.z;
                    _cameraFrustumData[i].w = plane.distance;
                }
                
                var depthEstimation = HizPass.GetDepthEstimation(ref camera);
                var occlusionKernel = depthEstimation.hizTexture && this.shouldOcclusionCulling ? _computeOcclusionCullingKernel : _computeFrustumCullingKernel;
                var tanFov = 1.0f / Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);

                cmd.SetComputeBufferParam(_computeShader, _clearUniqueCounterKernel, ShaderConstants._RWVisibleIndirectArgumentBuffer, _argsBuffer);
                cmd.DispatchCompute(_computeShader, _clearUniqueCounterKernel, 1, 1, 1);

                cmd.SetComputeIntParam(_computeShader, ShaderConstants._OffsetParams, _instanceCount);

                cmd.SetComputeVectorParam(_computeShader, ShaderConstants._TerrainSize, terrainData.size);
                cmd.SetComputeVectorParam(_computeShader, ShaderConstants._HeightMapTexture_TexelSize, new Vector4(_heightMap.texelSize.x, _heightMap.texelSize.y, _heightMap.width, _heightMap.height));

                cmd.SetComputeVectorParam(_computeShader, ShaderConstants._CameraZBufferParams, zBufferParams);
                cmd.SetComputeVectorParam(_computeShader, ShaderConstants._CameraDrawParams, new Vector4(tanFov, 64, camera.nearClipPlane, camera.farClipPlane));
                cmd.SetComputeVectorArrayParam(_computeShader, ShaderConstants._CameraFrustumPlanes, _cameraFrustumData);
                cmd.SetComputeMatrixParam(_computeShader, ShaderConstants._CameraViewMatrix, depthEstimation.viewMatrix * transform.localToWorldMatrix);
                cmd.SetComputeMatrixParam(_computeShader, ShaderConstants._CameraViewProjection, depthEstimation.viewProjectionMatrix * transform.localToWorldMatrix);

                cmd.SetComputeTextureParam(_computeShader, occlusionKernel, ShaderConstants._HeightMap, _heightMap);
                cmd.SetComputeBufferParam(_computeShader, occlusionKernel, ShaderConstants._AllInstancesPatchBuffer, _allInstancesPatchBuffer);
                cmd.SetComputeBufferParam(_computeShader, occlusionKernel, ShaderConstants._RWVisibleInstancesIndexBuffer, _visibleInstancesIndexBuffer);
                cmd.SetComputeBufferParam(_computeShader, occlusionKernel, ShaderConstants._RWVisibleIndirectArgumentBuffer, _argsBuffer);

                if (occlusionKernel == _computeOcclusionCullingKernel)
                {
                    cmd.SetComputeTextureParam(_computeShader, occlusionKernel, ShaderConstants._HizTexture, depthEstimation.hizTexture);
                    cmd.SetComputeVectorParam(_computeShader, ShaderConstants._HizTexture_Size, new Vector2(depthEstimation.hizTexture.width, depthEstimation.hizTexture.height));
                }
                
                cmd.DispatchCompute(_computeShader, occlusionKernel, Mathf.CeilToInt(_instanceCount / (float)_maxComputeWorkGroupSize), 1, 1);

#if UNITY_EDITOR
                if (this.debugMode)
                    cmd.RequestAsyncReadback(_argsBuffer, OnAsyncGPUReadbackRequest);
#endif
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

#if UNITY_EDITOR
        public void OnAsyncGPUReadbackRequest(AsyncGPUReadbackRequest req)
        {
            var data = req.GetData<uint>(0);
            drawInstancedCount = (int)data[1];
        }
#endif

        private void OnBakeCompleted()
        {
            _shouldUpdateTerrain = true;
        }

		private void Update()
		{
		}

		static class ShaderConstants
        {
            public const int chunkSize = 8;

            public const string _configureTag = "Setup Terrain Instances";

            public static readonly int _unity_BaseInstanceID = Shader.PropertyToID("unity_BaseInstanceID");

            public static readonly int _CameraDrawParams = Shader.PropertyToID("_CameraDrawParams");
            public static readonly int _CameraViewMatrix = Shader.PropertyToID("_CameraViewMatrix"); 
            public static readonly int _CameraViewProjection = Shader.PropertyToID("_CameraViewProjection");
            public static readonly int _CameraZBufferParams = Shader.PropertyToID("_CameraZBufferParams");
            public static readonly int _CameraFrustumPlanes = Shader.PropertyToID("_CameraFrustumPlanes");

            public static readonly int _OffsetParams = Shader.PropertyToID("_OffsetParams");
            public static readonly int _PivotMatrixWS = Shader.PropertyToID("_PivotMatrixWS");

            public static readonly int _HeightMap = Shader.PropertyToID("_HeightMap");
            public static readonly int _HeightMapTexture_TexelSize = Shader.PropertyToID("_HeightMapTexture_TexelSize");

            public static readonly int _TerrainSize = Shader.PropertyToID("_TerrainSize");
            public static readonly int _TerrainLightMap = Shader.PropertyToID("_TerrainLightMap");
            public static readonly int _TerrainNormalMap = Shader.PropertyToID("_TerrainNormalMap");
            public static readonly int _TerrainHeightMap = Shader.PropertyToID("_TerrainHeightMap");
            public static readonly int _TerrainHeightMap_TexelSize = Shader.PropertyToID("_TerrainHeightMap_TexelSize");

            public static readonly int _HizTexture = Shader.PropertyToID("_HizTexture");
            public static readonly int _HizTexture_Size = Shader.PropertyToID("_HizTexture_Size");

            public static readonly int _AllInstancesPatchBuffer = Shader.PropertyToID("_AllInstancesPatchBuffer");
            public static readonly int _VisibleInstancesIndexBuffer = Shader.PropertyToID("_VisibleInstancesIndexBuffer");
            public static readonly int _VisibleShadowIndexBuffer = Shader.PropertyToID("_VisibleShadowIndexBuffer");

            public static readonly int _RWVisibleInstancesIndexBuffer = Shader.PropertyToID("_RWVisibleInstancesIndexBuffer");
            public static readonly int _RWVisibleIndirectArgumentBuffer = Shader.PropertyToID("_RWVisibleIndirectArgumentBuffer");
        }
    }
}