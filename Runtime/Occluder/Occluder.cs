using System;

namespace UnityEngine.Rendering.Universal
{
    [ExecuteAlways]
    [Serializable]
    public sealed class Occluder : MonoBehaviour
    {
        public float radius;

        public Vector3 center;

        private int _previousLayer = 0;

        public Vector3 position
		{
            get
			{
                var scale = transform.localScale;
                var matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one * scale.x);
                return matrix.MultiplyPoint(center);
            }
		}

        void OnEnable()
        {
            OccluderManager.instance.Register(this, _previousLayer);
        }

        void OnDisable()
        {
            OccluderManager.instance.Unregister(this, gameObject.layer);
        }

        internal void UpdateLayer()
        {
            int layer = gameObject.layer;
            if (layer != _previousLayer)
            {
                OccluderManager.instance.UpdateOccluderLayer(this, _previousLayer, layer);
                _previousLayer = layer;
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (this.enabled)
			{
                var scale = transform.localScale;

                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, scale);
                Gizmos.color = CoreRenderPipelinePreferences.volumeGizmoColor;

                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one * scale.x);
                Gizmos.DrawWireSphere(this.center, this.radius);
            }
        }
#endif
    }
}