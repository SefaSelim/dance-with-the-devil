using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Run-start line (MAIN_GDD §2) with a one-line goal under it, on a dark band near the top of the game image so
    /// it does not sit on Defne or the first spawns. It stays fully visible for at least <see cref="_minHold"/>
    /// seconds and until the player's first key or mouse press (at most <see cref="_maxHold"/> seconds), then fades.
    /// Does not block play; unscaled time; no synthesized italics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IntroLine : MonoBehaviour
    {
        [Tooltip("Font. Empty = built-in LegacyRuntime.")]
        [SerializeField] private Font _font;
        [Tooltip("Overlay canvas sorting order (display is -1000, HUD 10).")]
        [SerializeField] private int _sortingOrder = 20;
        [Tooltip("Seconds always fully visible (unscaled).")]
        [SerializeField, Min(0f)] private float _minHold = 4f;
        [Tooltip("Seconds after which it fades even if the player has not pressed anything (unscaled).")]
        [SerializeField, Min(0f)] private float _maxHold = 10f;
        [Tooltip("Fade-out seconds (unscaled).")]
        [SerializeField, Min(0f)] private float _fade = 0.8f;

        private RectTransform _root;
        private CanvasGroup _group;
        private float _time;
        private bool _inputSeen;

        private void Awake()
        {
            _root = UiKit.CreateCanvas(transform, "IntroCanvas", _sortingOrder, false);
            Font font = UiKit.ResolveFont(_font);
            Vector2 mid = new Vector2(0.5f, 0.5f);
            Image band = UiKit.Box(_root, "Band", new Color(0.03f, 0.02f, 0.05f, 0.72f), mid, mid,
                new Vector2(0f, 104f), new Vector2(640f, 46f));
            _group = band.gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            Text intro = UiKit.Label(band.transform, "Intro", font, 16, UiKit.Cream, TextAnchor.MiddleCenter,
                mid, mid, new Vector2(0f, 8f), new Vector2(600f, 20f));
            UiKit.AddShadow(intro);
            intro.text = GameTexts.Intro;

            Text goal = UiKit.Label(band.transform, "Goal", font, 12, UiKit.Light, TextAnchor.MiddleCenter,
                mid, mid, new Vector2(0f, -12f), new Vector2(600f, 16f));
            UiKit.AddShadow(goal);
            goal.text = GameTexts.IntroGoal;
        }

        private void Update()
        {
            _time += Time.unscaledDeltaTime;
            if (!_inputSeen) _inputSeen = AnyPress();

            bool holding = _time < _minHold || (!_inputSeen && _time < _maxHold);
            if (holding) return;

            float fade = UiKit.ReduceMotion ? 0f : _fade;
            float alpha = fade > 0f ? _group.alpha - Time.unscaledDeltaTime / fade : 0f;
            if (alpha <= 0f)
            {
                _root.gameObject.SetActive(false);
                enabled = false;
                return;
            }
            _group.alpha = alpha;
        }

        private static bool AnyPress()
        {
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;
            Gamepad g = Gamepad.current;
            return (k != null && k.anyKey.wasPressedThisFrame)
                || (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame))
                || (g != null && (g.buttonSouth.wasPressedThisFrame || g.rightTrigger.wasPressedThisFrame));
        }
    }
}
