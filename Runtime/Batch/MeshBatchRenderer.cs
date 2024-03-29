﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEditor;

namespace UnityEngine.Rendering.Universal
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    public class MeshBatchRenderer : MonoBehaviour
    {
        public bool isCpuCulling = true;
        public bool isGpuCulling = true;

        public float mipScaleLevel = 16;
        public float distanceCulling = 0.8f;
        public float maxDrawDistance = 25;

        public Material instanceMaterial;
        public MeshBatchData instanceBatchData;

        private Mesh _instanceMesh;
        private MeshFilter _meshFilter;
        private MeshBatchChunk[] _chunks;
        private const float _chunkSizeX = 10;
        private const float _chunkSizeZ = 10;
        private Bounds _boundingBox = new Bounds();
        private Bounds _worldBoundingBox;
        private Matrix4x4 _localToWorldMatrix = Matrix4x4.zero;

        private bool _shouldBatchDispatch = true;
        private bool _shouldUpdateInstanceData = false;
        private List<int> _visibleChunkList = new List<int>();
        private Plane[] _cameraFrustumPlanes = new Plane[6];
        private Vector4[] _cameraFrustumData = new Vector4[6];

        private ComputeShader _cullingComputeShader;
        private ComputeBuffer _allBatchDataBuffer;
        private ComputeBuffer _allBatchPositionBuffer;
        private ComputeBuffer _allBatchVisibleIndexBuffer;
        private ComputeBuffer _argsBuffer;

        private int _clearUniqueCounterKernel;
        private int _computeFrustumCullingKernel;
        private int _computeOcclusionCullingKernel;
        private int _maxComputeWorkGroupSize = 64;

#if UNITY_EDITOR
        public bool debugMode;
        public int drawInstancedCount;
#endif

        internal enum MeshBatchProfile
        {
            FrustumCulling,
            Dispatch
        }
        private void Start()
        {
            _shouldUpdateInstanceData = true;
            _meshFilter = GetComponent<MeshFilter>();
            _cullingComputeShader = Resources.Load<ComputeShader>("CullingCompute");
            _clearUniqueCounterKernel = _cullingComputeShader.FindKernel("ClearIndirectArgument");
            _computeFrustumCullingKernel = _cullingComputeShader.FindKernel("ComputeFrustumCulling");
            _computeOcclusionCullingKernel = _cullingComputeShader.FindKernel("ComputeOcclusionCulling");
            if (instanceBatchData == null)
                instanceBatchData = new MeshBatchData();
            instanceBatchData.onUploadMeshData += UploadBatchData;
        }
        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.beginFrameRendering += OnBeginFrameRendering;
#if UNITY_EDITOR
            UnityEditor.Lightmapping.bakeCompleted += OnBakeCompleted;
#endif
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginFrameRendering -= OnBeginFrameRendering;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
#if UNITY_EDITOR
            UnityEditor.Lightmapping.bakeCompleted -= OnBakeCompleted;
#endif
        }
        public void OnDestroy()
        {
            instanceBatchData.onUploadMeshData -= UploadBatchData;
            _allBatchDataBuffer?.Dispose();
            _allBatchPositionBuffer?.Dispose();
            _allBatchVisibleIndexBuffer?.Dispose();
            _argsBuffer?.Dispose();

            _shouldUpdateInstanceData = false;
            _allBatchDataBuffer = null;
            _allBatchPositionBuffer = null;
            _allBatchVisibleIndexBuffer = null;
            _argsBuffer = null;
        }

        public void UploadBatchData()
        {
            _shouldUpdateInstanceData = true;
        }

        void InitializeInstanceGridConstants()
        {
            ref var allBatchPos = ref instanceBatchData.instanceData;

            _boundingBox.SetMinMax(Vector3.positiveInfinity, Vector3.negativeInfinity);
            for (int i = 0; i < allBatchPos.Count; i++)
                _boundingBox.Encapsulate(allBatchPos[i].worldPos);

            var max = _boundingBox.max;
            var min = _boundingBox.min;
            var cellCountX = Mathf.Max(1, Mathf.CeilToInt((max.x - min.x) / _chunkSizeX));
            var cellCountZ = Mathf.Max(1, Mathf.CeilToInt((max.z - min.z) / _chunkSizeZ));

            _chunks = new MeshBatchChunk[cellCountX * cellCountZ];

            for (int i = 0; i < _chunks.Length; i++)
            {
                Vector3 centerPosWS = new Vector3(i % cellCountX + 0.5f, 0, i / cellCountX + 0.5f);
                centerPosWS.x = Mathf.Lerp(min.x, max.x, centerPosWS.x / cellCountX);
                centerPosWS.z = Mathf.Lerp(min.z, max.z, centerPosWS.z / cellCountZ);
                _chunks[i] = new MeshBatchChunk(centerPosWS, _chunkSizeX, _chunkSizeZ);
            }

            for (int i = 0; i < allBatchPos.Count; i++)
            {
                BatchData gp = allBatchPos[i];

                int xID = Mathf.Min(cellCountX - 1, Mathf.FloorToInt(Mathf.InverseLerp(min.x, max.x, gp.worldPos.x) * cellCountX));
                int zID = Mathf.Min(cellCountZ - 1, Mathf.FloorToInt(Mathf.InverseLerp(min.z, max.z, gp.worldPos.z) * cellCountZ));

                _chunks[xID + zID * cellCountX].Append(gp);
            }

            Vector3[] allMeshPosWSSortedByCell = new Vector3[allBatchPos.Count];
            BatchData[] allMeshDataSortedByCell = new BatchData[allBatchPos.Count];

            for (int i = 0, offset = 0; i < _chunks.Length; i++)
            {
                for (int j = 0; j < _chunks[i].data.Count; j++)
                {
                    allMeshPosWSSortedByCell[offset] = _chunks[i].data[j].worldPos;
                    allMeshDataSortedByCell[offset] = _chunks[i].data[j];
                    offset++;
                }
            }

            var lightprobes = new SphericalHarmonicsL2[allBatchPos.Count];
            LightProbes.CalculateInterpolatedLightAndOcclusionProbes(allMeshPosWSSortedByCell, lightprobes, null);

            Color[] results = new Color[1];
            Vector3[] dirs = new Vector3[1];
            dirs[0].x = 0;
            dirs[0].y = 1;
            dirs[0].z = 0;

            for (int i = 0; i < allMeshPosWSSortedByCell.Length; i++)
			{
                results[0] = Color.black;
                lightprobes[i].Evaluate(dirs, results);

                allMeshDataSortedByCell[i].worldPos.w = results[0].grayscale;
            }

#if UNITY_EDITOR
            var threadGroupCount = Mathf.CeilToInt(allBatchPos.Count / (float)_maxComputeWorkGroupSize * 2) * _maxComputeWorkGroupSize;
#else
            var threadGroupCount = Mathf.CeilToInt(allBatchPos.Count / (float)_maxComputeWorkGroupSize) * _maxComputeWorkGroupSize;
#endif

            if (_allBatchPositionBuffer == null || _allBatchPositionBuffer != null && _allBatchPositionBuffer.count < allBatchPos.Count)
            {
                _allBatchPositionBuffer?.Dispose();
                _allBatchPositionBuffer = new ComputeBuffer(threadGroupCount, Marshal.SizeOf<Vector3>());
            }

            _allBatchPositionBuffer.SetData(allMeshPosWSSortedByCell);

            if (_allBatchDataBuffer == null || _allBatchDataBuffer != null && _allBatchDataBuffer.count < allBatchPos.Count)
            {
                _allBatchDataBuffer?.Dispose();
                _allBatchDataBuffer = new ComputeBuffer(threadGroupCount, Marshal.SizeOf<BatchData>());
            }

            _allBatchDataBuffer.SetData(allMeshDataSortedByCell);

            if (_allBatchVisibleIndexBuffer == null || _allBatchVisibleIndexBuffer != null && _allBatchVisibleIndexBuffer.count < allBatchPos.Count)
            {
                _allBatchVisibleIndexBuffer?.Dispose();
                _allBatchVisibleIndexBuffer = new ComputeBuffer(threadGroupCount, sizeof(uint));
            }
        }

        void InitializeInstanceMesh()
        {
            if (_argsBuffer == null && _meshFilter.sharedMesh != null || _meshFilter.sharedMesh != _instanceMesh)
            {
                _instanceMesh = _meshFilter.sharedMesh;

                if (_instanceMesh)
				{
                    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
                    args[0] = _instanceMesh.GetIndexCount(0);
                    args[1] = (uint)instanceBatchData.instanceData.Count;
                    args[2] = _instanceMesh.GetIndexStart(0);
                    args[3] = _instanceMesh.GetBaseVertex(0);
                    args[4] = 0;

                    _argsBuffer?.Dispose();
                    _argsBuffer = new ComputeBuffer(args.Length, sizeof(uint), ComputeBufferType.IndirectArguments);
                    _argsBuffer.SetData(args);
                }
                else
				{
                    _argsBuffer?.Dispose();
                    _argsBuffer = null;
                }
            }
        }

        void InitializeAllInstanceTransformBufferIfNeeded()
        {
            if (_shouldUpdateInstanceData)
            {
                ref var allBatchPos = ref instanceBatchData.instanceData;

                if (allBatchPos.Count > 0)
                {
                    this.InitializeInstanceGridConstants();
                }
                else
                {
                    _allBatchDataBuffer?.Dispose();
                    _allBatchPositionBuffer?.Dispose();
                    _allBatchVisibleIndexBuffer?.Dispose();

                    _allBatchDataBuffer = null;
                    _allBatchPositionBuffer = null;
                    _allBatchVisibleIndexBuffer = null;
                }

                if (instanceMaterial)
				{
                    instanceMaterial.SetBuffer(ShaderConstants._AllInstancesTransformBuffer, _allBatchDataBuffer);
                    instanceMaterial.SetBuffer(ShaderConstants._AllVisibleInstancesIndexBuffer, _allBatchVisibleIndexBuffer);
                }

                _shouldUpdateInstanceData = false;
            }
        }

        public void InitializeWorldBoundingBox()
		{
            if (transform.localToWorldMatrix != _localToWorldMatrix)
			{
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

        public void OnBeginFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            ref var allBatchPos = ref instanceBatchData.instanceData;

            this.InitializeInstanceMesh();
            this.InitializeAllInstanceTransformBufferIfNeeded();
            this.InitializeWorldBoundingBox();

            if (instanceMaterial != null && _argsBuffer != null && allBatchPos.Count > 0)
            {
                instanceMaterial.EnableKeyword("PROCEDURAL_INSTANCING_ON");
                instanceMaterial.SetInt(ShaderConstants._unity_BaseInstanceID, 0);
                instanceMaterial.SetMatrix(ShaderConstants._PivotMatrixWS, transform.localToWorldMatrix);

#if UNITY_EDITOR
                instanceMaterial.SetBuffer(ShaderConstants._AllInstancesTransformBuffer, _allBatchDataBuffer);
                instanceMaterial.SetBuffer(ShaderConstants._AllVisibleInstancesIndexBuffer, _allBatchVisibleIndexBuffer);
#endif

                foreach (var camera in cameras)
                {
                    if (camera.cameraType == CameraType.Preview)
                        return;

                    int mask = camera.cullingMask & (1 << gameObject.layer);
                    if (mask == 0)
                        return;

                    Graphics.DrawMeshInstancedIndirect(
                            _instanceMesh,
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
            if (mask == 0)
                return;

            if (_instanceMesh != null && _cullingComputeShader != null && instanceBatchData.instanceData.Count > 0 && _chunks != null)
            {
                var cameraOriginalFarPlane = camera.farClipPlane;
                camera.farClipPlane = maxDrawDistance;
                var cullingMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix * transform.localToWorldMatrix;
                GeometryUtility.CalculateFrustumPlanes(cullingMatrix, _cameraFrustumPlanes);
                camera.farClipPlane = cameraOriginalFarPlane;

                if (!GeometryUtility.TestPlanesAABB(_cameraFrustumPlanes, _boundingBox))
                    return;

                _visibleChunkList.Clear();

                if (this.isCpuCulling)
                {
                    for (int i = 0; i < _chunks.Length; i++)
                    {
                        Bounds cellBound = _chunks[i].boundingBox;
                        if (GeometryUtility.TestPlanesAABB(_cameraFrustumPlanes, cellBound))
                            _visibleChunkList.Add(i);
                    }
                }
                else
                {
                    for (int i = 0; i < _chunks.Length; i++)
                        _visibleChunkList.Add(i);
                }

                if (_visibleChunkList.Count == 0)
                    return;

                CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._configureTag);
				{
                    cmd.SetComputeBufferParam(_cullingComputeShader, _clearUniqueCounterKernel, ShaderConstants._RWVisibleIndirectArgumentBuffer, _argsBuffer);
                    cmd.DispatchCompute(_cullingComputeShader, _clearUniqueCounterKernel, 1, 1, 1);

                    var depthEstimation = HizPass.GetDepthEstimation(ref camera);
                    var occlusionKernel = depthEstimation.hizTexture && this.isGpuCulling ? _computeOcclusionCullingKernel : _computeFrustumCullingKernel;
                    var tanFov = 1.0f / (Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f));

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

                    cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._BoundMin, this._instanceMesh.bounds.min);
                    cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._BoundMax, this._instanceMesh.bounds.max);

                    cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._CameraZBufferParams, zBufferParams);
                    cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._CameraDrawParams, new Vector4(tanFov, this.mipScaleLevel, this.maxDrawDistance, this.distanceCulling));
                    cmd.SetComputeVectorArrayParam(_cullingComputeShader, ShaderConstants._CameraFrustumPlanes, _cameraFrustumData);
                    cmd.SetComputeMatrixParam(_cullingComputeShader, ShaderConstants._CameraViewMatrix, depthEstimation.viewMatrix * transform.localToWorldMatrix);
                    cmd.SetComputeMatrixParam(_cullingComputeShader, ShaderConstants._CameraViewProjection, depthEstimation.viewProjectionMatrix * transform.localToWorldMatrix);

                    cmd.SetComputeBufferParam(_cullingComputeShader, occlusionKernel, ShaderConstants._AllInstancesDataBuffer, _allBatchPositionBuffer);
                    cmd.SetComputeBufferParam(_cullingComputeShader, occlusionKernel, ShaderConstants._RWVisibleInstancesIndexBuffer, _allBatchVisibleIndexBuffer);
                    cmd.SetComputeBufferParam(_cullingComputeShader, occlusionKernel, ShaderConstants._RWVisibleIndirectArgumentBuffer, _argsBuffer);

                    if (occlusionKernel == _computeOcclusionCullingKernel)
                    {
                        cmd.SetComputeTextureParam(_cullingComputeShader, occlusionKernel, ShaderConstants._HizTexture, depthEstimation.hizTexture);
                        cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._HizTexture_TexelSize, new Vector4(depthEstimation.hizTexture.width, depthEstimation.hizTexture.height, 0, 0));
                    }

                    for (int i = 0; i < _visibleChunkList.Count; i++)
                    {
                        int targetCellFlattenID = _visibleChunkList[i];
                        int jobLength = _chunks[targetCellFlattenID].data.Count;

                        if (_shouldBatchDispatch)
                        {
                            while ((i < _visibleChunkList.Count - 1) && (_visibleChunkList[i + 1] == _visibleChunkList[i] + 1))
                            {
                                jobLength += _chunks[_visibleChunkList[i + 1]].data.Count;
                                i++;
                            }
                        }

                        if (jobLength > 0)
                        {
                            int memoryOffset = 0;
                            for (int j = 0; j < targetCellFlattenID; j++)
                                memoryOffset += _chunks[j].data.Count;

                            cmd.SetComputeIntParam(_cullingComputeShader, ShaderConstants._StartOffset, memoryOffset);
                            cmd.SetComputeIntParam(_cullingComputeShader, ShaderConstants._EndOffset, memoryOffset + jobLength);
                            cmd.DispatchCompute(_cullingComputeShader, occlusionKernel, Mathf.CeilToInt(jobLength / (float)_maxComputeWorkGroupSize), 1, 1);
                        }
                    }

#if UNITY_EDITOR
                    if (this.debugMode)
                        cmd.RequestAsyncReadback(_argsBuffer, OnAsyncGPUReadbackRequest);
#endif
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

#if UNITY_EDITOR
        private void OnAsyncGPUReadbackRequest(AsyncGPUReadbackRequest req)
        {
            var data = req.GetData<uint>(0);
            drawInstancedCount = (int)data[1];
        }
#endif

        private void OnBakeCompleted()
		{
            this.UploadBatchData();
        }

        static class ShaderConstants
        {
            public const string _configureTag = "Setup Batch Constants";
            public const string _renderTag = "Draw Batch Instanced";

            public static ProfilingSampler _profilingSampler = new ProfilingSampler(_renderTag);

            public static readonly int _unity_BaseInstanceID = Shader.PropertyToID("unity_BaseInstanceID"); 

            public static readonly int _BoundMin = Shader.PropertyToID("_BoundMin");
            public static readonly int _BoundMax = Shader.PropertyToID("_BoundMax");

            public static readonly int _CameraZBufferParams = Shader.PropertyToID("_CameraZBufferParams");
            public static readonly int _CameraDrawParams = Shader.PropertyToID("_CameraDrawParams");
            public static readonly int _CameraViewMatrix = Shader.PropertyToID("_CameraViewMatrix");
            public static readonly int _CameraViewProjection = Shader.PropertyToID("_CameraViewProjection");
            public static readonly int _CameraFrustumPlanes = Shader.PropertyToID("_CameraFrustumPlanes");

            public static readonly int _HizTexture = Shader.PropertyToID("_HizTexture");
            public static readonly int _HizTexture_TexelSize = Shader.PropertyToID("_HizTexture_TexelSize");

            public static readonly int _StartOffset = Shader.PropertyToID("_StartOffset");
            public static readonly int _EndOffset = Shader.PropertyToID("_EndOffset");
            public static readonly int _Offset = Shader.PropertyToID("_Offset");

            public static readonly int _PivotPosWS = Shader.PropertyToID("_PivotPosWS");
            public static readonly int _PivotScaleWS = Shader.PropertyToID("_PivotScaleWS");
            public static readonly int _PivotMatrixWS = Shader.PropertyToID("_PivotMatrixWS");

            public static readonly int _AllInstancesDataBuffer = Shader.PropertyToID("_AllInstancesDataBuffer");
            public static readonly int _AllInstancesTransformBuffer = Shader.PropertyToID("_AllInstancesTransformBuffer");
            public static readonly int _AllInstancesIndexBuffer = Shader.PropertyToID("_AllInstancesIndexBuffer");
            public static readonly int _AllVisibleInstancesIndexBuffer = Shader.PropertyToID("_AllVisibleInstancesIndexBuffer");

            public static readonly int _RWVisibleInstancesIndexBuffer = Shader.PropertyToID("_RWVisibleInstancesIndexBuffer");
            public static readonly int _RWVisibleIndirectArgumentBuffer = Shader.PropertyToID("_RWVisibleIndirectArgumentBuffer");
        }
    }
}