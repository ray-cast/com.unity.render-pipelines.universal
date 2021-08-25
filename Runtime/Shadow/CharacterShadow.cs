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

        public float range = 10;

        public List<Renderable> _renderable = new List<Renderable>();

        public Bounds worldBoundingBox
        {
            get { return _boundingBox; }
        }

        public void SetCasterMainShadow(bool enable)
        {
            var renderers = this.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                for (int j = 0; j < renderers.Length; j++)
                    renderers[j].renderingLayerMask = enable ? 1u : 2u;
            }
        }

		private void UpdateShadowData()
		{
            var renderers = this.GetComponentsInChildren<Renderer>();

            foreach (var renderable in _renderable)
            {
                if (renderable.isSkinnedMesh)
                    DestroyImmediate(renderable.mesh);
            }

            _renderable.Clear();

            if (renderers.Length > 0)
            {
                var startIndex = -1;

                for (int i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    if (renderer.isVisible && renderer.shadowCastingMode != ShadowCastingMode.Off)
                    {
                        startIndex = i;
                        break;
                    }
                }

                if (startIndex >= 0)
                {
                    _boundingBox = renderers[startIndex].bounds;

                    for (int j = startIndex + 1; j < renderers.Length; j++)
                    {
                        var renderer = renderers[j];
                        if (renderer.isVisible && renderer.shadowCastingMode != ShadowCastingMode.Off)
                            _boundingBox.Encapsulate(renderers[j].bounds);
                    }
                }
                else
                {
                    _boundingBox.SetMinMax(Vector3.zero, Vector3.zero);
                }

                foreach (var renderer in renderers)
                {
                    if (!renderer.isVisible)
                        continue;

                    if (renderer.shadowCastingMode == ShadowCastingMode.Off)
                        continue;

#if UNITY_EDITOR
                    var material = renderer.sharedMaterial;
#else
                    var material = renderer.material;
#endif
                    if (material == null)
                        continue;

                    var renderable = new Renderable();
                    renderable.material = material;
                    renderable.localToWorldMatrix = renderer.transform.localToWorldMatrix;
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

		private void OnEnable()
        {
            SetCasterMainShadow(false);
            CharacterShadowManager.instance.Register(this, _previousLayer);
            RenderPipelineManager.beginFrameRendering += OnBeginFrameRendering;
        }

        private void OnDisable()
        {
            SetCasterMainShadow(true);
            CharacterShadowManager.instance.Unregister(this, gameObject.layer);
            RenderPipelineManager.beginFrameRendering -= OnBeginFrameRendering;
        }

		private void OnDestroy()
		{
            foreach (var renderable in _renderable)
			{
                if (renderable.isSkinnedMesh)
                    DestroyImmediate(renderable.mesh);
            }
        }

        public void OnBeginFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            this.UpdateShadowData();
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (this.isActiveAndEnabled)
			{
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.color = CoreRenderPipelinePreferences.volumeGizmoColor;

                Gizmos.DrawWireCube(worldBoundingBox.center, worldBoundingBox.size);
            }
        }
#endif
    }
}