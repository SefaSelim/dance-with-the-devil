using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// 🧪 Test tool: a target for weapon testing. Flashes white when damaged (MaterialPropertyBlock on _BaseColor,
    /// no material instancing); on death hides its renderers and colliders and revives after a delay.
    /// Tip: set its Health invulnerability-after-hit to 0 so every rifle bullet registers.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class TrainingDummy : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Seconds from death to revive.")]
        [SerializeField, Min(0f)] private float _reviveDelay = 2f;
        [Tooltip("Flash duration in seconds.")]
        [SerializeField, Min(0f)] private float _flashDuration = 0.08f;
        [Tooltip("Flash colour.")]
        [SerializeField] private Color _flashColor = Color.white;

        private Health _health;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private MaterialPropertyBlock _block;
        private float _flashEndTime;
        private float _reviveTime;
        private bool _flashing;
        private bool _down;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
            SetFlash(false);
        }

        private void Update()
        {
            float now = Time.time;
            if (_flashing && now >= _flashEndTime) SetFlash(false);
            if (_down && now >= _reviveTime) Revive();
        }

        private void OnDamaged(DamageInfo info, float remaining)
        {
            if (_flashDuration <= 0f) return;
            _flashEndTime = Time.time + _flashDuration;
            SetFlash(true);
        }

        private void OnDied(DamageInfo info)
        {
            _down = true;
            _reviveTime = Time.time + _reviveDelay;
            SetFlash(false);
            SetVisible(false);
        }

        private void Revive()
        {
            _down = false;
            SetVisible(true);
            _health.Revive();
        }

        private void SetVisible(bool visible)
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = visible;
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = visible;
        }

        private void SetFlash(bool on)
        {
            if (_renderers == null || _flashing == on) return;
            _flashing = on;

            _block.Clear();
            if (on) _block.SetColor(BaseColorId, _flashColor);
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].SetPropertyBlock(_block);
        }
    }
}
