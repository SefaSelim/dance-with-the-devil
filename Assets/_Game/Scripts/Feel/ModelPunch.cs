using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Visual hit reaction on a model root (audit F33, F9): a small knock along the hit and a squash that spring back
    /// within about 0.15 s. Only the model moves; the NavMeshAgent / CharacterController and every rule stay as they
    /// are. Layered on top of whatever else poses the model: each frame the current local position and scale are
    /// taken as the new base when another script (crouch tell, Density body scale, procedural animation) has written
    /// them since this component did, so the punch never fights or drifts from them.
    /// </summary>
    [DefaultExecutionOrder(900)] // after brains and procedural animation, before the camera (1000)
    [DisallowMultipleComponent]
    public sealed class ModelPunch : MonoBehaviour
    {
        [Tooltip("Spring frequency in Hz (how fast the model snaps back).")]
        [SerializeField, Min(0.1f)] private float _frequency = 8f;
        [Tooltip("Spring damping: below 1 overshoots a little, 1 settles without overshoot.")]
        [SerializeField, Min(0.01f)] private float _damping = 0.4f;
        [Tooltip("Largest squash (fraction) a burst of hits can stack to.")]
        [SerializeField, Range(0f, 0.5f)] private float _maxSquash = 0.3f;

        private Transform _model;
        private bool _unscaledTime;
        private SecondOrderDynamics3 _offset;
        private SecondOrderDynamics _squash;
        private Vector3 _basePosition;
        private Vector3 _baseScale = Vector3.one;
        private Vector3 _writtenPosition;
        private Vector3 _writtenScale;
        private bool _written;
        private bool _active;
        private float _maxOffset;

        /// <summary>The model being punched.</summary>
        public Transform Model => _model;

        private void Awake() => EnsureSprings();

        /// <summary>Sets the model root to punch. <paramref name="unscaledTime"/>: keeps moving through freezes.</summary>
        public void Bind(Transform model, bool unscaledTime)
        {
            if (_model != model) ResetPose();
            _model = model;
            _unscaledTime = unscaledTime;
            EnsureSprings();
        }

        /// <summary>
        /// Knocks the model by <paramref name="worldOffset"/> (metres, world) and squashes it by
        /// <paramref name="squash"/> (0.15 = 15% wider and 15% shorter); both spring back.
        /// </summary>
        public void Punch(Vector3 worldOffset, float squash)
        {
            if (_model == null || !isActiveAndEnabled) return;
            EnsureSprings();
            Transform parent = _model.parent;
            Vector3 local = parent != null ? parent.InverseTransformVector(worldOffset) : worldOffset;
            _maxOffset = Mathf.Max(local.magnitude * 1.5f, 0.01f);
            _offset.Reset(Vector3.ClampMagnitude(_offset.Value + local, _maxOffset));
            _squash.Reset(Mathf.Clamp(_squash.Value + squash, -_maxSquash, _maxSquash));
            _active = true;
        }

        /// <summary>Puts the model back at its base pose at once and stops the spring.</summary>
        public void ResetPose()
        {
            if (_written && _model != null)
            {
                // Only undo what this component wrote; a value another script set since then is left alone.
                if (_model.localPosition == _writtenPosition) _model.localPosition = _basePosition;
                if (_model.localScale == _writtenScale) _model.localScale = _baseScale;
            }
            _written = false;
            _active = false;
            if (_offset != null) _offset.Reset(Vector3.zero);
            if (_squash != null) _squash.Reset(0f);
        }

        private void OnDisable() => ResetPose();

        private void LateUpdate()
        {
            if (!_active || _model == null) return;

            float dt = _unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            Vector3 offset = dt > 0f ? _offset.Update(dt, Vector3.zero) : _offset.Value;
            float squash = dt > 0f ? _squash.Update(dt, 0f) : _squash.Value;
            squash = Mathf.Clamp(squash, -_maxSquash, _maxSquash);

            Vector3 position = _model.localPosition;
            Vector3 scale = _model.localScale;
            if (!_written || position != _writtenPosition) _basePosition = position;
            if (!_written || scale != _writtenScale) _baseScale = scale;

            bool settled = offset.sqrMagnitude < 1e-6f && _offset.Velocity.sqrMagnitude < 1e-4f &&
                           Mathf.Abs(squash) < 1e-3f && Mathf.Abs(_squash.Velocity) < 1e-2f;
            if (settled)
            {
                _model.localPosition = _basePosition;
                _model.localScale = _baseScale;
                _written = false;
                _active = false;
                _offset.Reset(Vector3.zero);
                _squash.Reset(0f);
                return;
            }

            _writtenPosition = _basePosition + offset;
            _writtenScale = new Vector3(_baseScale.x * (1f + squash), _baseScale.y * (1f - squash), _baseScale.z * (1f + squash));
            _model.localPosition = _writtenPosition;
            _model.localScale = _writtenScale;
            _written = true;
        }

        private void EnsureSprings()
        {
            // r = 0: the jump made by Reset() does not feed back as an input velocity.
            if (_offset == null) _offset = new SecondOrderDynamics3(_frequency, _damping, 0f, Vector3.zero);
            if (_squash == null) _squash = new SecondOrderDynamics(_frequency, _damping, 0f, 0f);
        }

        private void OnValidate()
        {
            _offset?.SetParameters(_frequency, _damping, 0f);
            _squash?.SetParameters(_frequency, _damping, 0f);
        }
    }
}
