using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Visual check, not a gate: plays the Arena, drives the combo counter with Defne-credited hits and saves Game-view
    /// screenshots (HUD + post-processing pulse) at tier 1 and tier 2 to Logs/ComboCaptures. Explicit; run it with
    /// -testFilter ComboCaptureTests.
    /// </summary>
    [Explicit("Writes screenshots; run on demand.")]
    public sealed class ComboCaptureTests
    {
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTearDown]
        public IEnumerator UnloadArena() => TestScenes.LeaveLoadedScenes();

        [UnityTest]
        public IEnumerator Combo_TierUps_AreCaptured()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ComboCaptures");
            Directory.CreateDirectory(folder);
            yield return Wait(4f); // past the intro line
            var tracker = Object.FindAnyObjectByType<ComboTracker>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(tracker, "ComboTracker in the arena");
            var hit = new DamageInfo(10f, player.transform.position, Vector3.forward, player.gameObject, DamageKind.Bullet);

            yield return Feed(tracker, hit, 12);
            yield return Capture(folder, "01_combo_tier1");
            yield return Wait(0.35f);
            yield return Capture(folder, "02_combo_tier1_settled");
            yield return Feed(tracker, hit, 15);
            yield return Capture(folder, "03_combo_tier2");
        }

        private static IEnumerator Feed(ComboTracker tracker, DamageInfo hit, int count)
        {
            for (int i = 0; i < count; i++)
            {
                tracker.Counter.RegisterHit(hit);
                yield return null;
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
