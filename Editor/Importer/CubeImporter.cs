using System.IO;
using System.Collections.Generic;

using UnityEditor.Experimental.AssetImporters;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [ScriptedImporter(1, ".cube")]
    public class CubeImporter : ScriptedImporter
    {
        struct ParamsLogC
        {
            public float cut;
            public float a, b, c, d, e, f;
        };

        static ParamsLogC LogC = new ParamsLogC()
        {
            cut = 0.011361f,
            a = 5.555556f,
            b = 0.047996f,
            c = 0.244161f,
            d = 0.386036f,
            e = 5.301883f,
            f = 0.092819f
        };

        float LinearToLogC_Precise(float x)
        {
            float o;
            if (x > LogC.cut)
                o = LogC.c * Mathf.Log10(LogC.a * x + LogC.b) + LogC.d;
            else
                o = LogC.e * x + LogC.f;
            return o;
        }

        float LogCToLinear_Precise(float x)
        {
            float o;
            if (x > LogC.e * LogC.cut + LogC.f)
                o = (Mathf.Pow(10.0f, (x - LogC.d) / LogC.c) - LogC.b) / LogC.a;
            else
                o = (x - LogC.f) / LogC.e;

            return o;
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            Debug.Log("Import:" + ctx.assetPath);

            try
			{
                using (var stream = new StreamReader(ctx.assetPath))
                {
                    var values = new List<Color>();
                    string name = null;
                    int size = 0;

                    while (true)
                    {
                        var line = stream.ReadLine();
                        if (line == null)
                            break;

                        if (line.Length == 0)
                            continue;

                        if (line[0] == '#')
                            continue;

                        if (line.StartsWith("TITLE"))
                        {
                            name = line.Substring(5).Trim(new char[] { '"', ' ' });
                            continue;
                        }

                        if (line.StartsWith("DOMAIN_MIN") || line.StartsWith("DOMAIN_MAX"))
                            continue;

                        if (line.StartsWith("LUT_3D_SIZE"))
                        {
                            int.TryParse(line.Substring(11).Trim(), out size);
                            continue;
                        }

                        var rgb = line.Split(' ');

                        float.TryParse(rgb[0], out var r);
                        float.TryParse(rgb[1], out var g);
                        float.TryParse(rgb[2], out var b);

                        values.Add(new Color(LogCToLinear_Precise(r), LogCToLinear_Precise(g), LogCToLinear_Precise(b)));
                    }

                    if (values.Count == size * size * size && size > 0)
                    {
                        var width = size * size;
                        var colors = new Color[values.Count];

                        for (int y = 0; y < size; y++)
                        {
                            for (int z = 0; z < size; z++)
                            {
                                for (int x = 0; x < size; x++)
                                {
                                    var dst = (y * width + z * size + x);
                                    var src = (z * width + y * size + x);

                                    colors[dst] = values[src];
                                }
                            }
                        }

                        var assetsLut = new Texture2D(width, size, TextureFormat.RGBAHalf, false, true)
                        {
                            wrapMode = TextureWrapMode.Clamp,
                            filterMode = FilterMode.Bilinear
                        };

                        assetsLut.name = name;
                        assetsLut.SetPixels(colors);

                        ctx.AddObjectToAsset("Color Lookup Table", assetsLut);
                        ctx.SetMainObject(assetsLut);
                    }
                }
            }
            catch
			{
                Debug.LogError("Import:" + ctx.assetPath + "failed.");
            }
        }
    }
}