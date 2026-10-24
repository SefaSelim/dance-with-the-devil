using UnityEngine;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>Run-start line (MAIN_GDD §2), held then faded out on unscaled time. Does not block play.</summary>
    [DisallowMultipleComponent]
    public sealed class IntroLine : MonoBehaviour
    {
        [Tooltip("Font. Empty = built-in LegacyRuntime.")]
        [SerializeField] private Font _font;
        [Tooltip("Overlay canvas sorting order (display is -1000, HUD 10).")]
        [SerializeField] private int _sortingOrder = 20;
        [Tooltip("Seconds fully visible (unscaled).")]
        [SerializeField, Min(0f)] private float _hold = 3f;
        [Tooltip("Fade-out seconds (unscaled).")]
        [SerializeField, Min(0f)] private float _fade = 0.8f;

        private RectTransform _root;
        private Text _text;
        private float _time;

        private void Awake()
        {
            _root = UiKit.CreateCanvas(transform, "IntroCanvas", _sortingOrder, false);
            _text = UiKit.Label(_root, "Intro", UiKit.ResolveFont(_font), 16, UiKit.Cream, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(560f, 40f),
                FontStyle.Italic);
            UiKit.AddShadow(_text);
            _text.text = GameTexts.Intro;
        }

        private void Update()
        {
            _time += Time.unscaledDeltaTime;
            float fade = UiKit.ReduceMotion ? 0f : _fade;
            float alpha = _time <= _hold ? 1f : fade > 0f ? 1f - (_time - _hold) / fade : 0f;
            if (alpha <= 0f)
            {
                _root.gameObject.SetActive(false);
                enabled = false;
                return;
            }
            Color c = _text.color;
            if (!Mathf.Approximately(c.a, alpha))
            {
                c.a = alpha;
                _text.color = c;
            }
        }
    }
}
