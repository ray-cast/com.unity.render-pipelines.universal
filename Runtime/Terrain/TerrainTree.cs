using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    public sealed class TerrainTree
    {
        public Rect rect;
        public int mip;
        public float roughness;
        public TerrainPatch patch;
        public TerrainTree[] children;

        public TerrainTree(Rect r)
        {
            this.rect = r;
            this.mip = -1;
            this.roughness = 1;
        }

        public TerrainTree(Rect r, int m)
        {
            this.rect = r;
            this.mip = m;
            this.patch = new TerrainPatch(new Vector4(r.xMin, r.yMin, r.width, r.height), m);
            this.roughness = 1.0f;

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

        public void setHole(float x, float y)
		{
            if (x >= rect.x && y >= rect.y && x <= (rect.x + rect.width) && y <= (rect.y + rect.height))
			{
                this.patch.hole = 1;

                if (children != null)
				{
                    foreach (var child in children)
                        child.setHole(x, y);
                }
            }
		}

        public float Evaluate(TerrainData terrainData)
		{
            var width = terrainData.size.x;
            var height = terrainData.size.z;
            var dh = new Rect(rect.x / (float)width, rect.y / (float)height, rect.width / (float)width, rect.height / (float)height);
            var dh0 = terrainData.GetInterpolatedHeight(dh.center.x, dh.center.y);
            var dh1 = terrainData.GetInterpolatedHeight(dh.center.x, dh.yMax);
            var dh2 = terrainData.GetInterpolatedHeight(dh.xMin, dh.center.y);
            var dh3 = terrainData.GetInterpolatedHeight(dh.center.x, dh.yMin);
            var dh4 = terrainData.GetInterpolatedHeight(dh.xMax, dh.center.y);

            this.roughness = Mathf.Max(Mathf.Max(Mathf.Max(Mathf.Max(dh0, dh1), dh2), dh3), dh4) / rect.width;

            if (mip > 1)
			{
                foreach (var child in children)
                    this.roughness = Mathf.Max(roughness, child.Evaluate(terrainData));
            }

            return roughness;
        }

        public void CollectNodeInfo(Vector2 center, float factor, ref List<TerrainPatch> pacthes)
        {
            // http://files.cppblog.com/AstaTus/largeLOD.pdf
            var l = (center - rect.center).magnitude;
            var r = roughness + this.patch.hole;
            var f = l / (rect.width * r);

            if (mip == 0 || f >= factor)
            {
                pacthes.Add(this.patch);
            }
            else
            {
                foreach (var child in children)
                {
                    child.CollectNodeInfo(center, factor, ref pacthes);
                }
            }
        }
    }
}