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
    /// Visual check, not a gate: one enemy of each type from the arena pools comes at Defne (kept alive); saves frames
    /// of their run, tell and attack clips to Logs/EnemyAnimCaptures. Explicit; run it with -testFilter
    /// EnemyAnimCaptureTests.
    /// </summary>
    [Explicit("Writes screenshots; run on demand.")]
    public sealed class EnemyAnimCaptureTests
    {
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTearDown]
        public IEnumerator UnloadArena() => TestScenes.LeaveLoadedScenes();

        [UnityTest]
        public IEnumerator EnemyClips_AreCaptured()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "EnemyAnimCaptures");
            Directory.CreateDirectory(folder);
            var waves = Object.FindAnyObjectByType<WaveDirector>();
            if (waves != null) waves.enabled = false;
            yield return Wait(3.5f); // past the intro line
            Transform player = Object.FindAnyObjectByType<PlayerController>().transform;
            var health = player.GetComponent<Health>();

            var offsets = new[] { new Vector3(5f, 0f, 2f), new Vector3(-4f, 0f, 3f), new Vector3(1f, 0f, 6f) };
            int k = 0;
            foreach (EnemyPool pool in Object.FindObjectsByType<EnemyPool>(FindObjectsSortMode.None))
            {
                if (pool.Prefab == null || k >= offsets.Length) continue;
                Vector3 position = player.position + offsets[k++];
                if (NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas)) position = hit.position;
                pool.Get(position, Quaternion.identity, player);
            }

            for (int i = 0; i < 16; i++)
            {
                float end = Time.realtimeSinceStartup + 0.18f;
                while (Time.realtimeSinceStartup < end)
                {
                    health.Heal(1000f);
                    yield return null;
                }
                yield return Capture(folder, $"enemy_{i:00}");
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
