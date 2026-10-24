using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Measurement, not a gate: plays the Arena with the doubled waves, keeps Defne alive and fires rockets into the
    /// crowd for 20 s, then logs average / 95th-percentile frame time, enemies alive and live particles. Explicit; run
    /// it with -testFilter PerfCaptureTests. Editor numbers are pessimistic compared to a player build.
    /// </summary>
    [Explicit("Measurement; run on demand.")]
    public sealed class PerfCaptureTests
    {
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTearDown]
        public IEnumerator UnloadArena() => TestScenes.LeaveLoadedScenes();

        [UnityTest]
        public IEnumerator CrowdedFight_FrameTimeIsLogged()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            var player = Object.FindAnyObjectByType<PlayerController>();
            var health = player.GetComponent<Health>();
            var rocket = player.GetComponent<RocketLauncher>();
            var waves = Object.FindAnyObjectByType<WaveDirector>();
            rocket.RefillTimeMultiplier = 0.1f;

            var samples = new System.Collections.Generic.List<float>(2400);
            int maxAlive = 0, maxParticles = 0;
            float end = Time.realtimeSinceStartup + 20f;
            float nextRocket = 0f;
            while (Time.realtimeSinceStartup < end)
            {
                health.Heal(1000f);
                if (Time.realtimeSinceStartup > nextRocket && waves.ActiveEnemies.Count > 0)
                {
                    Vector3 target = waves.ActiveEnemies[0].transform.position;
                    rocket.TryFireAt(target, (target - player.transform.position).normalized);
                    nextRocket = Time.realtimeSinceStartup + 0.6f;
                }
                yield return null;
                samples.Add(Time.unscaledDeltaTime * 1000f);
                maxAlive = Mathf.Max(maxAlive, waves.ActiveEnemies.Count);
                int particles = 0;
                foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                    particles += ps.particleCount;
                maxParticles = Mathf.Max(maxParticles, particles);
            }

            samples.Sort();
            float avg = 0f;
            foreach (float s in samples) avg += s;
            avg /= Mathf.Max(1, samples.Count);
            float p95 = samples[Mathf.Clamp(Mathf.RoundToInt(samples.Count * 0.95f), 0, samples.Count - 1)];
            Debug.Log($"PerfCapture: frames {samples.Count}, avg {avg:F1} ms, p95 {p95:F1} ms, max enemies {maxAlive}, " +
                      $"max live particles {maxParticles}");
            Assert.Greater(samples.Count, 0);
        }
    }
}
