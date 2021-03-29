using System;
using UnityEngine;

namespace UnityEngine.Rendering.Universal
{
    public abstract class MonoBehaviourGrassBender : MonoBehaviour, IEquatable<MonoBehaviourGrassBender>
    {
        public Vector3 Position => transform.position;
        public float BendRadius { get => bendRadius; set => bendRadius = value; }
        public int Priority { get => priority; set => priority = value; }

        [Tooltip("Radius of the grass bending sphere."), Range(0.1f, 10f)]
        [SerializeField] private float bendRadius = 0.8f;

        [Tooltip("When concurrent bend sources limit is exceeded, benders with lower priority values will be served first.")]
        [SerializeField] private int priority = 0;
        [Tooltip("最小弯曲比例."), Range(0.0f, 0.7f)]
        public float minBend = 0.3f;
        [HideInInspector]
        public Vector3 LastPosition;
        [HideInInspector]
        public float LastRadius;
        [HideInInspector]
        public float changeVelocity = 0;
        //public bool showVec = false;
        [Tooltip("压弯回弹时间")]
        public float targetTime = 0.5f;
        public void SaveLastPosition()
        {
            LastPosition = Position;
        }

        public bool Equals (MonoBehaviourGrassBender other)
        {
            if (other is null) return false;
            return other.GetInstanceID() == GetInstanceID();
        }
    }
}
