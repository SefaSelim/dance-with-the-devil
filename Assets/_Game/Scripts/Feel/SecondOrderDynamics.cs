// SecondOrderDynamics.cs
// Frame-rate-independent spring with designer-friendly parameters (after t3ssel8r,
// "Giving Personality to Procedural Animations using Math").
//   f  frequency in Hz      — speed of response / oscillation
//   z  damping (zeta)       — 0 oscillates forever, <1 overshoots, 1 critically damped, >1 sluggish
//   r  initial response     — 0 eases in, 1 immediate, >1 overshoots at start, <0 anticipates
// Use AddVelocity() to "kick" the spring (recoil, landing dip, hit reaction).
// Plain C# classes: hold them in a MonoBehaviour and call SetParameters() from OnValidate.

using UnityEngine;

public class SecondOrderDynamics
{
    float xp, y, yd;
    float k1, k2, k3;

    public float Value => y;
    public float Velocity => yd;

    public SecondOrderDynamics(float f, float z, float r, float x0 = 0f)
    {
        SetParameters(f, z, r);
        Reset(x0);
    }

    public void SetParameters(float f, float z, float r)
    {
        f = Mathf.Max(0.0001f, f);
        k1 = z / (Mathf.PI * f);
        k2 = 1f / ((2f * Mathf.PI * f) * (2f * Mathf.PI * f));
        k3 = r * z / (2f * Mathf.PI * f);
    }

    public void Reset(float x0) { xp = x0; y = x0; yd = 0f; }

    public void AddVelocity(float v) { yd += v; }

    public float Update(float dt, float x)
    {
        if (dt <= 0f) return y;
        float xd = (x - xp) / dt;
        xp = x;
        return Update(dt, x, xd);
    }

    public float Update(float dt, float x, float xd)
    {
        if (dt <= 0f) return y;
        // Stability clamp: keeps the integrator stable on long frames (hitches, hitstop exit).
        float k2Stable = Mathf.Max(k2, Mathf.Max(dt * dt / 2f + dt * k1 / 2f, dt * k1));
        y += dt * yd;
        yd += dt * (x + k3 * xd - y - k1 * yd) / k2Stable;
        return y;
    }
}

public class SecondOrderDynamics3
{
    Vector3 xp, y, yd;
    float k1, k2, k3;

    public Vector3 Value => y;
    public Vector3 Velocity => yd;

    public SecondOrderDynamics3(float f, float z, float r, Vector3 x0)
    {
        SetParameters(f, z, r);
        Reset(x0);
    }

    public void SetParameters(float f, float z, float r)
    {
        f = Mathf.Max(0.0001f, f);
        k1 = z / (Mathf.PI * f);
        k2 = 1f / ((2f * Mathf.PI * f) * (2f * Mathf.PI * f));
        k3 = r * z / (2f * Mathf.PI * f);
    }

    public void Reset(Vector3 x0) { xp = x0; y = x0; yd = Vector3.zero; }

    public void AddVelocity(Vector3 v) { yd += v; }

    public Vector3 Update(float dt, Vector3 x)
    {
        if (dt <= 0f) return y;
        Vector3 xd = (x - xp) / dt;
        xp = x;
        return Update(dt, x, xd);
    }

    public Vector3 Update(float dt, Vector3 x, Vector3 xd)
    {
        if (dt <= 0f) return y;
        float k2Stable = Mathf.Max(k2, Mathf.Max(dt * dt / 2f + dt * k1 / 2f, dt * k1));
        y += dt * yd;
        yd += dt * (x + k3 * xd - y - k1 * yd) / k2Stable;
        return y;
    }
}
