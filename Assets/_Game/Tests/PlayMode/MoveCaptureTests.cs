using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Visual check, not a gate: plays the Arena with the waves off and pushes Defne along a diagonal, then sideways
    /// and backwards (the root keeps its aim), saving frames to Logs/MoveCaptures to judge the locomotion blend.
    /// Explicit; run it with -testFilter MoveCaptureTests.
    /// </summary>
    [Explicit("Writes screenshots; run on demand.")]
    public sealed class MoveCaptureTests
    {
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTearDown]
        public IEnumerator UnloadArena() => TestScenes.LeaveLoadedScenes();

        [UnityTest]
        public IEnumerator Locomotion_IsCaptured()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "MoveCaptures");
            Directory.CreateDirectory(folder);
            var waves = Object.FindAnyObjectByType<WaveDirector>();
            if (waves != null) waves.enabled = false;
            yield return Wait(3.5f);
            var player = Object.FindAnyObjectByType<PlayerController>();
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null) input.enabled = false;
            var body = player.GetComponent<CharacterController>();
            Transform root = player.transform;

            Vector3[] directions =
            {
                new Vector3(1f, 0f, 1f).normalized, // diagonal
                Vector3.right,                      // sideways
                Vector3.back,                       // backwards
            };
            for (int d = 0; d < directions.Length; d++)
            {
                float end = Time.time + 1.4f;
                int frame = 0;
                while (Time.time < end)
                {
                    root.rotation = Quaternion.identity; // aim stays forward
                    Vector3 step = directions[d] * (6f * Time.deltaTime);
                    if (body != null && body.enabled) body.Move(step);
                    else root.position += step;
                    frame++;
                    if (Time.time > end - 0.5f && frame % 6 == 0) yield return Capture(folder, $"move_{d}_{frame:000}");
                    else yield return null;
                }
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
