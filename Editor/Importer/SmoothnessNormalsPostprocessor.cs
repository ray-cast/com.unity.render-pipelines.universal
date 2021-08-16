using System.Collections.Generic;
using UnityEditor;

#if false
namespace UnityEngine.Rendering.Universal
{
    public class SmoothnessNormalsPostprocessor : AssetPostprocessor
    {
        private void OnPostprocessModel(GameObject gameObject)
        {
            foreach (MeshFilter mf in gameObject.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh;

                Dictionary<Vector3, List<int>> map = new Dictionary<Vector3, List<int>>();

                for (int v = 0; v < mesh.vertexCount; v++)
                {
                    if (!map.ContainsKey(mesh.vertices[v]))
                    {
                        map.Add(mesh.vertices[v], new List<int>());
                    }

                    map[mesh.vertices[v]].Add(v);
                }

                var colors = mesh.colors.Length == 0 ? new Color[mesh.vertexCount] : mesh.colors;

                foreach (var p in map)
                {
                    Vector3 normal = Vector3.zero;

                    foreach (var n in p.Value)
                        normal += mesh.normals[n];

                    normal /= p.Value.Count;

                    foreach (var n in p.Value)
                    {
                        colors[n].r = normal.x * 0.5f + 0.5f;
                        colors[n].g = normal.y * 0.5f + 0.5f;
                        colors[n].b = normal.z * 0.5f + 0.5f;
                    }
                }

                mesh.colors = colors;
            }
        }
    }
}
#endif