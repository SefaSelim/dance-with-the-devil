using UnityEngine;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Keeps a code-built overlay canvas on top of the pixel camera's game image (added by
    /// <see cref="UiKit.CreateCanvas"/>): the CanvasScaler uses a constant pixel size equal to the image's scale
    /// (integer when the rig draws at an integer scale), and the 640×360 content frame covers the image rect, so the
    /// HUD never sits on the letterbox bars and its blocks are as big as the world's texels.
    /// Re-applied only when the image rect changes (no per-frame allocations).
    /// </summary>
    [DefaultExecutionOrder(1100)] // after PixelCameraRig (1000) updated its layout this frame
    [DisallowMultipleComponent]
    public sealed class UiCanvasFit : MonoBehaviour
    {
        private CanvasScaler _scaler;
        private RectTransform _frame;
        private Rect _applied = new Rect(-1f, -1f, -1f, -1f);

        /// <summary>The 640×360 content frame (bottom-left pivot).</summary>
        public RectTransform Frame => _frame;

        public void Initialize(CanvasScaler scaler, RectTransform frame)
        {
            _scaler = scaler;
            _frame = frame;
            _applied = new Rect(-1f, -1f, -1f, -1f);
            Apply();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (_scaler == null || _frame == null) return;
            Rect r = UiKit.CurrentDisplayRect();
            if (r == _applied) return;
            _applied = r;
            float scale = Mathf.Max(0.01f, r.height / UiKit.ReferenceResolution.y);
            _scaler.scaleFactor = scale;
            _frame.anchoredPosition = new Vector2(r.x / scale, r.y / scale);
            _frame.sizeDelta = new Vector2(r.width / scale, r.height / scale);
        }
    }
}
