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
    /// MainMenu scene (design/ux/main-menu.md): Defne on a plinth between two candelabra, the Devil engraving behind
    /// her on a screen-space backdrop canvas (full view height for any aspect, feathered edges, behind the 3D stage),
    /// cold key light and a red back light, and the code-built menu UI. Sets the build list to
    /// MainMenu (0) then Arena (1).
    /// </summary>
    public static class MainMenuBuilder
    {
        private const string ThemePath = "Assets/_Game/Resources/UiTheme.asset";
        private const string MenuScenePath = "Assets/_Game/Scenes/MainMenu.unity";
        private const string ArenaScenePath = "Assets/_Game/Scenes/Arena.unity";
        private const string DevilImagePath = "Assets/_Game/Art/UI/devil_placeholder_dore.jpg";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";
        private const string Synty = "Assets/Synty/InterfaceDarkFantasyHUD";
        private const string SyntySprites = Synty + "/Sprites/DarkFantasy/SPR_DarkFantasy_";
        private const string Polygon = "Assets/PolygonDarkFantasy/Prefabs";

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
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            if (theme.ButtonPrefab == null || theme.SmallButtonPrefab == null)
                Debug.LogWarning("MainMenuBuilder: Synty button prefabs not found; buttons fall back to the code-built plates.");
            if (theme.TitleFont == null || theme.BodyFont == null || theme.PanelSprite == null || theme.ButtonSprite == null)
                Debug.LogWarning("MainMenuBuilder: some theme fonts/sprites were not found; screens fall back to the flat look.");
            else
                Debug.Log("MainMenuBuilder: UI theme built at " + ThemePath);
        }

        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(SyntySprites + name + ".png");

        [MenuItem("Dance/UI/Build Main Menu Scene")]
        public static void BuildMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.08f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.03f, 0.02f, 0.04f);
            RenderSettings.fogStartDistance = 7f;
            RenderSettings.fogEndDistance = 16f;
            RenderSettings.skybox = null;

            // Camera: a little low, Defne right of centre (the menu column sits on the left).
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.02f, 0.04f);
            cam.fieldOfView = 32f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 40f;
            camGo.transform.position = new Vector3(-0.6f, 1.35f, -5.6f);
            camGo.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.05f, 0f) - camGo.transform.position);
            camGo.AddComponent<AudioListener>();

            // Cold key light from the front left, red back light behind her, a faint warm fill.
            var keyGo = new GameObject("Key_Moon");
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.62f, 0.7f, 0.92f);
            key.intensity = 0.9f;
            key.shadows = LightShadows.Soft;
            keyGo.transform.rotation = Quaternion.Euler(28f, 35f, 0f);

            var backGo = new GameObject("Back_DevilRed");
            var back = backGo.AddComponent<Light>();
            back.type = LightType.Point;
            back.color = new Color(1f, 0.16f, 0.1f);
            back.intensity = 6f;
            back.range = 6f;
            backGo.transform.position = new Vector3(1.3f, 2.0f, 1.2f);

            // Ground: a dark disc of stone, the plinth and the sigil ring glowing on it.
            Material stone = StoneMaterial();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0.6f, -0.05f, 1f);
            ground.transform.localScale = new Vector3(14f, 0.05f, 14f);
            ground.GetComponent<Renderer>().sharedMaterial = stone;
            Object.DestroyImmediate(ground.GetComponent<Collider>());

            Vector3 defnePos = new Vector3(0.95f, 0.18f, 0.2f);
            var plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plinth.name = "Plinth";
            plinth.transform.position = new Vector3(defnePos.x, 0.09f, defnePos.z);
            plinth.transform.localScale = new Vector3(1.5f, 0.09f, 1.5f);
            plinth.GetComponent<Renderer>().sharedMaterial = stone;
            Object.DestroyImmediate(plinth.GetComponent<Collider>());

            UiTheme theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            Transform sigil = null;
            if (theme != null && theme.SigilSprite != null)
            {
                var sigilGo = new GameObject("Sigil");
                var sr = sigilGo.AddComponent<SpriteRenderer>();
                sr.sprite = theme.SigilSprite;
                sr.color = new Color(0.85f, 0.12f, 0.1f, 0.55f);
                sigilGo.transform.position = new Vector3(defnePos.x, 0.19f, defnePos.z);
                sigilGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                float size = Mathf.Max(theme.SigilSprite.bounds.size.x, 0.01f);
                sigilGo.transform.localScale = Vector3.one * (2.2f / size);
                sigil = sigilGo.transform;
            }

            // Defne: the game model, idling with the rifle, turned three-quarters to the camera.
            var defnePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterBuilder.DefnePrefabPath);
            GameObject defne = null;
            if (defnePrefab != null)
            {
                defne = (GameObject)PrefabUtility.InstantiatePrefab(defnePrefab, scene);
                defne.name = "Defne";
                defne.transform.position = defnePos;
                defne.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
                foreach (var r in defne.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            }
            else Debug.LogWarning("MainMenuBuilder: char_defne.prefab missing; run Dance/Character/Build Defne first.");

            // The Devil: the engraving on a screen-space backdrop behind the whole stage, multiplied deep red.
            Transform devil = BuildBackdrop(cam);

            // Candelabra either side of the plinth (decor: colliders removed).
            Place(Polygon + "/Props/SM_Prop_Candelabra_01.prefab", new Vector3(-0.35f, 0f, 0.9f), 20f, scene);
            Place(Polygon + "/Props/SM_Prop_Candelabra_01.prefab", new Vector3(2.35f, 0f, 1.2f), -25f, scene);

            var stageGo = new GameObject("MenuStage");
            var stage = stageGo.AddComponent<MainMenuStage>();
            stage.Configure(defne != null ? defne.transform : null, devil, back, sigil);

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
        /// The Devil engraving as a backdrop that always fills the view height (any aspect ratio): a Screen Space -
        /// Camera canvas on the menu camera, drawn at 30 m so the whole 3D stage stands in front of it. The image
        /// (feathered on all four sides, DevilArtBuilder.BuildMenuBackdrop) is 112% of the view height, right of
        /// centre so his face sits above Defne, and its width follows its aspect; beyond its soft edges the camera's
        /// dark clear colour shows, so no rectangle edge is ever visible. MainMenuStage breathes its scale (rest 1.04).
        /// </summary>
        private static Transform BuildBackdrop(Camera cam)
        {
            Sprite sprite = DevilArtBuilder.BuildMenuBackdrop();
            var canvasGo = new GameObject("DevilBackdrop", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 30f;
            canvas.sortingOrder = -100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var artGo = new GameObject("DevilEngraving", typeof(RectTransform));
            var rect = (RectTransform)artGo.transform;
            rect.SetParent(canvasGo.transform, false);
            rect.anchorMin = new Vector2(0.62f, -0.06f);
            rect.anchorMax = new Vector2(0.62f, 1.06f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one * 1.04f;
            var image = artGo.AddComponent<Image>();
            image.sprite = sprite;
            image.color = sprite != null ? new Color(0.62f, 0.13f, 0.11f, 1f) : new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = false;
            var fitter = artGo.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            fitter.aspectRatio = sprite != null && sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 0.88f;
            return rect;
        }

        private static Material StoneMaterial()
        {
            EnsureFolder(MaterialsFolder);
            string path = MaterialsFolder + "/mat_menu_stone.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", new Color(0.11f, 0.1f, 0.12f));
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void Place(string prefabPath, Vector3 position, float yaw, Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("MainMenuBuilder: missing " + prefabPath);
                return;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
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
