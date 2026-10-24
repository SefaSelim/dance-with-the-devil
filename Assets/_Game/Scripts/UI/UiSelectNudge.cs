using UnityEngine;
using UnityEngine.EventSystems;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Menu button feedback (design/ux/main-menu.md §2): the selected button slides a few units right, and hovering
    /// with the mouse selects it, so mouse and keyboard/pad share one focus. Respects UiKit.ReduceMotion.
    /// </summary>
    public sealed class UiSelectNudge : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        [SerializeField] private float _offset = 8f;
        [SerializeField] private float _speed = 18f;

        private RectTransform _rect;
        private Vector2 _home;
        private bool _selected;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _home = _rect.anchoredPosition;
        }

        public void OnSelect(BaseEventData eventData) => _selected = true;
        public void OnDeselect(BaseEventData eventData) => _selected = false;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        private void Update()
        {
            Vector2 target = _home + (_selected ? new Vector2(_offset, 0f) : Vector2.zero);
            if (UiKit.ReduceMotion)
            {
                _rect.anchoredPosition = target;
                return;
            }
            float k = 1f - Mathf.Exp(-_speed * Time.unscaledDeltaTime);
            _rect.anchoredPosition = Vector2.Lerp(_rect.anchoredPosition, target, k);
        }
    }
}
