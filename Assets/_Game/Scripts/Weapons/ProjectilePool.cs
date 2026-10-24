using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Pool of <see cref="Projectile"/>s. Instances come from the prefab, or, if none is set, a runtime cube
    /// (collider removed) with the fallback material, so a scene works without projectile prefabs.
    /// Instances live under a separate scene-root container so they do not move with the shooter.
    /// </summary>
    public sealed class ProjectilePool : MonoBehaviour
    {
        [Tooltip("Projectile prefab. Empty = runtime cube.")]
        [SerializeField] private Projectile _prefab;
        [Tooltip("Material of the runtime cube (only when no prefab).")]
        [SerializeField] private Material _fallbackMaterial;
        [Tooltip("Instances created up front on first use.")]
        [SerializeField, Min(0)] private int _prewarm = 16;
        [Tooltip("Maximum pooled (inactive) instances kept.")]
        [SerializeField, Min(1)] private int _maxSize = 256;

        private UnityEngine.Pool.ObjectPool<Projectile> _pool;
        private Transform _container;

        public int CountActive => _pool != null ? _pool.CountActive : 0;

        /// <summary>Set the source before first use (code-built setups). Ignored once the pool exists.</summary>
        public void Configure(Projectile prefab, Material fallbackMaterial)
        {
            if (_pool != null) return;
            _prefab = prefab;
            _fallbackMaterial = fallbackMaterial;
        }

        public Projectile Get()
        {
            EnsurePool();
            return _pool.Get();
        }

        public void Release(Projectile projectile)
        {
            if (projectile == null) return;
            if (_pool == null)
            {
                Destroy(projectile.gameObject);
                return;
            }
            _pool.Release(projectile);
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

            _container = new GameObject(name + "_Projectiles").transform;
            _pool = new UnityEngine.Pool.ObjectPool<Projectile>(Create, OnGet, OnRelease, OnDestroyItem,
                collectionCheck: false, defaultCapacity: Mathf.Max(1, _prewarm), maxSize: _maxSize);

            if (_prewarm <= 0) return;
            var warm = new Projectile[_prewarm];
            for (int i = 0; i < warm.Length; i++) warm[i] = _pool.Get();
            for (int i = 0; i < warm.Length; i++) _pool.Release(warm[i]);
        }

        private Projectile Create()
        {
            Projectile projectile = _prefab != null ? Instantiate(_prefab, _container) : CreateRuntime();
            projectile.BindPool(this);
            return projectile;
        }

        private Projectile CreateRuntime()
        {
            var root = new GameObject("Projectile");
            root.transform.SetParent(_container, false);
            Projectile projectile = root.AddComponent<Projectile>();

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Visual";
            var collider = cube.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }
            cube.transform.SetParent(root.transform, false);

            var meshRenderer = cube.GetComponent<MeshRenderer>();
            if (_fallbackMaterial != null) meshRenderer.sharedMaterial = _fallbackMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            projectile.Visual = cube.transform;
            return projectile;
        }

        private static void OnGet(Projectile projectile) => projectile.gameObject.SetActive(true);

        private static void OnRelease(Projectile projectile) => projectile.gameObject.SetActive(false);

        private static void OnDestroyItem(Projectile projectile)
        {
            if (projectile != null) Destroy(projectile.gameObject);
        }
    }
}
