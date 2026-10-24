using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Hız module and attack speed (production/design/combo-haste.md §3, §8): fire rate ×1.4, each bullet strays within
    /// ±6°, eject restores both at once, Contract 4 ×1.25 stacks by product (10.5 shots/s). Objects are built in code,
    /// no real input; each test uses its own region.
    /// </summary>
    public sealed class HasteModuleTests
    {
        private const float BaseFireRate = 6f;
        private const float HasteFactor = 1.4f;
        private const float SpreadDegrees = 6f;
        private const float BulletSpeedFloor = 30f;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.Destroy(_spawned[i]);
            _spawned.Clear();
        }

        /// <summary>Floor + player (CharacterController, Health, PlayerController, Rifle, PlayerModules), as in ModuleTests.</summary>
        private PlayerModules SpawnPlayer(Vector3 position)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = position + new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            _spawned.Add(floor);

            var go = new GameObject("Player");
            go.transform.position = position;
            _spawned.Add(go);
            new GameObject("Model").transform.SetParent(go.transform, false);
            var cc = go.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.height = 1.8f;
            cc.radius = 0.35f;
            go.AddComponent<Health>();
            go.AddComponent<PlayerController>();
            go.AddComponent<Rifle>();
            PlayerModules modules = go.AddComponent<PlayerModules>();
            Physics.SyncTransforms();
            return modules;
        }

        /// <summary>
        /// Active rifle bullets fired by <paramref name="source"/>. Thrown modules are projectiles with the same source,
        /// told apart by speed (rifle 40 m/s, thrown module 16 m/s).
        /// </summary>
        private static List<Projectile> ActiveBullets(GameObject source)
        {
            var list = new List<Projectile>();
            foreach (Projectile p in Object.FindObjectsByType<Projectile>(FindObjectsInactive.Exclude))
                if (p.IsActive && p.Source == source && p.Speed > BulletSpeedFloor) list.Add(p);
            return list;
        }

        private static float Yaw(Vector3 direction) => Vector3.SignedAngle(Vector3.forward, direction, Vector3.up);

        [UnityTest]
        public IEnumerator Haste_HeldFire_IntervalIsBaseDividedBy1Point4()
        {
            // Arrange
            var origin = new Vector3(0f, 0f, 2000f);
            PlayerModules modules = SpawnPlayer(origin);
            Rifle rifle = modules.Rifle;
            float expected = 1f / BaseFireRate / HasteFactor;

            // Act
            Assert.IsTrue(modules.EquipImmediate(ModuleType.Haste));
            var times = new List<float>();
            float deadline = Time.time + 3f;
            while (times.Count < 10 && Time.time < deadline)
            {
                if (rifle.TryFire(origin + Vector3.up, Vector3.forward)) times.Add(Time.time);
                yield return null;
            }

            // Assert
            Assert.AreEqual(BaseFireRate * HasteFactor, rifle.EffectiveFireRate, 1e-4f, "8.4 shots/s");
            Assert.AreEqual(expected, rifle.FireInterval, 1e-5f);
            Assert.AreEqual(10, times.Count, "ten shots within 3 s");
            float average = (times[times.Count - 1] - times[0]) / (times.Count - 1);
            Assert.AreEqual(expected, average, 0.01f, "held-fire cadence");
        }

        [UnityTest]
        public IEnumerator Haste_Bullets_StrayWithinSixDegrees()
        {
            // Arrange
            var origin = new Vector3(0f, 0f, 2100f);
            PlayerModules modules = SpawnPlayer(origin);
            Rifle rifle = modules.Rifle;
            modules.EquipImmediate(ModuleType.Haste);
            float maxYaw = 0f;
            int checkedBullets = 0;

            // Act: single bullets
            float deadline = Time.time + 3f;
            while (checkedBullets < 15 && Time.time < deadline)
            {
                if (rifle.TryFire(origin + Vector3.up, Vector3.forward))
                {
                    foreach (Projectile p in ActiveBullets(modules.gameObject))
                    {
                        float yaw = Mathf.Abs(Yaw(p.Direction));
                        Assert.LessOrEqual(yaw, SpreadDegrees + 1e-3f, "single bullet within ±6°");
                        maxYaw = Mathf.Max(maxYaw, yaw);
                    }
                    checkedBullets++;
                }
                yield return null;
            }

            // Assert
            Assert.AreEqual(15, checkedBullets);
            Assert.Greater(maxYaw, 0.5f, "bullets really stray (15 shots all within 0.5° is ~1e-16)");

            // Arrange: Triple Shot on top; each bullet rolls its own deviation around its ±12° line.
            yield return new WaitForSeconds(0.7f); // earlier bullets reach max range (25 m / 40 m/s)
            modules.EquipImmediate(ModuleType.TripleShot);
            float[] lines = { -12f, 0f, 12f };
            int salvos = 0;
            deadline = Time.time + 3f;
            while (salvos < 5 && Time.time < deadline)
            {
                if (rifle.TryFire(origin + Vector3.up, Vector3.forward))
                {
                    foreach (Projectile p in ActiveBullets(modules.gameObject))
                    {
                        float yaw = Yaw(p.Direction);
                        float nearest = float.PositiveInfinity;
                        for (int i = 0; i < lines.Length; i++) nearest = Mathf.Min(nearest, Mathf.Abs(yaw - lines[i]));
                        Assert.LessOrEqual(nearest, SpreadDegrees + 1e-3f, "triple bullet within ±6° of its line");
                    }
                    salvos++;
                }
                yield return null;
            }
            Assert.AreEqual(5, salvos);
        }

        [UnityTest]
        public IEnumerator Haste_Eject_RestoresFireRateAndSpreadAtOnce()
        {
            // Arrange
            var origin = new Vector3(0f, 0f, 2200f);
            PlayerModules modules = SpawnPlayer(origin);
            Rifle rifle = modules.Rifle;
            modules.EquipImmediate(ModuleType.Haste);
            Assert.AreEqual(SpreadDegrees, rifle.ModuleSpreadDegrees, 1e-5f);

            // Act
            Assert.IsTrue(modules.TryEject(ModuleType.Haste));

            // Assert: same frame
            Assert.IsFalse(modules.IsEquipped(ModuleType.Haste));
            Assert.AreEqual(BaseFireRate, rifle.EffectiveFireRate, 1e-4f, "back to 6 shots/s");
            Assert.AreEqual(1f / BaseFireRate, rifle.FireInterval, 1e-5f);
            Assert.AreEqual(0f, rifle.ModuleSpreadDegrees, "no spread");

            // A bullet fired after the eject flies straight.
            yield return null;
            Assert.IsTrue(rifle.TryFire(origin + Vector3.up, Vector3.forward));
            List<Projectile> bullets = ActiveBullets(modules.gameObject);
            Assert.AreEqual(1, bullets.Count);
            Assert.AreEqual(0f, Yaw(bullets[0].Direction), 1e-3f);
        }

        [UnityTest]
        public IEnumerator HasteAndContractFour_FireRate_StacksTo10Point5()
        {
            // Arrange
            var origin = new Vector3(0f, 0f, 2300f);
            PlayerModules modules = SpawnPlayer(origin);
            Rifle rifle = modules.Rifle;
            DealDefinition[] deals = DealLibrary.CreateDefaults();
            yield return null;

            // Act
            DealEffects.Apply(deals[3], new DealTargets { Rifle = rifle });
            modules.EquipImmediate(ModuleType.Haste);

            // Assert
            Assert.AreEqual(10.5f, rifle.EffectiveFireRate, 1e-3f, "6 × 1.4 × 1.25");
            Assert.IsTrue(modules.TryEject(ModuleType.Haste));
            Assert.AreEqual(7.5f, rifle.EffectiveFireRate, 1e-3f, "contract kept after the eject");

            for (int i = 0; i < deals.Length; i++) Object.Destroy(deals[i]);
        }
    }
}
