using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Visual check, not a gate: plays the real Arena scene and saves what the game camera draws (its low-res
    /// render texture) to Logs/ArenaCaptures: Defne standing, mid-run, firing the rifle, and right after a rocket.
    /// Explicit, so the normal test run skips it; run it with -testFilter ArenaCaptureTests.
    /// </summary>
    [Explicit("Writes screenshots; run on demand.")]
    public sealed class ArenaCaptureTests
    {
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTest]
        public IEnumerator Arena_PlayerShots_AreCaptured()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            yield return null;
            var rig = Object.FindAnyObjectByType<PixelCameraRig>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(rig, "camera rig");
            Assert.IsNotNull(player, "player");
            var waves = Object.FindAnyObjectByType<WaveDirector>();
            if (waves != null) waves.enabled = false; // a clean frame of the player

            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ArenaCaptures");
            Directory.CreateDirectory(folder);

            yield return Wait(0.6f);
            Save(rig, folder, "01_idle");

            // Mid-run: move her through the controller toward +x for half a second.
            var controller = player.GetComponent<CharacterController>();
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                controller.Move(Vector3.right * 6f * Time.deltaTime);
                yield return null;
            }
            Save(rig, folder, "02_run");

            Vector3 aim = player.transform.position + player.transform.forward * 6f + Vector3.up;
            var rifle = player.GetComponent<Rifle>();
            for (int i = 0; i < 4; i++)
            {
                rifle.TryFireAt(aim, player.transform.forward);
                yield return Wait(0.09f);
            }
            Save(rig, folder, "03_rifle");

            var rocket = player.GetComponent<RocketLauncher>();
            rocket.TryFireAt(aim + player.transform.forward * 4f, player.transform.forward);
            yield return Wait(0.12f);
            Save(rig, folder, "04_rocket_flight");
            yield return Wait(0.6f);
            Save(rig, folder, "05_after_rocket");
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        private static void Save(PixelCameraRig rig, string folder, string name)
        {
            RenderTexture rt = rig.RenderTexture;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
    }
}
