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
        public List<FlowerPrototype> allGrassPos;//所有草数据的数组

        //所有格子组成一个二维数组，每个格子关联了位置归属于此格子范围内的所有草
        //将二组数据按[xId,zId]=>xId+zId*cellCountX映射到一维数组就成了cellPosWSsList
        private FlowerCell[] cellPosWSsList; //for binning: binning will put each posWS into correct cell

        private Transform _transform;//草丛渲染器所在的结点
        private Vector3 _cacheTransformPos;//结点位置缓存
        private int _cacheInstanceCount = -1;

        private bool _shouldBatchDispatch = true;
        private bool _shouldUpdateInstanceData = false;// 草数据变化的标志位
        private List<int> _visibleCellIDList = new List<int>();
        private Plane[] _cameraFrustumPlanes = new Plane[6];

        private int _computeFrustumCulling;
        private int _computeOcclusionCulling;

        [Reload("Resources/CullingCompute.compute")]
        private ComputeShader _cullingComputeShader;
        private ComputeBuffer _allInstancesPosWSBuffer;
        private ComputeBuffer _allVisibleInstancesOnlyPosWSIDBuffer;
        private ComputeBuffer _argsBuffer;

#if UNITY_EDITOR
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
            allGrassPos = flowerGroup.floweres;
            flowerGroup.onChange += OnGrassGroupChange;

            _cullingComputeShader = Resources.Load<ComputeShader>("CullingCompute");
            _computeFrustumCulling = _cullingComputeShader.FindKernel("ComputeFrustumCulling");
            _computeOcclusionCulling = _cullingComputeShader.FindKernel("ComputeOcclusionCulling");

            DrawObjectsPass.DrawOpaqueAction += Render;
            DrawObjectsPass.ConfigureOpaqueAction += Configure;
        }

        public void OnDestroy()
        {
            DrawObjectsPass.DrawOpaqueAction -= Render;
            DrawObjectsPass.ConfigureOpaqueAction -= Configure;

            _allInstancesPosWSBuffer?.Release();
            _allVisibleInstancesOnlyPosWSIDBuffer?.Release();
            _argsBuffer?.Release();

            _allInstancesPosWSBuffer = null;
            _allVisibleInstancesOnlyPosWSIDBuffer = null;
            _argsBuffer = null;

            flowerGroup.onChange -= OnGrassGroupChange;
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

        void InitializeInstanceGridConstants()
        {
            boundingBox.SetMinMax(Vector3.positiveInfinity, Vector3.negativeInfinity);
            for (int i = 0; i < allGrassPos.Count; i++)
                boundingBox.Encapsulate(allGrassPos[i].finalWorldPos);

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
            for (int i = 0; i < allGrassPos.Count; i++)
            {
                FlowerPrototype gp = allGrassPos[i];
                Vector3 pos = gp.finalWorldPos;

                int xID = Mathf.Min(cellCountX - 1, Mathf.FloorToInt(Mathf.InverseLerp(min.x, max.x, pos.x) * cellCountX)); //use min to force within 0~[cellCountX-1]  
                int zID = Mathf.Min(cellCountZ - 1, Mathf.FloorToInt(Mathf.InverseLerp(min.z, max.z, pos.z) * cellCountZ)); //use min to force within 0~[cellCountZ-1]

                cellPosWSsList[xID + zID * cellCountX].AddGrass(gp);
            }

            //combine to a flatten array for compute buffer
            Vector3[] allGrassPosWSSortedByCell = new Vector3[allGrassPos.Count];

            for (int i = 0, offset = 0; i < cellPosWSsList.Length; i++)
            {
                for (int j = 0; j < cellPosWSsList[i].flowers.Count; j++)
                {
                    allGrassPosWSSortedByCell[offset] = cellPosWSsList[i].flowers[j].finalWorldPos;
                    offset++;
                }
            }

            _allInstancesPosWSBuffer?.Release();
            _allInstancesPosWSBuffer = new ComputeBuffer(allGrassPos.Count, sizeof(float) * 3); //float3 posWS only, per grass
            _allInstancesPosWSBuffer.SetData(allGrassPosWSSortedByCell);

            _allVisibleInstancesOnlyPosWSIDBuffer?.Release();
            _allVisibleInstancesOnlyPosWSIDBuffer = new ComputeBuffer(allGrassPos.Count, sizeof(uint), ComputeBufferType.Append); //uint only, per visible grass

            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            args[0] = (uint)flowerGroup.cachedGrassMesh.GetIndexCount(0);
            args[1] = (uint)allGrassPos.Count;
            args[2] = (uint)flowerGroup.cachedGrassMesh.GetIndexStart(0);
            args[3] = (uint)flowerGroup.cachedGrassMesh.GetBaseVertex(0);
            args[4] = 0;

            _argsBuffer?.Release();
            _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            _argsBuffer.SetData(args);
        }

        void SetupAllInstanceDataConstants()
        {
            if (allGrassPos.Count > 0)
            {
                this.InitializeInstanceGridConstants();

                flowerGroup.instanceMaterial.SetBuffer(ShaderConstants._AllInstancesTransformBuffer, _allInstancesPosWSBuffer);
                flowerGroup.instanceMaterial.SetBuffer(ShaderConstants._AllVisibleInstancesIndexBuffer, _allVisibleInstancesOnlyPosWSIDBuffer);
            }

            _cacheInstanceCount = allGrassPos.Count;
        }

        void UpdateAllInstanceTransformBufferIfNeeded()
        {
            if (!_shouldUpdateInstanceData && _cacheInstanceCount == allGrassPos.Count &&
                _argsBuffer != null &&
                _allInstancesPosWSBuffer != null &&
                _allVisibleInstancesOnlyPosWSIDBuffer != null)
            {
                return;
            }

            this.SetupAllInstanceDataConstants();
            _shouldUpdateInstanceData = false;
        }

        void Configure(ref CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
                return;

            if (flowerGroup.instanceMaterial == null || _cullingComputeShader == null || allGrassPos.Count == 0)
                return;

#if UNITY_EDITOR
            if (!(EditorApplication.isPlaying && renderingData.cameraData.isSceneViewCamera))
#endif
            {
                UpdateAllInstanceTransformBufferIfNeeded();

                Camera cam = renderingData.cameraData.camera;
                float cameraOriginalFarPlane = cam.farClipPlane;
                cam.farClipPlane = flowerGroup.drawDistance;
                GeometryUtility.CalculateFrustumPlanes(cam, _cameraFrustumPlanes);
                cam.farClipPlane = cameraOriginalFarPlane;

                if (!GeometryUtility.TestPlanesAABB(_cameraFrustumPlanes, boundingBox))
                    return;

                using (new ProfilingScope(cmd, ProfilingSampler.Get(GrassProfileId.FrustumCulling)))
                {
                    _visibleCellIDList.Clear();

                    if (flowerGroup.isCpuCulling)
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

                _allVisibleInstancesOnlyPosWSIDBuffer.SetCounterValue(0);

                var occlusionKernel = HizPass._hizRenderTarget ? this._computeOcclusionCulling : this._computeFrustumCulling;
                cmd.SetComputeMatrixParam(_cullingComputeShader, ShaderConstants._VPMatrix, cam.projectionMatrix * cam.worldToCameraMatrix);
                cmd.SetComputeFloatParam(_cullingComputeShader, ShaderConstants._MaxDrawDistance, flowerGroup.drawDistance);
                cmd.SetComputeFloatParam(_cullingComputeShader, ShaderConstants._CameraFov, Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad));
                cmd.SetComputeBufferParam(_cullingComputeShader, occlusionKernel, ShaderConstants._AllInstancesPosWSBuffer, _allInstancesPosWSBuffer);
                cmd.SetComputeBufferParam(_cullingComputeShader, occlusionKernel, ShaderConstants._AllVisibleInstancesIndexBuffer, _allVisibleInstancesOnlyPosWSIDBuffer);

                if (HizPass._hizRenderTarget)
                {
                    cmd.SetComputeTextureParam(_cullingComputeShader, occlusionKernel, ShaderConstants._HizTexture, HizPass._hizRenderTarget);
                    cmd.SetComputeVectorParam(_cullingComputeShader, ShaderConstants._HizSize, new Vector4(HizPass._hizRenderTarget.width, HizPass._hizRenderTarget.height, 0, 0));
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

                cmd.CopyCounterValue(_allVisibleInstancesOnlyPosWSIDBuffer, _argsBuffer, 4);
            }
        }

        void Render(ref CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
                return;

            if (flowerGroup.instanceMaterial == null || _argsBuffer == null || allGrassPos.Count == 0)
                return;

#if UNITY_EDITOR
            uint[] counter = new uint[5];
            _argsBuffer.GetData(counter);
            drawInstancedCount = (int)counter[1];
#endif

            using (new ProfilingScope(cmd, ShaderConstants._profilingSampler))
            {
                flowerGroup.instanceMaterial.SetVector(ShaderConstants._PivotPosWS, _transform.position);
                flowerGroup.instanceMaterial.SetVector(ShaderConstants._BoundSize, new Vector2(_transform.localScale.x, _transform.localScale.z));
#if UNITY_EDITOR
                flowerGroup.instanceMaterial.SetBuffer(ShaderConstants._AllInstancesTransformBuffer, _allInstancesPosWSBuffer);
                flowerGroup.instanceMaterial.SetBuffer(ShaderConstants._AllVisibleInstancesIndexBuffer, _allVisibleInstancesOnlyPosWSIDBuffer);
#endif

                cmd.DrawMeshInstancedIndirect(flowerGroup.cachedGrassMesh, 0, flowerGroup.instanceMaterial, 0, _argsBuffer);
            }
        }

        static class ShaderConstants
        {
            public const string _configureTag = "Setup Flower Constants";
            public const string _renderTag = "Draw Flower Instanced";

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