// DecalPool.cs  (URP)
// Ring-buffer pool of URP Decal Projectors for bullet holes, scorch marks, slashes.
// Requirements: URP with a Decal Renderer Feature on the active renderer; a material using
// "Shader Graphs/Decal" (or your own decal Shader Graph) with GPU Instancing enabled; variants
// packed into an atlas (tiles chosen by uvScale/uvBias so all decals share one material and batch).
// Limitations: decals do not project onto transparent surfaces (glass, water) — skip or use particles.
// HDRP has its own DecalProjector in UnityEngine.Rendering.HighDefinition with a similar API.

using UnityEngine;
using UnityEngine.Rendering.Universal;

public class DecalPool : MonoBehaviour
{
    public static DecalPool Instance { get; private set; }

    public Material decalMaterial;
    [Tooltip("Atlas layout of decalMaterial's base map, e.g. 2x2 = four variants.")]
    public Vector2Int atlasTiles = new Vector2Int(2, 2);
    [Tooltip("Hard cap. Desktop 128-256, mobile 32-64.")]
    public int capacity = 128;
    [Tooltip("Projector width/height range in metres (bullet holes 0.05-0.15).")]
    public Vector2 sizeRange = new Vector2(0.07f, 0.11f);
    [Tooltip("Projection depth in metres; enough to wrap slight curvature, small enough not to bleed.")]
    public float projectionDepth = 0.2f;
    public float drawDistance = 40f;
    [Tooltip("The decal this many slots ahead starts fading, so reuse never pops.")]
    public int fadeAheadSlots = 16;
    public float fadeOutSeconds = 0.6f;

    DecalProjector[] pool;
    float[] fadeStart;
    int next;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        capacity = Mathf.Max(1, capacity);
        pool = new DecalProjector[capacity];
        fadeStart = new float[capacity];
        for (int i = 0; i < capacity; i++) { pool[i] = Create(i); fadeStart[i] = -1f; }
    }

    DecalProjector Create(int i)
    {
        var go = new GameObject("Decal_" + i);
        go.transform.SetParent(transform, false);
        go.SetActive(false);
        var p = go.AddComponent<DecalProjector>();
        p.material = decalMaterial;
        p.drawDistance = drawDistance;
        return p;
    }

    /// <summary>Place a decal. tile = -1 picks a random atlas tile. Pass hitTransform for movers.</summary>
    public DecalProjector Spawn(Vector3 point, Vector3 normal, Transform parent = null, int tile = -1, float sizeScale = 1f)
    {
        int i = next;
        next = (next + 1) % capacity;

        var p = pool[i];
        if (p == null) p = pool[i] = Create(i); // the old parent was destroyed and took it along

        var t = p.transform;
        t.SetParent(transform, false);

        normal = normal.sqrMagnitude > 0f ? normal.normalized : Vector3.up;
        Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
        // Projector looks along +Z, so it must face INTO the surface. Random roll around the normal.
        Quaternion rot = Quaternion.LookRotation(-normal, up) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        float s = Random.Range(sizeRange.x, sizeRange.y) * sizeScale;
        p.size = new Vector3(s, s, projectionDepth);
        p.pivot = new Vector3(0f, 0f, projectionDepth * 0.5f);
        t.SetPositionAndRotation(point + normal * (projectionDepth * 0.5f), rot);

        int tilesX = Mathf.Max(1, atlasTiles.x), tilesY = Mathf.Max(1, atlasTiles.y);
        int count = tilesX * tilesY;
        int idx = tile < 0 ? Random.Range(0, count) : Mathf.Clamp(tile, 0, count - 1);
        p.uvScale = new Vector2(1f / tilesX, 1f / tilesY);
        p.uvBias = new Vector2((idx % tilesX) / (float)tilesX, (idx / tilesX) / (float)tilesY);

        p.fadeFactor = 1f;
        fadeStart[i] = -1f;
        if (parent != null && !parent.gameObject.isStatic) t.SetParent(parent, true);
        p.gameObject.SetActive(true);

        // Begin fading the decal that will be reused soon.
        int j = (i + Mathf.Clamp(fadeAheadSlots, 1, capacity - 1)) % capacity;
        if (capacity > 1 && pool[j] != null && pool[j].gameObject.activeSelf && fadeStart[j] < 0f)
            fadeStart[j] = Time.time;

        return p;
    }

    void Update()
    {
        for (int i = 0; i < capacity; i++)
        {
            if (fadeStart[i] < 0f) continue;
            var p = pool[i];
            if (p == null) { fadeStart[i] = -1f; continue; }
            float k = 1f - (Time.time - fadeStart[i]) / Mathf.Max(0.0001f, fadeOutSeconds);
            if (k <= 0f) { p.gameObject.SetActive(false); fadeStart[i] = -1f; }
            else p.fadeFactor = k;
        }
    }
}
