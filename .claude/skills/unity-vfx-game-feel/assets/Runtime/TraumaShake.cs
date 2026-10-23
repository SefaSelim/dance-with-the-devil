// TraumaShake.cs
// Camera shake using the trauma model (Squirrel Eiserloh, GDC 2016 "Juicing Your Cameras With Math")
// plus a separate directional kick spring.
//
//  - Events add trauma (0..1). Trauma decays linearly. Shake amount = trauma^exponent.
//  - Offsets come from Perlin noise (smooth), one seed per axis. Never Random per frame.
//  - Kick: a directional rotation impulse that recovers on a second-order spring. Use it to show
//    WHERE a force came from (gun pushes view back, hit from the left tilts right).
//
// Setup: put this on a dedicated child pivot of the camera rig, e.g.
//   CameraRoot (mouse look) -> RecoilPivot -> ShakePivot [TraumaShake] -> Camera
// so it never writes the transform your controller writes.
// With Cinemachine you can use Impulse Source/Listener instead, or put this on a child of the
// CinemachineCamera's tracking target only if that suits your rig.

using System.Collections.Generic;
using UnityEngine;

public class TraumaShake : MonoBehaviour
{
    /// <summary>Accessibility multiplier (0..1) — wire to the options menu.</summary>
    public static float GlobalScale = 1f;

    static readonly List<TraumaShake> Instances = new List<TraumaShake>();

    [Header("Shake limits at full trauma")]
    [Tooltip("Degrees. Rotation reads as a jolt without moving the camera through walls.")]
    public float maxYaw = 4f;
    public float maxPitch = 4f;
    public float maxRoll = 6f;
    [Tooltip("Metres (3D) or units (2D). Keep small in 3D.")]
    public Vector3 maxOffset = new Vector3(0.04f, 0.04f, 0f);

    [Header("Shake character")]
    [Tooltip("Noise frequency in Hz. 15-30 violent impacts, 5-10 heavy rumbles.")]
    public float frequency = 22f;
    [Tooltip("Trauma lost per second (linear). 1 = a full-trauma shake lasts ~1 s.")]
    public float decayPerSecond = 1.4f;
    [Tooltip("2 or 3. Makes small hits subtle and big hits dramatic.")]
    public float exponent = 2f;
    [Tooltip("Shake during hitstop sells the hit, so unscaled is the default.")]
    public bool useUnscaledTime = true;

    [Header("Directional kick spring")]
    public float kickFrequency = 6f;
    public float kickDamping = 0.5f;
    public float kickResponse = 2f;

    float trauma;
    float noiseTime;
    float seed;
    Vector3 basePos;
    Quaternion baseRot;
    SecondOrderDynamics3 kick;

    public float Trauma => trauma;

    void Awake()
    {
        seed = Random.value * 1000f;
        basePos = transform.localPosition;
        baseRot = transform.localRotation;
        kick = new SecondOrderDynamics3(kickFrequency, kickDamping, kickResponse, Vector3.zero);
    }

    void OnEnable() { Instances.Add(this); }
    void OnDisable() { Instances.Remove(this); }

    void OnValidate()
    {
        if (kick != null) kick.SetParameters(kickFrequency, kickDamping, kickResponse);
    }

    /// <summary>Add trauma (0..1). Typical: light damage 0.2, heavy hit 0.4, close explosion 0.8.</summary>
    public void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, amount));
    }

    /// <summary>Directional kick in degrees per second of angular velocity (x pitch, y yaw, z roll).
    /// Example gun shot: AddKick(new Vector3(-60f, Random.Range(-15f, 15f), 0f)).</summary>
    public void AddKick(Vector3 eulerVelocity)
    {
        kick.AddVelocity(eulerVelocity * GlobalScale);
    }

    /// <summary>Adds trauma to every active shaker with distance falloff (quadratic).</summary>
    public static void AddTraumaAt(Vector3 worldPos, float amount, float radius)
    {
        for (int i = 0; i < Instances.Count; i++)
        {
            var s = Instances[i];
            float d = Vector3.Distance(s.transform.position, worldPos);
            float k = Mathf.Clamp01(1f - d / Mathf.Max(0.001f, radius));
            if (k > 0f) s.AddTrauma(amount * k * k);
        }
    }

    float Noise(float offset)
    {
        // PerlinNoise is ~[0,1] and can slightly exceed it; remap and clamp to [-1,1].
        return Mathf.Clamp(Mathf.PerlinNoise(seed + offset, noiseTime) * 2f - 1f, -1f, 1f);
    }

    void LateUpdate()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        noiseTime += dt * frequency;
        trauma = Mathf.Max(0f, trauma - decayPerSecond * dt);

        float shake = Mathf.Pow(trauma, exponent) * GlobalScale;
        Vector3 kickEuler = kick.Update(dt, Vector3.zero, Vector3.zero);

        if (shake <= 0.0001f && kickEuler.sqrMagnitude < 0.000001f)
        {
            transform.localPosition = basePos;
            transform.localRotation = baseRot;
            return;
        }

        float pitch = maxPitch * shake * Noise(0f);
        float yaw   = maxYaw   * shake * Noise(100f);
        float roll  = maxRoll  * shake * Noise(200f);
        Vector3 offset = new Vector3(
            maxOffset.x * shake * Noise(300f),
            maxOffset.y * shake * Noise(400f),
            maxOffset.z * shake * Noise(500f));

        transform.localPosition = basePos + offset;
        transform.localRotation = baseRot * Quaternion.Euler(pitch + kickEuler.x, yaw + kickEuler.y, roll + kickEuler.z);
    }
}
