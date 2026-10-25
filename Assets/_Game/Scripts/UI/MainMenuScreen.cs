using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Main menu UI (design/ux/main-menu.md §2), built in code like the other screens: title and four buttons on the
    /// left (Başla, Ayarlar, Emeği Geçenler, Çıkış), a settings panel and a credits panel. Keyboard, mouse and gamepad
    /// navigate it; Esc / pad East closes an open panel. Başla fades to black and loads the arena. The 2D stage behind
    /// it (drawing of Defne with the Devil, engraving, glow, embers, fog) lives in the scene, see <see cref="MainMenuStage"/>.
    /// </summary>
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] private int _sortingOrder = 10;
        [SerializeField, Min(0f)] private float _fadeSeconds = 0.4f;

        private RectTransform _root;
        private GameObject _mainColumn;
        private GameObject _settingsPanel;
        private GameObject _creditsPanel;
        private Button _startButton;
        private Button _settingsButton;
        private Button _creditsButton;
        private Selectable _firstSetting;
        private Button _creditsBack;
        private TMP_Text _fullscreenValue;
        private Image _fade;
        private CanvasGroup _mainGroup;
        private GameObject _returnTo;
        private float _fadeStart = -1f;

        private void Start()
        {
            Time.timeScale = 1f;
            UiKit.EnsureEventSystem();
            Build();
            Select(_startButton.gameObject);
            PlayIntro();
        }

        /// <summary>Title and buttons pop in one after another (DOTween, unscaled; ReduceMotion = shown at once).</summary>
        private void PlayIntro()
        {
            int i = 0;
            foreach (Transform child in _mainColumn.transform)
            {
                var rect = (RectTransform)child;
                Button button = child.GetComponent<Button>();
                // Prefab buttons keep their fitted scale as the rest scale.
                float rest = button != null ? child.localScale.x : 1f;
                UiKit.PopIn(rect, 0.05f + i * 0.06f, 0.9f, 0.25f, rest);
                i++;
            }
        }

        private void Update()
        {
            if (_fadeStart >= 0f)
            {
                float t = _fadeSeconds <= 0f || UiKit.ReduceMotion ? 1f : (Time.unscaledTime - _fadeStart) / _fadeSeconds;
                _fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t));
                if (t >= 1f)
                {
                    _fadeStart = -1f;
                    SceneManager.LoadScene(GameSettings.ArenaScene);
                }
                return;
            }

            if (BackPressed() && (_settingsPanel.activeSelf || _creditsPanel.activeSelf)) ClosePanels();

            // Keep something selected so the keyboard / pad always has a focus (a mouse click on empty space clears it).
            EventSystem es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == null && AnyNavigationPressed())
            {
                Select(_settingsPanel.activeSelf ? _firstSetting.gameObject
                    : _creditsPanel.activeSelf ? _creditsBack.gameObject : _startButton.gameObject);
            }
        }

        // ------------------------------------------------------------ actions

        private void OnStart()
        {
            if (_fadeStart >= 0f) return;
            _mainGroup.interactable = false;
            _fade.gameObject.SetActive(true);
            _fadeStart = Time.unscaledTime;
        }

        private void OpenSettings()
        {
            _returnTo = _settingsButton.gameObject;
            ShowPanel(_settingsPanel);
            Select(_firstSetting.gameObject);
        }

        private void OpenCredits()
        {
            _returnTo = _creditsButton.gameObject;
            ShowPanel(_creditsPanel);
            Select(_creditsBack.gameObject);
        }

        private void ShowPanel(GameObject panel)
        {
            _settingsPanel.SetActive(panel == _settingsPanel);
            _creditsPanel.SetActive(panel == _creditsPanel);
            _mainGroup.interactable = false;
            UiKit.PopIn((RectTransform)panel.transform);
        }

        private void ClosePanels()
        {
            UiKit.NotifyClick();
            _settingsPanel.SetActive(false);
            _creditsPanel.SetActive(false);
            _mainGroup.interactable = true;
            Select(_returnTo != null ? _returnTo : _startButton.gameObject);
        }

        private static void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ToggleFullscreen()
        {
            GameSettings.SetFullscreen(!GameSettings.Fullscreen);
            _fullscreenValue.text = GameSettings.Fullscreen ? GameTexts.SettingsOn : GameTexts.SettingsOff;
        }

        private static void Select(GameObject go)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(go);
        }

        private static bool BackPressed()
        {
            Keyboard k = Keyboard.current;
            Gamepad g = Gamepad.current;
            return (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame);
        }

        private static bool AnyNavigationPressed()
        {
            Keyboard k = Keyboard.current;
            Gamepad g = Gamepad.current;
            return (k != null && (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame
                    || k.wKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame))
                || (g != null && (g.dpad.up.wasPressedThisFrame || g.dpad.down.wasPressedThisFrame
                    || g.leftStick.ReadValue().sqrMagnitude > 0.25f));
        }

        // ------------------------------------------------------------ build

        private void Build()
        {
            Font body = UiKit.ResolveFont(null);
            Font title = UiKit.TitleFont;
            UiTheme theme = UiKit.Theme;
            _root = UiKit.CreateCanvas(transform, "MenuCanvas", _sortingOrder, true);
            Vector2 tl = new Vector2(0f, 1f);
            Vector2 mid = new Vector2(0.5f, 0.5f);
            Vector2 bottom = new Vector2(0.5f, 0f);

            // Left shade so the column reads over the 3D scene.
            UiKit.Box(_root, "Shade", new Color(0.02f, 0.01f, 0.02f, 0.55f), Vector2.zero, Vector2.zero,
                Vector2.zero, new Vector2(230f, 360f));
            UiKit.Box(_root, "ShadeEdge", new Color(0.02f, 0.01f, 0.02f, 0.3f), Vector2.zero, Vector2.zero,
                new Vector2(230f, 0f), new Vector2(24f, 360f));

            RectTransform column = UiKit.Rect(_root, "Column", tl, tl, Vector2.zero, new Vector2(640f, 360f));
            _mainColumn = column.gameObject;
            _mainGroup = _mainColumn.AddComponent<CanvasGroup>();

            Text titleText = UiKit.Label(column, "Title", title, 34, UiKit.Cream, TextAnchor.MiddleLeft, tl, tl,
                new Vector2(36f, -44f), new Vector2(300f, 48f));
            titleText.text = GameTexts.MenuTitle;
            UiKit.AddShadow(titleText);
            Text subtitle = UiKit.Label(column, "Subtitle", body, 12, UiKit.CostRed, TextAnchor.MiddleLeft, tl, tl,
                new Vector2(40f, -90f), new Vector2(240f, 16f));
            subtitle.text = GameTexts.MenuSubtitle;
            UiKit.AddShadow(subtitle);
            if (theme != null && theme.DividerSprite != null)
            {
                Image divider = UiKit.Box(column, "Divider", new Color(0.85f, 0.8f, 0.78f, 0.9f), tl, tl,
                    new Vector2(30f, -106f), new Vector2(190f, 24f));
                divider.sprite = theme.DividerSprite;
                divider.preserveAspect = true;
            }

            // Minimal Synty prefab (focus = gold frame + gold label; hover selects through UiSelectNudge).
            GameObject prefab = UiKit.ButtonPrefab;
            Vector2 buttonSize = new Vector2(150f, 30f);
            const float buttonFont = 16f;
            _startButton = UiKit.PrefabButton(column, prefab, GameTexts.MenuStart, tl, tl, new Vector2(36f, -134f),
                buttonSize, out _, buttonFont, "Start");
            _settingsButton = UiKit.PrefabButton(column, prefab, GameTexts.MenuSettings, tl, tl,
                new Vector2(36f, -170f), buttonSize, out _, buttonFont, "Settings");
            _creditsButton = UiKit.PrefabButton(column, prefab, GameTexts.MenuCredits, tl, tl,
                new Vector2(36f, -206f), buttonSize, out _, buttonFont, "Credits");
            Button quit = UiKit.PrefabButton(column, prefab, GameTexts.MenuQuit, tl, tl, new Vector2(36f, -242f),
                buttonSize, out _, buttonFont, "Quit");
            _startButton.onClick.AddListener(OnStart);
            _settingsButton.onClick.AddListener(OpenSettings);
            _creditsButton.onClick.AddListener(OpenCredits);
            quit.onClick.AddListener(OnQuit);
            LinkVertical(_startButton, _settingsButton, _creditsButton, quit);
            foreach (Button b in new[] { _startButton, _settingsButton, _creditsButton, quit })
                b.gameObject.AddComponent<UiSelectNudge>();

            Text hint = UiKit.Label(column, "Hint", body, 10, UiKit.Dim, TextAnchor.MiddleLeft, tl, tl,
                new Vector2(40f, -282f), new Vector2(200f, 14f));
            hint.text = GameTexts.MenuHint;
            // Under the menu column (it used to sit over Defne's legs in the 2D menu).
            Text footer = UiKit.Label(_root, "Footer", body, 12, UiKit.Light, TextAnchor.MiddleLeft, Vector2.zero,
                Vector2.zero, new Vector2(40f, 14f), new Vector2(260f, 16f));
            footer.text = GameTexts.MenuFooter;
            UiKit.AddShadow(footer);

            BuildSettings(body);
            BuildCredits(body);

            _fade = UiKit.Stretch(_root.parent, "Fade").gameObject.AddComponent<Image>();
            _fade.color = new Color(0f, 0f, 0f, 0f);
            _fade.raycastTarget = true;
            _fade.gameObject.SetActive(false);
        }

        private void BuildSettings(Font body)
        {
            Vector2 mid = new Vector2(0.5f, 0.5f);
            Vector2 top = new Vector2(0.5f, 1f);
            Image panel = UiKit.Framed(_root, "SettingsPanel", UiKit.Cream, new Color(0.07f, 0.05f, 0.07f, 0.96f), mid, mid,
                new Vector2(70f, 0f), new Vector2(300f, 236f), 2f);
            _settingsPanel = panel.transform.parent.gameObject;
            Transform p = panel.transform;
            Text heading = UiKit.Label(p, "Title", UiKit.TitleFont, 20, UiKit.Cream, TextAnchor.MiddleCenter, top, top,
                new Vector2(0f, -12f), new Vector2(260f, 26f));
            heading.text = GameTexts.SettingsTitle;
            UiKit.AddShadow(heading);

            Slider master = SliderRow(p, body, GameTexts.SettingsMaster, -50f, GameSettings.Master, GameSettings.SetMaster);
            Slider music = SliderRow(p, body, GameTexts.SettingsMusic, -78f, GameSettings.Music, GameSettings.SetMusic);
            Slider sfx = SliderRow(p, body, GameTexts.SettingsSfx, -106f, GameSettings.Sfx, GameSettings.SetSfx);
            Slider shake = SliderRow(p, body, GameTexts.SettingsShake, -134f, GameSettings.Shake, GameSettings.SetShake);
            _firstSetting = master;

            // Fullscreen: a button that flips the value (one control, works the same on pad and keyboard).
            Text fsLabel = UiKit.Label(p, "FullscreenLabel", body, 12, UiKit.Cream, TextAnchor.MiddleLeft, top,
                new Vector2(0f, 0.5f), new Vector2(-122f, -162f), new Vector2(110f, 18f));
            fsLabel.text = GameTexts.SettingsFullscreen;
            Button fs = UiKit.PrefabButton(p, UiKit.SmallButtonPrefab,
                GameSettings.Fullscreen ? GameTexts.SettingsOn : GameTexts.SettingsOff, top, mid,
                new Vector2(48f, -162f), new Vector2(120f, 20f), out _fullscreenValue, 12f, "Fullscreen");
            fs.onClick.AddListener(ToggleFullscreen);

            Button back = UiKit.PrefabButton(p, UiKit.SmallButtonPrefab, GameTexts.MenuBack, new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(120f, 26f), out _, 14f, "Back");
            back.onClick.AddListener(ClosePanels);
            LinkVertical(master, music, sfx, shake, fs, back);
            _settingsPanel.SetActive(false);
        }

        private void BuildCredits(Font body)
        {
            Vector2 mid = new Vector2(0.5f, 0.5f);
            Vector2 top = new Vector2(0.5f, 1f);
            Image panel = UiKit.Framed(_root, "CreditsPanel", UiKit.Cream, new Color(0.07f, 0.05f, 0.07f, 0.96f), mid, mid,
                new Vector2(70f, 0f), new Vector2(340f, 236f), 2f);
            _creditsPanel = panel.transform.parent.gameObject;
            Transform p = panel.transform;
            Text heading = UiKit.Label(p, "Title", UiKit.TitleFont, 20, UiKit.Cream, TextAnchor.MiddleCenter, top, top,
                new Vector2(0f, -12f), new Vector2(300f, 26f));
            heading.text = GameTexts.CreditsTitle;
            UiKit.AddShadow(heading);
            Text text = UiKit.Label(p, "Body", body, 10, UiKit.Light, TextAnchor.UpperCenter, top, top,
                new Vector2(0f, -44f), new Vector2(300f, 140f));
            text.text = GameTexts.CreditsBody;
            _creditsBack = UiKit.PrefabButton(p, UiKit.SmallButtonPrefab, GameTexts.MenuBack, new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(120f, 26f), out _, 14f, "Back");
            _creditsBack.onClick.AddListener(ClosePanels);
            _creditsPanel.SetActive(false);
        }

        /// <summary>Label + slider (0..1); left/right arrows or the pad stick change it (uGUI steps 10%).</summary>
        private static Slider SliderRow(Transform parent, Font font, string label, float y, float value,
            UnityEngine.Events.UnityAction<float> onChanged)
        {
            Vector2 top = new Vector2(0.5f, 1f);
            Vector2 left = new Vector2(0f, 0.5f);
            Text text = UiKit.Label(parent, label, font, 12, UiKit.Cream, TextAnchor.MiddleLeft, top, left,
                new Vector2(-122f, y), new Vector2(110f, 18f));
            text.text = label;

            RectTransform root = UiKit.Rect(parent, label + "_Slider", top, new Vector2(0.5f, 0.5f), new Vector2(48f, y),
                new Vector2(120f, 12f));
            Image back = UiKit.Box(root, "Background", new Color(0.03f, 0.02f, 0.03f, 1f), left, left, Vector2.zero,
                new Vector2(120f, 6f));
            back.raycastTarget = true;
            RectTransform fillArea = UiKit.Rect(root, "FillArea", left, left, Vector2.zero, new Vector2(120f, 6f));
            // Slider stretches fill and handle over their areas, so their own size is only the extra beyond the area.
            Image fill = UiKit.Box(fillArea, "Fill", UiKit.HealthRed, left, left, Vector2.zero, Vector2.zero);
            RectTransform handleArea = UiKit.Rect(root, "HandleArea", left, left, Vector2.zero, new Vector2(120f, 12f));
            Image handle = UiKit.Box(handleArea, "Handle", UiKit.Cream, left, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(6f, 0f));
            handle.raycastTarget = true;

            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(value);
            ColorBlock colors = slider.colors;
            colors.highlightedColor = UiKit.Gold;
            colors.selectedColor = UiKit.Gold;
            slider.colors = colors;
            slider.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        /// <summary>Explicit up/down navigation down a column, wrapping at the ends.</summary>
        private static void LinkVertical(params Selectable[] items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                Navigation nav = items[i].navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = items[(i - 1 + items.Length) % items.Length];
                nav.selectOnDown = items[(i + 1) % items.Length];
                // Sliders keep left/right for their value; buttons have none.
                nav.selectOnLeft = null;
                nav.selectOnRight = null;
                items[i].navigation = nav;
            }
        }
    }
}
