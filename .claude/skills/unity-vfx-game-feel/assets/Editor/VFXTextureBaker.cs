// VFXTextureBaker.cs  (Editor only — place under an Editor folder)
// Bakes procedural VFX textures so no effect ever ships with the default particle blob.
// Menu: Tools/VFX/Bake Procedural Textures  -> Assets/VFX/Generated/Textures/
// These are good placeholders for judging shape and timing; replace with authored/simulated art
// (EmberGen, Houdini, Substance) for final quality.
//
// Conventions: RGB is white/near-white (colour comes from material tint + vertex colour),
// alpha holds the shape as a SOFT gradient so erosion in VFX_ParticleUnlit has something to eat.

using System.IO;
using UnityEditor;
using UnityEngine;

public static class VFXTextureBaker
{
    public const string Folder = "Assets/VFX/Generated/Textures";

    [MenuItem("Tools/VFX/Bake Procedural Textures")]
    public static void BakeAll()
    {
        Directory.CreateDirectory(Folder);
        Save("T_SoftGlow", 128, SoftGlow, TextureWrapMode.Clamp, true);
        Save("T_HotCore", 128, HotCore, TextureWrapMode.Clamp, true);
        Save("T_SparkStreak", 128, SparkStreak, TextureWrapMode.Clamp, true);
        Save("T_Ring", 256, Ring, TextureWrapMode.Clamp, true);
        Save("T_MuzzleStar", 256, MuzzleStar, TextureWrapMode.Clamp, true);
        Save("T_SlashTrail", 256, SlashTrail, TextureWrapMode.Clamp, true);
        Save("T_SmokeFlipbook4x4", 512, SmokeFlipbook, TextureWrapMode.Clamp, true);
        Save("T_BulletHoleAtlas2x2", 512, BulletHoleAtlas, TextureWrapMode.Clamp, true);
        Save("T_Scorch", 256, Scorch, TextureWrapMode.Clamp, true);
        Save("T_Noise", 256, TileNoise, TextureWrapMode.Repeat, false);
        AssetDatabase.Refresh();
        Debug.Log("VFXTextureBaker: textures written to " + Folder);
    }

    delegate Color Pixel(float u, float v);

    static void Save(string name, int size, Pixel fn, TextureWrapMode wrap, bool srgb)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, !srgb);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = fn((x + 0.5f) / size, (y + 0.5f) / size);
        tex.SetPixels(px);
        tex.Apply();
        string path = Folder + "/" + name + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = srgb;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.alphaIsTransparency = srgb;
        ti.wrapMode = wrap;
        ti.mipmapEnabled = true;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.SaveAndReimport();
    }

    // ---------- noise helpers (tileable value noise + fbm) ----------
    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = seed * 1013 + x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / 2147483647f;
        }
    }

    static int Wrap(int a, int p) => ((a % p) + p) % p;

    static float ValueNoise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        int xa = Wrap(x0, period), xb = Wrap(x0 + 1, period), ya = Wrap(y0, period), yb = Wrap(y0 + 1, period);
        float a = Hash(xa, ya, seed), b = Hash(xb, ya, seed), c = Hash(xa, yb, seed), d = Hash(xb, yb, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f; int period = basePeriod;
        for (int o = 0; o < octaves; o++)
        {
            sum += ValueNoise(u * period, v * period, period, seed + o * 31) * amp;
            norm += amp; amp *= 0.5f; period *= 2;
        }
        return sum / norm;
    }

    static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

    // ---------- textures ----------
    static Color SoftGlow(float u, float v)
    {
        float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
        float a = Mathf.Exp(-r * r * 4.5f) * (1f - Smooth(0.85f, 1f, r));
        return new Color(1f, 1f, 1f, a);
    }

    static Color HotCore(float u, float v)
    {
        float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
        float core = 1f - Smooth(0.0f, 0.18f, r);
        float halo = Mathf.Exp(-r * r * 6f) * 0.6f;
        float a = Mathf.Clamp01(Mathf.Max(core, halo)) * (1f - Smooth(0.85f, 1f, r));
        return new Color(1f, 1f, 1f, a);
    }

    // Elongated along U (horizontal). Stretched billboards stretch along velocity; if your streak
    // appears rotated 90 degrees in your Unity version, rotate this texture or swap u/v here.
    static Color SparkStreak(float u, float v)
    {
        float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
        float across = Mathf.Exp(-y * y * 18f);
        float along = 1f - Smooth(0.55f, 1f, Mathf.Abs(x));
        float core = Mathf.Exp(-(x * x * 3f + y * y * 60f));
        float a = Mathf.Clamp01(across * along * 0.8f + core);
        return new Color(1f, 1f, 1f, a);
    }

    static Color Ring(float u, float v)
    {
        float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
        float d = (r - 0.8f) / 0.07f;
        float inner = (r - 0.8f) < 0f ? Mathf.Exp(-((r - 0.8f) / 0.25f) * ((r - 0.8f) / 0.25f)) * 0.25f : 0f;
        float a = Mathf.Clamp01(Mathf.Exp(-d * d) + inner) * (1f - Smooth(0.95f, 1f, r));
        return new Color(1f, 1f, 1f, a);
    }

    static Color MuzzleStar(float u, float v)
    {
        Vector2 p = new Vector2(u - 0.5f, v - 0.5f) * 2f;
        float r = p.magnitude;
        float ang = Mathf.Atan2(p.y, p.x);
        const int spokes = 6;
        float spoke = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * spokes * 0.5f)), 18f);
        float jitter = 0.75f + 0.25f * ValueNoise(ang * 3f + 10f, 0.5f, 1000, 7);
        float spokeA = spoke * Mathf.Exp(-r * 2.2f / jitter);
        float core = Mathf.Exp(-r * r * 14f);
        float a = Mathf.Clamp01(core + spokeA) * (1f - Smooth(0.9f, 1f, r));
        return new Color(1f, 1f, 1f, a);
    }

    // U = age (0 newest at the blade), V = base(0) -> tip(1). Bright near the blade and the tip edge,
    // streaky noise along U so erosion breaks it into strands.
    static Color SlashTrail(float u, float v)
    {
        float streak = Fbm(u * 0.25f, v, 8, 3, 3); // stretched along U
        float edge = Smooth(0.0f, 0.35f, v) * (1f - Smooth(0.94f, 1f, v));
        float tipBoost = Mathf.Lerp(0.6f, 1f, Smooth(0.5f, 0.95f, v));
        float ageFade = Mathf.Pow(1f - u, 1.4f);
        float a = Mathf.Clamp01(edge * tipBoost * ageFade * Mathf.Lerp(0.55f, 1.15f, streak));
        return new Color(1f, 1f, 1f, a);
    }

    // 4x4 flipbook: a billowing puff that grows and breaks up over 16 frames.
    static Color SmokeFlipbook(float u, float v)
    {
        int fx = Mathf.Min(3, (int)(u * 4f)), fy = Mathf.Min(3, (int)(v * 4f));
        int frame = (3 - fy) * 4 + fx;              // top-left first, matching Unity's sheet order
        float t = frame / 15f;
        float lu = u * 4f - fx, lv = v * 4f - fy;     // local uv in tile
        Vector2 p = new Vector2(lu - 0.5f, lv - 0.5f) * 2f;
        float n = Fbm(lu + t * 0.35f, lv - t * 0.2f, 4, 5, 11);
        float radius = Mathf.Lerp(0.55f, 0.9f, t);
        float r = p.magnitude / radius + (n - 0.5f) * Mathf.Lerp(0.6f, 1.1f, t);
        float body = 1f - Smooth(0.35f, 1f, r);
        float a = Mathf.Clamp01(body * Mathf.Lerp(0.85f, 1.1f, n) * Mathf.Lerp(1f, 0.75f, t)) * (1f - Smooth(0.9f, 1f, p.magnitude));
        float light = Mathf.Lerp(0.62f, 1f, Mathf.Clamp01(0.5f + 0.5f * (-p.x * 0.4f + p.y * 0.6f) + (n - 0.5f) * 0.6f));
        return new Color(light, light, light, a);
    }

    // 2x2 atlas of bullet-hole variants: dark hole, rough lighter rim, radial cracks.
    static Color BulletHoleAtlas(float u, float v)
    {
        int tx = Mathf.Min(1, (int)(u * 2f)), ty = Mathf.Min(1, (int)(v * 2f));
        int seed = 100 + tx + ty * 2;
        float lu = u * 2f - tx, lv = v * 2f - ty;
        Vector2 p = new Vector2(lu - 0.5f, lv - 0.5f) * 2f;
        float r = p.magnitude, ang = Mathf.Atan2(p.y, p.x);
        float rough = (ValueNoise(ang * 4f + seed, 0.3f, 1000, seed) - 0.5f) * 0.12f;
        float hole = 1f - Smooth(0.12f + rough, 0.17f + rough, r);
        float rim = Mathf.Exp(-Mathf.Pow((r - 0.24f - rough) / 0.07f, 2f));
        float cracks = 0f;
        for (int k = 0; k < 5; k++)
        {
            float ca = Hash(k, seed, 5) * Mathf.PI * 2f;
            float len = Mathf.Lerp(0.45f, 0.85f, Hash(k, seed, 9));
            float da = Mathf.DeltaAngle(ang * Mathf.Rad2Deg, ca * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float w = Mathf.Exp(-Mathf.Pow(da * r * 40f, 2f)) * (1f - Smooth(len * 0.6f, len, r)) * Smooth(0.15f, 0.25f, r);
            cracks = Mathf.Max(cracks, w);
        }
        float a = Mathf.Clamp01(Mathf.Max(hole, Mathf.Max(rim * 0.55f, cracks * 0.7f))) * (1f - Smooth(0.9f, 1f, r));
        float val = Mathf.Lerp(0.35f, 0.04f, hole);  // rim lighter, hole near black
        return new Color(val, val * 0.97f, val * 0.94f, a);
    }

    static Color Scorch(float u, float v)
    {
        Vector2 p = new Vector2(u - 0.5f, v - 0.5f) * 2f;
        float n = Fbm(u, v, 4, 5, 21);
        float r = p.magnitude + (n - 0.5f) * 0.5f;
        float a = (1f - Smooth(0.2f, 0.95f, r)) * (1f - Smooth(0.9f, 1f, p.magnitude));
        float val = Mathf.Lerp(0.02f, 0.12f, n);
        return new Color(val, val * 0.95f, val * 0.9f, Mathf.Clamp01(a));
    }

    static Color TileNoise(float u, float v)
    {
        float n = Fbm(u, v, 4, 5, 42);
        return new Color(n, n, n, 1f);
    }
}
