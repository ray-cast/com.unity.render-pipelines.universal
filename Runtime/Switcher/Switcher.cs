using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
	[ExecuteAlways]
	[Serializable]
	public sealed class Switcher : MonoBehaviour
	{
        /// <summary>
        /// Specifies whether to apply the Switcher to the entire Scene or not.
        /// </summary>
        [Tooltip("When enabled, HDRP applies this Switcher to the entire Scene.")]
        public bool isGlobal = true;

        /// <summary>
        /// The Switcher priority in the stack. A higher value means higher priority. This supports negative values.
        /// </summary>
        [Tooltip("Sets the Switcher priority in the stack. A higher value means higher priority. You can use negative values.")]
        public float priority = 0f;

        /// <summary>
        /// The outer distance to start blending from. A value of 0 means no blending and Unity applies
        /// the Switcher overrides immediately upon entry.
        /// </summary>
        [Tooltip("Sets the outer distance to start blending from. A value of 0 means no blending and Unity applies the Switcher overrides immediately upon entry.")]
        public float blendDistance = 0f;

        /// <summary>
        /// The total weight of this volume in the Scene. 0 means no effect and 1 means full effect.
        /// </summary>
        [Range(0f, 1f), Tooltip("Sets the total weight of this Switcher in the Scene. 0 means no effect and 1 means full effect.")]
        public float weight = 1f;

        /// <summary>
        /// A list of every setting that this Switcher Profile stores.
        /// </summary>
        public List<SwitcherComponent> components = new List<SwitcherComponent>();

        int _previousLayer;
        float _previousPriority;

        void OnEnable()
		{
			_previousLayer = gameObject.layer;
			SwitcherManager.instance.Register(this, _previousLayer);
		}

		void OnDisable()
		{
			SwitcherManager.instance.Unregister(this, gameObject.layer);
		}

        void Update()
        {
            UpdateLayer();

            if (priority != _previousPriority)
            {
                SwitcherManager.instance.SetLayerDirty(gameObject.layer);
                _previousPriority = priority;
            }
        }

        internal void UpdateLayer()
        {
            int layer = gameObject.layer;
            if (layer != _previousLayer)
            {
                SwitcherManager.instance.UpdateSwitcherLayer(this, _previousLayer, layer);
                _previousLayer = layer;
            }
        }

#if UNITY_EDITOR
        List<Collider> m_TempColliders;

        void OnDrawGizmos()
        {
            if (m_TempColliders == null)
                m_TempColliders = new List<Collider>();

            var colliders = m_TempColliders;
            GetComponents(colliders);

            if (isGlobal || colliders == null)
                return;

            var scale = transform.localScale;
            var invScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, scale);
            Gizmos.color = CoreRenderPipelinePreferences.volumeGizmoColor;

            foreach (var collider in colliders)
            {
                if (!collider.enabled)
                    continue;

                switch (collider)
                {
                    case BoxCollider c:
                        Gizmos.DrawCube(c.center, c.size);
                        break;
                    case SphereCollider c:
                        // For sphere the only scale that is used is the transform.x
                        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one * scale.x);
                        Gizmos.DrawSphere(c.center, c.radius);
                        break;
                    case MeshCollider c:
                        // Only convex mesh m_Colliders are allowed
                        if (!c.convex)
                            c.convex = true;

                        // Mesh pivot should be centered or this won't work
                        Gizmos.DrawMesh(c.sharedMesh);
                        break;
                    default:
                        // Nothing for capsule (DrawCapsule isn't exposed in Gizmo), terrain, wheel and
                        // other m_Colliders...
                        break;
                }
            }

            colliders.Clear();
        }
#endif
    }
}