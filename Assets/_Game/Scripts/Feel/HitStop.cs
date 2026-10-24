// HitStop.cs
// Hit pause on impact. Two modes:
//  - Global: drops Time.timeScale for a short real-time duration. Simple; fine for single-player.
//  - Selective: freezes only the given Animators (attacker + target); the world, UI and audio keep
//    running. Preferred for multiplayer or busy scenes.
// Typical durations: light hit 0.04-0.06 s, medium 0.07-0.09 s, heavy 0.1-0.15 s.
// Overlapping requests extend the end time instead of stacking or restoring early.
// Play hit flashes / shake on unscaled time if you use Global mode, or they freeze too.

using System.Collections.Generic;
using UnityEngine;

public class HitStop : MonoBehaviour
{
    public static HitStop Instance { get; private set; }

    [Tooltip("Time scale during a global hitstop. 0 = full freeze, 0.05-0.2 = heavy slow-mo.")]
    [Range(0f, 1f)] public float globalStopScale = 0f;

    float globalEnd = -1f;
    float savedTimeScale = 1f;
    bool globalActive;

    struct Frozen { public float originalSpeed; public float end; }
    readonly Dictionary<Animator, Frozen> frozen = new Dictionary<Animator, Frozen>();
    readonly List<Animator> toRelease = new List<Animator>();

    public bool IsGlobalActive => globalActive;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Global hitstop. Map hit weight (0..1) to duration, e.g. Mathf.Lerp(0.04f, 0.12f, weight).</summary>
    public void TriggerGlobal(float durationSeconds)
    {
        if (durationSeconds <= 0f) return;
        if (!globalActive)
        {
            // Never start a hitstop while the game is paused, and remember the current scale
            // (we may be in slow motion) instead of forcing 1 on restore.
            if (Time.timeScale <= 0f) return;
            savedTimeScale = Time.timeScale;
            Time.timeScale = globalStopScale;
            globalActive = true;
        }
        globalEnd = Mathf.Max(globalEnd, Time.unscaledTime + durationSeconds);
    }

    /// <summary>Selective hitstop: freezes only these animators.</summary>
    public void TriggerSelective(float durationSeconds, params Animator[] actors)
    {
        if (durationSeconds <= 0f || actors == null) return;
        float end = Time.unscaledTime + durationSeconds;
        foreach (var a in actors)
        {
            if (a == null) continue;
            if (frozen.TryGetValue(a, out var f))
            {
                f.end = Mathf.Max(f.end, end);
                frozen[a] = f;
            }
            else
            {
                frozen[a] = new Frozen { originalSpeed = a.speed, end = end };
                a.speed = 0f;
            }
        }
    }

    /// <summary>Call from your pause system before it changes Time.timeScale.</summary>
    public void CancelAll()
    {
        if (globalActive) { Time.timeScale = savedTimeScale; globalActive = false; }
        foreach (var kv in frozen) if (kv.Key != null) kv.Key.speed = kv.Value.originalSpeed;
        frozen.Clear();
    }

    void Update()
    {
        float now = Time.unscaledTime;

        if (globalActive && now >= globalEnd)
        {
            Time.timeScale = savedTimeScale;
            globalActive = false;
        }

        if (frozen.Count == 0) return;
        toRelease.Clear();
        foreach (var kv in frozen)
            if (kv.Key == null || now >= kv.Value.end) toRelease.Add(kv.Key);
        foreach (var a in toRelease)
        {
            if (a != null) a.speed = frozen[a].originalSpeed;
            frozen.Remove(a);
        }
    }

    void OnDisable() { CancelAll(); }
}
