using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Player and weapon rules from MAIN_GDD §4 (Kaçınma, Oyuncu) and §5 (Silahlar).
    /// Objects are built in code; no real input. Each test uses its own region of space.
    /// </summary>
    public sealed class PlayerCombatTests
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

        private Health SpawnTarget(string name, Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = position;
            _spawned.Add(go);
            return go.AddComponent<Health>();
        }

        private static DamageInfo Hit(float amount) =>
            new DamageInfo(amount, Vector3.zero, Vector3.forward, null, DamageKind.Bullet);

        [UnityTest]
        public IEnumerator Health_InvulnerabilityAfterHit_BlocksSecondHit_ThenExpires()
        {
            Health health = Spawn("Health", new Vector3(0f, 0f, 0f)).AddComponent<Health>();

            Assert.IsTrue(health.TakeDamage(Hit(10f)));
            Assert.AreEqual(90f, health.Current, 1e-4f);
            Assert.IsTrue(health.IsInvulnerable);

            Assert.IsFalse(health.TakeDamage(Hit(10f)), "second hit inside 0.6 s must be ignored");
            Assert.AreEqual(90f, health.Current, 1e-4f);

            yield return new WaitForSeconds(0.7f);

            Assert.IsFalse(health.IsInvulnerable);
            Assert.IsTrue(health.TakeDamage(Hit(10f)));
            Assert.AreEqual(80f, health.Current, 1e-4f);
        }

        [Test]
        public void Health_Died_FiresOnceAtZero()
        {
            Health health = Spawn("Health", new Vector3(20f, 0f, 0f)).AddComponent<Health>();
            health.InvulnerabilityAfterHit = 0f;
            int died = 0;
            health.Died += _ => died++;

            Assert.IsTrue(health.TakeDamage(Hit(60f)));
            Assert.AreEqual(0, died);
            Assert.IsTrue(health.TakeDamage(Hit(60f)));
            Assert.AreEqual(1, died);
            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(0f, health.Current, 1e-4f);

            Assert.IsFalse(health.TakeDamage(Hit(10f)), "dead health ignores damage");
            Assert.AreEqual(1, died);

            health.Revive();
            Assert.IsFalse(health.IsDead);
            Assert.AreEqual(health.MaxHealth, health.Current, 1e-4f);
        }

        [UnityTest]
        public IEnumerator Dodge_GivesInvulnerability_AndEnforcesCooldown()
        {
            PlayerController player = Spawn("Player", new Vector3(40f, 0f, 0f)).AddComponent<PlayerController>();
            Health health = player.Health;

            Assert.IsTrue(player.TryDodge(Vector3.forward));
            Assert.IsTrue(player.IsDodging);
            Assert.IsTrue(health.IsInvulnerable);
            Assert.IsFalse(health.TakeDamage(Hit(10f)), "no damage during a dodge");
            Assert.IsFalse(player.TryDodge(Vector3.forward), "cannot dodge again mid-dodge");

            yield return new WaitForSeconds(0.3f); // dodge is 0.18 s

            Assert.IsFalse(player.IsDodging);
            Assert.IsFalse(health.IsInvulnerable);
            Assert.IsFalse(player.TryDodge(Vector3.forward), "on cooldown at ~0.3 s");

            yield return new WaitForSeconds(1.0f); // ~1.3 s

            Assert.IsFalse(player.TryDodge(Vector3.forward), "on cooldown at ~1.3 s");

            yield return new WaitForSeconds(0.5f); // ~1.8 s, cooldown is 1.6 s

            Assert.IsTrue(player.TryDodge(Vector3.forward), "charge back after 1.6 s");
        }

        [UnityTest]
        public IEnumerator RocketLauncher_ConsumesCharges_AndRefillsOne()
        {
            RocketLauncher launcher = Spawn("Rocket", new Vector3(60f, 200f, 0f)).AddComponent<RocketLauncher>();
            launcher.RefillTimeMultiplier = 0.1f; // 6.0 s → 0.6 s per charge, to keep the suite short
            Vector3 origin = launcher.transform.position;

            Assert.AreEqual(2, launcher.MaxCharges);
            Assert.AreEqual(2, launcher.Charges);
            Assert.IsTrue(launcher.TryFire(origin, Vector3.up));
            Assert.AreEqual(1, launcher.Charges);
            Assert.IsTrue(launcher.TryFire(origin, Vector3.up));
            Assert.AreEqual(0, launcher.Charges);
            Assert.IsFalse(launcher.TryFire(origin, Vector3.up), "no charge left");

            yield return new WaitForSeconds(0.8f); // one refill (0.6 s), not two (1.2 s)

            Assert.AreEqual(1, launcher.Charges);
            Assert.IsTrue(launcher.TryFire(origin, Vector3.up));
        }

        [Test]
        public void Machete_HitsTargetInFront_NotBehind()
        {
            var swingerPosition = new Vector3(100f, 0f, 0f);
            Machete machete = Spawn("Swinger", swingerPosition).AddComponent<Machete>();
            machete.BaseCritChance = 0f; // exact damage: no 5 % crit
            Health front = SpawnTarget("Front", swingerPosition + new Vector3(0f, 1f, 1.5f));
            Health behind = SpawnTarget("Behind", swingerPosition + new Vector3(0f, 1f, -1.5f));
            Physics.SyncTransforms();

            Assert.IsTrue(machete.TrySwing(Vector3.forward));
            Assert.IsTrue(machete.IsSwinging);
            Assert.AreEqual(front.MaxHealth - 35f, front.Current, 1e-4f, "target in front takes 35");
            Assert.AreEqual(behind.MaxHealth, behind.Current, 1e-4f, "target behind is untouched");
            Assert.IsFalse(machete.TrySwing(Vector3.forward), "cannot swing again during the swing");
        }
    }
}
