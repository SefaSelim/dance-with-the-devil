using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Whisper module rules from MAIN_GDD §7: intentional accept, capacity (3 slots, K-23), reject cone and shared cooldown, eject
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
            rifle.BaseCritChance = 0f; // exact damage: no 5 % crit
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

        [UnityTest]
        public IEnumerator Echo_AfterBounce_TravelsHalfOfRemainingRange()
        {
            // Arrange: one wall 3 m to the right, open space behind the shooter.
            var origin = new Vector3(0f, 1f, 1500f);
            GameObject shooter = Spawn("Shooter", origin);
            Rifle rifle = shooter.AddComponent<Rifle>();
            rifle.EchoEnabled = true;
            SpawnBox("Wall", origin + new Vector3(3f, 0f, 0f), new Vector3(0.5f, 3f, 6f));
            Physics.SyncTransforms();
            const float range = 25f;
            float travelledAtBounce = -1f;
            Projectile bullet = null;
            System.Action<Vector3> onBounce = _ =>
            {
                if (bullet != null && bullet.HasBounced && travelledAtBounce < 0f) travelledAtBounce = bullet.Travelled;
            };
            Projectile.Bounced += onBounce;

            // Act
            Assert.IsTrue(rifle.TryFire(origin, Vector3.right));
            foreach (Projectile p in Object.FindObjectsByType<Projectile>(FindObjectsInactive.Exclude))
                if (p.IsActive && p.Source == shooter) bullet = p;
            Assert.IsNotNull(bullet);
            float deadline = Time.time + 2f;
            while (bullet.IsActive && Time.time < deadline) yield return null;
            Projectile.Bounced -= onBounce;

            // Assert
            Assert.Greater(travelledAtBounce, 0f, "bounced");
            float expectedAfter = (range - travelledAtBounce) * 0.5f;
            float after = bullet.Travelled - travelledAtBounce;
            Assert.IsFalse(bullet.IsActive, "ended at its shortened range");
            Assert.LessOrEqual(after, expectedAfter + 0.01f, "at most half of the remaining range after the bounce");
            Assert.AreEqual(expectedAfter, after, 0.05f, "and that much in open space");
        }

        [UnityTest]
        public IEnumerator Offer_SlotsFull_OfferAppearsButBlocksUntilEject()
        {
            // Arrange: all three slots taken (K-23).
            var origin = new Vector3(0f, 0f, 1600f);
            PlayerModules modules = SpawnPlayer(origin);
            Assert.IsTrue(modules.EquipImmediate(ModuleType.TripleShot));
            Assert.IsTrue(modules.EquipImmediate(ModuleType.Echo));
            Assert.IsTrue(modules.EquipImmediate(ModuleType.Density));
            int echoSlot = modules.SlotOf(ModuleType.Echo);
            yield return null;

            // Act: a fourth type is offered right on the player and touched for longer than the accept time.
            bool canPresent = modules.CanPresentOffer;
            bool presented = modules.PresentOffer(ModuleType.Haste, origin + new Vector3(0.2f, 0f, 0f));
            yield return new WaitForSeconds(0.6f);

            // Assert: it appeared but is blocked, nothing was accepted.
            Assert.AreEqual(3, ModuleCatalog.SlotCount);
            Assert.IsTrue(modules.SlotsFull);
            Assert.IsTrue(canPresent, "full slots do not stop offers");
            Assert.IsTrue(presented);
            Assert.IsNotNull(modules.Current, "the blocked offer stays");
            Assert.IsTrue(modules.Current.SlotsFull, "shows DOLU");
            Assert.IsTrue(modules.Current.IsBlocked);
            Assert.IsFalse(modules.Current.IsEquippedDuplicate);
            Assert.AreEqual(0f, modules.Current.AcceptProgress, 1e-5f, "no progress while full");
            Assert.IsFalse(modules.IsEquipped(ModuleType.Haste));
            Assert.IsFalse(modules.EquipImmediate(ModuleType.Pierce), "no fourth module");
            Assert.AreEqual(3, modules.EquippedCount);

            // Act: free a slot (the shared cooldown is waited out first) and keep standing on the offer.
            yield return new WaitForSeconds(0.1f);
            Assert.IsTrue(modules.TryEject(ModuleType.Echo));
            yield return new WaitForSeconds(0.6f);

            // Assert: accepted into the freed slot.
            Assert.IsTrue(modules.IsEquipped(ModuleType.Haste));
            Assert.AreEqual(echoSlot, modules.SlotOf(ModuleType.Haste), "takes the slot the ejected module left");
            Assert.IsNull(modules.Current, "the accepted offer is gone");
            Assert.IsTrue(modules.SlotsFull);
        }

        [UnityTest]
        public IEnumerator Spawner_EquippedTypes_AreNeverDrawn()
        {
            // Arrange: three types equipped (all slots full).
            var origin = new Vector3(0f, 0f, 1700f);
            PlayerModules modules = SpawnPlayer(origin);
            modules.EquipImmediate(ModuleType.TripleShot);
            modules.EquipImmediate(ModuleType.Density);
            modules.EquipImmediate(ModuleType.Pierce);
            ModuleSpawner spawner = Spawn("Spawner", origin).AddComponent<ModuleSpawner>();
            spawner.Configure(modules, null, null);
            yield return null;

            // Act
            var drawn = new List<ModuleType>();
            for (int i = 0; i < 30; i++)
            {
                Assert.IsTrue(spawner.DrawTypeForTests(out ModuleType type), $"draw {i}");
                drawn.Add(type);
            }

            // Assert: only the three free types ever come out, and offers still may appear (K-23).
            CollectionAssert.DoesNotContain(drawn, ModuleType.TripleShot);
            CollectionAssert.DoesNotContain(drawn, ModuleType.Density);
            CollectionAssert.DoesNotContain(drawn, ModuleType.Pierce);
            CollectionAssert.Contains(drawn, ModuleType.Echo);
            CollectionAssert.Contains(drawn, ModuleType.Haste);
            CollectionAssert.Contains(drawn, ModuleType.CritChance);
            Assert.IsTrue(modules.SlotsFull);
            Assert.IsTrue(modules.CanPresentOffer);
        }

        [UnityTest]
        public IEnumerator Slots_AcceptFillsFirstFree_EjectKeepsOthersInPlace()
        {
            // Arrange
            var origin = new Vector3(0f, 0f, 1900f);
            PlayerModules modules = SpawnPlayer(origin);
            yield return null;

            // Act: fill in order, eject the middle one, then equip another.
            modules.EquipImmediate(ModuleType.Echo);
            modules.EquipImmediate(ModuleType.Density);
            modules.EquipImmediate(ModuleType.Haste);
            int densitySlot = modules.SlotOf(ModuleType.Density);
            Assert.IsTrue(modules.TryEject(ModuleType.Density));
            ModuleType? freed = modules.SlotType(densitySlot);
            int echoAfterEject = modules.SlotOf(ModuleType.Echo);
            int hasteAfterEject = modules.SlotOf(ModuleType.Haste);
            modules.EquipImmediate(ModuleType.Pierce);
            int pierceSlot = modules.SlotOf(ModuleType.Pierce);
            modules.ClearAll();

            // Assert
            Assert.AreEqual(1, densitySlot, "second accept takes slot 1");
            Assert.IsNull(freed, "eject empties its slot");
            Assert.AreEqual(-1, modules.SlotOf(ModuleType.Density));
            Assert.AreEqual(0, echoAfterEject, "the others keep their positions");
            Assert.AreEqual(2, hasteAfterEject);
            Assert.AreEqual(1, pierceSlot, "the next accept fills the first free slot");
            for (int i = 0; i < ModuleCatalog.SlotCount; i++) Assert.IsNull(modules.SlotType(i), $"slot {i} cleared");
            Assert.IsNull(modules.SlotType(-1));
            Assert.IsNull(modules.SlotType(ModuleCatalog.SlotCount));
            Assert.AreEqual(0, modules.EquippedCount);
            Assert.IsFalse(modules.SlotsFull);
            Assert.AreEqual(-1, modules.HighlightedSlot, "selection closed");
        }

        [UnityTest]
        public IEnumerator KeskinGoz_Equipped_RifleCrit32Percent_EjectRestores()
        {
            // Arrange
            var origin = new Vector3(0f, 0f, 1800f);
            PlayerModules modules = SpawnPlayer(origin);
            Rifle rifle = modules.Rifle;
            float baseDamage = rifle.Damage;
            float baseSpeed = modules.Player.MoveSpeedMultiplier;

            // Act
            Assert.IsTrue(modules.EquipImmediate(ModuleType.CritChance));
            float equipped = rifle.CritChance;
            yield return new WaitForSeconds(0.1f);
            bool slowWithoutFire = modules.FiringSlowActive;
            Assert.IsTrue(modules.TryEject(ModuleType.CritChance));

            // Assert
            Assert.AreEqual(0.32f, equipped, 1e-5f, "12 % + 20 %");
            Assert.AreEqual(baseDamage, rifle.Damage, 1e-5f, "no damage cost");
            Assert.IsFalse(slowWithoutFire, "the walk is slowed only while fire is held");
            Assert.AreEqual(baseSpeed, modules.Player.MoveSpeedMultiplier, 1e-5f);
            Assert.AreEqual(0.12f, rifle.CritChance, 1e-5f, "back to base");
        }
    }
}
