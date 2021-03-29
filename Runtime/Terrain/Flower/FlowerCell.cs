using UnityEngine.Assertions;
using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    public class FlowerCell
    {
        public List<FlowerPrototype> flowers;//在此单元格子中的所有花
        public Bounds cellBound;//包含所有花的包围盒
        public FlowerCell(Vector3 cellCenterPos, float cellSizeX, float cellSizeZ)//y方向上的size要根据所有花的实际高度去算出来
        {
            flowers = new List<FlowerPrototype>();
            Vector3 size = new Vector3(cellSizeX, 0, cellSizeZ);
            cellBound = new Bounds(cellCenterPos, size);
        }
        public void AddGrass(FlowerPrototype fp)
        {
            Assert.IsTrue(fp.finalWorldPos.x >= cellBound.min.x && fp.finalWorldPos.x <= cellBound.max.x);
            Assert.IsTrue(fp.finalWorldPos.z >= cellBound.min.z && fp.finalWorldPos.z <= cellBound.max.z);

            cellBound.Encapsulate(fp.finalWorldPos);
            flowers.Add(fp);
        }
    }
}
