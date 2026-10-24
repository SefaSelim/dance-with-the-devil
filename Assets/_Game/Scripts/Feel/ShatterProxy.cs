using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// An invisible box that stands in for a piece of a skinned model when it bursts into cubes
    /// (<see cref="PixelImpactFeedback.Shatter"/> sizes the burst from a part's lossy scale, which a bone does not
    /// have). Parented to a bone by the character builder, so it follows the animation; its lossy scale is the box size
    /// in metres. <see cref="DefneCostVisuals"/> uses these for the face loss (contract 4) and the final crumble.
    /// Enemy proxies also carry a small colour palette baked at build time from the textured body around the box
    /// (EnemyModelBuilder: atlas colours under the same identity tint and brightness as the enemy material), so the
    /// death pixels and hit chips show the character's real colours. Without a palette <see cref="Color"/> is used.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShatterProxy : MonoBehaviour
    {
        /// <summary>Most palette entries a proxy keeps.</summary>
        public const int MaxPalette = 6;

        [Tooltip("Colour of the cubes this piece bursts into (before any contract grey).")]
        [SerializeField] private Color _color = Color.gray;
        [Tooltip("Part of the face: bursts, one after another, with contract 4 (Yüzün).")]
        [SerializeField] private bool _face;
        [Tooltip("Baked surface colours around this box (as seen on screen, before lighting). Empty = Color.")]
        [SerializeField] private Color[] _palette = System.Array.Empty<Color>();
        [Tooltip("Cumulative weights of the palette (0..1, last = 1), same length as the palette.")]
        [SerializeField] private float[] _paletteCumulative = System.Array.Empty<float>();

        public Color Color => _color;
        public bool IsFace => _face;
        public bool HasPalette => _palette != null && _palette.Length > 0
                                  && _paletteCumulative != null && _paletteCumulative.Length == _palette.Length;

        public void Configure(Color color, bool face)
        {
            _color = color;
            _face = face;
        }

        /// <summary>
        /// Stores a palette (editor build time). <paramref name="weights"/> need not be normalised.
        /// Example: <c>proxy.SetPalette(new[] { red, black }, new[] { 3f, 1f });</c>
        /// </summary>
        public void SetPalette(Color[] colors, float[] weights)
        {
            int n = colors == null ? 0 : Mathf.Min(colors.Length, MaxPalette);
            if (weights == null || weights.Length < n) n = 0;
            float total = 0f;
            for (int i = 0; i < n; i++) total += Mathf.Max(0f, weights[i]);
            if (n == 0 || total <= 0f)
            {
                _palette = System.Array.Empty<Color>();
                _paletteCumulative = System.Array.Empty<float>();
                return;
            }
            _palette = new Color[n];
            _paletteCumulative = new float[n];
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                _palette[i] = colors[i];
                sum += Mathf.Max(0f, weights[i]);
                _paletteCumulative[i] = sum / total;
            }
            _paletteCumulative[n - 1] = 1f;
        }

        /// <summary>A palette colour picked by weight with <paramref name="random01"/> (0..1); <see cref="Color"/> without one.</summary>
        public Color PickColor(float random01)
        {
            if (!HasPalette) return _color;
            for (int i = 0; i < _palette.Length; i++)
                if (random01 <= _paletteCumulative[i]) return _palette[i];
            return _palette[_palette.Length - 1];
        }
    }
}
