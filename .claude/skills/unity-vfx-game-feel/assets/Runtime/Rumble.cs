// Rumble.cs
// Gamepad haptic pulses via the Input System (no-op without it).
// Low motor = heavy/body (cannon, explosion), high motor = sharp/buzz (gunshot, sword clash).
// Starting values: pistol (0.05, 0.25, 0.06 s), heavy melee (0.4, 0.6, 0.12 s), cannon (0.8, 0.4, 0.25 s).

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class Rumble : MonoBehaviour
{
    /// <summary>Accessibility / options multiplier (0..1).</summary>
    public static float GlobalScale = 1f;

    static Rumble instance;
    float endTime = -1f;

    public static void Pulse(float lowFrequency, float highFrequency, float durationSeconds)
    {
#if ENABLE_INPUT_SYSTEM
        var pad = Gamepad.current;
        if (pad == null || GlobalScale <= 0f) return;
        if (instance == null)
        {
            var go = new GameObject("[Rumble]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Rumble>();
        }
        pad.SetMotorSpeeds(Mathf.Clamp01(lowFrequency * GlobalScale), Mathf.Clamp01(highFrequency * GlobalScale));
        instance.endTime = Mathf.Max(instance.endTime, Time.unscaledTime + durationSeconds);
#endif
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (endTime > 0f && Time.unscaledTime >= endTime)
        {
            Gamepad.current?.ResetHaptics();
            endTime = -1f;
        }
#endif
    }

    void OnApplicationFocus(bool focus)
    {
#if ENABLE_INPUT_SYSTEM
        if (!focus) { Gamepad.current?.ResetHaptics(); endTime = -1f; }
#endif
    }

    void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        Gamepad.current?.ResetHaptics();
#endif
    }
}
