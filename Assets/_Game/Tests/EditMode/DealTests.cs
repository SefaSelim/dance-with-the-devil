using NUnit.Framework;
using UnityEngine;

namespace DanceWithTheDevil.Tests
{
    /// <summary>The six default contracts of MAIN_GDD §8 and the max-health stacking rule.</summary>
    public sealed class DealTests
    {
        private DealDefinition[] _deals;

        [SetUp]
        public void SetUp() => _deals = DealLibrary.CreateDefaults();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _deals.Length; i++)
                if (_deals[i] != null) Object.DestroyImmediate(_deals[i]);
        }

        [Test]
        public void Defaults_AreSixDealsInOrder()
        {
            Assert.AreEqual(DealLibrary.DealCount, _deals.Length);
            for (int i = 0; i < _deals.Length; i++)
            {
                Assert.AreEqual(i + 1, _deals[i].Number);
                Assert.IsFalse(string.IsNullOrEmpty(_deals[i].OfferLine), $"deal {i + 1} offer line");
                Assert.IsFalse(string.IsNullOrEmpty(_deals[i].CostName), $"deal {i + 1} cost name");
                Assert.IsFalse(string.IsNullOrEmpty(_deals[i].LedgerTaken), $"deal {i + 1} ledger");
            }
            Assert.AreEqual("Rengi", _deals[0].CostName);
            Assert.AreEqual("Kendisi", _deals[5].LedgerGivenName);
        }

        [Test]
        public void MaxHealth_Stacks_100_100_80_80_60_60_60()
        {
            float[] expected = { 100f, 100f, 80f, 80f, 60f, 60f, 60f };
            for (int taken = 0; taken < expected.Length; taken++)
                Assert.AreEqual(expected[taken], DealEffects.MaxHealthAfter(_deals, taken), 1e-4f, $"after {taken} deals");
        }

        [Test]
        public void DealFour_AttackSpeed_Rifle7Point5AndMacheteCooldown0Point4()
        {
            // Arrange
            var go = new GameObject("DealFourTarget");
            Rifle rifle = go.AddComponent<Rifle>();
            Machete machete = go.AddComponent<Machete>();
            float swing = machete.SwingDuration;

            // Act
            DealEffects.Apply(_deals[3], new DealTargets { Rifle = rifle, Machete = machete });

            // Assert
            Assert.AreEqual(7.5f, rifle.EffectiveFireRate, 1e-4f, "rifle 6 → 7.5 shots/s");
            Assert.AreEqual(1.1f, rifle.DamageMultiplier, 1e-4f, "damage +10% kept");
            Assert.AreEqual(0.4f, machete.Cooldown, 1e-4f, "machete cooldown 0.5 → 0.4 s");
            Assert.AreEqual(swing, machete.SwingDuration, 1e-6f, "swing duration unchanged");
            Assert.AreEqual(1.1f, machete.DamageMultiplier, 1e-4f);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void DealThree_CritChance_Plus15OnRifleMacheteAndRocket()
        {
            // Arrange
            var go = new GameObject("DealThreeTarget");
            Rifle rifle = go.AddComponent<Rifle>();
            Machete machete = go.AddComponent<Machete>();
            RocketLauncher rocket = go.AddComponent<RocketLauncher>();
            float enemySpeed = Enemy.SpeedMultiplier;

            // Act
            DealEffects.Apply(_deals[2], new DealTargets { Rifle = rifle, Machete = machete, Rocket = rocket });

            // Assert
            Assert.AreEqual(0.20f, rifle.CritChance, 1e-5f, "rifle 5 % → 20 %");
            Assert.AreEqual(0.20f, machete.CritChance, 1e-5f, "machete 5 % → 20 %");
            Assert.AreEqual(0.20f, rocket.CritChance, 1e-5f, "rocket 5 % → 20 %");
            Assert.AreEqual(2, rifle.PierceCount, "pierce kept");
            StringAssert.Contains("kritik", _deals[2].LedgerTaken);
            Enemy.SpeedMultiplier = enemySpeed; // contract 3 also speeds enemies up (static)
            Object.DestroyImmediate(go);
        }

        [Test]
        public void DealSix_OffersNoChoice_OthersDo()
        {
            for (int i = 0; i < 5; i++) Assert.IsTrue(_deals[i].OffersChoice, $"deal {i + 1}");
            Assert.IsFalse(_deals[5].OffersChoice);
        }
    }
}
