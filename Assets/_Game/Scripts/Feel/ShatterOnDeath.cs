using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// When the <see cref="Health"/> dies, bursts the model into coloured cubes (GDD §13: everything that gets hit
    /// scatters into its pixels) with the kill feel (capped shake, zoom kick, small screen shockwave:
    /// <see cref="PixelImpactFeedback.KillFeel"/>). No white pop at the body's centre: it was a solid cube that read as
    /// the enemy turning into a box for a frame before bursting. Hiding the model is left to the owner
    /// (enemy, training dummy).
    /// <list type="bullet">
    /// <item>Skinned model (Synty enemies): its <see cref="ShatterProxy"/> boxes on the bones burst into ~300 tiny
    /// pixel cubes in their baked palettes (<see cref="PixelImpactFeedback.Disintegrate"/>).</item>
    /// <item>Cube model (placeholders, dummy): each cube part is cut into chunky cells (<see cref="PixelImpactFeedback.Shatter"/>).</item>
    /// </list>
    /// While alive, every bullet or machete hit chips pixels off the part nearest the hit along the hit direction
    /// (<see cref="SprayChips"/>, called by <see cref="PixelImpactFeedback"/> from the ImpactBus).
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class ShatterOnDeath : MonoBehaviour
    {
        [Tooltip("Root of the cube parts. Empty = this object.")]
        [SerializeField] private Transform _model;
        [Tooltip("Cube speed multiplier for bullet kills.")]
        [SerializeField, Min(0f)] private float _bulletForce = 1f;
        [Tooltip("Cube speed multiplier for machete kills.")]
        [SerializeField, Min(0f)] private float _meleeForce = 1.1f;
        [Tooltip("Cube speed multiplier for rocket and explosion kills.")]
        [SerializeField, Min(0f)] private float _explosionForce = 1.6f;

        private Health _health;
        private Transform[] _parts;
        private ShatterProxy[] _proxies;   // parallel to _parts; null entries for plain cube parts
        private Color[] _colors;
        private bool _skinned;
        private Transform[] _visibleParts;
        private ShatterProxy[] _visibleProxies;
        private Color[] _visibleColors;

        /// <summary>True while alive with parts to chip.</summary>
        public bool CanChip => _health != null && !_health.IsDead && _parts != null && _parts.Length > 0 && isActiveAndEnabled;

        private void Awake()
        {
            _health = GetComponent<Health>();
            Transform root = _model != null ? _model : transform;
            ShatterProxy[] proxies = root.GetComponentsInChildren<ShatterProxy>(true);
            if (proxies.Length > 0)
            {
                _skinned = true;
                _parts = new Transform[proxies.Length];
                _proxies = proxies;
                _colors = new Color[proxies.Length];
                for (int i = 0; i < proxies.Length; i++)
                {
                    _parts[i] = proxies[i].transform;
                    _colors[i] = proxies[i].Color;
                }
            }
            else
            {
                MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                _parts = new Transform[renderers.Length];
                _proxies = new ShatterProxy[renderers.Length];
                _colors = new Color[renderers.Length];
                for (int i = 0; i < renderers.Length; i++)
                {
                    _parts[i] = renderers[i].transform;
                    _colors[i] = PixelImpactFeedback.PartColor(renderers[i]);
                }
            }
            _visibleParts = new Transform[_parts.Length];
            _visibleProxies = new ShatterProxy[_parts.Length];
            _visibleColors = new Color[_parts.Length];
        }

        private void OnEnable() => _health.Died += OnDied;
        private void OnDisable() => _health.Died -= OnDied;

        /// <summary>
        /// Chips pixels off the part nearest <paramref name="point"/>, sprayed along <paramref name="direction"/>.
        /// Example: <c>target.SprayChips(e.position, e.direction, e.intensity, 1f);</c>
        /// </summary>
        public void SprayChips(Vector3 point, Vector3 direction, float intensity, float countScale)
        {
            PixelImpactFeedback feedback = PixelImpactFeedback.Instance;
            if (feedback == null || !CanChip) return;
            feedback.Chip(_parts, _proxies, _colors, _parts.Length, point, direction, intensity, countScale);
        }

        private void OnDied(DamageInfo info)
        {
            PixelImpactFeedback feedback = PixelImpactFeedback.Instance;
            if (feedback == null) return;

            bool blast = info.Kind == DamageKind.Rocket || info.Kind == DamageKind.Explosion;
            float force = blast ? _explosionForce : (info.Kind == DamageKind.Melee ? _meleeForce : _bulletForce);

            // Only parts that are showing (e.g. not a hidden swing visual, not voxels already lost to a contract).
            // Preallocated arrays, filled from the front: no allocation per death.
            int visible = 0;
            Vector3 center = Vector3.zero;
            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i] == null || !_parts[i].gameObject.activeInHierarchy) continue;
                _visibleParts[visible] = _parts[i];
                _visibleProxies[visible] = _proxies[i];
                _visibleColors[visible] = _colors[i];
                center += _parts[i].position;
                visible++;
            }

            Vector3 point = info.Point.sqrMagnitude > 0f ? info.Point : transform.position + Vector3.up;
            if (_skinned)
            {
                feedback.Disintegrate(_visibleProxies, visible, point, info.Direction, force, blast);
            }
            else
            {
                // Shatter reads whole arrays: clear the unused tail so hidden parts do not burst.
                for (int i = visible; i < _visibleParts.Length; i++) _visibleParts[i] = null;
                feedback.Shatter(_visibleParts, _visibleColors, point, info.Direction, force);
            }

            Vector3 body = visible > 0 ? center / visible : transform.position + Vector3.up;
            // Rocket kills come in groups and the blast already warps the screen: a lighter kill feel each.
            feedback.KillFeel(body, blast ? 0.6f : 1f);
        }
    }
}
