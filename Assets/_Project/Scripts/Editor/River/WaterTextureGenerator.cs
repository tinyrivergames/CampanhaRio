using System.IO;
using UnityEditor;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// Writes the procedural water textures to Art/Textures/Water (menu: CampanhaRio > Setup > Generate Water Textures).
    /// Everything is tileable and generated from seeded noise, so re-running gives the same result.
    /// </summary>
    public static class WaterTextureGenerator
    {
        public const string Dir = "Assets/_Project/Art/Textures/Water";
        public const string NormalPath = Dir + "/T_WaterNormal.png";
        public const string FoamPath = Dir + "/T_WaterFoam.png";
        public const string BlobPath = Dir + "/T_FX_FoamBlob.png";
        public const string RingPath = Dir + "/T_FX_Ring.png";
        public const string DropletPath = Dir + "/T_FX_Droplet.png";
        public const string LeafPath = Dir + "/T_FX_Leaf.png";

        [MenuItem("CampanhaRio/Setup/Generate Water Textures")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Dir);
            Write(NormalPath, NormalMap(256), linear: true, normal: true);
            Write(FoamPath, FoamAtlas(512), linear: true);
            Write(BlobPath, Sprite(128, Blob), linear: false);
            Write(RingPath, Sprite(128, Ring), linear: false);
            Write(DropletPath, Sprite(32, Droplet), linear: false);
            Write(LeafPath, Sprite(64, Leaf), linear: false);
            AssetDatabase.Refresh();
        }

        public static bool Exist() => File.Exists(NormalPath) && File.Exists(FoamPath) && File.Exists(BlobPath) &&
                                      File.Exists(RingPath) && File.Exists(DropletPath) && File.Exists(LeafPath);

        // ------------------------------------------------------------------ tileable noise

        internal static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144269504);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
            }
        }

        /// <summary>Periodic gradient noise in [0,1]. period = lattice cells per tile.</summary>
        internal static float Noise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float Grad(int ix, int iy, float dx, float dy)
            {
                float a = Hash(((ix % period) + period) % period, ((iy % period) + period) % period, seed) * Mathf.PI * 2f;
                return Mathf.Cos(a) * dx + Mathf.Sin(a) * dy;
            }
            float u = fx * fx * (3f - 2f * fx), v = fy * fy * (3f - 2f * fy);
            float n = Mathf.Lerp(Mathf.Lerp(Grad(x0, y0, fx, fy), Grad(x0 + 1, y0, fx - 1f, fy), u),
                                 Mathf.Lerp(Grad(x0, y0 + 1, fx, fy - 1f), Grad(x0 + 1, y0 + 1, fx - 1f, fy - 1f), u), v);
            return n * 0.75f + 0.5f;
        }

        /// <summary>Tileable fBm over a unit tile (u, v in 0..1).</summary>
        internal static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(u * period, v * period, period, seed + o * 17) * amp;
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        // ------------------------------------------------------------------ textures

        static Texture2D NormalMap(int size)
        {
            var height = new float[size, size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                height[x, y] = Fbm(x / (float)size, y / (float)size, 4, 4, 11);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            const float strength = 6f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = height[(x + 1) % size, y] - height[(x - 1 + size) % size, y];
                float dy = height[x, (y + 1) % size] - height[x, (y - 1 + size) % size];
                var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                tex.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
            }
            return tex;
        }

        /// <summary>R = flow streaks (long along V, broken, soft), G = painterly foam cells, B = breakup noise.</summary>
        static Texture2D FoamAtlas(int size)
        {
            var streaks = new float[size, size];
            var rng = new System.Random(5);
            for (int s = 0; s < 1700; s++)
            {
                float cx = (float)rng.NextDouble() * size, cy = (float)rng.NextDouble() * size;
                float length = Mathf.Lerp(0.07f, 0.3f, (float)rng.NextDouble()) * size;
                float width = Mathf.Lerp(0.8f, 2f, (float)rng.NextDouble());
                float amp = Mathf.Lerp(0.55f, 1f, (float)rng.NextDouble());
                float wiggle = Mathf.Lerp(1f, 5f, (float)rng.NextDouble());
                int seed = rng.Next(1000);
                int halfLen = Mathf.CeilToInt(length * 0.5f), halfW = Mathf.CeilToInt(width * 3f + wiggle);
                for (int j = -halfLen; j <= halfLen; j++)
                {
                    float p = j / length + 0.5f;                                 // 0..1 along the stroke
                    float envelope = Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI);     // tapered ends
                    float broken = Smooth(0.35f, 0.6f, Noise(p * 6f + seed, seed * 0.37f, 64, seed));
                    float centerX = cx + Mathf.Sin(p * 3.1f + seed) * wiggle;
                    float w = width * (0.6f + 0.6f * Noise(p * 9f, seed, 64, seed + 3)); // uneven width
                    int y = ((Mathf.RoundToInt(cy) + j) % size + size) % size;
                    for (int i = -halfW; i <= halfW; i++)
                    {
                        float dx = (Mathf.Round(centerX) + i) - centerX;
                        float value = amp * envelope * broken * Mathf.Exp(-dx * dx / (2f * w * w));
                        int x = ((Mathf.RoundToInt(centerX) + i) % size + size) % size;
                        if (value > streaks[x, y]) streaks[x, y] = value;
                    }
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float cells = Smooth(0.42f, 0.72f, Fbm(u, v, 8, 4, 23));
                float breakup = Fbm(u, v, 6, 3, 31);
                tex.SetPixel(x, y, new Color(Mathf.Clamp01(streaks[x, y]), cells, breakup, 1f));
            }
            return tex;
        }

        /// <summary>HLSL-style smoothstep(edge0, edge1, x). (Mathf.SmoothStep interpolates between its first two values instead.)</summary>
        internal static float Smooth(float edge0, float edge1, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge0, edge1, x));

        delegate float Shape(float u, float v, float noise);

        static Texture2D Sprite(int size, Shape shape)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float n = Fbm(x / (float)size, y / (float)size, 4, 3, 41);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(shape(u, v, n))));
            }
            return tex;
        }

        static float Blob(float u, float v, float n)
        {
            float r = Mathf.Sqrt(u * u + v * v) + (n - 0.5f) * 0.45f; // wobbly painted edge
            return Smooth(0.95f, 0.55f, r) * Mathf.Lerp(0.75f, 1f, n);
        }

        static float Ring(float u, float v, float n)
        {
            float r = Mathf.Sqrt(u * u + v * v) + (n - 0.5f) * 0.08f;
            return Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.07f, 2f)) * Mathf.Lerp(0.6f, 1f, n);
        }

        static float Droplet(float u, float v, float n) => Smooth(1f, 0.3f, Mathf.Sqrt(u * u + v * v));

        static float Leaf(float u, float v, float n)
        {
            // Pointed oval leaf with a soft stem line
            float along = v, across = u / Mathf.Max(0.05f, 0.55f * Mathf.Cos(along * Mathf.PI * 0.5f));
            float body = Smooth(1f, 0.85f, Mathf.Abs(across)) * Smooth(1f, 0.9f, Mathf.Abs(along));
            float vein = 1f - 0.35f * Mathf.Exp(-u * u / 0.002f) * Smooth(1f, 0.2f, Mathf.Abs(along));
            return body * vein;
        }

        static void Write(string path, Texture2D tex, bool linear, bool normal = false)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !linear;
            importer.alphaIsTransparency = !linear;
            importer.wrapMode = linear ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.textureCompression = linear ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
    }
}
