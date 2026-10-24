using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// The rifle flash sits on the drawn gun tip, also with the Yoğunluk module (body widened 1.5× in X/Z), and that
    /// module does not stretch the gun. Regression for SefaSelim's report 2026-10-24 ("alev silahın ucundan çıkmıyor,
    /// büyüme upgrade'inde bozuluyor").
    /// </summary>
    public sealed class MuzzleFlashTests
    {
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTearDown]
        public IEnumerator UnloadArena() => TestScenes.LeaveLoadedScenes();
        private const float MaxFlashOffset = 0.06f;

        [UnityTest]
        public IEnumerator RifleFlash_StaysOnMuzzle_WithAndWithoutDensity()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            // Arrange
            var waves = Object.FindAnyObjectByType<WaveDirector>();
            if (waves != null) waves.enabled = false;
            var player = Object.FindAnyObjectByType<PlayerController>();
            var rifle = player.GetComponent<Rifle>();
            var hold = player.GetComponentInChildren<WeaponHold>();
            Assert.IsNotNull(RealisticVfx.Instance, "RealisticVfx in the arena");
            Assert.IsNotNull(hold, "WeaponHold on Defne");
            yield return Frames(20);
            Transform muzzle = rifle.Muzzle;
            Assert.IsNotNull(muzzle, "rifle muzzle");
            float gunLength = Vector3.Distance(hold.Socket.position, muzzle.position);

            // Act + Assert: plain body.
            yield return FireAndCheck(player, rifle, muzzle, "normal body");

            // Act: Yoğunluk widens the body.
            player.BodyRadiusScale = 1.5f;
            yield return Frames(10);

            // Assert: the gun keeps its length and stays uniformly scaled, the flash stays on its tip.
            Vector3 s = hold.Socket.GetChild(0).lossyScale;
            Assert.That(Vector3.Distance(hold.Socket.position, muzzle.position), Is.EqualTo(gunLength).Within(gunLength * 0.03f),
                "gun length with Yoğunluk");
            Assert.That(Mathf.Max(s.x, s.y, s.z) / Mathf.Min(s.x, s.y, s.z), Is.LessThan(1.03f), "gun scale stays uniform");
            yield return FireAndCheck(player, rifle, muzzle, "Yoğunluk body");
            player.BodyRadiusScale = 1f;
        }

        private static IEnumerator FireAndCheck(PlayerController player, Rifle rifle, Transform muzzle, string label)
        {
            yield return new WaitForSeconds(0.3f); // fire-rate gap
            Vector3 aim = player.transform.position + player.transform.forward * 8f + Vector3.up;
            Assert.IsTrue(rifle.TryFireAt(aim, player.transform.forward), "fired (" + label + ")");
            // Check the frame it fired and the next two (the flash follows the gun while it lives).
            for (int f = 0; f < 3; f++)
            {
                yield return new WaitForEndOfFrame();
                float best = float.MaxValue;
                foreach (ParticleSystem ps in RealisticVfx.Instance.GetComponentsInChildren<ParticleSystem>())
                {
                    if (!ps.name.StartsWith("vfx_muzzle_rifle") || !ps.IsAlive(true)) continue; // alive incl. the smoke child: the core lives only ~3 frames
                    best = Mathf.Min(best, Vector3.Distance(ps.transform.position, muzzle.position));
                }
                Assert.That(best, Is.LessThan(MaxFlashOffset), $"flash distance from the muzzle, frame {f} ({label})");
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }
    }
}
