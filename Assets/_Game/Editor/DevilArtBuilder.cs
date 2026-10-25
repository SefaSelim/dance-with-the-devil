using System.IO;
using UnityEditor;
using UnityEngine;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// Image steps for the Devil art (menu Dance/UI/Build Devil Art):
    /// <list type="bullet">
    /// <item><see cref="BuildDevilArt"/>: the hand-drawn Devil (<see cref="SourcePath"/>, black background) to
    /// <see cref="OutputPath"/> with the black background keyed out: the background is what a flood fill from the top
    /// and left edges (and the empty left part of the bottom edge) reaches through dark pixels, so the black inside
    /// the figure (line art, gaps between armour plates, shadows under the arms) stays opaque; a 1-2 px soft edge. Imported
    /// as a full-rect Sprite and set on the UI theme (UiTheme.DevilSprite), which DevilScreen uses when its own sprite
    /// field is empty, so no scene edit is needed and Arena rebuilds keep it.</item>
    /// <item><see cref="BuildMenuBackdrop"/>: the menu's Doré engraving with feathered edges (no visible rectangle),
    /// used by MainMenuBuilder for the full-height backdrop behind the 2D stage.</item>
    /// <item><see cref="BuildPlayerArt"/> (run by the same menu): Defne with the Devil behind her
    /// (<see cref="PlayerSourcePath"/>, black background) to <see cref="PlayerArtPath"/> with the black keyed out; the
    /// background is what a flood fill from the top, left and right edges reaches through dark pixels, so dark areas
    /// enclosed by the figures (tank top, trousers, rifle, line art) stay opaque. Plus <see cref="PortraitPath"/>, a
    /// square crop of Defne's head and shoulders on a dark vignette with soft edges, and its greyscale copy
    /// <see cref="PortraitGreyPath"/>. All set on the UI theme (PlayerArtSprite, PortraitSprite, PortraitGreySprite).</item>
    /// </list>
    /// </summary>
    public static class DevilArtBuilder
    {
        public const string SourcePath = "Assets/Devil-handdraw.jpeg";
        public const string OutputPath = "Assets/_Game/Art/UI/devil_handdraw.png";
        public const string BackdropSourcePath = "Assets/_Game/Art/UI/devil_placeholder_dore.jpg";
        public const string BackdropPath = "Assets/_Game/Art/UI/menu_devil_backdrop.png";
        public const string PlayerSourcePath = "Assets/Devil-player.jpeg";
        public const string PlayerArtPath = "Assets/_Game/Art/UI/devil_player.png";
        public const string PortraitPath = "Assets/_Game/Art/UI/defne_portrait.png";
        public const string PortraitGreyPath = "Assets/_Game/Art/UI/defne_portrait_grey.png";
        private const string ThemePath = "Assets/_Game/Resources/UiTheme.asset";

        // Player art: gaps narrower than ~2 × this (px) between dark parts of the figures count as figure.
        private const int HoleCloseRadius = 14;
        // Background pixels (reached by the flood fill) fade in over this brightest-channel range: JPEG noise and the
        // faint stray marks in the empty left part stay invisible.
        private const float PlayerKeyLow = 0.06f;
        private const float PlayerKeyHigh = 0.16f;
        // Portrait crop, fractions of the source (from the TOP-left): centre and side (fraction of the height).
        private const float PortraitCentreX = 0.58f;
        private const float PortraitCentreYFromTop = 0.465f;
        private const float PortraitSide = 0.27f;

        // Key: brightest channel below Low = background, above High = figure (JPEG black noise stays under ~0.04).
        private const float KeyLow = 0.035f;
        private const float KeyHigh = 0.13f;
        private const float MaskThreshold = 0.09f;
        // Devil flood fill: the bottom edge seeds the background only left of this fraction of the width (the cape
        // and legs reach the bottom further right).
        private const float DevilBottomSeedMaxX = 0.34f;
        private const int EdgeGrow = 2;
        private const int EdgeBlur = 2;

        [MenuItem("Dance/UI/Build Devil Art")]
        public static void BuildDevilArt()
        {
            Texture2D src = LoadImage(SourcePath);
            if (src == null)
            {
                Debug.LogError("DevilArtBuilder: " + SourcePath + " not found.");
                BuildPlayerArt();
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
            // Background = the black reached by a flood fill from the top and left edges and the empty left part of
            // the bottom edge (the figure is cut off at the right and bottom). Dark gaps enclosed by the figure
            // (between armour plates, under the arms, between the legs) are not reached and stay opaque black.
            byte[] closed = Morph(Morph(mask, w, h, HoleCloseRadius, true), w, h, HoleCloseRadius, false);
            byte[] background = FloodFromEdges(closed, w, h, false, Mathf.RoundToInt(DevilBottomSeedMaxX * w));
            var solid = new byte[n];
            for (int i = 0; i < n; i++) solid[i] = background[i] == 0 ? (byte)1 : (byte)0;
            solid = Morph(solid, w, h, EdgeGrow - 1, true);
            float[] soft = BoxBlur(solid, w, h, EdgeBlur);

            for (int i = 0; i < n; i++)
            {
                float ramp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(KeyLow, KeyHigh, lum[i]));
                float a = Mathf.Max(soft[i], ramp);
                px[i].a = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);
            }

            Sprite sprite = WriteSprite(px, w, h, OutputPath);
            UiTheme theme = LoadTheme();
            if (sprite != null && theme != null)
            {
                theme.DevilSprite = sprite;
                EditorUtility.SetDirty(theme);
                AssetDatabase.SaveAssets();
                Debug.Log($"DevilArtBuilder: {OutputPath} ({w}×{h}) built and set on the UI theme.");
            }
            BuildPlayerArt();
        }

        private static UiTheme LoadTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null)
            {
                MainMenuBuilder.BuildTheme(); // picks the built sprites up itself
                theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            }
            return theme;
        }

        /// <summary>Defne + the Devil keyed (menu stage), Defne's portrait and its grey copy (ending).</summary>
        public static void BuildPlayerArt()
        {
            Texture2D src = LoadImage(PlayerSourcePath);
            if (src == null)
            {
                Debug.LogWarning("DevilArtBuilder: " + PlayerSourcePath + " not found; no player art or portrait.");
                return;
            }
            int w = src.width, h = src.height, n = w * h;
            Color32[] original = src.GetPixels32(); // rows bottom-up
            Object.DestroyImmediate(src);

            // 1. Keyed full image.
            var px = (Color32[])original.Clone();
            var lum = new float[n];
            var mask = new byte[n];
            for (int i = 0; i < n; i++)
            {
                Color32 c = px[i];
                float l = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) / 255f;
                lum[i] = l;
                mask[i] = l > MaskThreshold ? (byte)1 : (byte)0;
            }
            byte[] closed = Morph(Morph(mask, w, h, HoleCloseRadius, true), w, h, HoleCloseRadius, false);
            byte[] background = FloodFromEdges(closed, w, h, true, 0);
            var solid = new byte[n];
            for (int i = 0; i < n; i++) solid[i] = background[i] == 0 ? (byte)1 : (byte)0;
            solid = Morph(solid, w, h, 1, true);
            float[] soft = BoxBlur(solid, w, h, EdgeBlur);
            for (int i = 0; i < n; i++)
            {
                float ramp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PlayerKeyLow, PlayerKeyHigh, lum[i]));
                px[i].a = (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.Max(soft[i], ramp)) * 255f);
            }
            Sprite player = WriteSprite(px, w, h, PlayerArtPath);

            // 2. Portrait: a square around Defne's head and shoulders, original pixels on a dark vignette.
            int side = Mathf.Min(Mathf.RoundToInt(PortraitSide * h), Mathf.Min(w, h));
            int x0 = Mathf.Clamp(Mathf.RoundToInt(PortraitCentreX * w - side * 0.5f), 0, w - side);
            int y0 = Mathf.Clamp(Mathf.RoundToInt((1f - PortraitCentreYFromTop) * h - side * 0.5f), 0, h - side);
            var crop = new Color32[side * side];
            var grey = new Color32[side * side];
            for (int y = 0; y < side; y++)
            {
                float v = (y + 0.5f) / side * 2f - 1f;
                for (int x = 0; x < side; x++)
                {
                    float u = (x + 0.5f) / side * 2f - 1f;
                    // Rounded-square distance: 1 at the middle of each edge.
                    float d = Mathf.Pow(u * u * u * u + v * v * v * v, 0.25f);
                    float shade = Mathf.Lerp(1f, 0.55f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.95f, d)));
                    float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, d));
                    Color32 c = original[(y0 + y) * w + x0 + x];
                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    crop[y * side + x] = new Color32((byte)(c.r * shade), (byte)(c.g * shade), (byte)(c.b * shade), a);
                    float l = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) * shade * 0.92f;
                    grey[y * side + x] = new Color32((byte)Mathf.Clamp(l * 0.96f, 0f, 255f),
                        (byte)Mathf.Clamp(l * 0.98f, 0f, 255f), (byte)Mathf.Clamp(l * 1.03f, 0f, 255f), a);
                }
            }
            Sprite portrait = WriteSprite(crop, side, side, PortraitPath);
            Sprite portraitGrey = WriteSprite(grey, side, side, PortraitGreyPath);

            UiTheme theme = LoadTheme();
            if (theme == null) return;
            if (player != null) theme.PlayerArtSprite = player;
            if (portrait != null) theme.PortraitSprite = portrait;
            if (portraitGrey != null) theme.PortraitGreySprite = portraitGrey;
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log($"DevilArtBuilder: {PlayerArtPath} ({w}×{h}), {PortraitPath} and {PortraitGreyPath} " +
                      $"({side}×{side}) built and set on the UI theme.");
        }

        /// <summary>
        /// Background = pixels with mask 0 connected (4-neighbour) to the top or left edge, the right edge when
        /// <paramref name="seedRight"/>, and the bottom edge left of <paramref name="bottomSeedMaxX"/> (px); figures
        /// cut off at an edge do not seed there. Returns 1 for background.
        /// </summary>
        private static byte[] FloodFromEdges(byte[] mask, int w, int h, bool seedRight, int bottomSeedMaxX)
        {
            var bg = new byte[mask.Length];
            var stack = new int[mask.Length];
            int top = 0;
            for (int x = 0; x < w; x++) Seed(mask, bg, stack, ref top, (h - 1) * w + x); // top row (rows bottom-up)
            for (int x = 0; x < Mathf.Min(bottomSeedMaxX, w); x++) Seed(mask, bg, stack, ref top, x);
            for (int y = 0; y < h; y++)
            {
                Seed(mask, bg, stack, ref top, y * w);
                if (seedRight) Seed(mask, bg, stack, ref top, y * w + w - 1);
            }
            while (top > 0)
            {
                int i = stack[--top];
                int x = i % w, y = i / w;
                if (x > 0) Seed(mask, bg, stack, ref top, i - 1);
                if (x < w - 1) Seed(mask, bg, stack, ref top, i + 1);
                if (y > 0) Seed(mask, bg, stack, ref top, i - w);
                if (y < h - 1) Seed(mask, bg, stack, ref top, i + w);
            }
            return bg;
        }

        private static void Seed(byte[] mask, byte[] bg, int[] stack, ref int top, int i)
        {
            if (mask[i] != 0 || bg[i] != 0) return;
            bg[i] = 1;
            stack[top++] = i;
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
