// LightFlash.cs
// Short light pulse for muzzle flashes, explosions and impacts: spike on frame 0, ease out.
// The light is disabled when idle so it costs nothing. Shadows off is strongly recommended.
// URP Forward has a per-object light limit; with many simultaneous flashes use Forward+ or keep
// only the nearest few.

using UnityEngine;

[RequireComponent(typeof(Light))]
public class LightFlash : MonoBehaviour
{
    /// <summary>Accessibility multiplier for flashes (0..1).</summary>
    public static float GlobalScale = 1f;

    [Tooltip("Peak intensity (URP/Built-in units). Muzzle 2-6, cannon 10-30, explosion 20-60.")]
    public float peakIntensity = 4f;
    [Tooltip("Seconds. Muzzle 0.03-0.06, cannon 0.08-0.15, explosion 0.1-0.3.")]
    public float duration = 0.05f;
    [Tooltip("Normalised intensity over normalised time. Default: instant peak, fast ease-out.")]
    public AnimationCurve intensityCurve = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, -4f), new Keyframe(0.35f, 0.25f, -1.2f, -1.2f), new Keyframe(1f, 0f, -0.2f, 0f));
    [Range(0f, 0.5f)] public float randomIntensity = 0.2f;
    [Range(0f, 0.5f)] public float randomRange = 0.1f;
    public bool useUnscaledTime = false;

    Light lightComp;
    float baseRange;
    float t = 1f;
    float currentPeak;

    void Awake()
    {
        lightComp = GetComponent<Light>();
        baseRange = lightComp.range;
        lightComp.intensity = 0f;
        lightComp.enabled = false;
    }

    public void Flash(float intensityScale = 1f)
    {
        currentPeak = peakIntensity * intensityScale * GlobalScale * (1f + Random.Range(-randomIntensity, randomIntensity));
        lightComp.range = baseRange * (1f + Random.Range(-randomRange, randomRange));
        t = 0f;
        lightComp.enabled = true;
        lightComp.intensity = currentPeak * intensityCurve.Evaluate(0f); // visible on the same frame
    }

    void Update()
    {
        if (t >= 1f) return;
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        t += dt / Mathf.Max(0.0001f, duration);
        if (t >= 1f)
        {
            lightComp.intensity = 0f;
            lightComp.enabled = false;
            return;
        }
        lightComp.intensity = currentPeak * intensityCurve.Evaluate(t);
    }
}
