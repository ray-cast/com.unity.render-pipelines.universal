using UnityEditor;
using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    internal enum GrassProfileId
    {
        Dispatch,
        DrawGrass
    }

    [ExecuteAlways]
    public class InstancedIndirectGrassRenderer : MonoBehaviour
    {
        private ProfilingSampler _profilingSampler;

        //在xz平面上可计算出一个能把所有草都包围起来的最小包围框
        //对包围框按[cellSizeX,cellSizeZ]的大小分割出cellCountX*cellCountZ个小格子
        //计算出位置在xz平面按
        //smaller the number, CPU needs more time, but GPU is faster
        private const float cellSizeX = 10; //unity unit (m)
        private const float cellSizeZ = 10; //unity unit (m)
        private Bounds boundingBox = new Bounds();

        public GrassGroup grassGroup; //草丛数据结构

        //所有格子组成一个二维数组，每个格子关联了位置归属于此格子范围内的所有草
        //将二组数据按[xId,zId]=>xId+zId*cellCountX映射到一维数组就成了cellPosWSsList
        private GrassCell[] cellPosWSsList; //for binning: binning will put each posWS into correct cell

        private Transform _transform;//草丛渲染器所在的结点
        private Vector3 _cacheTransformPos;//结点位置缓存
        private int _cacheInstanceCount = -1;
        private Mesh _cachedGrassMesh;

        private bool _shouldBatchDispatch = true;
        private bool _shouldUpdateInstanceData = true; //草数据变化的标志位
        private List<int> _visibleCellIDList = new List<int>();
        private Plane[] _cameraFrustumPlanes = new Plane[6];

        private int _clearUniqueCounter;
        private int _computeFrustumCulling;
        private int _computeOcclusionCulling;

        [Reload("Resources/CullingCompute.compute")]
        private ComputeShader _cullingComputeShader;

        private ComputeBuffer _allInstancesPosWSBuffer;
        private ComputeBuffer _allInstancesIndexBuffer;
        private ComputeBuffer _allVisibleInstancesIndexBuffer;
        private ComputeBuffer _argsBuffer;

        private Vector4[] _allColors;
        private Vector4[] _allScales;
        private MaterialPropertyBlock properties;

#if UNITY_EDITOR
        public bool debugMode;
        public int drawInstancedCount;
#endif

		private void OnEnable()
        {
            if (grassGroup == null)
                grassGroup = new GrassGroup();

            grassGroup.Init(this);
            grassGroup.onChange += OnGrassGroupChange;
            grassGroup.onColorChange += OnColorChange;
            grassGroup.onScaleChange += OnScaleChange;

            _transform = transform;
            _cacheTransformPos = _transform.position;
            _profilingSampler = new ProfilingSampler(ShaderConstants._renderTag);
            _shouldBatchDispatch = true;
            _shouldUpdateInstanceData = true;

            _allScales = new Vector4[GrassGroup.maxScaleLimits];
            _allColors = new Vector4[GrassGroup.maxColorLimits * 2];

            _cullingComputeShader = Resources.Load<ComputeShader>("CullingCompute");
            _clearUniqueCounter = _cullingComputeShader.FindKernel("ClearIndirectArgument");
            _computeFrustumCulling = _cullingComputeShader.FindKernel("ComputeFrustumCulling");
            _computeOcclusionCulling = _cullingComputeShader.FindKernel("ComputeOcclusionCulling");

            Debug.LogFormat("{0} UpdateAllInstanceTransformBuffer (Slow) " +
                "\nsupportsComputeShaders={1}" +
                "\ngraphicsShaderLevel={2}" +
                "\nmaxComputeBufferInputsVertex={3}" +
                "\nmaxComputeBufferInputsFragment={4}" +
                "\nmaxComputeBufferInputsCompute={5}",
                _transform.name,
                SystemInfo.supportsComputeShaders,
                SystemInfo.graphicsShaderLevel,
                SystemInfo.maxComputeBufferInputsVertex,
                SystemInfo.maxComputeBufferInputsFragment,
                SystemInfo.maxComputeBufferInputsCompute
                );

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

            grassGroup.onChange -= OnGrassGroupChange;
            grassGroup.onColorChange -= OnColorChange;
            grassGroup.onScaleChange -= OnScaleChange;

            _allInstancesPosWSBuffer?.Release();
            _allInstancesIndexBuffer?.Release();
            _allVisibleInstancesIndexBuffer?.Release();
            _argsBuffer?.Release();

            _allInstancesPosWSBuffer = null;
            _allInstancesIndexBuffer = null;
            _allVisibleInstancesIndexBuffer = null;
            _argsBuffer = null;
            _shouldUpdateInstanceData = false;
        }

        void OnGrassGroupChange()
        {
            _shouldUpdateInstanceData = true;
        }

        void OnColorChange()
		{
            this.InitializeColorConstants();
		}

        void OnScaleChange()
        {
            this.InitializeScaleConstants();
        }

        void InitializeColorConstants()
        {
            for (int i = 0; i < grassGroup.allColors.Count && i < GrassGroup.maxColorLimits; i++)
            {
                _allColors[i * 2] = grassGroup.allColors[i].dryColorFinal;
                _allColors[i * 2 + 1] = grassGroup.allColors[i].healthyColorFinal;
            }

            if (grassGroup.instanceMaterial)
                grassGroup.instanceMaterial.SetVectorArray(ShaderConstants._AllColorsBuffer, _allColors);
        }

        void InitializeScaleConstants()
		{
            for (int i = 0; i < grassGroup.allScales.Count && i < GrassGroup.maxScaleLimits; i++)
            {
                Vector3 scale = Vector3.Scale(grassGroup.allScales[i], transform.lossyScale);// 把所有父结点的缩放也考虑进去
                _allScales[i] = new Vector4(scale.x, scale.y, scale.z, 1.0f);
            }

            if (grassGroup.instanceMaterial)
                grassGroup.instanceMaterial.SetVectorArray(ShaderConstants._AllScalesBuffer, _allScales);
        }

        void InitializeInstanceGridBoundingBox()
        {
            ref var allElementPos = ref grassGroup.grasses;

            boundingBox.SetMinMax(Vector3.positiveInfinity, Vector3.negativeInfinity);

            for (int i = 0; i < allElementPos.Count; i++)
            {
                Vector3 finalWorldPos = Vector3.Scale(allElementPos[i].worldPos, transform.lossyScale);
                boundingBox.Encapsulate(finalWorldPos);
            }
        }

        void InitializeInstanceGridConstants()
        {
            ref var allGrassPos = ref grassGroup.grasses;

            var max = boundingBox.max;
            var min = boundingBox.min;
            var cellCountX = Mathf.Max(1, Mathf.CeilToInt((max.x - min.x) / cellSizeX));
            var cellCountZ = Mathf.Max(1, Mathf.CeilToInt((max.z - min.z) / cellSizeZ));

            cellPosWSsList = new GrassCell[cellCountX * cellCountZ];

            for (int i = 0; i < cellPosWSsList.Length; i++)
            {
                Vector3 centerPosWS = new Vector3(i % cellCountX + 0.5f, 0, i / cellCountX + 0.5f);
                centerPosWS.x = Mathf.Lerp(min.x, max.x, centerPosWS.x / cellCountX);
                centerPosWS.z = Mathf.Lerp(min.z, max.z, centerPosWS.z / cellCountZ);
                cellPosWSsList[i] = new GrassCell(centerPosWS, cellSizeX, cellSizeZ);
            }

            for (int i = 0; i < allGrassPos.Count; i++)
            {
                GrassPrototype gp = allGrassPos[i];
                Vector3 pos = Vector3.Scale(gp.worldPos, transform.localScale);

                int xID = Mathf.Min(cellCountX - 1, Mathf.FloorToInt(Mathf.InverseLerp(min.x, max.x, pos.x) * cellCountX)); //use min to force within 0~[cellCountX-1]  
                int zID = Mathf.Min(cellCountZ - 1, Mathf.FloorToInt(Mathf.InverseLerp(min.z, max.z, pos.z) * cellCountZ)); //use min to force within 0~[cellCountZ-1]

                cellPosWSsList[xID + zID * cellCountX].AddGrass(gp);
            }

            Vector3[] allGrassPosWSSortedByCell = new Vector3[allGrassPos.Count];
            UInt3[] allGrassIndexSortedByCell = new UInt3[allGrassPos.Count];
            
            for (int i = 0, offset = 0; i < cellPosWSsList.Length; i++)
            {
                for (int j = 0; j < cellPosWSsList[i].grasses.Count; j++)
                {
                    ref var grassCell = ref cellPosWSsList[i];
                    allGrassPosWSSortedByCell[offset] = Vector3.Scale(grassCell.grasses[j].worldPos, transform.localScale);
                    allGrassIndexSortedByCell[offset] = grassCell.grasses[j].allIndexes;
                    offset++;
                }
            }

#if UNITY_EDITOR
            var allocInstanceCount = allGrassPos.Count << 1;
#else
            var allocInstanceCount = allGrassPos.Count;
#endif

            if (_allInstancesPosWSBuffer == null)
                _allInstancesPosWSBuffer = new ComputeBuffer(allGrassPos.Count, sizeof(float) * 3);
            else if (_allInstancesPosWSBuffer.count < allGrassPos.Count)
            {
                if (_allInstancesPosWSBuffer != null)
                    _allInstancesPosWSBuffer.Release();

                _allInstancesPosWSBuffer = new ComputeBuffer(allocInstanceCount, sizeof(float) * 3);
            }

            _allInstancesPosWSBuffer.SetData(allGrassPosWSSortedByCell);

            if (_allInstancesIndexBuffer == null)
                _allInstancesIndexBuffer = new ComputeBuffer(allGrassPos.Count, sizeof(uint) * 3);
            else if (_allInstancesIndexBuffer.count < allGrassPos.Count)
            {
                if (_allInstancesIndexBuffer != null)
                    _allInstancesIndexBuffer.Release();

                _allInstancesIndexBuffer = new ComputeBuffer(allocInstanceCount, sizeof(uint) * 3);
            }

            _allInstancesIndexBuffer.SetData(allGrassIndexSortedByCell);

            if (_allVisibleInstancesIndexBuffer == null)
                _allVisibleInstancesIndexBuffer = new ComputeBuffer(allGrassPos.Count, sizeof(uint));
            else if (_allVisibleInstancesIndexBuffer.count < allGrassPos.Count)
            {
                if (_allVisibleInstancesIndexBuffer != null)
                    _allVisibleInstancesIndexBuffer.Release();

                _allVisibleInstancesIndexBuffer = new ComputeBuffer(allocInstanceCount, sizeof(uint));
            }

            this._cachedGrassMesh = grassGroup.cachedGrassMesh;

            if (_argsBuffer == null)
            { 
                uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
                args[0] = (uint)_cachedGrassMesh.GetIndexCount(0);
                args[1] = (uint)allGrassPos.Count;
                args[2] = (uint)_cachedGrassMesh.GetIndexStart(0);
                args[3] = (uint)_cachedGrassMesh.GetBaseVertex(0);
                args[4] = 0;

                _argsBuffer = new ComputeBuffer(args.Length, sizeof(uint), ComputeBufferType.IndirectArguments);
                _argsBuffer.SetData(args);
            }

            var lightprobes = new SphericalHarmonicsL2[allGrassPos.Count];
            var occlusionprobes = new Vector4[allGrassPos.Count];
            LightProbes.CalculateInterpolatedLightAndOcclusionProbes(allGrassPosWSSortedByCell, lightprobes, occlusionprobes);
            
            properties = new MaterialPropertyBlock();
            properties.CopySHCoefficientArraysFrom(lightprobes);
            properties.CopyProbeOcclusionArrayFrom(occlusionprobes);
        }

        void SetupAllInstanceDataConstants()
		{
            ref var allGrassPos = ref grassGroup.grasses;

            if (allGrassPos.Count > 0)
            {
                this.InitializeColorConstants();
                this.InitializeScaleConstants();
                this.InitializeInstanceGridBoundingBox();
                this.InitializeInstanceGridConstants();

                if (grassGroup.instanceMaterial)
                {
                    grassGroup.instanceMaterial.SetVectorArray(ShaderConstants._AllColorsBuffer, _allColors);
                    grassGroup.instanceMaterial.SetVectorArray(ShaderConstants._AllScalesBuffer, _allScales);

                    grassGroup.instanceMaterial.SetBuffer(ShaderConstants._AllInstancesTransformBuffer, _allInstancesPosWSBuffer);
                    grassGroup.instanceMaterial.SetBuffer(ShaderConstants._AllInstancesIndexBuffer, _allInstancesIndexBuffer);
                    grassGroup.instanceMaterial.SetBuffer(ShaderConstants._AllVisibleInstancesIndexBuffer, _allVisibleInstancesIndexBuffer);
                }
            }

            _cacheInstanceCount = allGrassPos.Count;
        }

        void UpdateAllInstanceTransformBufferIfNeeded()
        {
            if (!_shouldUpdateInstanceData &&
                _cacheInstanceCount == grassGroup.grasses.Count &&
                _argsBuffer != null &&
                _allInstancesPosWSBuffer != null &&
                _allInstancesIndexBuffer != null &&
                _allVisibleInstancesIndexBuffer != null)
            {
                return;
            }

            Debug.LogFormat("{0} UpdateAllInstanceTransformBuffer (Slow) " +
                "\nsupportsComputeShaders={1}" +
                "\ngraphicsShaderLevel={2}" +
                "\nmaxComputeBufferInputsVertex={3}" +
                "\nmaxComputeBufferInputsFragment={4}" +
                "\nmaxComputeBufferInputsCompute={5}",
                _transform.name, 
                SystemInfo.supportsComputeShaders,
                SystemInfo.graphicsShaderLevel,
                SystemInfo.maxComputeBufferInputsVertex,
                SystemInfo.maxComputeBufferInputsFragment,
                SystemInfo.maxComputeBufferInputsCompute
                );

            this.SetupAllInstanceDataConstants();
            _shouldUpdateInstanceData = false;
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying && camera != Camera.main)
                return;
#endif

            if (grassGroup.instanceMaterial == null || _cullingComputeShader == null || grassGroup.grasses.Count == 0)
                return;

            UpdateAllInstanceTransformBufferIfNeeded();

            float cameraOriginalFarPlane = camera.farClipPlane;
            camera.farClipPlane = grassGroup.maxDrawDistance;
            GeometryUtility.CalculateFrustumPlanes(camera, _cameraFrustumPlanes);
            camera.farClipPlane = cameraOriginalFarPlane;

            if (!GeometryUtility.TestPlanesAABB(_cameraFrustumPlanes, boundingBox))
                return;

            _visibleCellIDList.Clear();

            if (grassGroup.isCpuCulling)
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

            using (new ProfilingScope(cmd, ShaderConstants._profilingSampler))
            {
                cmd.SetComputeBufferParam(_cullingComputeShader, _clearUniqueCounter, ShaderConstants._RWVisibleIndirectArgumentBuffer, _argsBuffer);
                cmd.DispatchCompute(_cullingComputeShader, _clearUniqueCounter, 1, 1, 1);

                var tanFov = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad);
                var hizRenderTarget = HizPass.GetHizTexture(ref camera);
                int occlusionKernel = hizRenderTarget && grassGroup.isGpuCulling ? _computeOcclusionCulling : _computeFrustumCulling;

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

                cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._CameraZBufferParams, zBufferParams);
                cmd.SetComputeMatrixParam(_cullingComputeShader, ShaderConstants._CameraViewMatrix, camera.worldToCameraMatrix);
                cmd.SetComputeMatrixParam(_cullingComputeShader, ShaderConstants._CameraViewProjection, GL.GetGPUProjectionMatrix(camera.projectionMatrix, false));
                cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._CameraDrawParams, new Vector4(tanFov, grassGroup.maxDrawDistance, grassGroup.sensity, grassGroup.distanceCulling));

                cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._Offset, new Vector3(0, this._cachedGrassMesh.bounds.size.y, 0));
                cmd.SetComputeBufferParam(_cullingComputeShader, occlusionKernel, ShaderConstants._AllInstancesPosWSBuffer, _allInstancesPosWSBuffer);
                cmd.SetComputeBufferParam(_cullingComputeShader, occlusionKernel, ShaderConstants._RWVisibleInstancesIndexBuffer, _allVisibleInstancesIndexBuffer);
                cmd.SetComputeBufferParam(_cullingComputeShader, occlusionKernel, ShaderConstants._RWVisibleIndirectArgumentBuffer, _argsBuffer);

                if (occlusionKernel == _computeOcclusionCulling)
                {
                    cmd.SetComputeTextureParam(_cullingComputeShader, occlusionKernel, ShaderConstants._HizTexture, hizRenderTarget);
                    cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._HizTexture_TexelSize, new Vector4(hizRenderTarget.width, hizRenderTarget.height, 0, 0));
                }

                for (int i = 0; i < _visibleCellIDList.Count; i++)
                {
                    int targetCellID = _visibleCellIDList[i];
                    int jobLength = cellPosWSsList[targetCellID].grasses.Count;

                    if (_shouldBatchDispatch)
                    {
                        while ((i < _visibleCellIDList.Count - 1) && (_visibleCellIDList[i + 1] == _visibleCellIDList[i] + 1))
                        {
                            jobLength += cellPosWSsList[_visibleCellIDList[i + 1]].grasses.Count;
                            i++;
                        }
                    }

                    if (jobLength > 0)
                    {
                        int memoryOffset = 0;
                        for (int j = 0; j < targetCellID; j++)
                            memoryOffset += cellPosWSsList[j].grasses.Count;

                        using (new ProfilingScope(cmd, ProfilingSampler.Get(GrassProfileId.Dispatch)))
                        {
                            cmd.SetComputeIntParam(_cullingComputeShader, ShaderConstants._StartOffset, memoryOffset);
                            cmd.SetComputeIntParam(_cullingComputeShader, ShaderConstants._EndOffset, memoryOffset + jobLength);
                            cmd.DispatchCompute(_cullingComputeShader, occlusionKernel, Mathf.CeilToInt(jobLength / 64f), 1, 1);
                        }
                    }
                }
            }
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

            if (grassGroup.instanceMaterial == null || _argsBuffer == null || grassGroup.grasses.Count == 0)
                return;

            grassGroup.instanceMaterial.SetVector(ShaderConstants._PivotPosWS, _transform.position);
            grassGroup.instanceMaterial.SetVector(ShaderConstants._BoundSize, _transform.localScale);

#if UNITY_EDITOR
            grassGroup.instanceMaterial.SetVectorArray(ShaderConstants._AllColorsBuffer, _allColors);
            grassGroup.instanceMaterial.SetVectorArray(ShaderConstants._AllScalesBuffer, _allScales);

            grassGroup.instanceMaterial.SetBuffer(ShaderConstants._AllInstancesTransformBuffer, _allInstancesPosWSBuffer);
            grassGroup.instanceMaterial.SetBuffer(ShaderConstants._AllInstancesIndexBuffer, _allInstancesIndexBuffer);
            grassGroup.instanceMaterial.SetBuffer(ShaderConstants._AllVisibleInstancesIndexBuffer, _allVisibleInstancesIndexBuffer);
#endif

            Graphics.DrawMeshInstancedIndirect(
                    _cachedGrassMesh, 
                    0, 
                    grassGroup.instanceMaterial, 
                    boundingBox, 
                    _argsBuffer, 
                    0, 
                    properties, ShadowCastingMode.Off,
                    false, 
                    0, 
                    null,
                    LightProbeUsage.CustomProvided
                );
        }

        static class ShaderConstants
        {
            public const string _configureTag = "Setup Grass Constants";
            public const string _renderTag = "Draw Grass Instanced";

            public static ProfilingSampler _profilingSampler = new ProfilingSampler(_renderTag);

            public static readonly int _PivotPosWS = Shader.PropertyToID("_PivotPosWS");
            public static readonly int _BoundSize = Shader.PropertyToID("_BoundSize");

            public static readonly int _CameraZBufferParams = Shader.PropertyToID("_CameraZBufferParams");
            public static readonly int _CameraDrawParams = Shader.PropertyToID("_CameraDrawParams");
            public static readonly int _CameraViewMatrix = Shader.PropertyToID("_CameraViewMatrix");
            public static readonly int _CameraViewProjection = Shader.PropertyToID("_CameraViewProjection");

            public static readonly int _HizTexture = Shader.PropertyToID("_HizTexture");
            public static readonly int _HizTexture_TexelSize = Shader.PropertyToID("_HizTexture_TexelSize");

            public static readonly int _StartOffset = Shader.PropertyToID("_StartOffset");
            public static readonly int _EndOffset = Shader.PropertyToID("_EndOffset");
            public static readonly int _Offset = Shader.PropertyToID("_Offset");

            public static readonly int _AllColorsBuffer = Shader.PropertyToID("_AllColorsBuffer");
            public static readonly int _AllScalesBuffer = Shader.PropertyToID("_AllScalesBuffer");
            public static readonly int _AllInstancesPosWSBuffer = Shader.PropertyToID("_AllInstancesPosWSBuffer");
            public static readonly int _AllInstancesTransformBuffer = Shader.PropertyToID("_AllInstancesTransformBuffer");
            public static readonly int _AllInstancesIndexBuffer = Shader.PropertyToID("_AllInstancesIndexBuffer");
            public static readonly int _AllVisibleInstancesIndexBuffer = Shader.PropertyToID("_AllVisibleInstancesIndexBuffer");

            public static readonly int _RWVisibleInstancesIndexBuffer = Shader.PropertyToID("_RWVisibleInstancesIndexBuffer");
            public static readonly int _RWVisibleIndirectArgumentBuffer = Shader.PropertyToID("_RWVisibleIndirectArgumentBuffer");
        }
    }
}
