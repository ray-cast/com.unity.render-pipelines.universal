using System.Collections.Generic;
using UnityEditor;

namespace UnityEngine.Rendering.Universal
{
    internal enum FlowerProfileId
    {
        FrustumCulling,
        Dispatch
    }

    [ExecuteAlways]
    public class InstancedIndirectFlowerRenderer : MonoBehaviour
    {
        //在xz平面上可计算出一个能把所有草都包围起来的最小包围框
        //对包围框按[cellSizeX,cellSizeZ]的大小分割出cellCountX*cellCountZ个小格子
        //计算出位置在xz平面按
        //smaller the number, CPU needs more time, but GPU is faster
        private const float cellSizeX = 10; //unity unit (m)
        private const float cellSizeZ = 10; //unity unit (m)
        private Bounds boundingBox = new Bounds();

        public FlowerGroup flowerGroup;//草丛数据结构
        public List<FlowerPrototype> allFlowerPos;//所有草数据的数组

        //所有格子组成一个二维数组，每个格子关联了位置归属于此格子范围内的所有草
        //将二组数据按[xId,zId]=>xId+zId*cellCountX映射到一维数组就成了cellPosWSsList
        private FlowerCell[] cellPosWSsList; //for binning: binning will put each posWS into correct cell
        private MaterialPropertyBlock properties;

        private Transform _transform;//草丛渲染器所在的结点
        private Vector3 _cacheTransformPos;//结点位置缓存
        private int _cacheInstanceCount = -1;

        private bool _shouldBatchDispatch = true;
        private bool _shouldUpdateInstanceData = false;// 草数据变化的标志位
        private List<int> _visibleCellIDList = new List<int>();
        private Plane[] _cameraFrustumPlanes = new Plane[6];

        private int _clearUniqueCounter;
        private int _computeFrustumCulling;
        private int _computeOcclusionCulling;

        [Reload("Resources/CullingCompute.compute")]
        private ComputeShader _cullingComputeShader;
        private ComputeBuffer _allInstancesPosWSBuffer;
        private ComputeBuffer _allVisibleInstancesIndexBuffer;
        private ComputeBuffer _argsBuffer;

#if UNITY_EDITOR
        public bool debugMode;
        public int drawInstancedCount;
#endif

        private void OnEnable()
        {
            if (flowerGroup == null)
                flowerGroup = new FlowerGroup();

            _transform = transform;
            _cacheTransformPos = _transform.position;
            _shouldUpdateInstanceData = true;

            flowerGroup.Init(this);
            allFlowerPos = flowerGroup.floweres;
            flowerGroup.onChange += OnGrassGroupChange;

            _cullingComputeShader = Resources.Load<ComputeShader>("CullingCompute");
            _clearUniqueCounter = _cullingComputeShader.FindKernel("ClearIndirectArgument");
            _computeFrustumCulling = _cullingComputeShader.FindKernel("ComputeFrustumCulling");
            _computeOcclusionCulling = _cullingComputeShader.FindKernel("ComputeOcclusionCulling");

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;

#if UNITY_EDITOR
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
#endif
        }

        public void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

#if UNITY_EDITOR
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
#endif

            flowerGroup.onChange -= OnGrassGroupChange;

            _allInstancesPosWSBuffer?.Release();
            _allVisibleInstancesIndexBuffer?.Release();
            _argsBuffer?.Release();

            _shouldUpdateInstanceData = false;
            _allInstancesPosWSBuffer = null;
            _allVisibleInstancesIndexBuffer = null;
            _argsBuffer = null;
        }

        void OnGrassGroupChange()
        {
            _shouldUpdateInstanceData = true;
        }

        void InitializeInstanceGridConstants()
        {
            boundingBox.SetMinMax(Vector3.positiveInfinity, Vector3.negativeInfinity);
            for (int i = 0; i < allFlowerPos.Count; i++)
            {
                Vector3 finalWorldPos = Vector3.Scale(allFlowerPos[i].worldPos, transform.lossyScale);
                boundingBox.Encapsulate(finalWorldPos);
            }

            var max = boundingBox.max;
            var min = boundingBox.min;
            var cellCountX = Mathf.Max(1, Mathf.CeilToInt((max.x - min.x) / cellSizeX));
            var cellCountZ = Mathf.Max(1, Mathf.CeilToInt((max.z - min.z) / cellSizeZ));

            //init per cell posWS list memory
            cellPosWSsList = new FlowerCell[cellCountX * cellCountZ]; //flatten 2D array
            for (int i = 0; i < cellPosWSsList.Length; i++)
            {
                Vector3 centerPosWS = new Vector3(i % cellCountX + 0.5f, 0, i / cellCountX + 0.5f);
                centerPosWS.x = Mathf.Lerp(min.x, max.x, centerPosWS.x / cellCountX);
                centerPosWS.z = Mathf.Lerp(min.z, max.z, centerPosWS.z / cellCountZ);
                cellPosWSsList[i] = new FlowerCell(centerPosWS, cellSizeX, cellSizeZ);
            }

            //binning, put each posWS into the correct cell
            for (int i = 0; i < allFlowerPos.Count; i++)
            {
                FlowerPrototype gp = allFlowerPos[i];
                Vector3 pos = Vector3.Scale(gp.worldPos, transform.localScale);

                int xID = Mathf.Min(cellCountX - 1, Mathf.FloorToInt(Mathf.InverseLerp(min.x, max.x, pos.x) * cellCountX)); //use min to force within 0~[cellCountX-1]  
                int zID = Mathf.Min(cellCountZ - 1, Mathf.FloorToInt(Mathf.InverseLerp(min.z, max.z, pos.z) * cellCountZ)); //use min to force within 0~[cellCountZ-1]

                cellPosWSsList[xID + zID * cellCountX].AddGrass(gp);
            }

            //combine to a flatten array for compute buffer
            Vector3[] allGrassPosWSSortedByCell = new Vector3[allFlowerPos.Count];

            for (int i = 0, offset = 0; i < cellPosWSsList.Length; i++)
            {
                for (int j = 0; j < cellPosWSsList[i].flowers.Count; j++)
                {
                    allGrassPosWSSortedByCell[offset] = Vector3.Scale(cellPosWSsList[i].flowers[j].worldPos, transform.localScale);
                    offset++;
                }
            }

            if (_allInstancesPosWSBuffer == null || _allInstancesPosWSBuffer != null && _allInstancesPosWSBuffer.count < allFlowerPos.Count)
            {
                if (_allInstancesPosWSBuffer != null)
                    _allInstancesPosWSBuffer.Release();

#if UNITY_EDITOR
                _allInstancesPosWSBuffer = new ComputeBuffer(allFlowerPos.Count << 1, sizeof(float) * 3); //float3 posWS only, per grass
#else
                _allInstancesPosWSBuffer = new ComputeBuffer(allFlowerPos.Count, sizeof(float) * 3); //float3 posWS only, per grass
#endif
            }

            _allInstancesPosWSBuffer.SetData(allGrassPosWSSortedByCell);

            if (_allVisibleInstancesIndexBuffer == null || _allVisibleInstancesIndexBuffer != null && _allVisibleInstancesIndexBuffer.count < allFlowerPos.Count)
            {
                if (_allVisibleInstancesIndexBuffer != null)
                    _allVisibleInstancesIndexBuffer.Release();

#if UNITY_EDITOR
                _allVisibleInstancesIndexBuffer = new ComputeBuffer(allFlowerPos.Count << 1, sizeof(uint)); //uint only, per visible grass
#else
                _allVisibleInstancesIndexBuffer = new ComputeBuffer(allFlowerPos.Count, sizeof(uint)); //uint only, per visible grass
#endif
            }

            if (_argsBuffer == null)
			{
                uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
                args[0] = (uint)flowerGroup.cachedGrassMesh.GetIndexCount(0);
                args[1] = (uint)allFlowerPos.Count;
                args[2] = (uint)flowerGroup.cachedGrassMesh.GetIndexStart(0);
                args[3] = (uint)flowerGroup.cachedGrassMesh.GetBaseVertex(0);
                args[4] = 0;

                _argsBuffer?.Release();
                _argsBuffer = new ComputeBuffer(args.Length, sizeof(uint), ComputeBufferType.IndirectArguments);
                _argsBuffer.SetData(args);
            }

            var lightprobes = new SphericalHarmonicsL2[allFlowerPos.Count];
            var occlusionprobes = new Vector4[allFlowerPos.Count];
            LightProbes.CalculateInterpolatedLightAndOcclusionProbes(allGrassPosWSSortedByCell, lightprobes, occlusionprobes);

            properties = new MaterialPropertyBlock();
            properties.CopySHCoefficientArraysFrom(lightprobes);
            properties.CopyProbeOcclusionArrayFrom(occlusionprobes);
        }

        void SetupAllInstanceDataConstants()
        {
            if (allFlowerPos.Count > 0)
            {
                this.InitializeInstanceGridConstants();

                flowerGroup.instanceMaterial.SetBuffer(ShaderConstants._AllInstancesTransformBuffer, _allInstancesPosWSBuffer);
                flowerGroup.instanceMaterial.SetBuffer(ShaderConstants._AllVisibleInstancesIndexBuffer, _allVisibleInstancesIndexBuffer);
            }

            _cacheInstanceCount = allFlowerPos.Count;
        }

        void UpdateAllInstanceTransformBufferIfNeeded()
        {
            if (!_shouldUpdateInstanceData &&
                _cacheInstanceCount == allFlowerPos.Count &&
                _argsBuffer != null &&
                _allInstancesPosWSBuffer != null &&
                _allVisibleInstancesIndexBuffer != null)
            {
                return;
            }

            this.SetupAllInstanceDataConstants();
            _shouldUpdateInstanceData = false;
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying && camera != Camera.main)
                return;
#endif

            if (flowerGroup.instanceMaterial == null || _cullingComputeShader == null || allFlowerPos.Count == 0)
                return;

            UpdateAllInstanceTransformBufferIfNeeded();

            float cameraOriginalFarPlane = camera.farClipPlane;
            camera.farClipPlane = flowerGroup.maxDrawDistance;
            GeometryUtility.CalculateFrustumPlanes(camera, _cameraFrustumPlanes);
            camera.farClipPlane = cameraOriginalFarPlane;

            if (!GeometryUtility.TestPlanesAABB(_cameraFrustumPlanes, boundingBox))
                return;

            _visibleCellIDList.Clear();

            if (flowerGroup.isCpuCulling)
            {
                for (int i = 0; i < cellPosWSsList.Length; i++)
                {
                    Bounds cellBound = cellPosWSsList[i].cellBound;
                    if (GeometryUtility.TestPlanesAABB(_cameraFrustumPlanes, cellBound))
                        _visibleCellIDList.Add(i);
                }
            }
            else
            {
                for (int i = 0; i < cellPosWSsList.Length; i++)
                    _visibleCellIDList.Add(i);
            }

            if (_visibleCellIDList.Count == 0)
                return;

            CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._configureTag);
            {
                cmd.SetComputeBufferParam(_cullingComputeShader, _clearUniqueCounter, ShaderConstants._RWVisibleIndirectArgumentBuffer, _argsBuffer);
                cmd.DispatchCompute(_cullingComputeShader, _clearUniqueCounter, 1, 1, 1);

                var size = this.flowerGroup.cachedGrassMesh.bounds.size;

                var hizRenderTarget = HizPass.GetHizTexture(ref camera);
                int occlusionKernel = hizRenderTarget && flowerGroup.isGpuCulling ? _computeOcclusionCulling : _computeFrustumCulling;

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

                cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._BoundMin, this.flowerGroup.cachedGrassMesh.bounds.min * 1.5f);
                cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._BoundMax, this.flowerGroup.cachedGrassMesh.bounds.max * 1.5f);

                cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._CameraZBufferParams, zBufferParams);
                cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._CameraDrawParams, new Vector4(36, flowerGroup.maxDrawDistance, flowerGroup.sensity, flowerGroup.distanceCulling));
                cmd.SetComputeMatrixParam(_cullingComputeShader, ShaderConstants._CameraViewMatrix, camera.worldToCameraMatrix);
                cmd.SetComputeMatrixParam(_cullingComputeShader, ShaderConstants._CameraViewProjection, GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix);

                cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._Offset, new Vector3(0, size.y, 0));
                cmd.SetComputeBufferParam(_cullingComputeShader, occlusionKernel, ShaderConstants._AllInstancesDataBuffer, _allInstancesPosWSBuffer);
                cmd.SetComputeBufferParam(_cullingComputeShader, occlusionKernel, ShaderConstants._RWVisibleInstancesIndexBuffer, _allVisibleInstancesIndexBuffer);
                cmd.SetComputeBufferParam(_cullingComputeShader, occlusionKernel, ShaderConstants._RWVisibleIndirectArgumentBuffer, _argsBuffer);

                if (occlusionKernel == _computeOcclusionCulling)
                {
                    cmd.SetComputeTextureParam(_cullingComputeShader, occlusionKernel, ShaderConstants._HizTexture, hizRenderTarget);
                    cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._HizTexture_TexelSize, new Vector4(hizRenderTarget.width, hizRenderTarget.height, 0, 0));
                }

                for (int i = 0; i < _visibleCellIDList.Count; i++)
                {
                    int targetCellFlattenID = _visibleCellIDList[i];
                    int jobLength = cellPosWSsList[targetCellFlattenID].flowers.Count;

                    if (_shouldBatchDispatch)
                    {
                        while ((i < _visibleCellIDList.Count - 1) && (_visibleCellIDList[i + 1] == _visibleCellIDList[i] + 1))
                        {
                            jobLength += cellPosWSsList[_visibleCellIDList[i + 1]].flowers.Count;
                            i++;
                        }
                    }

                    if (jobLength > 0)
                    {
                        int memoryOffset = 0;
                        for (int j = 0; j < targetCellFlattenID; j++)
                            memoryOffset += cellPosWSsList[j].flowers.Count;

                        using (new ProfilingScope(cmd, ProfilingSampler.Get(FlowerProfileId.Dispatch)))
                        {
                            cmd.SetComputeIntParam(_cullingComputeShader, ShaderConstants._StartOffset, memoryOffset);
                            cmd.SetComputeIntParam(_cullingComputeShader, ShaderConstants._EndOffset, memoryOffset + jobLength);
                            cmd.DispatchCompute(_cullingComputeShader, occlusionKernel, Mathf.CeilToInt(jobLength / 64f), 1, 1);
                        }
                    }
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
#if UNITY_EDITOR
            if (this.debugMode && _argsBuffer != null)
            {
                uint[] counter = new uint[5];
                _argsBuffer.GetData(counter);
                drawInstancedCount = (int)counter[1];
            }
#endif
        }

        public void LateUpdate()
        {
            if (_cacheTransformPos != _transform.position)
            {
                _shouldUpdateInstanceData = true;
                _cacheTransformPos = _transform.position;
            }

            if (flowerGroup.instanceMaterial == null || _argsBuffer == null || allFlowerPos.Count == 0)
                return;

            flowerGroup.instanceMaterial.SetVector(ShaderConstants._PivotPosWS, _transform.position);
            flowerGroup.instanceMaterial.SetVector(ShaderConstants._BoundSize, new Vector2(_transform.localScale.x, _transform.localScale.z));
#if UNITY_EDITOR
            flowerGroup.instanceMaterial.SetBuffer(ShaderConstants._AllInstancesTransformBuffer, _allInstancesPosWSBuffer);
            flowerGroup.instanceMaterial.SetBuffer(ShaderConstants._AllVisibleInstancesIndexBuffer, _allVisibleInstancesIndexBuffer);
#endif

            Graphics.DrawMeshInstancedIndirect(
                    flowerGroup.cachedGrassMesh,
                    0,
                    flowerGroup.instanceMaterial,
                    boundingBox,
                    _argsBuffer,
                    0,
                    properties,
                    ShadowCastingMode.Off,
                    false,
                    0,
                    null,
                    LightProbeUsage.CustomProvided
                );
        }

        static class ShaderConstants
        {
            public const string _configureTag = "Setup Flower Constants";
            public const string _renderTag = "Draw Flower Instanced";

            public static ProfilingSampler _profilingSampler = new ProfilingSampler(_renderTag);

            public static readonly int _BoundMin = Shader.PropertyToID("_BoundMin");
            public static readonly int _BoundMax = Shader.PropertyToID("_BoundMax");

            public static readonly int _CameraZBufferParams = Shader.PropertyToID("_CameraZBufferParams");
            public static readonly int _CameraDrawParams = Shader.PropertyToID("_CameraDrawParams");
            public static readonly int _CameraViewMatrix = Shader.PropertyToID("_CameraViewMatrix");
            public static readonly int _CameraViewProjection = Shader.PropertyToID("_CameraViewProjection");

            public static readonly int _HizTexture = Shader.PropertyToID("_HizTexture");
            public static readonly int _HizTexture_TexelSize = Shader.PropertyToID("_HizTexture_TexelSize");

            public static readonly int _StartOffset = Shader.PropertyToID("_StartOffset");
            public static readonly int _EndOffset = Shader.PropertyToID("_EndOffset");
            public static readonly int _Offset = Shader.PropertyToID("_Offset");

            public static readonly int _PivotPosWS = Shader.PropertyToID("_PivotPosWS");
            public static readonly int _BoundSize = Shader.PropertyToID("_BoundSize");

            public static readonly int _AllColorsBuffer = Shader.PropertyToID("_AllColorsBuffer");
            public static readonly int _AllScalesBuffer = Shader.PropertyToID("_AllScalesBuffer");
            public static readonly int _AllInstancesDataBuffer = Shader.PropertyToID("_AllInstancesDataBuffer");
            public static readonly int _AllInstancesTransformBuffer = Shader.PropertyToID("_AllInstancesTransformBuffer");
            public static readonly int _AllInstancesIndexBuffer = Shader.PropertyToID("_AllInstancesIndexBuffer");
            public static readonly int _AllVisibleInstancesIndexBuffer = Shader.PropertyToID("_AllVisibleInstancesIndexBuffer");

            public static readonly int _RWVisibleInstancesIndexBuffer = Shader.PropertyToID("_RWVisibleInstancesIndexBuffer");
            public static readonly int _RWVisibleIndirectArgumentBuffer = Shader.PropertyToID("_RWVisibleIndirectArgumentBuffer");
        }
    }
}