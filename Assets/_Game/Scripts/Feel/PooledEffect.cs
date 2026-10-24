// PooledEffect.cs
// Returns a pooled one-shot ParticleSystem to its pool when it finishes.
// The ROOT system must use main.stopAction = ParticleSystemStopAction.Callback.

using UnityEngine;
using UnityEngine.Pool;

public class PooledEffect : MonoBehaviour
{
    public IObjectPool<ParticleSystem> pool;
    ParticleSystem ps;

    void Awake() { ps = GetComponent<ParticleSystem>(); }

    // Called by Unity when the root system finishes, because stopAction = Callback.
    void OnParticleSystemStopped()
    {
        if (pool != null && ps != null) pool.Release(ps);
        else gameObject.SetActive(false);
    }
}
