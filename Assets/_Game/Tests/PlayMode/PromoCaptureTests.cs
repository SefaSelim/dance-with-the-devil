using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Not a gate: store-page screenshots (itch.io cover and gallery) to Logs/PromoCaptures at 2× the Game view:
    /// the main menu; 45 s of RunBot fighting the waves (Defne kept alive) with a shot every 2.5 s, each with and
    /// without the HUD; the Devil's line and contract card after a death; the ending after HAYIR.
    /// Explicit; run it with -testFilter PromoCaptureTests.
    /// </summary>
    [Explicit("Writes screenshots; run on demand.")]
    public sealed class PromoCaptureTests
    {
        private const string MenuPath = "Assets/_Game/Scenes/MainMenu.unity";
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";
        private const int SuperSize = 2;

        [UnityTearDown]
        public IEnumerator Leave() => TestScenes.LeaveLoadedScenes();

        [UnityTest, Timeout(600000)]
        public IEnumerator StoreShots_AreCaptured()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Editor only.");
            yield break;
#else
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "PromoCaptures");
            Directory.CreateDirectory(folder);

            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                MenuPath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return Wait(2.5f);
            yield return Shot(folder, "00_menu", true);

            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return Wait(1f);
            var bot = Object.FindAnyObjectByType<RunBot>(FindObjectsInactive.Include);
            var player = Object.FindAnyObjectByType<PlayerController>();
            var health = player.GetComponent<Health>();
            var devil = Object.FindAnyObjectByType<DevilDirector>();
            bot.enabled = true;

            for (int i = 0; i < 18; i++)
            {
                float end = Time.realtimeSinceStartup + 2.5f;
                while (Time.realtimeSinceStartup < end)
                {
                    health.Heal(1000f);
                    if (devil != null && devil.State != DevilState.Playing) devil.Advance();
                    if (devil != null && devil.State == DevilState.Offer) devil.ChooseYes();
                    yield return null;
                }
                yield return Shot(folder, $"10_fight_{i:00}_hud", true);
                yield return Shot(folder, $"20_fight_{i:00}_clean", false);
            }

            // The Devil: a death, his line, then the card.
            bot.enabled = false;
            yield return Wait(0.6f);
            health.TakeDamage(new DamageInfo(99999f, player.transform.position, Vector3.forward, null, DamageKind.Contact));
            yield return Wait(3.2f);
            yield return Shot(folder, "30_devil_line", true);
            for (int a = 0; a < 6 && devil.State != DevilState.Offer && devil.State != DevilState.Forced; a++)
            {
                devil.Advance();
                yield return Wait(1.6f);
            }
            yield return Wait(1.2f);
            yield return Shot(folder, "31_devil_card", true);

            // The ending: HAYIR on this card (or on the next offered one).
            for (int tries = 0; tries < 40 && devil.State != DevilState.Ended; tries++)
            {
                if (devil.State == DevilState.Offer) devil.ChooseNo();
                else if (devil.State == DevilState.Playing)
                    health.TakeDamage(new DamageInfo(99999f, player.transform.position, Vector3.forward, null, DamageKind.Contact));
                else devil.Advance();
                yield return Wait(1f);
            }
            yield return Wait(4f);
            yield return Shot(folder, "40_ending", true);
#endif
        }

        private static IEnumerator Shot(string folder, string name, bool withHud)
        {
            Canvas[] hidden = null;
            if (!withHud)
            {
                hidden = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
                foreach (Canvas c in hidden) c.enabled = !IsHud(c) && c.enabled;
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"), SuperSize);
            yield return null;
            yield return null;
            if (hidden != null)
                foreach (Canvas c in hidden) if (c != null) c.enabled = true;
        }

        // Every overlay canvas except the game image itself (PixelDisplayCanvas); the "KRİTİK!"/combo popups over
        // the enemies stay, they are part of the action.
        private static bool IsHud(Canvas c)
        {
            for (Transform t = c.transform; t != null; t = t.parent)
                if (t.name == "PixelDisplayCanvas" || t.name.Contains("Popup")) return false;
            return true;
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
