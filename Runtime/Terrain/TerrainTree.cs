using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    public sealed class TerrainTree
    {
        public Rect rect;
        public int mip;
        public int index;
        public TerrainPatch patch;
        public TerrainTree[] children;

        public TerrainTree(Rect r)
        {
            this.rect = r;
            this.index = -1;
            this.mip = -1;
        }

        public TerrainTree(Rect r, int m)
        {
            this.rect = r;
            this.mip = m;
            this.patch = new TerrainPatch(new Vector4(r.xMin, r.yMin, r.width, r.height), m);
            this.index = -1;

            if (this.mip > 0)
            {
                var halfWidth = r.width / 2;
                var halfHeight = r.height / 2;

                children = new TerrainTree[4];
                children[0] = new TerrainTree(new Rect(r.xMin, r.yMin, halfWidth, halfHeight), m - 1);
                children[1] = new TerrainTree(new Rect(r.xMin + halfWidth, r.yMin, halfWidth, halfHeight), m - 1);
                children[2] = new TerrainTree(new Rect(r.xMin + halfWidth, r.yMin + halfHeight, halfWidth, halfHeight), m - 1);
                children[3] = new TerrainTree(new Rect(r.xMin, r.yMin + halfHeight, halfWidth, halfHeight), m - 1);
            }
        }

        public TerrainTree GetActiveNode(Vector2 center)
        {
            if (rect.Contains(center))
            {
                if (index >= 0)
                {
                    return this;
                }
                else
                {
                    foreach (var child in children)
                    {
                        var ans = child.GetActiveNode(center);
                        if (ans != null)
                        {
                            return ans;
                        }
                    }
                }
            }

            return null;
        }

        public void CollectNodeInfo(Vector2 center, List<TerrainPatch> pacthes)
        {
            if (mip >= 0 && (mip == 0 || (center - rect.center).magnitude >= 100 * Mathf.Pow(2, mip)))
            {
                this.index = pacthes.Count;
                pacthes.Add(this.patch);
            }
            else
            {
                this.index = -1;
                foreach (var child in children)
                {
                    child.CollectNodeInfo(center, pacthes);
                }
            }
        }
    }
}