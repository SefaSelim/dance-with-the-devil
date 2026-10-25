using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Visual check, not a gate: two Nişancı from the arena pool shoot at Defne; saves frames of the orb projectiles
    /// (core, halo, trail, embers) to Logs/OrbCaptures. Explicit; run it with -testFilter OrbCaptureTests.
    /// </summary>
    [Explicit("Writes screenshots; run on demand.")]
    public sealed class OrbCaptureTests
    {
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTearDown]
        public IEnumerator UnloadArena() => TestScenes.LeaveLoadedScenes();

        [UnityTest]
        public IEnumerator ShooterOrbs_AreCaptured()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "OrbCaptures");
            Directory.CreateDirectory(folder);
            var waves = Object.FindAnyObjectByType<WaveDirector>();
            if (waves != null) waves.enabled = false;
            yield return Wait(3.5f); // past the intro line
            Transform player = Object.FindAnyObjectByType<PlayerController>().transform;
            var health = player.GetComponent<Health>();

            foreach (EnemyPool pool in Object.FindObjectsByType<EnemyPool>(FindObjectsSortMode.None))
            {
                if (pool.Prefab == null || pool.Prefab.Type != EnemyType.Shooter) continue;
                foreach (Vector3 offset in new[] { new Vector3(8f, 0f, 3f), new Vector3(-6f, 0f, 7f) })
                {
                    Vector3 position = player.position + offset;
                    if (NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas)) position = hit.position;
                    pool.Get(position, Quaternion.identity, player);
                }
            }

            for (int i = 0; i < 6; i++)
            {
                float end = Time.realtimeSinceStartup + 0.55f;
                while (Time.realtimeSinceStartup < end)
                {
                    health.Heal(1000f);
                    yield return null;
                }
                yield return Capture(folder, $"orb_{i}");
            }
        }

        private static IEnumerator Capture(string folder, string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
