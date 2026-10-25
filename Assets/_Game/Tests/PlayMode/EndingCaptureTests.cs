using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Visual check, not a gate: plays the Arena, lets Defne die and says EVET to every contract until the sixth
    /// death, then saves the longest ending (all six costs in the ledger) and a HAYIR-at-death-3 ending to
    /// Logs/EndingCaptures. Explicit; run it with -testFilter EndingCaptureTests.
    /// </summary>
    [Explicit("Writes screenshots; run on demand.")]
    public sealed class EndingCaptureTests
    {
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTearDown]
        public IEnumerator UnloadArena() => TestScenes.LeaveLoadedScenes();

        [UnityTest]
        public IEnumerator Endings_AreCaptured()
        {
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "EndingCaptures");
            Directory.CreateDirectory(folder);

            yield return Load();
            // Six contracts, then the seventh death ends the run (contract 6 takes "Kendisi").
            yield return DieAndAnswer(yesCount: 7, sayNoAtEnd: false);
            yield return Wait(5f);
            yield return Capture(folder, "01_always_yes");

            yield return Load();
            yield return DieAndAnswer(yesCount: 2, sayNoAtEnd: true);
            yield return Wait(4f);
            yield return Capture(folder, "02_no_at_death_3");
        }

        private static IEnumerator Load()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            yield return Wait(1f);
            var waves = Object.FindAnyObjectByType<WaveDirector>();
            if (waves != null) waves.enabled = false;
        }

        /// <summary>Kills Defne repeatedly; EVET for the first <paramref name="yesCount"/> cards, then HAYIR if asked.</summary>
        private static IEnumerator DieAndAnswer(int yesCount, bool sayNoAtEnd)
        {
            var devil = Object.FindAnyObjectByType<DevilDirector>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(devil, "DevilDirector");
            int deaths = sayNoAtEnd ? yesCount + 1 : yesCount;
            for (int i = 0; i < deaths; i++)
            {
                var health = player.GetComponent<Health>();
                health.TakeDamage(new DamageInfo(99999f, player.transform.position, Vector3.forward, null, DamageKind.Contact));
                yield return Wait(2.5f);
                for (int a = 0; a < 4; a++)
                {
                    devil.Advance();
                    yield return Wait(0.6f);
                }
                if (i < yesCount && i < 5) devil.ChooseYes();
                else if (sayNoAtEnd) devil.ChooseNo();
                yield return Wait(2.5f);
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
