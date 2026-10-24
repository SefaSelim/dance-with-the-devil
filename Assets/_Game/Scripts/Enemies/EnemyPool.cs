using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Pool of one enemy prefab. Instances live under a scene-root container. <see cref="Get"/> spawns an instance at a
    /// position; a dead enemy returns itself one frame after death (<see cref="Enemy.Despawn"/>).
    /// The prewarm runs in Start (scene load), not on the first spawn, so the packs of the later windows (bullet-hell
    /// pass 2026-10-25: active cap up to 32, ~20 of one type alive) do not instantiate a batch of enemies mid-fight.
    /// </summary>
    public sealed class EnemyPool : MonoBehaviour
    {
        [Tooltip("Enemy prefab (root has Enemy, Health, NavMeshAgent, a brain and colliders).")]
        [SerializeField] private Enemy _prefab;
        [Tooltip("Instances created up front when the scene starts (packs and higher active caps need more than 6).")]
        [SerializeField, Min(0)] private int _prewarmCount = 32; // = WaveDirector max active cap: one type can fill it after 5:00
        [Tooltip("Maximum pooled (inactive) instances kept.")]
        [SerializeField, Min(1)] private int _maxSize = 32;

        private UnityEngine.Pool.ObjectPool<Enemy> _pool;
        private Transform _container;

        public Enemy Prefab => _prefab;
        public int CountActive => _pool != null ? _pool.CountActive : 0;
        public int CountInactive => _pool != null ? _pool.CountInactive : 0;

        /// <summary>Set the prefab before first use (code-built setups). Ignored once the pool exists.</summary>
        public void Configure(Enemy prefab, int prewarm = 0)
        {
            if (_pool != null) return;
            _prefab = prefab;
            _prewarmCount = Mathf.Max(0, prewarm);
        }

        private void Start()
        {
            if (_prefab != null) EnsurePool();
        }

        /// <summary>Takes an instance and spawns it at <paramref name="position"/> facing <paramref name="rotation"/>.</summary>
        public Enemy Get(Vector3 position, Quaternion rotation, Transform target)
        {
            EnsurePool();
            if (_pool == null) return null;
            Enemy enemy = _pool.Get();
            enemy.Spawn(position, rotation, target);
            return enemy;
        }

        public void Release(Enemy enemy)
        {
            if (enemy == null) return;
            if (_pool == null)
            {
                Destroy(enemy.gameObject);
                return;
            }
            _pool.Release(enemy);
        }

        private void OnDestroy()
        {
            if (_pool != null)
            {
                _pool.Clear();
                _pool = null;
            }
            if (_container != null) Destroy(_container.gameObject); // also removes active instances
        }

        private void EnsurePool()
        {
            if (_pool != null) return;
            if (_prefab == null)
            {
                Debug.LogError($"{nameof(EnemyPool)} '{name}': no prefab set.", this);
                return;
            }

            _container = new GameObject(name + "_Enemies").transform;
            _pool = new UnityEngine.Pool.ObjectPool<Enemy>(Create, OnGet, OnRelease, OnDestroyItem,
                collectionCheck: false, defaultCapacity: Mathf.Max(1, _prewarmCount), maxSize: _maxSize);

            if (_prewarmCount <= 0) return;
            var warm = new Enemy[_prewarmCount];
            for (int i = 0; i < warm.Length; i++) warm[i] = _pool.Get();
            for (int i = 0; i < warm.Length; i++) _pool.Release(warm[i]);
        }

        private Enemy Create()
        {
            Enemy enemy = Instantiate(_prefab, _container);
            enemy.name = _prefab.name;
            enemy.gameObject.SetActive(false);
            enemy.BindPool(this);
            return enemy;
        }

        private static void OnGet(Enemy enemy)
        {
            // Activated by Enemy.Spawn so it is placed before its agent is enabled.
        }

        private static void OnRelease(Enemy enemy) => enemy.gameObject.SetActive(false);

        private static void OnDestroyItem(Enemy enemy)
        {
            if (enemy != null) Destroy(enemy.gameObject);
        }
    }
}
