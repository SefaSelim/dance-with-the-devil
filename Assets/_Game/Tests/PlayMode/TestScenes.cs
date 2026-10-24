using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Shared tear-down for tests that load a real scene (the Arena): moves to a fresh empty scene and unloads the
    /// rest, so later code-built tests do not run beside a live arena (its waves, Devil, combo and frame cost made the
    /// timing-sensitive dodge test fail when it ran after them).
    /// </summary>
    public static class TestScenes
    {
        private static int _serial;

        public static IEnumerator LeaveLoadedScenes()
        {
            Scene empty = SceneManager.CreateScene("TestEmpty_" + (++_serial));
            SceneManager.SetActiveScene(empty);
            var others = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s != empty && s.isLoaded) others.Add(s);
            }
            foreach (Scene s in others) yield return SceneManager.UnloadSceneAsync(s);
            RunPause.Reset();
            Time.timeScale = 1f;
            // Let the unload settle here, so its long frames land in this tear-down and not inside the next test's
            // timed wait. No Resources.UnloadUnusedAssets: in Play mode it crashed the Editor twice (URP GPU Resident
            // Drawer, ObjectDispatcher.DispatchTypeChangesAndClear, 2026-10-24).
            System.GC.Collect();
            float end = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
