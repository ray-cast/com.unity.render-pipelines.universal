using System;

namespace UnityEngine.Rendering.Universal
{
    [Serializable]
    public class FlowerPrototype
    {
        [NonSerialized]
        public InstancedIndirectFlowerRenderer renderer;
        public Vector3 worldPos;//单棵草的位置，基于世界坐标
        public Vector3 finalWorldPos
        {
            get
            {
                //把所有父结点的缩放也考虑进去
                Vector3 lossy = renderer.transform.lossyScale;
                return new Vector3(worldPos.x * lossy.x, worldPos.y * lossy.y, worldPos.z * lossy.z);
            }
            set {
                Vector3 lossy = renderer.transform.lossyScale;
                worldPos = new Vector3(value.x / lossy.x, value.y / lossy.y, value.z / lossy.z);
            }
        }
        public FlowerPrototype(InstancedIndirectFlowerRenderer renderer)
        {
            this.renderer = renderer;
        }
    }
}
