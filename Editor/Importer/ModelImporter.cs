using UnityEditor;

namespace UnityEngine.Rendering.Universal
{
    public class TiledAssetPostProcessor : AssetPostprocessor
    {
        uint ReverseBits32(uint bits)
        {
            bits = (bits << 16) | (bits >> 16);
            bits = ((bits & 0x00ff00ff) << 8) | ((bits & 0xff00ff00) >> 8);
            bits = ((bits & 0x0f0f0f0f) << 4) | ((bits & 0xf0f0f0f0) >> 4);
            bits = ((bits & 0x33333333) << 2) | ((bits & 0xcccccccc) >> 2);
            bits = ((bits & 0x55555555) << 1) | ((bits & 0xaaaaaaaa) >> 1);
            return bits;
        }

        Vector2 Hammersley(uint i, uint samplesCount)
        {
            float E1 = i / (float)samplesCount;
            float E2 = ReverseBits32(i) * 2.3283064365386963e-10f;
            return new Vector2(E1, E2);
        }

        Vector3 CosineSampleHemisphere(Vector2 E)
        {
            float Phi = 2 * Mathf.PI * E.x;

            float cosTheta = Mathf.Sqrt(E.y);
            float sinTheta = Mathf.Sqrt(1 - cosTheta * cosTheta);

            Vector3 H;
            H.x = sinTheta * Mathf.Cos(Phi);
            H.y = sinTheta * Mathf.Sin(Phi);
            H.z = cosTheta;

            return H;
        }

        Vector3 UniformSampleSphere(Vector2 E)
        {
            float Phi = 2 * Mathf.PI * E.x;
            float CosTheta = 1 - 2 * E.y;
            float SinTheta = Mathf.Sqrt(1 - CosTheta * CosTheta);

            Vector3 H;
            H.x = SinTheta * Mathf.Cos(Phi);
            H.y = SinTheta * Mathf.Sin(Phi);
            H.z = CosTheta;

            return H;
        }

        Vector3 TangentToWorld(Vector3 N, Vector3 H)
        {
            Vector3 TangentY = Mathf.Abs(N.z) < 0.999f ? Vector3.forward : Vector3.right;
            Vector3 TangentX = Vector3.Normalize(Vector3.Cross(TangentY, N));
            return Vector3.Normalize(TangentX * H.x + Vector3.Cross(N, TangentX) * H.y + N * H.z);
        }

        Vector4 ConvertToH4(SphericalHarmonicsL2 sh9)
		{
			float rt2 = Mathf.Sqrt(2.0f);
			float rt32 = Mathf.Sqrt(3.0f / 2.0f);
			float rt52 = Mathf.Sqrt(5.0f / 2.0f);
			float rt152 = Mathf.Sqrt(15.0f / 2.0f);
			float[,] convMatrix = new float[4, 9]
			{
				{ 1.0f / rt2, 0, 0.5f * rt32, 0, 0, 0, 0, 0, 0 },
				{ 0, 1.0f / rt2, 0, 0, 0, (3.0f / 8.0f) * rt52, 0, 0, 0 },
				{ 0, 0, 1.0f / (2.0f * rt2), 0, 0, 0, 0.25f * rt152, 0, 0 },
				{ 0, 0, 0, 1.0f / rt2, 0, 0, 0, (3.0f / 8.0f) * rt52, 0 }
			};

			float[] hBasis = new float[4];

			for (int row = 0; row < 4; ++row)
			{
				hBasis[row] = 0.0f;
				for (int col = 0; col < 9; ++col)
					hBasis[row] += convMatrix[row, col] * sh9[0, col];
			}

			return new Vector4(hBasis[0], hBasis[1], hBasis[2], hBasis[3]);
		}

        private void OnPostprocessModel(GameObject gameObject)
        {
            const uint NumSamples = 128;

            MeshCollider[] colliders = gameObject.GetComponentsInChildren<MeshCollider>();

            if (colliders.Length > 0)
			{
                foreach (var collider in colliders)
                    collider.convex = false;

                foreach (MeshFilter mf in gameObject.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = mf.sharedMesh;
                    var colors = new Vector4[mesh.vertexCount];

                    for (int i = 0; i < mesh.vertexCount; i++)
                    {
                        var V = mf.transform.TransformPoint(mesh.vertices[i]);
                        var N = mf.transform.TransformDirection(mesh.normals[i]);

                        SphericalHarmonicsL2 sh9 = new SphericalHarmonicsL2();

                        for (uint sample = 0; sample < NumSamples; sample++)
                        {
                            var E = Hammersley(sample, NumSamples);
                            var L = TangentToWorld(N, UniformSampleSphere(E));

                            bool hit = false;

                            foreach (var collider in colliders)
                            {
                                if (collider.Raycast(new Ray(V + N * 1e-5f, L), out var raycastHit, float.MaxValue))
                                {
                                    hit = true;
                                    break;
                                }
                            }

                            sh9.AddDirectionalLight(mf.transform.InverseTransformDirection(L), hit ? Color.black : Color.white, 1.0f);
                        }

                        var directions = new Vector3[1] { Vector3.up };
                        var result = new Color[1];

                        sh9.Evaluate(directions, result);

                        colors[i] = ConvertToH4(sh9);
                    }

                    mesh.SetUVs(2, colors);
                }
            }
        }
    }
}