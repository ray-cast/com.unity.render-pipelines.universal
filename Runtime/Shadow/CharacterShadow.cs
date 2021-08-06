using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    [ExecuteAlways]
    [Serializable]
    public sealed class CharacterShadow : MonoBehaviour
    {
        public struct Renderable
        {
            public Mesh mesh;
            public Material material;
            public int shadowPass;
            public Matrix4x4 localToWorldMatrix;
            public bool isSkinnedMesh;
        }

        private int _previousLayer = 0;

        private Bounds _boundingBox = new Bounds();

        public List<Renderable> _renderable = new List<Renderable>();

        public Bounds worldBoundingBox
		{
            get { return _boundingBox; }
		}

        private void OnEnable()
        {
            CharacterShadowManager.instance.Register(this, _previousLayer);
        }

        private void OnDisable()
        {
            CharacterShadowManager.instance.Unregister(this, gameObject.layer);
        }

		private void OnDestroy()
		{
            foreach (var renderable in _renderable)
			{
                if (renderable.isSkinnedMesh)
                    DestroyImmediate(renderable.mesh);
            }
        }

        private void LateUpdate()
		{
            var renderers = this.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                if (renderers[0].shadowCastingMode != ShadowCastingMode.Off)
                    _boundingBox = renderers[0].bounds;

                for (int j = 1; j < renderers.Length; j++)
                {
                    if (renderers[j].shadowCastingMode != ShadowCastingMode.Off)
                        _boundingBox.Encapsulate(renderers[j].bounds);
                }

                foreach (var renderable in _renderable)
				{
                    if (renderable.isSkinnedMesh)
                        DestroyImmediate(renderable.mesh);
                }

                _renderable.Clear();

                foreach (var renderer in renderers)
                {
                    if (renderer.shadowCastingMode == ShadowCastingMode.Off)
                        continue;

                    var renderable = new Renderable();
                    renderable.localToWorldMatrix = renderer.transform.localToWorldMatrix;
#if UNITY_EDITOR
                    renderable.material = renderer.sharedMaterial;
#else
                    renderable.material = renderer.material;
#endif
                    renderable.shadowPass = renderable.material.FindPass("ShadowCaster");
                    renderable.isSkinnedMesh = renderer is SkinnedMeshRenderer;

                    if (renderable.isSkinnedMesh)
                    {
                        renderable.mesh = new Mesh();
                        var smr = renderer as SkinnedMeshRenderer;
                        smr.BakeMesh(renderable.mesh);
                    }
                    else
                    {
#if UNITY_EDITOR
                        renderable.mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
#else
                        renderable.mesh = renderer.GetComponent<MeshFilter>().mesh;
#endif
                    }

                    _renderable.Add(renderable);
                }
            }
            else
			{
                _boundingBox.SetMinMax(Vector3.zero, Vector3.zero);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = CoreRenderPipelinePreferences.volumeGizmoColor;

            Gizmos.DrawWireCube(worldBoundingBox.center, worldBoundingBox.size);
        }
#endif
    }
}