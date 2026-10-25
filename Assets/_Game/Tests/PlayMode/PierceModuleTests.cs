using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Delici whisper module (user decision 2026-10-25): rifle bullets pass through 2 more enemies (adding to Contract
    /// 3's +2), and each enemy passed cuts the bullet's damage by 30 % (×0.7 per pierce). Crit forced to 0 so damage is
    /// exact. Objects are built in code; each test uses its own region.
    /// </summary>
    public sealed class PierceModuleTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.Destroy(_spawned[i]);
            _spawned.Clear();
        }

        private GameObject Spawn(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _spawned.Add(go);
            return go;
        }

        private Health SpawnTarget(Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Target";
            go.transform.position = position;
            _spawned.Add(go);
            Health health = go.AddComponent<Health>();
            health.InvulnerabilityAfterHit = 0f;
            return health;
        }

        /// <summary>Player with a CharacterController, Health, PlayerController, Rifle and PlayerModules.</summary>
        private PlayerModules SpawnPlayer(Vector3 position)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = position + new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            _spawned.Add(floor);
            GameObject go = Spawn("Player", position);
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

        [UnityTest]
        public IEnumerator Bullet_WithPierceModule_PassesThreeEnemies_DamageFallsThirtyPercentEach()
        {
            // Arrange: the module's values on a bare rifle; four targets in a row.
            var origin = new Vector3(0f, 1f, 4000f);
            Rifle rifle = Spawn("Shooter", origin).AddComponent<Rifle>();
            rifle.BaseCritChance = 0f;
            rifle.ModulePierceCount = 2;
            rifle.PierceDamageFalloff = 0.3f;
            Health a = SpawnTarget(origin + new Vector3(3f, 0f, 0f));
            Health b = SpawnTarget(origin + new Vector3(6f, 0f, 0f));
            Health c = SpawnTarget(origin + new Vector3(9f, 0f, 0f));
            Health d = SpawnTarget(origin + new Vector3(12f, 0f, 0f));

            // Act
            Assert.IsTrue(rifle.TryFire(origin, Vector3.right));
            yield return new WaitForSeconds(0.6f);

            // Assert
            Assert.AreEqual(a.MaxHealth - 10f, a.Current, 1e-3f, "first target: full damage");
            Assert.AreEqual(b.MaxHealth - 7f, b.Current, 1e-3f, "second: ×0.7");
            Assert.AreEqual(c.MaxHealth - 4.9f, c.Current, 1e-3f, "third: ×0.49");
            Assert.AreEqual(d.MaxHealth, d.Current, 1e-3f, "the bullet stops in the third enemy");
        }

        [UnityTest]
        public IEnumerator Bullet_NoModule_StopsOnFirstEnemy()
        {
            // Arrange
            var origin = new Vector3(0f, 1f, 4100f);
            Rifle rifle = Spawn("Shooter", origin).AddComponent<Rifle>();
            rifle.BaseCritChance = 0f;
            Health a = SpawnTarget(origin + new Vector3(3f, 0f, 0f));
            Health b = SpawnTarget(origin + new Vector3(6f, 0f, 0f));

            // Act
            Assert.IsTrue(rifle.TryFire(origin, Vector3.right));
            yield return new WaitForSeconds(0.4f);

            // Assert
            Assert.AreEqual(a.MaxHealth - 10f, a.Current, 1e-3f);
            Assert.AreEqual(b.MaxHealth, b.Current, 1e-3f);
        }

        [UnityTest]
        public IEnumerator PierceModule_StacksWithContract_EjectRestores()
        {
            // Arrange
            PlayerModules modules = SpawnPlayer(new Vector3(0f, 0f, 4200f));
            Rifle rifle = modules.Rifle;
            rifle.PierceCount += DealEffects.ExtraPierce; // contract 3
            yield return null;

            // Act
            Assert.IsTrue(modules.EquipImmediate(ModuleType.Pierce));
            int equippedPierce = rifle.TotalPierceCount;
            float equippedFalloff = rifle.PierceDamageFalloff;
            Assert.IsTrue(modules.TryEject(ModuleType.Pierce));

            // Assert
            Assert.AreEqual(4, equippedPierce, "contract +2 and module +2 add up");
            Assert.AreEqual(0.3f, equippedFalloff, 1e-5f);
            Assert.AreEqual(2, rifle.TotalPierceCount, "eject keeps only the contract's pierce");
            Assert.AreEqual(0f, rifle.PierceDamageFalloff, 1e-5f, "eject removes the damage falloff");
        }
    }
}
