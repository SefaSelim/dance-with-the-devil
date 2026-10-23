// WeaponRecoil.cs
// Decomposed recoil for shooters. Each channel owns its own transform so nothing fights:
//   CameraRoot (mouse look) -> RecoilPivot [cameraRecoilPivot] -> ShakePivot [TraumaShake] -> Camera
//   Camera -> WeaponSway -> WeaponKick [weaponKickTarget] -> WeaponModel
//
// 1. Weapon visual kick: cosmetic, snappy spring with overshoot (position back, muzzle up, random roll).
// 2. Camera recoil (aim punch): follows a per-shot pattern, accumulates during sustained fire, and
//    recovers toward the pre-spray aim after firing stops. This is a balance parameter.
// 3. Screen shake / kick: optional, via TraumaShake.
// Call Fire() once per shot, on the same frame as the muzzle flash and sound.
//
// Note on recovery: if the player pulls the mouse down to compensate, full recovery will
// over-correct. Many shooters only recover the part the player didn't compensate; track the
// mouse-look pitch delta during the spray if you need that.

using UnityEngine;

public class WeaponRecoil : MonoBehaviour
{
    [Header("Targets")]
    public Transform weaponKickTarget;
    public Transform cameraRecoilPivot;
    public TraumaShake optionalShake;

    [Header("Weapon visual kick (per shot)")]
    [Tooltip("Local metres. Z negative = back toward the camera.")]
    public Vector3 kickPosition = new Vector3(0f, 0.004f, -0.045f);
    [Tooltip("Degrees muzzle-up per shot.")]
    public float kickPitch = 4f;
    [Tooltip("Random +/- degrees of yaw and roll per shot.")]
    public Vector2 kickRandomYawRoll = new Vector2(1.2f, 2.5f);
    public float kickFrequency = 9f;
    [Range(0f, 2f)] public float kickDamping = 0.42f;
    public float kickResponse = 2.5f;

    [Header("Camera recoil (aim)")]
    [Tooltip("Per-shot (yaw, pitch) in degrees. Index advances each shot and clamps at the end.")]
    public Vector2[] pattern =
    {
        new Vector2(0.0f, 0.9f), new Vector2(0.1f, 0.9f), new Vector2(-0.15f, 0.8f),
        new Vector2(0.25f, 0.7f), new Vector2(0.3f, 0.6f), new Vector2(-0.35f, 0.5f),
        new Vector2(-0.4f, 0.45f), new Vector2(0.35f, 0.4f),
    };
    [Range(0f, 0.5f)] public float patternRandomness = 0.15f;
    [Tooltip("How fast the camera reaches the recoil target (snap).")]
    public float climbFrequency = 12f;
    [Range(0f, 2f)] public float climbDamping = 0.9f;
    [Tooltip("Seconds without firing before recovery starts.")]
    public float recoveryDelay = 0.08f;
    [Tooltip("Half-life of recovery in seconds (smaller = faster).")]
    public float recoveryHalfLife = 0.12f;
    [Range(0f, 1f)] public float recoveryFraction = 1f;
    [Tooltip("Seconds without firing before the pattern restarts.")]
    public float patternResetTime = 0.35f;

    [Header("Shake")]
    public float traumaPerShot = 0f;
    [Tooltip("Directional camera kick velocity (deg/s) applied to TraumaShake, x = pitch (negative = up).")]
    public Vector3 shakeKick = new Vector3(-40f, 0f, 0f);

    SecondOrderDynamics3 kickPos, kickRot, camSpring;
    Vector3 basePos;
    Quaternion baseRot, camBaseRot;
    Vector2 recoilTarget;      // accumulated (yaw, pitch)
    Vector2 recoveryFloor;     // what remains after recovery (captured when recovery starts)
    bool recovering;
    float lastShotTime = -999f;
    int shotIndex;

    void Awake()
    {
        if (weaponKickTarget) { basePos = weaponKickTarget.localPosition; baseRot = weaponKickTarget.localRotation; }
        if (cameraRecoilPivot) camBaseRot = cameraRecoilPivot.localRotation;
        kickPos = new SecondOrderDynamics3(kickFrequency, kickDamping, kickResponse, Vector3.zero);
        kickRot = new SecondOrderDynamics3(kickFrequency, kickDamping, kickResponse, Vector3.zero);
        camSpring = new SecondOrderDynamics3(climbFrequency, climbDamping, 1f, Vector3.zero);
    }

    void OnValidate()
    {
        kickPos?.SetParameters(kickFrequency, kickDamping, kickResponse);
        kickRot?.SetParameters(kickFrequency, kickDamping, kickResponse);
        camSpring?.SetParameters(climbFrequency, climbDamping, 1f);
    }

    public void Fire(float intensity = 1f)
    {
        if (Time.time - lastShotTime > patternResetTime) shotIndex = 0;
        lastShotTime = Time.time;
        recovering = false;

        // 1. Visual kick: impulse on the springs (velocity), so overshoot and settle come from f/zeta.
        float w = 2f * Mathf.PI * kickFrequency; // velocity that produces roughly the target displacement
        kickPos.AddVelocity(kickPosition * w * intensity);
        kickRot.AddVelocity(new Vector3(
            -kickPitch,
            Random.Range(-kickRandomYawRoll.x, kickRandomYawRoll.x),
            Random.Range(-kickRandomYawRoll.y, kickRandomYawRoll.y)) * w * intensity);

        // 2. Camera recoil target accumulates along the pattern.
        if (pattern != null && pattern.Length > 0)
        {
            Vector2 step = pattern[Mathf.Min(shotIndex, pattern.Length - 1)];
            step *= 1f + Random.Range(-patternRandomness, patternRandomness);
            recoilTarget += step * intensity;
        }
        shotIndex++;

        // 3. Optional cosmetic shake/kick.
        if (optionalShake)
        {
            if (traumaPerShot > 0f) optionalShake.AddTrauma(traumaPerShot * intensity);
            if (shakeKick != Vector3.zero) optionalShake.AddKick(shakeKick * intensity);
        }
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        if (weaponKickTarget)
        {
            Vector3 p = kickPos.Update(dt, Vector3.zero, Vector3.zero);
            Vector3 r = kickRot.Update(dt, Vector3.zero, Vector3.zero);
            weaponKickTarget.localPosition = basePos + p;
            weaponKickTarget.localRotation = baseRot * Quaternion.Euler(r);
        }

        if (cameraRecoilPivot)
        {
            if (Time.time - lastShotTime > recoveryDelay)
            {
                // Exponential, frame-rate-independent recovery toward (1 - recoveryFraction) of the target.
                if (!recovering) { recovering = true; recoveryFloor = recoilTarget * (1f - recoveryFraction); }
                float k = 1f - Mathf.Exp(-0.6931472f / Mathf.Max(0.0001f, recoveryHalfLife) * dt);
                recoilTarget = Vector2.Lerp(recoilTarget, recoveryFloor, k);
                if (recoilTarget.sqrMagnitude < 0.00001f) recoilTarget = Vector2.zero;
            }

            Vector3 cam = camSpring.Update(dt, new Vector3(recoilTarget.x, recoilTarget.y, 0f));
            // Pitch up is negative X rotation in Unity.
            cameraRecoilPivot.localRotation = camBaseRot * Quaternion.Euler(-cam.y, cam.x, 0f);
        }
    }
}
