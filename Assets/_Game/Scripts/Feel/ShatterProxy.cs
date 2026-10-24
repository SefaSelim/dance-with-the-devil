using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// An invisible box that stands in for a piece of a skinned model when it bursts into cubes
    /// (<see cref="PixelImpactFeedback.Shatter"/> sizes the burst from a part's lossy scale, which a bone does not
    /// have). Parented to a bone by the character builder, so it follows the animation; its lossy scale is the box size
    /// in metres. <see cref="DefneCostVisuals"/> uses these for the face loss (contract 4) and the final crumble.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShatterProxy : MonoBehaviour
    {
        [Tooltip("Colour of the cubes this piece bursts into (before any contract grey).")]
        [SerializeField] private Color _color = Color.gray;
        [Tooltip("Part of the face: bursts, one after another, with contract 4 (Yüzün).")]
        [SerializeField] private bool _face;

        public Color Color => _color;
        public bool IsFace => _face;

        public void Configure(Color color, bool face)
        {
            _color = color;
            _face = face;
        }
    }
}
