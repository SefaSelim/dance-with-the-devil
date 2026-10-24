using System.IO;
using UnityEditor;
using UnityEngine;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// Image steps for the Devil art (menu Dance/UI/Build Devil Art):
    /// <list type="bullet">
    /// <item><see cref="BuildDevilArt"/>: the hand-drawn Devil (<see cref="SourcePath"/>, black background) to
    /// <see cref="OutputPath"/> with the black keyed out: alpha from the brightest channel with a soft ramp, the dark
    /// line art inside the figure kept opaque (morphological closing of the figure mask), a 1-2 px soft edge. Imported
    /// as a full-rect Sprite and set on the UI theme (UiTheme.DevilSprite), which DevilScreen uses when its own sprite
    /// field is empty, so no scene edit is needed and Arena rebuilds keep it.</item>
    /// <item><see cref="BuildMenuBackdrop"/>: the menu's Doré engraving with feathered edges (no visible rectangle),
    /// used by MainMenuBuilder for the full-height backdrop behind the 3D stage.</item>
    /// </list>
    /// </summary>
    public static class DevilArtBuilder
    {
        public const string SourcePath = "Assets/Devil-handdraw.jpeg";
        public const string OutputPath = "Assets/_Game/Art/UI/devil_handdraw.png";
        public const string BackdropSourcePath = "Assets/_Game/Art/UI/devil_placeholder_dore.jpg";
        public const string BackdropPath = "Assets/_Game/Art/UI/menu_devil_backdrop.png";
        private const string ThemePath = "Assets/_Game/Resources/UiTheme.asset";

        // Key: brightest channel below Low = background, above High = figure (JPEG black noise stays under ~0.04).
        private const float KeyLow = 0.035f;
        private const float KeyHigh = 0.13f;
        private const float MaskThreshold = 0.09f;
        // Gaps in the figure narrower than about 2 × this (px) are filled: black outlines and hatching stay opaque.
        private const int CloseRadius = 8;
        private const int EdgeGrow = 2;
        private const int EdgeBlur = 2;

        [MenuItem("Dance/UI/Build Devil Art")]
        public static void BuildDevilArt()
        {
            Texture2D src = LoadImage(SourcePath);
            if (src == null)
            {
                Debug.LogError("DevilArtBuilder: " + SourcePath + " not found.");
                return;
            }
            int w = src.width, h = src.height, n = w * h;
            Color32[] px = src.GetPixels32();
            Object.DestroyImmediate(src);

            var lum = new float[n];
            var mask = new byte[n];
            for (int i = 0; i < n; i++)
            {
                Color32 c = px[i];
                float l = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) / 255f;
                lum[i] = l;
                mask[i] = l > MaskThreshold ? (byte)1 : (byte)0;
            }
            // Closing (dilate then erode) fills the line art; a small grow keeps the outer outline.
            mask = Morph(mask, w, h, CloseRadius, true);
            mask = Morph(mask, w, h, CloseRadius, false);
            mask = Morph(mask, w, h, EdgeGrow, true);
            float[] soft = BoxBlur(mask, w, h, EdgeBlur);

            for (int i = 0; i < n; i++)
            {
                float ramp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(KeyLow, KeyHigh, lum[i]));
                float a = Mathf.Max(soft[i], ramp);
                px[i].a = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);
            }

            Sprite sprite = WriteSprite(px, w, h, OutputPath);
            if (sprite == null) return;
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null)
            {
                MainMenuBuilder.BuildTheme(); // picks the sprite up itself
                theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            }
            if (theme != null)
            {
                theme.DevilSprite = sprite;
                EditorUtility.SetDirty(theme);
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"DevilArtBuilder: {OutputPath} ({w}×{h}) built and set on the UI theme.");
        }

        /// <summary>The Doré engraving with soft edges on all four sides; null when the source is missing.</summary>
        public static Sprite BuildMenuBackdrop()
        {
            Texture2D src = LoadImage(BackdropSourcePath);
            if (src == null)
            {
                Debug.LogWarning("DevilArtBuilder: " + BackdropSourcePath + " not found; the menu has no backdrop.");
                return null;
            }
            int w = src.width, h = src.height;
            Color32[] px = src.GetPixels32();
            Object.DestroyImmediate(src);
            const float featherX = 0.22f;
            const float featherY = 0.12f;
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float fy = Feather(v, featherY);
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float a = fy * Feather(u, featherX);
                    int i = y * w + x;
                    px[i].a = (byte)Mathf.RoundToInt(a * 255f);
                }
            }
            return WriteSprite(px, w, h, BackdropPath);
        }

        private static float Feather(float t, float width)
        {
            float edge = Mathf.Min(t, 1f - t);
            float k = Mathf.Clamp01(edge / width);
            return k * k * (3f - 2f * k);
        }

        private static Texture2D LoadImage(string assetPath)
        {
            if (!File.Exists(assetPath)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(File.ReadAllBytes(assetPath))) return tex;
            Object.DestroyImmediate(tex);
            return null;
        }

        private static Sprite WriteSprite(Color32[] px, int w, int h, string path)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply(false);
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogError("DevilArtBuilder: could not import " + path + " as a sprite.");
            return sprite;
        }

        /// <summary>Square max (dilate) or min (erode) filter of radius <paramref name="r"/>, separable.</summary>
        private static byte[] Morph(byte[] src, int w, int h, int r, bool dilate)
        {
            if (r <= 0) return src;
            var tmp = new byte[src.Length];
            var dst = new byte[src.Length];
            byte hit = dilate ? (byte)1 : (byte)0;
            byte miss = dilate ? (byte)0 : (byte)1;
            // Rows: count of 'hit' pixels in the window (sliding).
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                int count = 0;
                for (int x = -r; x < r; x++)
                    if (x >= 0 && x < w && src[row + x] == hit) count++;
                for (int x = 0; x < w; x++)
                {
                    int add = x + r;
                    if (add < w && src[row + add] == hit) count++;
                    int rem = x - r - 1;
                    if (rem >= 0 && src[row + rem] == hit) count--;
                    tmp[row + x] = count > 0 ? hit : miss;
                }
            }
            for (int x = 0; x < w; x++)
            {
                int count = 0;
                for (int y = -r; y < r; y++)
                    if (y >= 0 && y < h && tmp[y * w + x] == hit) count++;
                for (int y = 0; y < h; y++)
                {
                    int add = y + r;
                    if (add < h && tmp[add * w + x] == hit) count++;
                    int rem = y - r - 1;
                    if (rem >= 0 && tmp[rem * w + x] == hit) count--;
                    dst[y * w + x] = count > 0 ? hit : miss;
                }
            }
            return dst;
        }

        /// <summary>Separable box blur of a 0/1 mask to 0..1.</summary>
        private static float[] BoxBlur(byte[] src, int w, int h, int r)
        {
            var a = new float[src.Length];
            var b = new float[src.Length];
            float inv = 1f / (2 * r + 1);
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    for (int k = -r; k <= r; k++) sum += src[row + Mathf.Clamp(x + k, 0, w - 1)];
                    a[row + x] = sum * inv;
                }
            }
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    float sum = 0f;
                    for (int k = -r; k <= r; k++) sum += a[Mathf.Clamp(y + k, 0, h - 1) * w + x];
                    b[y * w + x] = sum * inv;
                }
            }
            return b;
        }
    }
}
