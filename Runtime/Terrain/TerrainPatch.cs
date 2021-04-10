namespace UnityEngine.Rendering.Universal
{
    public struct TerrainPatch
    {
        public Vector4 rect;
        public int mip;
        public int neighbor;
        public int padding1;
        public int padding2;

        public TerrainPatch(Vector4 r, int level)
        {
            rect = r;
            mip = level;
            neighbor = 0;
            padding1 = 0;
            padding2 = 0;
        }
    }
}