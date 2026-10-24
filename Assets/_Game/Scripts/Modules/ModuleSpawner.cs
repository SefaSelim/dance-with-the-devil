using System;
using UnityEngine;
using UnityEngine.AI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Offer director (MAIN_GDD §7 Sıklık; first GDD §11-12): offers at ~8, ~25 and ~42 s of run time, then every
    /// 14-20 s after the previous one; never while an offer is unresolved (it waits). Types come from a shuffled bag
    /// of the three, no repeat before the bag empties. The offer appears ≥4 m from the player on free floor that the
    /// camera sees and the player has line of sight to. Scaled time: stops during hitstop and the contract screen.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModuleSpawner : MonoBehaviour
    {
        private const int MaxAttempts = 24;

        [Header("References")]
        [Tooltip("The player's module component.")]
        [SerializeField] private PlayerModules _modules;
        [Tooltip("Camera rig for the 'visible' check. Empty = taken from the player's controller; none = no check.")]
        [SerializeField] private PixelCameraRig _cameraRig;
        [Tooltip("Optional candidate floor points. Empty = random points around the player snapped to the NavMesh.")]
        [SerializeField] private Transform[] _floorPoints = Array.Empty<Transform>();

        [Header("Timing (GDD §7)")]
        [Tooltip("Start on Start(). Off = call Begin().")]
        [SerializeField] private bool _beginOnStart = true;
        [Tooltip("Run times of the tutorial offers in seconds (GDD: ~8, ~25, ~42).")]
        [SerializeField] private float[] _tutorialTimes = { 8f, 25f, 42f };
        [Tooltip("Shortest gap after the tutorial offers, seconds (GDD: 14).")]
        [SerializeField, Min(0.1f)] private float _intervalMin = 14f;
        [Tooltip("Longest gap after the tutorial offers, seconds (GDD: 20).")]
        [SerializeField, Min(0.1f)] private float _intervalMax = 20f;
        [Tooltip("Retry delay when no valid floor point was found, seconds.")]
        [SerializeField, Min(0.05f)] private float _retryDelay = 0.25f;
        [Tooltip("Random seed. 0 = a new seed each run.")]
        [SerializeField] private int _seed;

        [Header("Placement (GDD §7)")]
        [Tooltip("Minimum flat distance from the player, metres (GDD: ≥4).")]
        [SerializeField, Min(0f)] private float _minDistance = 4f;
        [Tooltip("Maximum flat distance from the player for random points, metres.")]
        [SerializeField, Min(0.1f)] private float _maxDistance = 8f;
        [Tooltip("NavMesh.SamplePosition search radius, metres.")]
        [SerializeField, Min(0.1f)] private float _navMeshSampleRadius = 1.5f;
        [Tooltip("Viewport margin for the visible check (0..0.5).")]
        [SerializeField, Range(0f, 0.45f)] private float _viewportMargin = 0.08f;
        [Tooltip("Free-space capsule radius at the point (nothing may overlap it).")]
        [SerializeField, Min(0.05f)] private float _clearRadius = 0.45f;
        [Tooltip("Layers that make a point not free / block line of sight.")]
        [SerializeField] private LayerMask _obstacleMask = ~0;

        private readonly RaycastHit[] _lineHits = new RaycastHit[16];
        private readonly ModuleType[] _bag = new ModuleType[ModuleCatalog.Count];
        private System.Random _random;
        private int _bagIndex = ModuleCatalog.Count;
        private int _lastType = -1;
        private int _offersSpawned;
        private float _elapsed;
        private float _nextOfferTime;
        private float _lastSpawnTime;
        private bool _begun;

        public bool Paused { get; set; }
        public bool HasBegun => _begun;
        public float Elapsed => _elapsed;
        /// <summary>Run time when the next offer is due (it still waits for a free slot and a valid point).</summary>
        public float NextOfferTime => _nextOfferTime;
        public int OffersSpawned => _offersSpawned;
        /// <summary>The active offer, or null.</summary>
        public ModuleOffer Current => _modules != null ? _modules.Current : null;
        public PlayerModules Modules => _modules;

        /// <summary>For code-built setups (tests).</summary>
        public void Configure(PlayerModules modules, PixelCameraRig cameraRig, Transform[] floorPoints)
        {
            _modules = modules;
            _cameraRig = cameraRig;
            _floorPoints = floorPoints ?? Array.Empty<Transform>();
        }

        private void Start()
        {
            if (_beginOnStart) Begin();
        }

        /// <summary>Starts (or restarts) the offer clock at 0.</summary>
        public void Begin()
        {
            _random = _seed != 0 ? new System.Random(_seed) : new System.Random();
            _begun = true;
            _elapsed = 0f;
            _offersSpawned = 0;
            _bagIndex = ModuleCatalog.Count;
            _lastType = -1;
            _lastSpawnTime = 0f;
            _nextOfferTime = ScheduledTime(0);
            if (_cameraRig == null && _modules != null && _modules.Player != null) _cameraRig = _modules.Player.CameraRig;
        }

        private float ScheduledTime(int index)
        {
            if (_tutorialTimes != null && index < _tutorialTimes.Length) return _tutorialTimes[index];
            float min = Mathf.Min(_intervalMin, _intervalMax);
            float max = Mathf.Max(_intervalMin, _intervalMax);
            return _lastSpawnTime + Mathf.Lerp(min, max, (float)_random.NextDouble());
        }

        private void Update()
        {
            if (!_begun || Paused || _modules == null) return;
            PlayerController player = _modules.Player;
            if (player == null || player.Health.IsDead) return;

            _elapsed += Time.deltaTime;
            if (_elapsed < _nextOfferTime || !_modules.CanPresentOffer) return;

            if (!TryFindPoint(player.transform.position, out Vector3 point))
            {
                _nextOfferTime = _elapsed + _retryDelay;
                return;
            }

            ModuleType type = NextType();
            if (_modules.PresentOffer(type, point))
            {
                _offersSpawned++;
                _lastSpawnTime = _elapsed;
                _nextOfferTime = ScheduledTime(_offersSpawned);
                // A tutorial time already passed (late first offers) is not skipped, it comes as soon as possible.
            }
        }

        private ModuleType NextType()
        {
            if (_bagIndex >= _bag.Length)
            {
                for (int i = 0; i < _bag.Length; i++) _bag[i] = (ModuleType)i;
                for (int i = _bag.Length - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
                }
                // No back-to-back repeat across bag boundaries either.
                if ((int)_bag[0] == _lastType) (_bag[0], _bag[_bag.Length - 1]) = (_bag[_bag.Length - 1], _bag[0]);
                _bagIndex = 0;
            }
            ModuleType type = _bag[_bagIndex++];
            _lastType = (int)type;
            return type;
        }

        private bool TryFindPoint(Vector3 playerPosition, out Vector3 point)
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                Vector3 candidate;
                if (_floorPoints != null && _floorPoints.Length > 0)
                {
                    Transform t = _floorPoints[_random.Next(_floorPoints.Length)];
                    if (t == null) continue;
                    candidate = t.position;
                }
                else
                {
                    float angle = (float)_random.NextDouble() * Mathf.PI * 2f;
                    float distance = Mathf.Lerp(_minDistance, Mathf.Max(_minDistance, _maxDistance), (float)_random.NextDouble());
                    candidate = playerPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                }

                if (!ProjectToFloor(candidate, out point)) continue;
                if (FlatDistance(point, playerPosition) < _minDistance) continue;
                if (!IsVisible(point) || !IsFree(point) || !HasLineOfSight(playerPosition, point)) continue;
                return true;
            }
            point = default;
            return false;
        }

        private bool ProjectToFloor(Vector3 candidate, out Vector3 point)
        {
            if (NavMesh.SamplePosition(candidate, out NavMeshHit navHit, _navMeshSampleRadius, NavMesh.AllAreas))
            {
                point = navHit.position;
                return true;
            }
            // No NavMesh: the floor collider under the point.
            if (Physics.Raycast(candidate + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f, _obstacleMask,
                    QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }
            point = candidate;
            return false;
        }

        private bool IsVisible(Vector3 point)
        {
            if (_cameraRig == null || _cameraRig.GameCamera == null) return true;
            Vector3 v = _cameraRig.GameCamera.WorldToViewportPoint(point + Vector3.up);
            return v.z > 0f && v.x > _viewportMargin && v.x < 1f - _viewportMargin &&
                   v.y > _viewportMargin && v.y < 1f - _viewportMargin;
        }

        private bool IsFree(Vector3 point)
        {
            Vector3 bottom = point + Vector3.up * (_clearRadius + 0.05f);
            Vector3 top = point + Vector3.up * 1.6f;
            return !Physics.CheckCapsule(bottom, top, _clearRadius, _obstacleMask, QueryTriggerInteraction.Ignore);
        }

        private bool HasLineOfSight(Vector3 playerPosition, Vector3 point)
        {
            Vector3 from = playerPosition + Vector3.up;
            Vector3 delta = point + Vector3.up - from;
            float distance = delta.magnitude;
            if (distance < 1e-3f) return true;
            int count = Physics.RaycastNonAlloc(from, delta / distance, _lineHits, distance, _obstacleMask,
                QueryTriggerInteraction.Ignore);
            Transform playerRoot = _modules.transform;
            for (int i = 0; i < count; i++)
            {
                Collider collider = _lineHits[i].collider;
                if (collider == null || collider.transform.IsChildOf(playerRoot)) continue;
                if (collider.GetComponentInParent<IDamageable>() != null) continue; // enemies do not hide the floor
                return false;
            }
            return true;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
