using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Visual check, not a gate: plays the MainMenu scene (menu, settings, credits) and then the Arena (HUD over the
    /// gothic dressing) and saves Game-view screenshots, UI included, to Logs/MenuCaptures. Explicit; run it with
    /// -testFilter MenuCaptureTests.
    /// </summary>
    [Explicit("Writes screenshots; run on demand.")]
    public sealed class MenuCaptureTests
    {
        private const string MenuPath = "Assets/_Game/Scenes/MainMenu.unity";
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTest]
        public IEnumerator MainMenuAndHud_AreCaptured()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                MenuPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "MenuCaptures");
            Directory.CreateDirectory(folder);
            yield return Wait(1.2f);
            Assert.IsNotNull(Object.FindAnyObjectByType<MainMenuScreen>(), "menu screen");
            yield return Capture(folder, "01_menu");

            Click("Settings");
            yield return Wait(0.3f);
            yield return Capture(folder, "02_settings");
            Click("Back");
            yield return Wait(0.2f);
            Click("Credits");
            yield return Wait(0.3f);
            yield return Capture(folder, "03_credits");

#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return Wait(1.5f);
            var waves = Object.FindAnyObjectByType<WaveDirector>();
            if (waves != null) waves.enabled = false;
            yield return Capture(folder, "04_arena_hud");
            var pause = Object.FindAnyObjectByType<PauseOverlay>();
            if (pause != null)
            {
                pause.SendMessage("Open");
                yield return Wait(0.2f);
                yield return Capture(folder, "05_pause");
                RunPause.Reset();
            }
        }

        private static void Click(string name)
        {
            foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude))
            {
                if (b.name != name || !b.gameObject.activeInHierarchy) continue;
                b.onClick.Invoke();
                return;
            }
            Assert.Fail("button " + name);
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
