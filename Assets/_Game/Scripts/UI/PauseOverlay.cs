using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Minimal pause overlay: Esc (or pad Start) during normal play opens "Devam" and "Çık"; Esc / Enter / pad South
    /// or a click on Devam resumes; a click on Çık (or pad Select) quits (stops play mode in the Editor). Never opens
    /// on the Devil or ending screens. Display only: time, sound and input are handled by <see cref="RunPause"/>.
    /// The in-run restart stays on the results screen (open question A-15). Added at runtime by <see cref="GameHud"/>
    /// when the scene has none.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseOverlay : MonoBehaviour
    {
        [Tooltip("Font. Empty = built-in LegacyRuntime.")]
        [SerializeField] private Font _font;
        [Tooltip("Overlay canvas sorting order (above everything).")]
        [SerializeField] private int _sortingOrder = 300;

        private DevilDirector _devil;
        private EndingScreen _ending;
        private PlayerInputReader _input;
        private RectTransform _root;
        private bool _openedThisFrame;

        public bool IsOpen => RunPause.IsPaused;

        private void Awake()
        {
            RunPause.Reset();
            Build();
        }

        private void Start() => UiKit.EnsureEventSystem();

        private void OnDestroy() => RunPause.Reset();

        /// <summary>The Devil flow (pause only while it is Playing), the ending and the player's input reader.</summary>
        public void Configure(DevilDirector devil, EndingScreen ending, PlayerInputReader input)
        {
            _devil = devil;
            _ending = ending;
            _input = input;
        }

        private bool CanPause =>
            (_devil == null || _devil.State == DevilState.Playing) && (_ending == null || !_ending.IsVisible);

        private void Update()
        {
            Keyboard k = Keyboard.current;
            Gamepad g = Gamepad.current;
            bool toggle = (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.startButton.wasPressedThisFrame);

            if (!RunPause.IsPaused)
            {
                if (_root.gameObject.activeSelf) _root.gameObject.SetActive(false);
                if (toggle && CanPause) Open();
                return;
            }

            if (_openedThisFrame)
            {
                _openedThisFrame = false;
                return;
            }
            if (toggle || UiKit.ConfirmPressed()) Resume();
            else if (g != null && g.selectButton.wasPressedThisFrame) Quit();
        }

        private void Open()
        {
            RunPause.Pause(_input);
            _root.gameObject.SetActive(true);
            _openedThisFrame = true;
            UiKit.NotifyClick();
        }

        private void Resume()
        {
            UiKit.NotifyClick();
            ResumeFromButton();
        }

        private void Quit()
        {
            UiKit.NotifyClick();
            RunPause.Quit();
        }

        // Buttons already raise UiKit.Clicked through UiKit.MakeButton.
        private void ResumeFromButton()
        {
            RunPause.Resume();
            _root.gameObject.SetActive(false);
        }

        private void QuitFromButton() => RunPause.Quit();

        private void Build()
        {
            Font font = UiKit.ResolveFont(_font);
            _root = UiKit.CreateCanvas(transform, "PauseCanvas", _sortingOrder, true);
            Image back = UiKit.Stretch(_root, "Back").gameObject.AddComponent<Image>();
            back.color = new Color(0.02f, 0.01f, 0.03f, 0.7f);
            back.raycastTarget = true; // blocks clicks to anything below

            Vector2 mid = new Vector2(0.5f, 0.5f);
            Image panel = UiKit.Framed(_root, "Panel", UiKit.Cream, UiKit.Panel, mid, mid, Vector2.zero,
                new Vector2(220f, 112f));
            Text title = UiKit.Label(panel.transform, "Title", font, 16, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(200f, 20f), FontStyle.Bold);
            title.text = GameTexts.PauseTitle;
            UiKit.AddShadow(title);

            // Synty prefabs: Devam on the main button, Ana menü on the small dim one. Navigation None: the keys stay
            // as documented above (Enter resumes even if the mouse last touched Ana menü).
            var noNavigation = new Navigation { mode = Navigation.Mode.None };
            Button resume = UiKit.PrefabButton(panel.transform, UiKit.ButtonPrefab, GameTexts.PauseResume, mid, mid,
                new Vector2(0f, -4f), new Vector2(160f, 26f), out _, 14f, "Resume");
            resume.navigation = noNavigation;
            resume.onClick.AddListener(ResumeFromButton);

            Button quit = UiKit.PrefabButton(panel.transform, UiKit.SmallButtonPrefab, GameTexts.PauseQuit, mid, mid,
                new Vector2(0f, -36f), new Vector2(160f, 22f), out TMP_Text quitLabel, 12f, "Quit");
            if (quitLabel != null) quitLabel.color = UiKit.Light;
            quit.navigation = noNavigation;
            quit.onClick.AddListener(QuitFromButton);

            _root.gameObject.SetActive(false);
        }
    }
}
