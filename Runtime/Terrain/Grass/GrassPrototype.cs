using System;

namespace UnityEngine.Rendering.Universal
{
    [Serializable]
    public class GrassPrototype
    {
        [NonSerialized]
        public InstancedIndirectGrassRenderer renderer;
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
        [SerializeField]
        uint dryColorIndex=0;//项部颜色的数组索引
        [SerializeField]
        uint healthyColorIndex=1;//底部颜色的数组索引
        [SerializeField]
        public uint scaleIndex=0;//缩放的数组索引
        public uint colorIndex
        {
            get { return dryColorIndex / 2; }
            set
            {
                dryColorIndex = value * 2;
                healthyColorIndex = dryColorIndex + 1;
            }
        }
        public UInt3 allIndexes { get
            {
                return new UInt3(dryColorIndex, healthyColorIndex, scaleIndex);
            }
        }
        public GrassPrototype(InstancedIndirectGrassRenderer renderer)
        {
            this.renderer = renderer;
        }
    }
    [Serializable]
    public class GrassColor
    {
        public Color dryColor =  new Color(178f / 255, 221f / 255, 66f / 255);
        public Color dryColorFinal
        {
            get
            {
                //要跟颜色空间一致
                if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                    return dryColor.linear;
                else if (QualitySettings.activeColorSpace == ColorSpace.Gamma)
                    return dryColor.gamma;
                else
                    return dryColor;
            }
        }
        public Color healthyColor = new Color(66f / 255, 150f / 255, 80f / 255);
        public Color healthyColorFinal
        {
            get
            {
                //要跟颜色空间一致
                if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                    return healthyColor.linear;
                else if (QualitySettings.activeColorSpace == ColorSpace.Gamma)
                    return healthyColor.gamma;
                else
                    return healthyColor;
            }
        }
        public bool isUsing = false;
    }
}
