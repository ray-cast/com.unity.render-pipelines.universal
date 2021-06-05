using System;
using UnityEditor;

namespace UnityEngine.Rendering.Universal
{
    [ExecuteAlways]
    [Serializable]
    public sealed class CapsuleOccluder : MonoBehaviour
    {
        public enum Axis
		{
            X,
            Y,
            Z
		}

        public float radius = 0.5f;
        public float height = 1;

        public Vector3 center;
        public Axis axis = Axis.Y;

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
            CapsuleOccluderManager.instance.Register(this, _previousLayer);
        }

        void OnDisable()
        {
            CapsuleOccluderManager.instance.Unregister(this, gameObject.layer);
        }

        internal void UpdateLayer()
        {
            int layer = gameObject.layer;
            if (layer != _previousLayer)
            {
                CapsuleOccluderManager.instance.UpdateOccluderLayer(this, _previousLayer, layer);
                _previousLayer = layer;
            }
        }

#if UNITY_EDITOR
        public void DrawWireCapsule(Vector3 pos, Quaternion rot, float radius, float height, Color color = default(Color))
        {
            if (color != default(Color))
                Handles.color = color;

            using (new Handles.DrawingScope(Matrix4x4.TRS(pos, rot, Handles.matrix.lossyScale)))
            {
                var pointOffset = (height - (radius * 2)) / 2;

                if (axis == Axis.X)
				{
                    //draw sideways
                    Handles.DrawWireArc(Vector3.left * pointOffset, Vector3.up, Vector3.back, 180, radius);
                    Handles.DrawLine(new Vector3(pointOffset, -radius, 0), new Vector3(-pointOffset, -radius, 0));
                    Handles.DrawLine(new Vector3(pointOffset, radius, 0), new Vector3(-pointOffset, radius, 0));
                    Handles.DrawWireArc(Vector3.right * pointOffset, Vector3.up, Vector3.back, -180, radius);
                    //draw frontways
                    Handles.DrawWireArc(Vector3.left * pointOffset, Vector3.back, Vector3.down, 180, radius);
                    Handles.DrawLine(new Vector3(pointOffset, 0, -radius), new Vector3(-pointOffset, 0, -radius));
                    Handles.DrawLine(new Vector3(pointOffset, 0, radius), new Vector3(-pointOffset, 0, radius));
                    Handles.DrawWireArc(Vector3.right * pointOffset, Vector3.back, Vector3.down, -180, radius);
                    //draw center
                    Handles.DrawWireDisc(Vector3.left * pointOffset, Vector3.left, radius);
                    Handles.DrawWireDisc(Vector3.right * pointOffset, Vector3.left, radius);
                }
                else if (axis == Axis.Y)
				{
                    //draw sideways
                    Handles.DrawWireArc(Vector3.up * pointOffset, Vector3.left, Vector3.back, -180, radius);
                    Handles.DrawLine(new Vector3(0, pointOffset, -radius), new Vector3(0, -pointOffset, -radius));
                    Handles.DrawLine(new Vector3(0, pointOffset, radius), new Vector3(0, -pointOffset, radius));
                    Handles.DrawWireArc(Vector3.down * pointOffset, Vector3.left, Vector3.back, 180, radius);
                    //draw frontways
                    Handles.DrawWireArc(Vector3.up * pointOffset, Vector3.back, Vector3.left, 180, radius);
                    Handles.DrawLine(new Vector3(-radius, pointOffset, 0), new Vector3(-radius, -pointOffset, 0));
                    Handles.DrawLine(new Vector3(radius, pointOffset, 0), new Vector3(radius, -pointOffset, 0));
                    Handles.DrawWireArc(Vector3.down * pointOffset, Vector3.back, Vector3.left, -180, radius);
                    //draw center
                    Handles.DrawWireDisc(Vector3.up * pointOffset, Vector3.up, radius);
                    Handles.DrawWireDisc(Vector3.down * pointOffset, Vector3.up, radius);
                }
                else
                {
                    //draw sideways
                    Handles.DrawWireArc(Vector3.forward * pointOffset, Vector3.left, Vector3.up, -180, radius);
                    Handles.DrawLine(new Vector3(-radius, 0, pointOffset), new Vector3(-radius, 0, -pointOffset));
                    Handles.DrawLine(new Vector3(radius, 0, pointOffset), new Vector3(radius, 0, -pointOffset));
                    Handles.DrawWireArc(Vector3.back * pointOffset, Vector3.left, Vector3.up, 180, radius);
                    //draw frontways
                    Handles.DrawWireArc(Vector3.forward * pointOffset, Vector3.up, Vector3.left, 180, radius);
                    Handles.DrawLine(new Vector3(0, -radius, pointOffset), new Vector3(0, -radius, -pointOffset));
                    Handles.DrawLine(new Vector3(0, radius, pointOffset), new Vector3(0, radius , -pointOffset));
                    Handles.DrawWireArc(Vector3.back * pointOffset, Vector3.up, Vector3.left, -180, radius);
                    //draw center
                    Handles.DrawWireDisc(Vector3.forward * pointOffset, Vector3.forward, radius);
                    Handles.DrawWireDisc(Vector3.back * pointOffset, Vector3.forward, radius);
                }
            }
        }

        void OnDrawGizmos()
        {
            if (this.enabled)
                this.DrawWireCapsule(this.position, transform.rotation, radius, height);
        }
#endif
    }
}