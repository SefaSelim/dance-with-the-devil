using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Whisper module rules from MAIN_GDD §7: intentional accept, capacity, reject cone and shared cooldown, eject
    /// removing effects, Echo bounce. Objects are built in code, no real input; each test uses its own region.
    /// </summary>
    public sealed class ModuleTests
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

        private GameObject SpawnBox(string name, Vector3 position, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            _spawned.Add(go);
            return go;
        }

        /// <summary>Floor + player (CharacterController, Health, PlayerController, Rifle, PlayerModules), facing +z.</summary>
        private PlayerModules SpawnPlayer(Vector3 position)
        {
            SpawnBox("Floor", position + new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f));
            GameObject go = Spawn("Player", position);
            new GameObject("Model").transform.SetParent(go.transform, false); // the visual root Density widens
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

        private static void Teleport(Component player, Vector3 position)
        {
            player.transform.position = position;
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator Accept_NeedsContinuousContact_LeavingResets()
        {
            var origin = new Vector3(0f, 0f, 1000f);
            PlayerModules modules = SpawnPlayer(origin);
            int accepted = 0;
            modules.Accepted += _ => accepted++;

            Assert.IsTrue(modules.PresentOffer(ModuleType.Echo, origin + new Vector3(0.2f, 0f, 0f)));
            Vector3 offerPosition = modules.Current.Position;

            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(modules.IsEquipped(ModuleType.Echo), "0.3 s of contact is not enough");
            Assert.Greater(modules.Current.AcceptProgress, 0f);

            Teleport(modules, origin + new Vector3(0f, 0f, -3f)); // step away
            yield return null;
            yield return null;
            Assert.AreEqual(0f, modules.Current.AcceptProgress, 1e-5f, "leaving resets the ring");

            Teleport(modules, new Vector3(offerPosition.x, origin.y, offerPosition.z));
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(modules.IsEquipped(ModuleType.Echo), "contact time is not summed across visits");

            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(modules.IsEquipped(ModuleType.Echo));
            Assert.AreEqual(1, accepted);
            Assert.IsNull(modules.Current, "the accepted offer is gone");
        }

        [UnityTest]
        public IEnumerator Capacity_DuplicateShowsEquipped_AndIsNotAdded()
        {
            var origin = new Vector3(0f, 0f, 1100f);
            PlayerModules modules = SpawnPlayer(origin);
            Assert.IsTrue(modules.EquipImmediate(ModuleType.Density));
            Assert.IsFalse(modules.EquipImmediate(ModuleType.Density), "one per type");
            int accepted = 0;
            modules.Accepted += _ => accepted++;

            Assert.IsTrue(modules.PresentOffer(ModuleType.Density, origin));
            Assert.IsFalse(modules.PresentOffer(ModuleType.Echo, origin), "one offer at a time");

            yield return new WaitForSeconds(0.6f);
            Assert.IsNotNull(modules.Current, "duplicate offer stays");
            Assert.IsTrue(modules.Current.IsEquippedDuplicate, "shows TAKILI");
            Assert.AreEqual(0f, modules.Current.AcceptProgress, 1e-5f);
            Assert.AreEqual(0, accepted);
            Assert.AreEqual(1, modules.EquippedCount);
        }

        [UnityTest]
        public IEnumerator Reject_ThrowsOfferInCone_AndStartsSharedCooldown()
        {
            var origin = new Vector3(0f, 0f, 1200f);
            PlayerModules modules = SpawnPlayer(origin);
            ModuleType? rejected = null;
            modules.Rejected += t => rejected = t;

            // In front (aim is +z), 2.5 m away: inside the 60° / 3 m cone.
            Assert.IsTrue(modules.PresentOffer(ModuleType.TripleShot, origin + new Vector3(0f, 0f, 2.5f)));
            Assert.IsTrue(modules.TryReject());
            Assert.AreEqual(ModuleType.TripleShot, rejected);
            Assert.AreEqual(1, modules.SmallNoCount);
            Assert.IsNull(modules.Current);
            Assert.IsFalse(modules.RejectReady, "cooldown started");

            // Behind the player: a whiff, but it also starts the cooldown.
            Assert.IsTrue(modules.PresentOffer(ModuleType.Echo, origin + new Vector3(0f, 0f, -2.5f)));
            Assert.IsFalse(modules.TryReject(), "on cooldown");
            yield return new WaitForSeconds(0.7f);
            Assert.IsTrue(modules.RejectReady);
            Assert.IsFalse(modules.TryReject(), "offer behind is outside the cone");
            Assert.IsFalse(modules.RejectReady, "a whiff starts the cooldown");
            Assert.IsNotNull(modules.Current);
            Assert.AreEqual(1, modules.SmallNoCount);

            // Eject shares the same cooldown.
            modules.EquipImmediate(ModuleType.Density);
            Assert.IsFalse(modules.TryEject(ModuleType.Density), "shared cooldown");
        }

        [UnityTest]
        public IEnumerator Eject_RemovesEffects()
        {
            var origin = new Vector3(0f, 0f, 1300f);
            PlayerModules modules = SpawnPlayer(origin);
            Rifle rifle = modules.Rifle;
            CharacterController cc = modules.Player.CharacterController;
            float baseDamage = rifle.Damage;
            rifle.DamageMultiplier = 1.1f; // a contract multiplier stacks by product

            modules.EquipImmediate(ModuleType.Density);
            modules.EquipImmediate(ModuleType.TripleShot);
            modules.EquipImmediate(ModuleType.Echo);
            Assert.AreEqual(baseDamage * 1.1f * 1.6f, rifle.Damage, 1e-4f);
            Assert.AreEqual(0.35f * 1.5f, cc.radius, 1e-4f);
            Assert.IsTrue(rifle.TripleShot);
            Assert.IsTrue(rifle.EchoEnabled);
            Assert.Greater(rifle.RecoilSpeed, 0f);

            ModuleType? ejected = null;
            modules.Ejected += t => ejected = t;
            Assert.IsTrue(modules.TryEject(ModuleType.Density));
            Assert.AreEqual(ModuleType.Density, ejected);
            Assert.IsFalse(modules.IsEquipped(ModuleType.Density));
            Assert.AreEqual(baseDamage * 1.1f, rifle.Damage, 1e-4f, "damage back to base (contract kept)");
            Assert.AreEqual(0.35f, cc.radius, 1e-4f, "body back to base");
            Assert.AreEqual(1, modules.SmallNoCount);

            yield return new WaitForSeconds(0.7f);
            modules.Player.AddExternalVelocity(Vector3.right * 2f);
            Assert.IsTrue(modules.TryEject(ModuleType.TripleShot));
            Assert.IsFalse(rifle.TripleShot);
            Assert.AreEqual(0f, rifle.RecoilSpeed);
            Assert.AreEqual(Vector3.zero, modules.Player.ExternalVelocity, "remaining recoil stops");

            yield return new WaitForSeconds(0.7f);
            Assert.IsTrue(modules.TryEject(ModuleType.Echo));
            Assert.IsFalse(rifle.EchoEnabled);
        }

        [UnityTest]
        public IEnumerator Echo_BouncesOnce_ThenDespawnsOnSecondWall()
        {
            var origin = new Vector3(0f, 1f, 1400f);
            GameObject shooter = Spawn("Shooter", origin); // no collider, no PlayerController
            Rifle rifle = shooter.AddComponent<Rifle>();
            rifle.EchoEnabled = true;
            rifle.PierceCount = 1; // pass the target so the bullet can reach the second wall
            ProjectilePool pool = shooter.GetComponent<ProjectilePool>();

            SpawnBox("WallA", origin + new Vector3(3f, 0f, 0f), new Vector3(0.5f, 3f, 6f));
            SpawnBox("WallB", origin + new Vector3(-3f, 0f, 0f), new Vector3(0.5f, 3f, 6f));
            GameObject targetGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            targetGo.transform.position = origin + new Vector3(-1.5f, 0f, 0f); // behind the shooter
            _spawned.Add(targetGo);
            Health target = targetGo.AddComponent<Health>();
            target.InvulnerabilityAfterHit = 0f;
            Physics.SyncTransforms();

            Assert.IsTrue(rifle.TryFire(origin, Vector3.right));
            Assert.AreEqual(1, pool.CountActive);

            yield return new WaitForSeconds(0.4f); // 9 m path = 0.225 s; full range (25 m) would be 0.625 s

            Assert.AreEqual(target.MaxHealth - 10f, target.Current, 1e-4f, "hit once, only after the bounce");
            Assert.AreEqual(0, pool.CountActive, "second wall ends it");
        }
    }
}
