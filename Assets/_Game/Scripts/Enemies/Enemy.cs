using System;
using UnityEngine;
using UnityEngine.AI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Shared enemy body (GDD §6): stats, NavMeshAgent setup, target, hit reaction (white flash, a visual knock and
    /// squash of the model; the agent itself is not moved; CharacterAnimator adds its own flinch), rocket knockback,
    /// death and the pooled spawn path. Behaviour lives in a brain on the same object (<see cref="ChaserBrain"/>,
    /// <see cref="ShooterBrain"/>), which listens to <see cref="Spawned"/> and <see cref="Died"/>.
    /// On death: renderers and colliders hide, the agent stops, <see cref="Died"/> fires, and next frame the enemy
    /// returns to its <see cref="EnemyPool"/> (or deactivates). <see cref="ShatterOnDeath"/> on the same object does
    /// the cube burst.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public sealed class Enemy : MonoBehaviour, IKnockbackable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Global enemy speed multiplier (Contract 3 sets 1.15, GDD §8). Applied to agent speed every frame.</summary>
        public static float SpeedMultiplier = 1f;

        [Header("Type")]
        [Tooltip("Enemy type. Stats come from the GDD §6 defaults for this type unless Override Stats is on.")]
        [SerializeField] private EnemyType _type = EnemyType.Chaser;
        [Tooltip("Use the Stats below instead of the GDD defaults for the type.")]
        [SerializeField] private bool _overrideStats;
        [Tooltip("Custom stats (only with Override Stats).")]
        [SerializeField] private EnemyStats _stats = EnemyStats.For(EnemyType.Chaser);

        [Header("Visuals")]
        [Tooltip("Root of the cube model (hit flash, hide on death). Empty = child named \"Model\", else this object.")]
        [SerializeField] private Transform _model;
        [Tooltip("Hit flash duration in seconds (0.12 = about 7 frames: readable at the pixel camera).")]
        [SerializeField, Min(0f)] private float _flashDuration = 0.12f;
        [Tooltip("Hit flash colour.")]
        [SerializeField] private Color _flashColor = Color.white;

        [Header("Hit reaction (visual only, every damaging hit)")]
        [Tooltip("Knock of the model along the hit, metres. The NavMeshAgent does not move (no rule change).")]
        [SerializeField, Min(0f)] private float _hitPunchDistance = 0.12f;
        [Tooltip("Squash of the model on a hit (0.16 = 16% wider and shorter); springs back in about 0.12 s.")]
        [SerializeField, Range(0f, 0.4f)] private float _hitSquash = 0.16f;

        [Header("Knockback")]
        [Tooltip("Seconds over which a knockback push (rocket, GDD §5) is spent.")]
        [SerializeField, Min(0.01f)] private float _knockbackDuration = 0.15f;

        private Health _health;
        private NavMeshAgent _agent;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private MaterialPropertyBlock _block;
        private ModelPunch _punch;
        private CharacterAnimator _animator;
        private EnemyPool _pool;
        private Transform _target;
        private IDamageable _targetDamageable;

        private float _flashEndTime;
        private bool _flashing;
        private bool _alive;
        private int _despawnFrame = -1;
        private Vector3 _knockbackVelocity;
        private float _knockbackEndTime;
        private float _appliedSpeed = -1f;
        private float _speedScale = 1f;

        /// <summary>Raised at the end of <see cref="Spawn"/>, after the enemy is placed, visible and at full health.</summary>
        public event Action<Enemy> Spawned;
        /// <summary>Raised once per life when health reaches 0.</summary>
        public event Action<Enemy> Died;
        /// <summary>Applied damage for audio; lethal hits use the separate death cue.</summary>
        public static event Action<Enemy, float> DamageTaken;

        public EnemyType Type => _type;
        public EnemyStats Stats => _stats;
        public Health Health => _health;
        public NavMeshAgent Agent => _agent;
        /// <summary>Root of the cube model ("Model" child, else this transform).</summary>
        public Transform Model
        {
            get
            {
                if (_model == null)
                {
                    Transform child = transform.Find("Model");
                    _model = child != null ? child : transform;
                }
                return _model;
            }
        }
        public Transform Target => _target;
        /// <summary>The target's damageable (player Health), or null.</summary>
        public IDamageable TargetDamageable => _targetDamageable;
        public bool IsAlive => _alive;
        /// <summary>True while a knockback push is being applied (brains may skip their own movement).</summary>
        public bool IsKnockedBack => Time.time < _knockbackEndTime;
        /// <summary>True when the agent can path (enabled and on a NavMesh).</summary>
        public bool HasNavMesh => _agent != null && _agent.enabled && _agent.isOnNavMesh;
        /// <summary>Local speed scale on top of the global multiplier (brains use it, e.g. a slower back-off). Min 0.</summary>
        public float SpeedScale
        {
            get => _speedScale;
            set => _speedScale = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            _health = GetComponent<Health>();
            _agent = GetComponent<NavMeshAgent>();
            if (!_overrideStats) _stats = EnemyStats.For(_type);

            _renderers = Model.GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            _block = new MaterialPropertyBlock();
            if (!TryGetComponent(out _punch)) _punch = gameObject.AddComponent<ModelPunch>();
            if (Model != transform) _punch.Bind(Model, false); // never the root: that would fight the agent
            // The procedural animator does not subscribe to Health itself; OnDamaged calls its Flinch.
            TryGetComponent(out _animator);

            _health.InvulnerabilityAfterHit = 0f;
            _health.SetMaxHealth(_stats.MaxHealth, true);

            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            _agent.avoidancePriority = UnityEngine.Random.Range(30, 70); // mixed priorities: they push past, not stack
            _agent.autoBraking = true;
            _agent.enabled = false; // enabled by Spawn at a NavMesh position
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnHealthDied;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnHealthDied;
            SetFlash(false);
        }

        /// <summary>
        /// Places the enemy at <paramref name="position"/> (should be on the NavMesh), full health, visible, chasing
        /// <paramref name="target"/>. Used by the pool and by scene-placed enemies.
        /// </summary>
        public void Spawn(Vector3 position, Quaternion rotation, Transform target)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            _despawnFrame = -1;
            _knockbackEndTime = 0f;
            _knockbackVelocity = Vector3.zero;
            _speedScale = 1f;
            SetTarget(target);

            _agent.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _agent.enabled = true;
            if (_agent.isOnNavMesh) _agent.Warp(position);
            _appliedSpeed = -1f;
            ApplySpeed();

            _health.SetMaxHealth(_stats.MaxHealth, true);
            _health.Revive();
            SetFlash(false);
            _punch.ResetPose();
            SetVisible(true);
            _alive = true;

            Spawned?.Invoke(this);
        }

        public void Spawn(Vector3 position, Transform target) => Spawn(position, transform.rotation, target);

        /// <summary>Changes the chase target (player). Caches its damageable.</summary>
        public void SetTarget(Transform target)
        {
            _target = target;
            _targetDamageable = target != null ? target.GetComponentInParent<IDamageable>() : null;
        }

        /// <summary>Kills the enemy outright with <paramref name="info"/> (its Amount is ignored).</summary>
        public void Kill(in DamageInfo info)
        {
            if (!_alive) return;
            var lethal = info;
            lethal.Amount = _health.Current + 1f;
            _health.TakeDamage(lethal);
        }

        /// <summary>Pushes the enemy (rocket knockback): <paramref name="impulse"/> is the push distance vector.</summary>
        public void ApplyKnockback(Vector3 impulse)
        {
            if (!_alive) return;
            impulse.y = 0f;
            _knockbackVelocity = impulse / _knockbackDuration;
            _knockbackEndTime = Time.time + _knockbackDuration;
        }

        /// <summary>Flat direction to the target (zero if none).</summary>
        public Vector3 FlatToTarget()
        {
            if (_target == null) return Vector3.zero;
            Vector3 d = _target.position - transform.position;
            d.y = 0f;
            return d;
        }

        /// <summary>Turns (yaw only) toward a flat direction at <paramref name="degreesPerSecond"/> (≤0 = instantly).</summary>
        public void FaceDirection(Vector3 direction, float degreesPerSecond)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) return;
            Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = degreesPerSecond <= 0f
                ? look
                : Quaternion.RotateTowards(transform.rotation, look, degreesPerSecond * Time.deltaTime);
        }

        /// <summary>Moves along the NavMesh by <paramref name="offset"/> this frame (lunges, knockback). No-op off the NavMesh.</summary>
        public void MoveBy(Vector3 offset)
        {
            if (HasNavMesh) _agent.Move(offset);
        }

        /// <summary>Stops and clears the current path (safe off the NavMesh).</summary>
        public void StopMoving()
        {
            if (!HasNavMesh) return;
            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
        }

        /// <summary>Sets a destination (safe off the NavMesh). Returns false if not possible.</summary>
        public bool MoveTo(Vector3 destination)
        {
            if (!HasNavMesh) return false;
            _agent.isStopped = false;
            return _agent.SetDestination(destination);
        }

        private void Update()
        {
            if (_despawnFrame >= 0)
            {
                if (Time.frameCount > _despawnFrame) Despawn();
                return;
            }

            if (_flashing && Time.time >= _flashEndTime) SetFlash(false);
            if (!_alive) return;

            ApplySpeed();
            if (Time.time < _knockbackEndTime) MoveBy(_knockbackVelocity * Time.deltaTime);
        }

        private void ApplySpeed()
        {
            float speed = _stats.MoveSpeed * SpeedMultiplier * _speedScale;
            if (Mathf.Approximately(speed, _appliedSpeed) || !_agent.enabled) return;
            _appliedSpeed = speed;
            _agent.speed = speed;
        }

        private void OnDamaged(DamageInfo info, float remaining)
        {
            if (_alive) DamageTaken?.Invoke(this, remaining);
            if (!_alive || remaining <= 0f) return;
            if (_flashDuration > 0f)
            {
                _flashEndTime = Time.time + _flashDuration;
                SetFlash(true);
            }

            Vector3 push = info.Direction;
            push.y = 0f;
            if (push.sqrMagnitude < 1e-6f)
            {
                push = transform.position - info.Point;
                push.y = 0f;
            }
            if (push.sqrMagnitude > 1e-6f)
            {
                push.Normalize();
                _punch.Punch(push * _hitPunchDistance, _hitSquash);
            }
            else
            {
                _punch.Punch(Vector3.zero, _hitSquash);
            }
            // Flinch takes the direction the hit came from (toward the attacker); zero rocks straight back.
            if (_animator != null) _animator.Flinch(-push);
        }

        private void OnHealthDied(DamageInfo info)
        {
            if (!_alive) return;
            _alive = false;
            SetFlash(false);
            SetVisible(false);
            if (_agent.enabled)
            {
                if (_agent.isOnNavMesh) _agent.isStopped = true;
                _agent.enabled = false;
            }
            // Deactivate next frame so other Died listeners (ShatterOnDeath) still see the model this frame.
            _despawnFrame = Time.frameCount;
            Died?.Invoke(this);
        }

        /// <summary>Returns to the pool now (or deactivates). Called automatically one frame after death.</summary>
        public void Despawn()
        {
            if (!gameObject.activeSelf) return;
            _despawnFrame = -1;
            _alive = false;
            if (_agent.enabled) _agent.enabled = false;
            if (_pool != null) _pool.Release(this);
            else gameObject.SetActive(false);
        }

        internal void BindPool(EnemyPool pool) => _pool = pool;

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
