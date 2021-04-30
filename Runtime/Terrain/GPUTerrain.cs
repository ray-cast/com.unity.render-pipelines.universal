using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor;
using Unity.Collections;

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
    public class GPUTerrain : MonoBehaviour
    {
        public Material material;
        public TerrainData terrainData;

        private Texture2D _normalMap;
        private RenderTexture _heightMap;

        private Bounds _boundingBox = new Bounds();
        private Plane[] _cameraFrustumPlanes = new Plane[6];
        private Vector4[] _cameraFrustumData = new Vector4[6];

        private Mesh _instancePatchMesh;

        private ComputeShader _cullingComputeShader;
        private ComputeBuffer _allInstancesPatchBuffer;
        private ComputeBuffer _visibleInstancesIndexBuffer;
        private ComputeBuffer _argsBuffer;
        private ComputeBuffer _shadowBuffer;

        private int _clearUniqueCounterKernel;
        private int _cullTerrainKernel;
        private int _cullTerrainShadowKernel;

        private bool _sholudUpdateTerrain;

        [SerializeField]
        private int drawInstancedCount = 0;
        private int patchSize = 64;
        private TerrainTree _terrainTree;
        private List<TerrainPatch> _terrainPatches;
        private TerrainData _cacheTerrainData;

        void InitializeNormalMap()
		{
            _heightMap = terrainData.heightmapTexture;

            var colors = new Color32[_heightMap.width * _heightMap.height];
            int index = 0;

            for (int i = 0; i < _heightMap.width; i++)
            {
                for (int j = 0; j < _heightMap.height; j++)
                {
                    var normal = terrainData.GetInterpolatedNormal((float)i / (_heightMap.width - 1), (float)j / (_heightMap.height - 1));
                    colors[index++] = new Color(normal.z * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.x * 0.5f + 0.5f);
                }
            }

            _normalMap = new Texture2D(_heightMap.width, _heightMap.height, TextureFormat.RGBA32, -1, true);
            _normalMap.SetPixels32(colors);
            _normalMap.Apply();
        }

        void InitializeTerrainData()
        {
            var rect = new Rect(0, 0, terrainData.size.x, terrainData.size.z);

            var chunkX = Mathf.CeilToInt(rect.width / patchSize);
            var chunkY = Mathf.CeilToInt(rect.height / patchSize);

            var written = 0;
            var children = new TerrainTree[chunkX * chunkY];

            for (var i = 0; i < rect.width; i += patchSize)
            {
                for (var j = 0; j < rect.height; j += patchSize)
                {
                    children[written++] = new TerrainTree(new Rect(i, j, patchSize, patchSize), 3);
                }
            }

            _boundingBox.min = new Vector3(rect.min.x, 0, rect.min.y);
            _boundingBox.max = new Vector3(rect.max.x, terrainData.size.y, rect.max.y);

            _terrainTree = new TerrainTree(rect);
            _terrainTree.children = children;
            _terrainPatches = new List<TerrainPatch>(_terrainTree.children.Length * 4);
        }

        void InitializeTerrainPatches(Vector3 camPos)
        {
            if (terrainData == null || material == null) return;

            var center = new Vector2(camPos.x, camPos.z);

            _terrainPatches.Clear();
            _terrainTree.CollectNodeInfo(center, _terrainPatches);

            var chunkX = Mathf.CeilToInt(terrainData.size.x / patchSize) * 4;
            var chunkY = Mathf.CeilToInt(terrainData.size.z / patchSize) * 4;

            var pathchFlag = new int[chunkX * chunkY];

            for (var i = 0; i < _terrainPatches.Count; i++)
            {
                var patch = _terrainPatches[i];
                if (patch.mip == 3)
				{
                    var centerX = Mathf.FloorToInt(patch.rect.x + patch.rect.z * 0.5f) / patchSize;
                    var centerY = Mathf.FloorToInt(patch.rect.y + patch.rect.w * 0.5f) / patchSize;

                    pathchFlag[centerY * chunkX + centerX] = patch.mip;
                }
                else if (patch.mip == 2)
				{
                    var centerX = Mathf.FloorToInt(patch.rect.x + patch.rect.z * 0.5f) / patchSize;
                    var centerY = Mathf.FloorToInt(patch.rect.y + patch.rect.w * 0.5f) / patchSize;

                    pathchFlag[centerY * chunkX + centerX] = patch.mip;
                }
            }

            for (var i = 0; i < _terrainPatches.Count; i++)
            {
                var patch = _terrainPatches[i];
                /*var nodeCenter = new Vector2(patch.rect.x + patch.rect.z * 0.5f, patch.rect.y + patch.rect.w * 0.5f);
                var topNode = _terrainTree.GetActiveNode(nodeCenter + new Vector2(0, patch.rect.w));
                var bottomNode = _terrainTree.GetActiveNode(nodeCenter + new Vector2(0, -patch.rect.w));
                var leftNode = _terrainTree.GetActiveNode(nodeCenter + new Vector2(-patch.rect.z, 0));
                var rightNode = _terrainTree.GetActiveNode(nodeCenter + new Vector2(patch.rect.z, 0));
                var nei = new bool4(topNode != null && topNode.mip > patch.mip,
                                              bottomNode != null && bottomNode.mip > patch.mip,
                                              leftNode != null && leftNode.mip > patch.mip,
                                              rightNode != null && rightNode.mip > patch.mip);
                
                patch.neighbor = (1 * (nei.x ? 1 : 0)) + ((1 << 1) * (nei.y ? 1 : 0)) + ((1 << 2) * (nei.z ? 1 : 0)) + ((1 << 3) * (nei.w ? 1 : 0));*/
                patch.neighbor = 0;
                _terrainPatches[i] = patch;
            }

            if (_visibleInstancesIndexBuffer == null || _visibleInstancesIndexBuffer != null && _visibleInstancesIndexBuffer.count < _terrainPatches.Count)
			{
                _visibleInstancesIndexBuffer = new ComputeBuffer(_terrainPatches.Count, sizeof(uint));

                if (material)
                    material.SetBuffer(ShaderConstants._VisibleInstancesIndexBuffer, _visibleInstancesIndexBuffer);
            }

            if (_allInstancesPatchBuffer == null || _allInstancesPatchBuffer != null && _allInstancesPatchBuffer.count < _terrainPatches.Count)
			{
                _allInstancesPatchBuffer = new ComputeBuffer(_terrainPatches.Count, Marshal.SizeOf<TerrainPatch>());

                if (material)
                    material.SetBuffer(ShaderConstants._AllInstancesPatchBuffer, _allInstancesPatchBuffer);
            }
            
            _allInstancesPatchBuffer.SetData(_terrainPatches.ToArray());
        }

        void UpdateTerrainIfNeeded()
		{
            if (_sholudUpdateTerrain || _cacheTerrainData != terrainData)
			{
                this.InitializeNormalMap();
                this.InitializeTerrainData();

                if (Camera.main)
                    this.InitializeTerrainPatches(Camera.main.transform.position);

                _sholudUpdateTerrain = false;
                _cacheTerrainData = terrainData;
            }
		}

        void UpdateTerrainPatchesIfNeeded(Vector3 camPos)
        {
            this.InitializeTerrainPatches(camPos);
        }

        void OnEnable()
        {
            _sholudUpdateTerrain = true;

            _instancePatchMesh = Resources.Load<Mesh>("Quad");

            _cullingComputeShader = Resources.Load<ComputeShader>("TerrainCulling");
            _cullTerrainKernel = this._cullingComputeShader.FindKernel("CullTerrain");
            _cullTerrainShadowKernel = this._cullingComputeShader.FindKernel("CullTerrainShadow");
            _clearUniqueCounterKernel = this._cullingComputeShader.FindKernel("ClearIndirectArgument");

            _cacheTerrainData = terrainData;

            uint[] args = new uint[5];
            args[0] = (uint)_instancePatchMesh.GetIndexCount(0);
            args[1] = (uint)0;
            args[2] = (uint)_instancePatchMesh.GetIndexStart(0);
            args[3] = (uint)_instancePatchMesh.GetBaseVertex(0);
            args[4] = 0;

            if (_argsBuffer != null) _argsBuffer.Release();
            if (_shadowBuffer != null) _shadowBuffer.Release();

            _argsBuffer = new ComputeBuffer(args.Length, sizeof(uint), ComputeBufferType.IndirectArguments);
            _argsBuffer.SetData(args);

            _shadowBuffer = new ComputeBuffer(args.Length, sizeof(uint), ComputeBufferType.IndirectArguments);
            _shadowBuffer.SetData(args);

            //ShadowUtils.CustomRenderShadowSlice += this.RenderShadowmap;
            DrawObjectsPass.ConfigureOpaqueAction += Configure;
        }

        void OnDisable()
        {
            if (_allInstancesPatchBuffer != null)
                _allInstancesPatchBuffer.Release();

            if (_visibleInstancesIndexBuffer != null)
                _visibleInstancesIndexBuffer.Release();

            if (_argsBuffer != null)
                _argsBuffer.Release();

            if (_shadowBuffer != null)
                _shadowBuffer.Release();

            if (_normalMap != null)
                DestroyImmediate(_normalMap);

            _terrainPatches = null;
            _allInstancesPatchBuffer = null;
            _visibleInstancesIndexBuffer = null;
            _argsBuffer = null;
            _shadowBuffer = null;
            _normalMap = null;

            //ShadowUtils.CustomRenderShadowSlice -= this.RenderShadowmap;
            DrawObjectsPass.ConfigureOpaqueAction -= Configure;
        }

        void Configure(ref CommandBuffer cmd, ref RenderingData renderingData)
        {
            UnityEngine.Assertions.Assert.IsNotNull(_cullingComputeShader);

            if (terrainData == null || material == null)
                return;

            if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
                return;

#if UNITY_EDITOR
            if (EditorApplication.isPlaying && renderingData.cameraData.isSceneViewCamera)
                return;
#endif

            ref var camera = ref renderingData.cameraData.camera;

            var hizRT = HizPass.GetHizTexture(ref camera);
            if (hizRT)
            {
                UpdateTerrainPatchesIfNeeded(camera.transform.position);

                cmd.SetComputeBufferParam(_cullingComputeShader, _clearUniqueCounterKernel, ShaderConstants._RWVisibleIndirectArgumentBuffer, _argsBuffer);
                cmd.DispatchCompute(_cullingComputeShader, _clearUniqueCounterKernel, 1, 1, 1);

                GeometryUtility.CalculateFrustumPlanes(camera, _cameraFrustumPlanes);

                for (int i = 0; i < _cameraFrustumPlanes.Length; i++)
				{
                    ref var plane = ref _cameraFrustumPlanes[i];
                    _cameraFrustumData[i].x = plane.normal.x;
                    _cameraFrustumData[i].y = plane.normal.y;
                    _cameraFrustumData[i].z = plane.normal.z;
                    _cameraFrustumData[i].w = plane.distance;
                }

                cmd.SetComputeFloatParam(_cullingComputeShader, ShaderConstants._OffsetParams, _terrainPatches.Count);

                cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._TerrainSize, terrainData.size);
                cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._HizTexture_Size, new Vector2(hizRT.width, hizRT.height));
                cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._HeightMapTexture_TexelSize, new Vector4(_heightMap.texelSize.x, _heightMap.texelSize.y, _heightMap.width, _heightMap.height));
                cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._CameraDrawParams, new Vector4(camera.fieldOfView, camera.nearClipPlane, camera.farClipPlane, 0));
                cmd.SetComputeVectorArrayParam(_cullingComputeShader, ShaderConstants._CameraFrustumPlanes, _cameraFrustumData);

                cmd.SetComputeMatrixParam(_cullingComputeShader, ShaderConstants._CameraViewProjection, camera.projectionMatrix * camera.worldToCameraMatrix);

                cmd.SetComputeTextureParam(_cullingComputeShader, _cullTerrainKernel, ShaderConstants._HeightMap, _heightMap);
                cmd.SetComputeTextureParam(_cullingComputeShader, _cullTerrainKernel, ShaderConstants._HizTexture, hizRT);

                cmd.SetComputeBufferParam(_cullingComputeShader, _cullTerrainKernel, ShaderConstants._AllInstancesPatchBuffer, _allInstancesPatchBuffer);
                cmd.SetComputeBufferParam(_cullingComputeShader, _cullTerrainKernel, ShaderConstants._RWVisibleInstancesIndexBuffer, _visibleInstancesIndexBuffer);
                cmd.SetComputeBufferParam(_cullingComputeShader, _cullTerrainKernel, ShaderConstants._RWVisibleIndirectArgumentBuffer, _argsBuffer);

                cmd.DispatchCompute(_cullingComputeShader, _cullTerrainKernel, Mathf.CeilToInt(_terrainPatches.Count / 64f), 1, 1);

#if UNITY_EDITOR
                uint[] counter = new uint[5];
                _argsBuffer.GetData(counter);
                drawInstancedCount = (int)counter[1];
#endif
            }
        }

        public void LateUpdate()
        {
            if (_instancePatchMesh && material)
			{
                this.UpdateTerrainIfNeeded();

#if UNITY_EDITOR
                material.SetTexture("_TerrainNormalMapTexture", _normalMap);
                material.SetTexture("_TerrainHeightMapTexture", _heightMap);

                material.SetVector("_TerrainParam", terrainData.size);
                material.SetVector("_TerrainHeightMapTexture_TexelSize", new Vector4(_heightMap.texelSize.x, _heightMap.texelSize.y, _heightMap.width, _heightMap.height));

                material.SetBuffer(ShaderConstants._AllInstancesPatchBuffer, _allInstancesPatchBuffer);
                material.SetBuffer(ShaderConstants._VisibleInstancesIndexBuffer, _visibleInstancesIndexBuffer);
#endif

                Graphics.DrawMeshInstancedIndirect(_instancePatchMesh, 0, material, _boundingBox, _argsBuffer);
            }
        }

        static class ShaderConstants
        {
            public static readonly int _CameraDrawParams = Shader.PropertyToID("_CameraDrawParams");
            public static readonly int _CameraFrustumPlanes = Shader.PropertyToID("_CameraFrustumPlanes");
            public static readonly int _CameraViewProjection = Shader.PropertyToID("_CameraViewProjection");

            public static readonly int _OffsetParams = Shader.PropertyToID("_OffsetParams");
            public static readonly int _TerrainSize = Shader.PropertyToID("_TerrainSize");

            public static readonly int _HeightMap = Shader.PropertyToID("_HeightMap");
            public static readonly int _HeightMapTexture_TexelSize = Shader.PropertyToID("_HeightMapTexture_TexelSize");

            public static readonly int _HizTexture = Shader.PropertyToID("_HizTexture");
            public static readonly int _HizTexture_Size = Shader.PropertyToID("_HizTexture_Size");

            public static readonly int _AllInstancesPatchBuffer = Shader.PropertyToID("_AllInstancesPatchBuffer");
            public static readonly int _VisibleInstancesIndexBuffer = Shader.PropertyToID("_VisibleInstancesIndexBuffer");

            public static readonly int _RWVisibleInstancesIndexBuffer = Shader.PropertyToID("_RWVisibleInstancesIndexBuffer");
            public static readonly int _RWVisibleIndirectArgumentBuffer = Shader.PropertyToID("_RWVisibleIndirectArgumentBuffer");
        }
    }
}