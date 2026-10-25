using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Critical hits (production/design/crit-difficulty.md §2): a crit doubles the hit's damage, the flag reaches
    /// Health.Damaged (DamageInfo.IsCrit) and the ImpactBus event (ImpactEvent.isCrit) for the rifle, machete and
    /// rocket; Keskin Göz and contract 3 add to the chance. Chances are forced to 0 or 1 so every test is deterministic.
    /// Objects are built in code; each test uses its own region.
    /// </summary>
    public sealed class CritTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<ImpactEvent> _impacts = new List<ImpactEvent>();
        private readonly List<DamageInfo> _damaged = new List<DamageInfo>();

        [SetUp]
        public void SetUp()
        {
            _impacts.Clear();
            _damaged.Clear();
            ImpactBus.OnImpact += OnImpact;
        }

        [TearDown]
        public void TearDown()
        {
            ImpactBus.OnImpact -= OnImpact;
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.Destroy(_spawned[i]);
            _spawned.Clear();
        }

        private void OnImpact(ImpactEvent e) => _impacts.Add(e);

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
            health.Damaged += (info, _) => _damaged.Add(info);
            Physics.SyncTransforms();
            return health;
        }

        private ImpactEvent? FirstImpact(ImpactKind kind)
        {
            for (int i = 0; i < _impacts.Count; i++)
                if (_impacts[i].kind == kind) return _impacts[i];
            return null;
        }

        [UnityTest]
        public IEnumerator Rifle_CritBullet_DealsDoubleDamage_AndFlagsHealthAndImpact()
        {
            // Arrange
            var origin = new Vector3(0f, 1f, 3000f);
            Rifle rifle = Spawn("Shooter", origin).AddComponent<Rifle>();
            rifle.BaseCritChance = 1f;
            Health target = SpawnTarget(origin + new Vector3(3f, 0f, 0f));

            // Act
            Assert.IsTrue(rifle.TryFire(origin, Vector3.right));
            yield return new WaitForSeconds(0.25f);

            // Assert
            Assert.AreEqual(target.MaxHealth - 20f, target.Current, 1e-4f, "10 × 2");
            Assert.AreEqual(1, _damaged.Count);
            Assert.IsTrue(_damaged[0].IsCrit, "DamageInfo.IsCrit reaches Health.Damaged");
            ImpactEvent? hit = FirstImpact(ImpactKind.Bullet);
            Assert.IsTrue(hit.HasValue);
            Assert.IsTrue(hit.Value.isCrit, "ImpactEvent.isCrit");
        }

        [UnityTest]
        public IEnumerator Rifle_NoCrit_DealsBaseDamage_AndIsNotFlagged()
        {
            // Arrange
            var origin = new Vector3(0f, 1f, 3100f);
            Rifle rifle = Spawn("Shooter", origin).AddComponent<Rifle>();
            rifle.BaseCritChance = 0f;
            Health target = SpawnTarget(origin + new Vector3(3f, 0f, 0f));

            // Act
            Assert.IsTrue(rifle.TryFire(origin, Vector3.right));
            yield return new WaitForSeconds(0.25f);

            // Assert
            Assert.AreEqual(target.MaxHealth - 10f, target.Current, 1e-4f);
            Assert.AreEqual(1, _damaged.Count);
            Assert.IsFalse(_damaged[0].IsCrit);
            ImpactEvent? hit = FirstImpact(ImpactKind.Bullet);
            Assert.IsTrue(hit.HasValue);
            Assert.IsFalse(hit.Value.isCrit);
        }

        [UnityTest]
        public IEnumerator Rifle_CritBullet_CritsEveryPiercedTarget()
        {
            // Arrange
            var origin = new Vector3(0f, 1f, 3200f);
            Rifle rifle = Spawn("Shooter", origin).AddComponent<Rifle>();
            rifle.BaseCritChance = 1f;
            rifle.PierceCount = 1;
            Health first = SpawnTarget(origin + new Vector3(3f, 0f, 0f));
            Health second = SpawnTarget(origin + new Vector3(6f, 0f, 0f));

            // Act
            Assert.IsTrue(rifle.TryFire(origin, Vector3.right));
            yield return new WaitForSeconds(0.4f);

            // Assert
            Assert.AreEqual(first.MaxHealth - 20f, first.Current, 1e-4f);
            Assert.AreEqual(second.MaxHealth - 20f, second.Current, 1e-4f, "the crit bullet crits through the pierce");
        }

        [Test]
        public void Machete_Crit_DoublesDamage_AndFlagsHealthAndImpact()
        {
            // Arrange
            var position = new Vector3(0f, 0f, 3300f);
            Machete machete = Spawn("Swinger", position).AddComponent<Machete>();
            machete.BaseCritChance = 1f;
            Health target = SpawnTarget(position + new Vector3(0f, 1f, 1.5f));

            // Act
            Assert.IsTrue(machete.TrySwing(Vector3.forward));

            // Assert
            Assert.AreEqual(target.MaxHealth - 70f, target.Current, 1e-4f, "35 × 2");
            Assert.AreEqual(1, _damaged.Count);
            Assert.IsTrue(_damaged[0].IsCrit);
            ImpactEvent? hit = FirstImpact(ImpactKind.Melee);
            Assert.IsTrue(hit.HasValue);
            Assert.IsTrue(hit.Value.isCrit);
        }

        [UnityTest]
        public IEnumerator Rocket_Crit_DoublesDamage_AndFlagsTheExplosionEvent()
        {
            // Arrange
            var origin = new Vector3(0f, 1f, 3400f);
            RocketLauncher launcher = Spawn("Launcher", origin).AddComponent<RocketLauncher>();
            launcher.BaseCritChance = 1f;
            Health target = SpawnTarget(origin + new Vector3(3f, 0f, 0f));

            // Act
            Assert.IsTrue(launcher.TryFire(origin, Vector3.right));
            yield return new WaitForSeconds(0.5f); // 3 m at 18 m/s

            // Assert
            Assert.AreEqual(target.MaxHealth - 80f, target.Current, 1e-4f, "40 × 2");
            Assert.AreEqual(1, _damaged.Count);
            Assert.IsTrue(_damaged[0].IsCrit);
            ImpactEvent? blast = FirstImpact(ImpactKind.Explosion);
            Assert.IsTrue(blast.HasValue);
            Assert.IsTrue(blast.Value.isCrit, "one explosion event, flagged when any target critted");
        }

        [Test]
        public void CritChance_KeskinGozAndContractThree_AddUpAndClamp()
        {
            // Arrange
            Rifle rifle = Spawn("Rifle", new Vector3(0f, 0f, 3500f)).AddComponent<Rifle>();

            // Act
            float baseChance = rifle.CritChance;
            rifle.ModuleCritChance = 0.2f;
            float withModule = rifle.CritChance;
            rifle.ContractCritChance = DealEffects.CritChanceBonus;
            float withBoth = rifle.CritChance;
            rifle.ContractCritChance = 5f;
            float clamped = rifle.CritChance;

            // Assert
            Assert.AreEqual(0.12f, baseChance, 1e-5f);
            Assert.AreEqual(0.32f, withModule, 1e-5f);
            Assert.AreEqual(0.47f, withBoth, 1e-5f);
            Assert.AreEqual(1f, clamped, 1e-5f);
        }
    }
}
