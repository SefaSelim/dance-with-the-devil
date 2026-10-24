using UnityEngine;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Keeps an ornate frame's top "Rim" copy the same colour and visibility as its border block, so screens that
    /// recolour the border (slot states, the Devil card) also recolour the visible bevel. Added by UiKit.Framed.
    /// </summary>
    public sealed class UiRimTint : MonoBehaviour
    {
        [SerializeField] private Image _border;
        [SerializeField] private Image _rim;

        public void Initialize(Image border, Image rim)
        {
            _border = border;
            _rim = rim;
        }

        private void LateUpdate()
        {
            if (_border == null || _rim == null) return;
            if (_rim.color != _border.color) _rim.color = _border.color;
            if (_rim.enabled != _border.enabled) _rim.enabled = _border.enabled;
        }
    }
}
