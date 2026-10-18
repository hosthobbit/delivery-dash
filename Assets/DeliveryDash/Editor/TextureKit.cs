using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeliveryDash.EditorTools
{
    /// <summary>Procedural textures (brick, slate, tarmac, paving, grass, newsprint…), saved as PNG assets.</summary>
    public static class TextureKit
    {
        public const string Dir = "Assets/DeliveryDash/Generated/Textures/";

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 982451653;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        /// <summary>Smooth value noise, tileable with period `period`.</summary>
        public static float Noise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int Wrap(int v) => ((v % period) + period) % period;
            float a = Hash(Wrap(x0), Wrap(y0), seed), b = Hash(Wrap(x0 + 1), Wrap(y0), seed);
            float c = Hash(Wrap(x0), Wrap(y0 + 1), seed), d = Hash(Wrap(x0 + 1), Wrap(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        public static float Fbm(float u, float v, int baseFreq, int seed, int octaves = 4)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            int f = baseFreq;
            for (int i = 0; i < octaves; i++) { sum += Noise(u * f, v * f, f, seed + i) * amp; norm += amp; amp *= 0.5f; f *= 2; }
            return sum / norm;
        }

        public static Texture2D Save(string name, int size, Func<float, float, Color> f, bool alpha = false, int height = 0)
        {
            Directory.CreateDirectory(Dir);
            int h = height > 0 ? height : size;
            var t = new Texture2D(size, h, TextureFormat.RGBA32, false);
            var px = new Color[size * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = f((x + 0.5f) / size, (y + 0.5f) / h);
            t.SetPixels(px);
            t.Apply();
            string path = Dir + name + ".png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.alphaIsTransparency = alpha;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 1024;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Running-bond brickwork: 1 texture tile = 1.2 m square (16 courses of 5 bricks: real-world brick size).</summary>
        public static Texture2D Brick(string name, Color baseCol, Color varCol, Color mortar, int seed) => Save(name, 512, (u, v) =>
        {
            const int rows = 16, perRow = 5;
            float rv = v * rows;
            int row = Mathf.FloorToInt(rv);
            float ru = u * perRow + (row % 2) * 0.5f;
            int col = Mathf.FloorToInt(ru);
            float fu = ru - col, fv = rv - row;
            float m = 0.045f * perRow / 5f;
            bool joint = fu < m * 0.6f || fu > 1 - m * 0.6f || fv < 0.1f || fv > 0.9f;
            float n = Fbm(u, v, 8, seed);
            if (joint) return mortar * (0.92f + n * 0.12f);
            float h = Hash(((col % perRow) + perRow) % perRow, row, seed);
            var c = Color.Lerp(baseCol, varCol, h) * (0.88f + n * 0.2f);
            // soft bevel: a little darker near the brick edges
            float edge = Mathf.Min(Mathf.Min(fu, 1 - fu) * 6f, Mathf.Min(fv, 1 - fv) * 4f);
            c *= Mathf.Lerp(0.88f, 1f, Mathf.Clamp01(edge));
            c.a = 1;
            return c;
        });

        /// <summary>Painted render: a soft, slightly mottled wall.</summary>
        public static Texture2D Render(string name, int seed) => Save(name, 256, (u, v) =>
        {
            float n = Fbm(u, v, 4, seed) * 0.6f + Fbm(u, v, 32, seed + 9) * 0.4f;
            float g = 0.93f + n * 0.1f;
            return new Color(g, g, g, 1);
        });

        /// <summary>Roof slates: 1 tile = 1 m × 1 m, staggered rows.</summary>
        public static Texture2D Slate(string name, int seed) => Save(name, 256, (u, v) =>
        {
            const int rows = 5, per = 4;
            float rv = v * rows; int row = Mathf.FloorToInt(rv);
            float ru = u * per + (row % 2) * 0.5f; int col = Mathf.FloorToInt(ru);
            float fu = ru - col, fv = rv - row;
            float h = Hash(((col % per) + per) % per, row, seed);
            float g = 0.38f + h * 0.12f + Fbm(u, v, 16, seed) * 0.08f;
            if (fu < 0.04f || fu > 0.96f) g *= 0.6f;
            g *= Mathf.Lerp(0.7f, 1f, fv);          // overlap shadow at the bottom of each row
            return new Color(g * 0.95f, g * 1.0f, g * 1.12f, 1);
        });

        public static Texture2D Tarmac(string name, int seed) => Save(name, 512, (u, v) =>
        {
            float n = Fbm(u, v, 16, seed, 5);
            float speck = Hash((int)(u * 512), (int)(v * 512), seed) > 0.97f ? 0.08f : 0f;
            float g = 0.24f + n * 0.1f + speck;
            return new Color(g, g * 1.0f, g * 1.06f, 1);
        });

        /// <summary>Paving slabs: 1 tile = 1.2 m (2 × 2 slabs).</summary>
        public static Texture2D Paving(string name, Color tint, int seed) => Save(name, 256, (u, v) =>
        {
            float fu = (u * 2) % 1f, fv = (v * 2) % 1f;
            int sx = (int)(u * 2), sy = (int)(v * 2);
            bool joint = fu < 0.025f || fu > 0.975f || fv < 0.025f || fv > 0.975f;
            float h = Hash(sx, sy, seed);
            float n = Fbm(u, v, 16, seed);
            var c = tint * (0.86f + h * 0.1f + n * 0.12f);
            if (joint) c *= 0.62f;
            c.a = 1;
            return c;
        });

        public static Texture2D Grass(string name, int seed) => Save(name, 256, (u, v) =>
        {
            float n = Fbm(u, v, 8, seed), blades = Fbm(u, v, 64, seed + 3);
            var c = Color.Lerp(new Color(0.33f, 0.6f, 0.24f), new Color(0.5f, 0.74f, 0.3f), n) * (0.85f + blades * 0.3f);
            c.a = 1;
            return c;
        });

        public static Texture2D Leaves(string name, int seed) => Save(name, 256, (u, v) =>
        {
            float n = Fbm(u, v, 12, seed), clumps = Fbm(u, v, 4, seed + 5);
            var c = Color.Lerp(new Color(0.18f, 0.42f, 0.2f), new Color(0.38f, 0.66f, 0.28f), n * 0.7f + clumps * 0.5f);
            c.a = 1;
            return c;
        });

        public static Texture2D Wood(string name, int seed) => Save(name, 256, (u, v) =>
        {
            float grain = Mathf.Sin((u * 40f + Fbm(u, v, 4, seed) * 6f) * Mathf.PI) * 0.5f + 0.5f;
            float g = 0.8f + grain * 0.2f;
            return new Color(g, g, g, 1);
        });

        /// <summary>Newsprint for the papers: columns of grey "text" and a bold masthead.</summary>
        public static Texture2D Newsprint(string name) => Save(name, 256, (u, v) =>
        {
            var paper = new Color(0.96f, 0.95f, 0.9f, 1);
            if (v > 0.82f) return u > 0.06f && u < 0.94f && v < 0.96f && Hash((int)(u * 60), 1, 3) > 0.25f ? new Color(0.12f, 0.12f, 0.14f, 1) : paper;
            if (v > 0.78f) return new Color(0.85f, 0.2f, 0.2f, 1);
            int colIdx = (int)(u * 3);
            float cu = u * 3 - colIdx;
            if (cu < 0.08f || cu > 0.92f) return paper;
            int line = (int)(v * 40);
            float lv = v * 40 - line;
            if (lv > 0.55f) return paper;
            float len = 0.6f + Hash(colIdx, line, 7) * 0.4f;
            return cu < len ? new Color(0.55f, 0.55f, 0.58f, 1) : paper;
        });

        /// <summary>A soft round blob (particles, shadows, glows).</summary>
        public static Texture2D Soft(string name) => Save(name, 64, (u, v) =>
        {
            float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
            float a = Mathf.Clamp01(1f - d);
            a = a * a * (3 - 2 * a);
            return new Color(1, 1, 1, a);
        }, true);

        public static Texture2D Ring(string name) => Save(name, 128, (u, v) =>
        {
            float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
            float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.09f);
            float dot = Mathf.Clamp01(1f - d / 0.16f);
            float a = Mathf.Max(ring, dot);
            return new Color(1, 1, 1, a);
        }, true);

        public static Texture2D Square(string name) => Save(name, 16, (u, v) => Color.white, true);

        /// <summary>The "!" warning sign: yellow rounded triangle with a black exclamation mark.</summary>
        public static Texture2D Warning(string name) => Save(name, 128, (u, v) =>
        {
            var p = new Vector2(u - 0.5f, v - 0.42f);
            // triangle via 3 half-planes
            float d1 = p.y + 0.36f, d2 = -0.866f * p.x - 0.5f * p.y + 0.36f, d3 = 0.866f * p.x - 0.5f * p.y + 0.36f;
            float inside = Mathf.Min(d1, Mathf.Min(d2, d3));
            if (inside < 0) return new Color(0, 0, 0, 0);
            float border = inside < 0.04f ? 1 : 0;
            bool bar = Mathf.Abs(p.x) < 0.045f && p.y > -0.12f && p.y < 0.2f;
            bool dot = new Vector2(p.x, p.y + 0.22f).magnitude < 0.05f;
            if (bar || dot || border > 0) return new Color(0.1f, 0.08f, 0.12f, 1);
            return new Color(1f, 0.82f, 0.15f, 1);
        }, true);

        /// <summary>Cracked glass overlay for broken windows.</summary>
        public static Texture2D Cracked(string name) => Save(name, 256, (u, v) =>
        {
            var p = new Vector2(u - 0.45f, v - 0.55f);
            float ang = Mathf.Atan2(p.y, p.x), r = p.magnitude;
            float spokes = Mathf.Abs(Mathf.Sin(ang * 7f + r * 3f));
            float rings = Mathf.Abs(Mathf.Sin(r * 40f));
            bool crack = spokes < 0.04f + r * 0.02f || (rings < 0.05f && r < 0.3f && spokes < 0.4f);
            var glass = new Color(0.55f, 0.66f, 0.75f, 1);
            return crack ? new Color(0.95f, 0.97f, 1f, 1) : glass * (0.85f + Fbm(u, v, 4, 3) * 0.2f);
        });

        /// <summary>A chequered finish-line strip.</summary>
        public static Texture2D Chequer(string name) => Save(name, 64, (u, v) => ((int)(u * 8) + (int)(v * 2)) % 2 == 0 ? Color.white : new Color(0.1f, 0.1f, 0.12f, 1));
    }
}
