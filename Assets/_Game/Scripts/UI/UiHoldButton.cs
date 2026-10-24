using UnityEngine;
using UnityEngine.EventSystems;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Pointer state of a code-built UI block (left button only): held, hovered, the unscaled time of the last press
    /// and a press counter. Screens use it for deliberate inputs (hold EVET, click HAYIR, click the speech bubble)
    /// and ignore presses that started before their input lock. Needs a GraphicRaycaster on the canvas and a
    /// raycast-target Graphic on this GameObject.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler,
        IPointerExitHandler
    {
        public bool IsHeld { get; private set; }
        public bool IsHovered { get; private set; }
        /// <summary>Time.unscaledTime of the last left-button press on this block.</summary>
        public float PressedAt { get; private set; } = float.NegativeInfinity;
        /// <summary>Incremented on every left-button press; compare with a remembered value to detect a click.</summary>
        public int PressCount { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            IsHeld = true;
            PressedAt = Time.unscaledTime;
            PressCount++;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            IsHeld = false;
        }

        public void OnPointerEnter(PointerEventData eventData) => IsHovered = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            IsHovered = false;
            IsHeld = false;
        }

        private void OnDisable()
        {
            IsHeld = false;
            IsHovered = false;
        }
    }
}
