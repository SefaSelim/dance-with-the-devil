using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Focus feedback of the minimal prefab buttons (added by <see cref="UiKit.PrefabButton"/> and
    /// <see cref="UiKit.PrefabHoldButton"/>): the thin frame turns gold while the button is selected (keyboard/pad) or
    /// hovered, and the button gets a small scale punch when it gains focus or is pressed (DOTween, unscaled time,
    /// skipped with <see cref="UiKit.ReduceMotion"/>). The label's gold comes from the Button's colour tint.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiButtonFx : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler
    {
        [SerializeField] private Image _frame;
        [SerializeField] private Color _normal = new Color(0.62f, 0.58f, 0.52f, 0.75f);
        [SerializeField] private Color _focus = new Color(1f, 0.82f, 0.3f, 1f);
        [SerializeField, Range(0f, 0.3f)] private float _focusPunch = 0.07f;
        [SerializeField, Range(0f, 0.3f)] private float _pressPunch = 0.05f;

        private Vector3 _restScale = Vector3.one;
        private bool _selected;
        private bool _hovered;
        private bool _focused;
        private Tween _tween;

        private void Awake() => _restScale = transform.localScale;

        /// <summary>Called right after the button was fitted (its scale is the rest scale).</summary>
        public void Initialize(Image frame, Color normal, Color focus)
        {
            _frame = frame;
            _normal = normal;
            _focus = focus;
            _restScale = transform.localScale;
            Refresh(false);
        }

        private void OnEnable()
        {
            EventSystem es = EventSystem.current;
            _selected = es != null && es.currentSelectedGameObject == gameObject;
            _hovered = false;
            Refresh(false);
        }

        private void OnDisable()
        {
            _tween?.Kill();
            _tween = null;
            transform.localScale = _restScale;
            _hovered = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            Refresh(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            Refresh(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Refresh(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Refresh(false);
        }

        public void OnPointerDown(PointerEventData eventData) => Punch(_pressPunch);

        private void Refresh(bool punchOnGain)
        {
            bool focus = _selected || _hovered;
            if (_frame != null) _frame.color = focus ? _focus : _normal;
            if (focus && !_focused && punchOnGain) Punch(_focusPunch);
            _focused = focus;
        }

        private void Punch(float amount)
        {
            if (amount <= 0f || !isActiveAndEnabled) return;
            _tween?.Kill();
            transform.localScale = _restScale;
            if (UiKit.ReduceMotion) return;
            _tween = transform.DOPunchScale(_restScale * amount, 0.25f, 6, 0.6f).SetUpdate(true).SetLink(gameObject);
        }
    }
}
