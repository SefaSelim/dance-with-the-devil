// ImpactFeedbackRouter.cs
// Example wiring: one ImpactEvent fans out to every channel in the same frame.
//   - pooled VFX prefab per surface (aligned to normal, sparks biased toward reflection)
//   - decal (DecalPool, URP)
//   - camera trauma with distance falloff
//   - hitstop for melee
//   - rumble for the local player
// Effect prefabs: one-shot ParticleSystems with playOnAwake true, loop false, bursts, and
// main.stopAction = Callback on the ROOT system (PooledEffect returns it to the pool).
// Adapt freely: this is a reference for structure, not a framework.
// Requires URP because it uses DecalPool; remove the decal lines for Built-in/HDRP.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class ImpactFeedbackRouter : MonoBehaviour
{
    [System.Serializable]
    public class SurfaceEntry
    {
        public SurfaceType surface;
        public ParticleSystem effectPrefab;
        public bool spawnDecal = true;
        public float decalSize = 1f;
        public int decalTile = -1;
    }

    public SurfaceEntry[] surfaces;
    [Tooltip("0 = align effect to the surface normal, 1 = align to the reflected direction.")]
    [Range(0f, 1f)] public float reflectionBias = 0.5f;

    [Header("Camera")]
    public float bulletTrauma = 0.04f;
    public float meleeTrauma = 0.35f;
    public float explosionTrauma = 0.9f;
    public float explosionRadius = 25f;

    [Header("Hitstop (melee)")]
    public Vector2 hitstopRange = new Vector2(0.04f, 0.12f);

    readonly Dictionary<SurfaceType, SurfaceEntry> map = new Dictionary<SurfaceType, SurfaceEntry>();
    readonly Dictionary<ParticleSystem, ObjectPool<ParticleSystem>> pools = new Dictionary<ParticleSystem, ObjectPool<ParticleSystem>>();

    void Awake()
    {
        foreach (var s in surfaces) if (s != null) map[s.surface] = s;
    }

    void OnEnable() { ImpactBus.OnImpact += Handle; }
    void OnDisable() { ImpactBus.OnImpact -= Handle; }

    ObjectPool<ParticleSystem> PoolFor(ParticleSystem prefab)
    {
        if (pools.TryGetValue(prefab, out var pool)) return pool;
        ObjectPool<ParticleSystem> created = null;
        created = new ObjectPool<ParticleSystem>(
            createFunc: () =>
            {
                var ps = Instantiate(prefab, transform);
                var main = ps.main;
                main.stopAction = ParticleSystemStopAction.Callback;
                ps.gameObject.AddComponent<PooledEffect>().pool = created;
                return ps;
            },
            actionOnGet: ps => ps.gameObject.SetActive(true),
            actionOnRelease: ps => ps.gameObject.SetActive(false),
            actionOnDestroy: ps => { if (ps) Destroy(ps.gameObject); },
            collectionCheck: false, defaultCapacity: 8, maxSize: 64);
        pools[prefab] = created;
        return created;
    }

    void Handle(ImpactEvent e)
    {
        // 1. VFX
        if (!map.TryGetValue(e.surface, out var entry)) map.TryGetValue(SurfaceType.Default, out entry);
        if (entry != null && entry.effectPrefab != null)
        {
            Vector3 n = e.normal.sqrMagnitude > 0f ? e.normal.normalized : Vector3.up;
            Vector3 dir = n;
            if (e.direction.sqrMagnitude > 0f)
                dir = Vector3.Slerp(n, Vector3.Reflect(e.direction.normalized, n), reflectionBias).normalized;

            var ps = PoolFor(entry.effectPrefab).Get();
            ps.transform.SetPositionAndRotation(e.position, Quaternion.LookRotation(dir));
            ps.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.3f, e.intensity); // needs Scaling Mode = Hierarchy
            ps.Clear(true);
            ps.Play(true);

            // 2. Decal
            if (entry.spawnDecal && e.kind != ImpactKind.Melee && DecalPool.Instance != null)
                DecalPool.Instance.Spawn(e.position, n, e.hitTransform, entry.decalTile, entry.decalSize);
        }

        // 3. Camera
        switch (e.kind)
        {
            case ImpactKind.Bullet:
                if (e.instigatorIsLocalPlayer) TraumaShake.AddTraumaAt(e.position, bulletTrauma * e.intensity, 6f);
                break;
            case ImpactKind.Melee:
                TraumaShake.AddTraumaAt(e.position, meleeTrauma * Mathf.Lerp(0.5f, 1f, e.intensity), 12f);
                break;
            case ImpactKind.Explosion:
                TraumaShake.AddTraumaAt(e.position, explosionTrauma * e.intensity, explosionRadius);
                break;
        }

        // 4. Time
        if (e.kind == ImpactKind.Melee && HitStop.Instance != null)
            HitStop.Instance.TriggerGlobal(Mathf.Lerp(hitstopRange.x, hitstopRange.y, e.intensity));

        // 5. Haptics
        if (e.instigatorIsLocalPlayer)
        {
            if (e.kind == ImpactKind.Melee) Rumble.Pulse(0.3f * e.intensity, 0.6f * e.intensity, 0.1f);
            else if (e.kind == ImpactKind.Explosion) Rumble.Pulse(0.8f * e.intensity, 0.4f * e.intensity, 0.25f);
        }
    }
}
