using System;
using System.Collections.Generic;

using Unity.Jobs;
using Unity.Collections;

namespace UnityEngine.Rendering.Universal
{

    public class BatchRenderer : MonoBehaviour
    {
        [SerializeField] private GameObject[] prefabs;
        [SerializeField] private int split = 10;

        private BatchRendererGroup _batchRendererGroup;

        private JobHandle _jobDependency;

        private List<int> _batchIndex;

        private void OnEnable()
        {
            _batchRendererGroup = new BatchRendererGroup(CullingCallback);
            _batchIndex = new List<int>(10);

            foreach (var prefab in prefabs)
                SetupBatch(prefab);

        }

        private static Mesh GetMesh(Component renderer)
        {
            if (renderer is MeshRenderer)
                return renderer.GetComponent<MeshFilter>().sharedMesh;

            var meshRenderer = renderer as SkinnedMeshRenderer;
            if (meshRenderer != null)
                return meshRenderer.sharedMesh;

            throw new InvalidOperationException();
        }

        private void SetupBatch(GameObject prefab)
        {
            var materials = new List<Material>(10);
            var renderers = prefab.GetComponentsInChildren<Renderer>();

            foreach (var renderer in renderers)
            {
                renderer.GetSharedMaterials(materials);

                var mesh = GetMesh(renderer);

                foreach (var material in materials)
                {
                    _batchIndex.Add(_batchRendererGroup.AddBatch(
                        mesh,
                        0,
                        material,
                        0,
                        ShadowCastingMode.Off,
                        false,
                        false,
                        new Bounds(Vector3.zero, Vector3.one * float.MaxValue),
                        split * split * split,
                        null,
                        gameObject));
                }
            }
        }

        private void OnDisable()
        {
            _batchIndex.Clear();
            _batchRendererGroup.Dispose();
        }

        private void Update()
        {
            _jobDependency.Complete();

            var jobHandlers = new NativeArray<JobHandle>(_batchIndex.Count, Allocator.Temp);
            foreach (var batchIndex in _batchIndex)
            {
                jobHandlers[batchIndex] = (new UpdateMatrixJob
                {
                    Matrices = _batchRendererGroup.GetBatchMatrices(batchIndex),
                    Time = Time.time,
                    Split = split
                }.Schedule(split * split * split, 16));
            }

            _jobDependency = JobHandle.CombineDependencies(jobHandlers);
            jobHandlers.Dispose();
        }

        private JobHandle CullingCallback(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext)
        {
            _jobDependency.Complete();

            for (var batchIndex = 0; batchIndex < cullingContext.batchVisibility.Length; ++batchIndex)
            {
                var batchVisibility = cullingContext.batchVisibility[batchIndex];

                for (var i = 0; i < batchVisibility.instancesCount; ++i)
                {
                    cullingContext.visibleIndices[batchVisibility.offset + i] = batchVisibility.offset + i;
                }

                batchVisibility.visibleCount = batchVisibility.instancesCount;
                cullingContext.batchVisibility[batchIndex] = batchVisibility;
            }

            return default;
        }

        private struct UpdateMatrixJob : IJobParallelFor
        {
            public NativeArray<Matrix4x4> Matrices;
            public float Time;
            public float Split;

            public void Execute(int index)
            {
                var id = new Vector3(index / Split / Split, index / Split % Split, index % Split);
                Matrices[index] = Matrix4x4.TRS(id * 10, Quaternion.Euler(id + Vector3.one * Time), Vector3.one);
            }
        }
    }
}
