using System;

namespace UnityEngine.Rendering.Universal
{
    [Serializable]
    public class GrassPrototype
    {
        public Vector3 worldPos;//单棵草的位置，基于世界坐标

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

        public UInt3 allIndexes 
        {
            get
            {
                return new UInt3(dryColorIndex, healthyColorIndex, scaleIndex);
            }
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
