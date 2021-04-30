using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    public class MeshBatchChunk
    {
        public List<BatchData> data;
        public Bounds boundingBox;

        public MeshBatchChunk(Vector3 center, float sizeX, float sizeZ)
        {
            data = new List<BatchData>();
            boundingBox = new Bounds(center, new Vector3(sizeX, 0, sizeZ));
        }

        public void AddGrass(BatchData fp)
        {
            boundingBox.Encapsulate(fp.worldPos);
            data.Add(fp);
        }
    }
}
