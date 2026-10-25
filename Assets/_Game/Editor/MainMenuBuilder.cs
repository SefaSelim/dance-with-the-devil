using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// Builds the UI theme asset (Synty Interface Dark Fantasy HUD sprites, Pirata One / Markazi fonts) and the
    /// MainMenu scene (design/ux/main-menu.md): a 2D stage on a screen-space camera canvas (the Doré engraving, a red
    /// glow, the keyed drawing of Defne with the Devil behind her, embers and fog animated by MainMenuStage), a plain
    /// clearing camera and the code-built menu UI. Sets the build list to MainMenu (0) then Arena (1).
    /// </summary>
    public static class MainMenuBuilder
    {
        private const string ThemePath = "Assets/_Game/Resources/UiTheme.asset";
        private const string MenuScenePath = "Assets/_Game/Scenes/MainMenu.unity";
        private const string ArenaScenePath = "Assets/_Game/Scenes/Arena.unity";
        private const string Synty = "Assets/Synty/InterfaceDarkFantasyHUD";
        private const string SyntySprites = Synty + "/Sprites/DarkFantasy/SPR_DarkFantasy_";

        [MenuItem("Dance/UI/Build Theme + Main Menu")]
        public static void BuildAll()
        {
            BuildTheme();
            BuildMenuScene();
        }

        [MenuItem("Dance/UI/Build Theme")]
        public static void BuildTheme()
        {
            EnsureFolder("Assets/_Game/Resources");
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UiTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            theme.TitleFont = AssetDatabase.LoadAssetAtPath<Font>(Synty + "/Fonts/Pirata One/PirataOne-Regular.ttf");
            theme.BodyFont = AssetDatabase.LoadAssetAtPath<Font>(Synty + "/Fonts/Markazi/MarkaziText-SemiBold.ttf");
            theme.PanelSprite = Sprite("Frame_Box_Medium_01_Variant_01");
            theme.PanelFillSprite = Sprite("Frame_Box_Medium_01_Background");
            theme.ButtonSprite = Sprite("Box_Hotbar_01");
            theme.SlotSprite = Sprite("Frame_Box_Small_01");
            theme.DividerSprite = Sprite("Bar_05");
            theme.GlowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Synty + "/Sprites/FX/SPR_FX_DarkFantasy_Glow_01.png");
            theme.SigilSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Synty + "/Sprites/HUD/SPR_FX_DarkFantasy_Sigil_Ring_Large_01.png");
            theme.VignetteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Synty + "/Sprites/FX/SPR_FX_DarkFantasy_Vignette_Damage_Tendrils_01.png");
            // Minimal buttons: one prefab for both sizes (dark parchment bar, centred label) + a thin frame.
            var button = AssetDatabase.LoadAssetAtPath<GameObject>(
                Synty + "/Prefabs/Buttons/HUD_DarkFantasy_Button_WorldSpace_Item_01.prefab");
            theme.ButtonPrefab = button;
            theme.SmallButtonPrefab = button;
            theme.ButtonFrameSprite = Sprite("Frame_Box_Small_01");
            var devilArt = AssetDatabase.LoadAssetAtPath<Sprite>(DevilArtBuilder.OutputPath);
            if (devilArt != null) theme.DevilSprite = devilArt;
            var playerArt = AssetDatabase.LoadAssetAtPath<Sprite>(DevilArtBuilder.PlayerArtPath);
            if (playerArt != null) theme.PlayerArtSprite = playerArt;
            var portrait = AssetDatabase.LoadAssetAtPath<Sprite>(DevilArtBuilder.PortraitPath);
            if (portrait != null) theme.PortraitSprite = portrait;
            var portraitGrey = AssetDatabase.LoadAssetAtPath<Sprite>(DevilArtBuilder.PortraitGreyPath);
            if (portraitGrey != null) theme.PortraitGreySprite = portraitGrey;
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            if (theme.ButtonPrefab == null || theme.SmallButtonPrefab == null)
                Debug.LogWarning("MainMenuBuilder: Synty button prefabs not found; buttons fall back to the code-built plates.");
            if (theme.TitleFont == null || theme.BodyFont == null || theme.PanelSprite == null || theme.ButtonSprite == null)
                Debug.LogWarning("MainMenuBuilder: some theme fonts/sprites were not found; screens fall back to the flat look.");
            else
                Debug.Log("MainMenuBuilder: UI theme built at " + ThemePath);
        }

        private const string SoftGlowPath = "Assets/_Game/Art/UI/menu_soft_glow.png";

        /// <summary>A 256² white radial gradient (alpha = (1 − r)², uncompressed) for the menu glow and fog.</summary>
        private static Sprite SoftGlowSprite()
        {
            if (!System.IO.File.Exists(SoftGlowPath))
            {
                const int size = 256;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f;
                        float dy = (y + 0.5f) / size * 2f - 1f;
                        float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                        float a = (1f - r) * (1f - r);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                }
                tex.SetPixels32(pixels);
                System.IO.File.WriteAllBytes(SoftGlowPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(SoftGlowPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(SoftGlowPath);
                importer.textureType = TextureImporterType.Sprite;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(SoftGlowPath);
        }

        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(SyntySprites + name + ".png");

        [MenuItem("Dance/UI/Build Main Menu Scene")]
        public static void BuildMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2D menu: no lights or 3D stage; the camera only clears and carries the backdrop canvas.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.08f, 0.12f);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.02f, 0.04f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 40f;
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();

            UiTheme theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            var stageGo = new GameObject("MenuStage");
            var stage = stageGo.AddComponent<MainMenuStage>();
            BuildStage(cam, theme, stage);

            var menuGo = new GameObject("MainMenu");
            menuGo.AddComponent<MainMenuScreen>();
            // Music and the UI click; no player, waves or Devil on the menu.
            var audio = new GameObject("GameAudio").AddComponent<GameAudio>();
            var aso = new SerializedObject(audio);
            aso.FindProperty("_bank").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<SoundBank>("Assets/_Game/Audio/SoundBank.asset");
            aso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            SetBuildScenes();
            Debug.Log("MainMenuBuilder: main menu scene saved at " + MenuScenePath);
        }

        /// <summary>
        /// The 2D stage on one Screen Space - Camera canvas behind the menu UI (1920×1080 reference, height matched, so
        /// every layer keeps its size and only the visible width changes with the aspect ratio). Back to front: the
        /// Doré engraving (feathered, full height, deep red), a red glow behind the characters, fog wisps, back embers,
        /// Defne with the Devil (keyed drawing, full height, bottom-anchored at 64% of the width so the left menu column
        /// stays clear at 4:3 … 21:9), front embers and a low fog. MainMenuStage animates it (ReduceMotion = still).
        /// </summary>
        private static void BuildStage(Camera cam, UiTheme theme, MainMenuStage stage)
        {
            var canvasGo = new GameObject("MenuBackdrop", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;
            canvas.sortingOrder = -100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            Transform root = canvasGo.transform;
            // A smooth generated radial glow: the Synty Glow_01 sprite showed stepped octagon rings at this size.
            Sprite glowSprite = SoftGlowSprite();

            // Doré engraving: 112% of the view height, behind everything, dimmer than before so the drawing reads.
            Sprite engraving = DevilArtBuilder.BuildMenuBackdrop();
            RectTransform devil = FullHeight(root, "DevilEngraving", engraving, 0.62f, -0.06f, 1.06f,
                new Vector2(0.5f, 0.5f), engraving != null ? new Color(0.46f, 0.1f, 0.09f, 1f) : Color.clear, 0.88f);
            devil.localScale = Vector3.one * 1.04f;

            // Red glow behind the characters.
            Image glow = null;
            if (glowSprite != null)
            {
                glow = NewImage(root, "RedGlow", glowSprite, new Color(0.8f, 0.12f, 0.06f, 0.3f));
                var rect = glow.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.66f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(1300f, 1150f);
            }

            RectTransform fogBack = Layer(root, "FogBack");
            RectTransform embersBack = Layer(root, "EmbersBack");

            Sprite player = theme != null ? theme.PlayerArtSprite : null;
            if (player == null) player = AssetDatabase.LoadAssetAtPath<Sprite>(DevilArtBuilder.PlayerArtPath);
            if (player == null)
                Debug.LogWarning("MainMenuBuilder: devil_player.png missing; run Dance/UI/Build Devil Art first.");
            // Right edge on the screen edge: the drawing's Devil is cut by the image border, which would show as a
            // hard vertical line anywhere else. Any aspect keeps the left part (empty black) behind the menu column.
            RectTransform characters = FullHeight(root, "DefneAndDevil", player, 1f, 0f, 1f, new Vector2(1f, 0f),
                player != null ? Color.white : Color.clear, 0.746f);

            RectTransform embersFront = Layer(root, "EmbersFront");
            RectTransform fogFront = Layer(root, "FogFront");

            stage.Configure(devil, characters, glow, fogBack, fogFront, embersBack, embersFront, glowSprite);
        }

        /// <summary>An image as tall as the given anchor span at <paramref name="x"/>, width from its aspect.</summary>
        private static RectTransform FullHeight(Transform parent, string name, Sprite sprite, float x, float yMin,
            float yMax, Vector2 pivot, Color color, float fallbackAspect)
        {
            Image image = NewImage(parent, name, sprite, color);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(x, yMin);
            rect.anchorMax = new Vector2(x, yMax);
            rect.pivot = pivot;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            fitter.aspectRatio = sprite != null && sprite.rect.height > 0f
                ? sprite.rect.width / sprite.rect.height : fallbackAspect;
            return rect;
        }

        private static Image NewImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>An empty full-canvas layer (MainMenuStage fills it with pooled embers or fog at runtime).</summary>
        private static RectTransform Layer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void SetBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(ArenaScenePath, true),
            };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != MenuScenePath && s.path != ArenaScenePath) scenes.Add(new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
