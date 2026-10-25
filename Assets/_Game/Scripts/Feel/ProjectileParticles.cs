using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Restarts the particle layers of a pooled projectile (enemy orb) on every flight. The pool activates an instance
    /// at the spot where it last died and only then <see cref="Projectile.Launch"/> moves it to the muzzle, so
    /// world-space layers (embers) are started in LateUpdate, after the launch, or they would spray a line of embers
    /// along the jump. Local layers (core, halo) play on enable. Everything is cleared when the instance goes back to
    /// the pool, so no old particles show on the next flight. Visual only: no effect on hits.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProjectileParticles : MonoBehaviour
    {
        private ParticleSystem[] _systems;
        private bool _restartWorld;

        private void Awake() => _systems = GetComponentsInChildren<ParticleSystem>(true);

        private void OnEnable() => _restartWorld = true;

        private void OnDisable()
        {
            _restartWorld = false;
            if (_systems == null) return;
            for (int i = 0; i < _systems.Length; i++)
                if (_systems[i] != null) _systems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void LateUpdate()
        {
            if (!_restartWorld || _systems == null) return;
            _restartWorld = false;
            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystem system = _systems[i];
                if (system == null) continue;
                if (system.main.simulationSpace == ParticleSystemSimulationSpace.World)
                {
                    system.Clear(false);
                    system.Play(false);
                }
                else if (!system.isPlaying)
                {
                    system.Play(false);
                }
            }
        }
    }
}
