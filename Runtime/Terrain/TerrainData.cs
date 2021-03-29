namespace UnityEngine.Rendering.Universal
{
    public struct UInt3
    {
        uint x;
        uint y;
        uint z;
        public UInt3(uint x, uint y, uint z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    public class TerrainData : ScriptableObject
    {
    }
}