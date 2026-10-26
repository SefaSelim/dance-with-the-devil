using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Visual check, not a gate: plays the Arena with RunBot driving Defne and the waves on, and saves a game-view
    /// frame every 2 s for 30 s to Logs/MapWaveCaptures, to see enemies come in through the entrances and the
    /// camera over the map. Explicit; run it with -testFilter MapWaveCaptureTests.
    /// </summary>
    [Explicit("Writes screenshots; run on demand.")]
    public sealed class MapWaveCaptureTests
    {
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTearDown]
        public IEnumerator UnloadArena() => TestScenes.LeaveLoadedScenes();

        [UnityTest]
        public IEnumerator Waves_OnTheMap_AreCaptured()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "MapWaveCaptures");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            var bot = Object.FindAnyObjectByType<RunBot>(FindObjectsInactive.Include);
            Assert.IsNotNull(bot, "RunBot");
            bot.enabled = true;
            var health = Object.FindAnyObjectByType<PlayerController>().GetComponent<Health>();
            for (int i = 0; i < 15; i++)
            {
                float end = Time.realtimeSinceStartup + 2f;
                while (Time.realtimeSinceStartup < end)
                {
                    health.Heal(1000f);
                    yield return null;
                }
                yield return new WaitForEndOfFrame();
                Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(folder, $"wave_{i:00}.png"), tex.EncodeToPNG());
                Object.Destroy(tex);
            }
        }
    }
}
