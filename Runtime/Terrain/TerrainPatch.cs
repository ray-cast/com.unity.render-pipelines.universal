namespace UnityEngine.Rendering.Universal
{
    public struct TerrainPatch
    {
        public Vector4 rect;
        public int mip;
        public int neighbor;
        public int hole;
        public int padding2;

        public TerrainPatch(Vector4 r, int level, int holed = 0)
        {
            rect = r;
            mip = level;
            neighbor = 0;
            hole = holed;
            padding2 = 0;
        }
    }
}