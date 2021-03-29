using UnityEditor;
using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    internal enum GrassProfileId
    {
        FrustumCulling,
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

        private bool _shouldBatchDispatch = true;
        private bool _shouldUpdateInstanceData = true; //草数据变化的标志位
        private List<int> _visibleCellIDList = new List<int>();
        private Plane[] _cameraFrustumPlanes = new Plane[6];

        private int _computeFrustumCulling;
        private int _computeOcclusionCulling;

        [Reload("Resources/CullingCompute.compute")]
        private ComputeShader _cullingComputeShader;

        private ComputeBuffer _allInstancesPosWSBuffer;
        private ComputeBuffer _allInstancesIndexBuffer;
        private ComputeBuffer _allColorsBuffer;
        private ComputeBuffer _allScalesBuffer;
        private ComputeBuffer _allVisibleInstancesIndexBuffer;
        private ComputeBuffer _argsBuffer;

        private Vector4[] _allColors;
        private Vector4[] _allScales;

#if UNITY_EDITOR
        public int drawInstancedCount;
#endif

        private void OnEnable()
        {
            if (grassGroup == null)
                grassGroup = new GrassGroup();

            grassGroup.Init(this);
            grassGroup.onChange += OnGrassGroupChange;

            _transform = transform;
            _cacheTransformPos = _transform.position;
            _shouldUpdateInstanceData = true;
            _profilingSampler = new ProfilingSampler(ShaderConstants._renderTag);
            _shouldBatchDispatch = true;

            _allScales = new Vector4[GrassGroup.maxScaleLimits];
            _allColors = new Vector4[GrassGroup.maxColorLimits * 2];

            _cullingComputeShader = Resources.Load<ComputeShader>("CullingCompute");
            _computeFrustumCulling = _cullingComputeShader.FindKernel("ComputeFrustumCulling");
            _computeOcclusionCulling = _cullingComputeShader.FindKernel("ComputeOcclusionCulling");

            DrawObjectsPass.DrawOpaqueAction += Render;
            DrawObjectsPass.ConfigureOpaqueAction += Configure;
        }

        public void OnDisable()
        {
            DrawObjectsPass.DrawOpaqueAction -= Render;
            DrawObjectsPass.ConfigureOpaqueAction -= Configure;

            _allInstancesPosWSBuffer?.Release();
            _allInstancesIndexBuffer?.Release();
            _allColorsBuffer?.Release();
            _allScalesBuffer?.Release();
            _allVisibleInstancesIndexBuffer?.Release();
            _argsBuffer?.Release();

            _allInstancesPosWSBuffer = null;
            _allInstancesIndexBuffer = null;
            _allColorsBuffer = null;
            _allScalesBuffer = null;
            _allVisibleInstancesIndexBuffer = null;
            _argsBuffer = null;

            grassGroup.onChange -= OnGrassGroupChange;
        }

        void OnGrassGroupChange()
        {
            _shouldUpdateInstanceData = true;
        }

        public void Update()
        {
            if (_cacheTransformPos != _transform.position)
			{
                _shouldUpdateInstanceData = true;
                _cacheTransformPos = _transform.position;
            }
        }

        void InitializeColorConstants()
        {
            for (int i = 0; i < grassGroup.allColors.Count && i < GrassGroup.maxColorLimits; i++)
            {
                _allColors[i * 2] = grassGroup.allColors[i].dryColorFinal;
                _allColors[i * 2 + 1] = grassGroup.allColors[i].healthyColorFinal;
            }

            _allColorsBuffer?.Release();
            _allColorsBuffer = new ComputeBuffer(_allColors.Length, sizeof(float) * 4);
            _allColorsBuffer.SetData(_allColors);
        }

        void InitializeScaleConstants()
		{
            for (int i = 0; i < grassGroup.allScales.Count && i < GrassGroup.maxScaleLimits; i++)
            {
                Vector3 scale = Vector3.Scale(grassGroup.allScales[i], transform.lossyScale);// 把所有父结点的缩放也考虑进去
                _allScales[i] = new Vector4(scale.x, scale.y, scale.z, 1.0f);
            }

            _allScalesBuffer?.Release();
            _allScalesBuffer = new ComputeBuffer(_allScales.Length, sizeof(float) * 4);
            _allScalesBuffer.SetData(_allScales);
        }

        void InitializeInstanceGridConstants()
        {
            boundingBox.SetMinMax(Vector3.positiveInfinity, Vector3.negativeInfinity);

            ref var allGrassPos = ref grassGroup.grasses;

            for (int i = 0; i < allGrassPos.Count; i++)
                boundingBox.Encapsulate(allGrassPos[i].finalWorldPos);

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

            //binning, put each posWS into the correct cell
            for (int i = 0; i < allGrassPos.Count; i++)
            {
                GrassPrototype gp = allGrassPos[i];
                Vector3 pos = gp.finalWorldPos;

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
                    allGrassPosWSSortedByCell[offset] = grassCell.grasses[j].finalWorldPos;
                    allGrassIndexSortedByCell[offset] = grassCell.grasses[j].allIndexes;
                    offset++;
                }
            }

            _allInstancesPosWSBuffer?.Release();
            _allInstancesPosWSBuffer = new ComputeBuffer(allGrassPos.Count, sizeof(float) * 3); //float3 posWS only, per grass
            _allInstancesPosWSBuffer.SetData(allGrassPosWSSortedByCell);

            _allInstancesIndexBuffer?.Release();
            _allInstancesIndexBuffer = new ComputeBuffer(allGrassPos.Count, sizeof(uint) * 3);//uint3 drycolorindex healthycolorindex scaleindex
            _allInstancesIndexBuffer.SetData(allGrassIndexSortedByCell);

            _allVisibleInstancesIndexBuffer?.Release();
            _allVisibleInstancesIndexBuffer = new ComputeBuffer(allGrassPos.Count, sizeof(uint), ComputeBufferType.Append); //uint only, per visible grass

            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            args[0] = (uint)grassGroup.cachedGrassMesh.GetIndexCount(0);
            args[1] = (uint)allGrassPos.Count;
            args[2] = (uint)grassGroup.cachedGrassMesh.GetIndexStart(0);
            args[3] = (uint)grassGroup.cachedGrassMesh.GetBaseVertex(0);
            args[4] = 0;

            _argsBuffer?.Release();
            _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            _argsBuffer.SetData(args);
        }

        void SetupAllInstanceDataConstants()
		{
            ref var allGrassPos = ref grassGroup.grasses;

            if (allGrassPos.Count > 0)
            {
                this.InitializeColorConstants();
                this.InitializeScaleConstants();
                this.InitializeInstanceGridConstants();

                grassGroup.instanceMaterial.SetVectorArray(ShaderConstants._AllColorsBuffer, _allColors);
                grassGroup.instanceMaterial.SetVectorArray(ShaderConstants._AllScalesBuffer, _allScales);
                grassGroup.instanceMaterial.SetBuffer(ShaderConstants._AllInstancesTransformBuffer, _allInstancesPosWSBuffer);
                grassGroup.instanceMaterial.SetBuffer(ShaderConstants._AllInstancesIndexBuffer, _allInstancesIndexBuffer);
                grassGroup.instanceMaterial.SetBuffer(ShaderConstants._AllVisibleInstancesIndexBuffer, _allVisibleInstancesIndexBuffer);
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
                _allColorsBuffer != null &&
                _allScalesBuffer != null &&
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

        void Configure(ref CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
                return;

            if (grassGroup.instanceMaterial == null || _cullingComputeShader == null || grassGroup.grasses.Count == 0)
                return;

#if UNITY_EDITOR
            if (!(EditorApplication.isPlaying && renderingData.cameraData.isSceneViewCamera))
#endif
            {
                UpdateAllInstanceTransformBufferIfNeeded();

                Camera cam = renderingData.cameraData.camera;
                float cameraOriginalFarPlane = cam.farClipPlane;
                cam.farClipPlane = grassGroup.maxDrawDistance;
                GeometryUtility.CalculateFrustumPlanes(cam, _cameraFrustumPlanes);
                cam.farClipPlane = cameraOriginalFarPlane;

                if (!GeometryUtility.TestPlanesAABB(_cameraFrustumPlanes, boundingBox))
                    return;

                using (new ProfilingScope(cmd, ProfilingSampler.Get(GrassProfileId.FrustumCulling)))
                {
                    _visibleCellIDList.Clear();

                    if (grassGroup.isCpuCulling)
                    {
                        //slow loop
                        //TODO: (A)replace this forloop by a quadtree test?
                        //TODO: (B)convert this forloop to job+burst? (UnityException: TestPlanesAABB can only be called from the main thread.)
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
                }

                _allVisibleInstancesIndexBuffer.SetCounterValue(0);

                var occlusionKernel = HizPass._hizRenderTarget ? this._computeOcclusionCulling : this._computeFrustumCulling;
                cmd.SetComputeMatrixParam(_cullingComputeShader, ShaderConstants._VPMatrix, cam.projectionMatrix * cam.worldToCameraMatrix);
                cmd.SetComputeFloatParam(_cullingComputeShader, ShaderConstants._MaxDrawDistance, grassGroup.maxDrawDistance);
                cmd.SetComputeFloatParam(_cullingComputeShader, ShaderConstants._CameraFov, Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad));
                cmd.SetComputeBufferParam(_cullingComputeShader, occlusionKernel, ShaderConstants._AllInstancesPosWSBuffer, _allInstancesPosWSBuffer);
                cmd.SetComputeBufferParam(_cullingComputeShader, occlusionKernel, ShaderConstants._AllVisibleInstancesIndexBuffer, _allVisibleInstancesIndexBuffer);

                if (HizPass._hizRenderTarget)
                {
                    cmd.SetComputeTextureParam(_cullingComputeShader, occlusionKernel, ShaderConstants._HizTexture, HizPass._hizRenderTarget);
                    cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._HizSize, new Vector4(HizPass._hizRenderTarget.width, HizPass._hizRenderTarget.height, 0, 0));
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

                cmd.CopyCounterValue(_allVisibleInstancesIndexBuffer, _argsBuffer, 4);
            }
        }

        void Render(ref CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
                return;

            if (grassGroup.instanceMaterial == null || _argsBuffer == null || grassGroup.grasses.Count == 0)
                return;

#if UNITY_EDITOR
            uint[] counter = new uint[5];
            _argsBuffer.GetData(counter);
            drawInstancedCount = (int)counter[1];
#endif

            using (new ProfilingScope(cmd, ShaderConstants._profilingSampler))
            {
                grassGroup.instanceMaterial.SetVector(ShaderConstants._PivotPosWS, _transform.position);
                grassGroup.instanceMaterial.SetVector(ShaderConstants._BoundSize, _transform.localScale);

#if UNITY_EDITOR
                grassGroup.instanceMaterial.SetVectorArray(ShaderConstants._AllColorsBuffer, _allColors);
                grassGroup.instanceMaterial.SetVectorArray(ShaderConstants._AllScalesBuffer, _allScales);

                grassGroup.instanceMaterial.SetBuffer(ShaderConstants._AllInstancesTransformBuffer, _allInstancesPosWSBuffer);
                grassGroup.instanceMaterial.SetBuffer(ShaderConstants._AllInstancesIndexBuffer, _allInstancesIndexBuffer);
                grassGroup.instanceMaterial.SetBuffer(ShaderConstants._AllVisibleInstancesIndexBuffer, _allVisibleInstancesIndexBuffer);
#endif

                cmd.DrawMeshInstancedIndirect(grassGroup.cachedGrassMesh, 0, grassGroup.instanceMaterial, 0, _argsBuffer);
            }
        }

        static class ShaderConstants
        {
            public const string _configureTag = "Setup Grass Constants";
            public const string _renderTag = "Draw Grass Instanced";

            public static ProfilingSampler _profilingSampler = new ProfilingSampler(_renderTag);

            public static readonly int _VPMatrix = Shader.PropertyToID("_VPMatrix");
            public static readonly int _MaxDrawDistance = Shader.PropertyToID("_MaxDrawDistance");
            public static readonly int _CameraFov = Shader.PropertyToID("_CameraFov");
            public static readonly int _HizTexture = Shader.PropertyToID("_HizTexture");
            public static readonly int _HizSize = Shader.PropertyToID("_HizSize");
            public static readonly int _StartOffset = Shader.PropertyToID("_StartOffset");
            public static readonly int _EndOffset = Shader.PropertyToID("_EndOffset");

            public static readonly int _PivotPosWS = Shader.PropertyToID("_PivotPosWS");
            public static readonly int _BoundSize = Shader.PropertyToID("_BoundSize");            

            public static readonly int _AllColorsBuffer = Shader.PropertyToID("_AllColorsBuffer");
            public static readonly int _AllScalesBuffer = Shader.PropertyToID("_AllScalesBuffer");
            public static readonly int _AllInstancesPosWSBuffer = Shader.PropertyToID("_AllInstancesPosWSBuffer");
            public static readonly int _AllInstancesTransformBuffer = Shader.PropertyToID("_AllInstancesTransformBuffer");
            public static readonly int _AllInstancesIndexBuffer = Shader.PropertyToID("_AllInstancesIndexBuffer");
            public static readonly int _AllVisibleInstancesIndexBuffer = Shader.PropertyToID("_AllVisibleInstancesIndexBuffer");
        }
    }
}
