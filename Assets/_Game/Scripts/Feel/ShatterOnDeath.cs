using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// When the <see cref="Health"/> dies, bursts the model's cube parts into coloured cubes through
    /// <see cref="PixelImpactFeedback.Shatter"/> (GDD §13: everything that gets hit scatters into its pixels).
    /// Hiding the model is left to the owner (enemy, training dummy).
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class ShatterOnDeath : MonoBehaviour
    {
        [Tooltip("Root of the cube parts. Empty = this object.")]
        [SerializeField] private Transform _model;
        [Tooltip("Cube speed multiplier for bullet kills.")]
        [SerializeField, Min(0f)] private float _bulletForce = 0.8f;
        [Tooltip("Cube speed multiplier for machete kills.")]
        [SerializeField, Min(0f)] private float _meleeForce = 1.1f;
        [Tooltip("Cube speed multiplier for rocket and explosion kills.")]
        [SerializeField, Min(0f)] private float _explosionForce = 1.6f;

        private Health _health;
        private MeshRenderer[] _renderers;
        private Transform[] _parts;
        private Transform[] _visibleParts;
        private Color[] _colors;
        private Color[] _visibleColors;

        private void Awake()
        {
            _health = GetComponent<Health>();
            Transform root = _model != null ? _model : transform;
            _renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            _parts = new Transform[_renderers.Length];
            _colors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _parts[i] = _renderers[i].transform;
                _colors[i] = PixelImpactFeedback.PartColor(_renderers[i]);
            }
        }

        private void OnEnable() => _health.Died += OnDied;
        private void OnDisable() => _health.Died -= OnDied;

        private void OnDied(DamageInfo info)
        {
            PixelImpactFeedback feedback = PixelImpactFeedback.Instance;
            if (feedback == null) return;

            float force = info.Kind switch
            {
                DamageKind.Rocket or DamageKind.Explosion => _explosionForce,
                DamageKind.Melee => _meleeForce,
                _ => _bulletForce,
            };
            // Only parts that are showing (e.g. not a hidden swing visual, not voxels already lost to a contract).
            int visible = 0;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i].gameObject.activeInHierarchy) visible++;
            }
            if (_visibleParts == null || _visibleParts.Length != visible)
            {
                _visibleParts = new Transform[visible];
                _visibleColors = new Color[visible];
            }
            for (int i = 0, j = 0; i < _renderers.Length; i++)
            {
                if (!_renderers[i].gameObject.activeInHierarchy) continue;
                _visibleParts[j] = _parts[i];
                _visibleColors[j] = _colors[i];
                j++;
            }

            Vector3 point = info.Point.sqrMagnitude > 0f ? info.Point : transform.position + Vector3.up;
            feedback.Shatter(_visibleParts, _visibleColors, point, info.Direction, force);
        }
    }
}
