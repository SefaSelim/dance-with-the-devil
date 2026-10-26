using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Visual check, not a gate: loads the Arena and saves its first 40 frames (Logs/StartCaptures), with Defne's root,
    /// model and camera positions per frame in start.txt, to look at what moves when the run begins. Explicit; run it
    /// with -testFilter StartCaptureTests.
    /// </summary>
    [Explicit("Writes screenshots; run on demand.")]
    public sealed class StartCaptureTests
    {
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTearDown]
        public IEnumerator UnloadArena() => TestScenes.LeaveLoadedScenes();

        [UnityTest]
        public IEnumerator FirstFrames_AreCaptured()
        {
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "StartCaptures");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
#if UNITY_EDITOR
            AsyncOperation load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone) yield return null;
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            var log = new StringBuilder();
            for (int i = 0; i < 40; i++)
            {
                yield return new WaitForEndOfFrame();
                var player = Object.FindAnyObjectByType<PlayerController>();
                var animator = player != null ? player.GetComponentInChildren<Animator>() : null;
                Transform hips = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
                Camera cam = Camera.main;
                log.AppendLine($"{i} t={Time.time:F3} root={(player != null ? player.transform.position.ToString("F3") : "-")} " +
                               $"rot={(player != null ? player.transform.eulerAngles.y.ToString("F1") : "-")} " +
                               $"hips={(hips != null ? hips.position.ToString("F3") : "-")} " +
                               $"cam={(cam != null ? cam.transform.position.ToString("F2") : "-")}");
                Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(folder, $"f_{i:00}.png"), tex.EncodeToPNG());
                Object.Destroy(tex);
            }
            File.WriteAllText(Path.Combine(folder, "start.txt"), log.ToString());
        }
    }
}
