using UnityEngine.Assertions;
using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    public class GrassCell
    {
        public List<GrassPrototype> grasses;//在此单元格子中的所有草
        public Bounds cellBound;//包含所有草的包围盒
        public GrassCell(Vector3 cellCenterPos, float cellSizeX, float cellSizeZ)//y方向上的size要根据所有草的实际高度去算出来
        {
            grasses = new List<GrassPrototype>();
            Vector3 size = new Vector3(cellSizeX, 0, cellSizeZ);
            cellBound = new Bounds(cellCenterPos, size);
        }
        public void AddGrass(GrassPrototype gp)
        {
            Assert.IsTrue(gp.worldPos.x >= cellBound.min.x && gp.worldPos.x <= cellBound.max.x);
            Assert.IsTrue(gp.worldPos.z >= cellBound.min.z && gp.worldPos.z <= cellBound.max.z);

            cellBound.Encapsulate(gp.worldPos);
            grasses.Add(gp);
        }
    }
}
