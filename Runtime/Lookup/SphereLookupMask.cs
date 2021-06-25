using System;
using UnityEditor;

namespace UnityEngine.Rendering.Universal
{
    [ExecuteAlways]
    [Serializable]
    public sealed class SphereLookupMask : MonoBehaviour
    {
        public float radius = 1.0f;
        public Vector3 center;

        private int _previousLayer = 0;

        public Vector3 position
		{
            get
			{
                return transform.localToWorldMatrix.MultiplyPoint(center);
            }
		}

        void OnEnable()
        {
            VignetteLookupManager.instance.Register(this, _previousLayer);
        }

        void OnDisable()
        {
            VignetteLookupManager.instance.Unregister(this, gameObject.layer);
        }

        internal void UpdateLayer()
        {
            int layer = gameObject.layer;
            if (layer != _previousLayer)
            {
                VignetteLookupManager.instance.UpdateOccluderLayer(this, _previousLayer, layer);
                _previousLayer = layer;
            }
        }

#if UNITY_EDITOR
        public void DrawWireCapsule(Vector3 pos, float radius, Color color = default(Color))
        {
            Gizmos.color = color;
            Gizmos.DrawSphere(pos, radius);
        }

        void OnDrawGizmos()
        {
            if (this.enabled)
                this.DrawWireCapsule(this.position, radius);
        }
#endif
    }
}