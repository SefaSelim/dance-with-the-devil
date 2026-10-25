using NUnit.Framework;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// The shared crit roll (production/design/crit-difficulty.md §2): seeded, its rate matches the chance within a
    /// tolerance; 0 never and 1 always crit; chances add up and clamp to 0..1.
    /// </summary>
    public sealed class CritRollTests
    {
        private const int Rolls = 20000;

        [TearDown]
        public void TearDown() => CritRoll.Seed(System.Environment.TickCount);

        [TestCase(0.05f, 0.006f)]
        [TestCase(0.12f, 0.008f)]
        [TestCase(0.32f, 0.012f)]
        [TestCase(0.47f, 0.012f)]
        public void Roll_SeededManyTimes_RateWithinTolerance(float chance, float tolerance)
        {
            // Arrange
            CritRoll.Seed(12345);
            int crits = 0;

            // Act
            for (int i = 0; i < Rolls; i++)
                if (CritRoll.Roll(chance)) crits++;

            // Assert
            Assert.AreEqual(chance, crits / (float)Rolls, tolerance);
        }

        [Test]
        public void Roll_SameSeed_SameSequence()
        {
            // Arrange
            var first = new bool[64];
            var second = new bool[64];

            // Act
            CritRoll.Seed(7);
            for (int i = 0; i < first.Length; i++) first[i] = CritRoll.Roll(0.5f);
            CritRoll.Seed(7);
            for (int i = 0; i < second.Length; i++) second[i] = CritRoll.Roll(0.5f);

            // Assert
            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void Roll_ZeroAndOne_NeverAndAlways()
        {
            // Arrange
            CritRoll.Seed(99);
            int zeroCrits = 0;
            int oneCrits = 0;

            // Act
            for (int i = 0; i < 1000; i++)
            {
                if (CritRoll.Roll(0f)) zeroCrits++;
                if (CritRoll.Roll(1f)) oneCrits++;
            }

            // Assert
            Assert.AreEqual(0, zeroCrits);
            Assert.AreEqual(1000, oneCrits);
        }

        [Test]
        public void Combine_BasePlusBonuses_ClampsToOne()
        {
            // Act / Assert
            Assert.AreEqual(0.47f, CritRoll.Combine(CritRoll.DefaultChance, 0.2f, 0.15f), 1e-5f);
            Assert.AreEqual(1f, CritRoll.Combine(0.5f, 0.5f, 0.5f), 1e-5f);
            Assert.AreEqual(0f, CritRoll.Combine(-1f, 0f, 0f), 1e-5f);
        }

        [Test]
        public void DefaultChance_IsTwelvePercent_SeededRateMatches()
        {
            // Arrange (user decision 2026-10-25: base 5 % -> 12 %)
            CritRoll.Seed(2026);
            int crits = 0;

            // Act
            for (int i = 0; i < Rolls; i++)
                if (CritRoll.Roll(CritRoll.DefaultChance)) crits++;

            // Assert
            Assert.AreEqual(0.12f, CritRoll.DefaultChance, 1e-6f);
            Assert.AreEqual(0.12f, crits / (float)Rolls, 0.008f);
        }

        [Test]
        public void DamageInfo_OldConstructor_IsNotCrit()
        {
            // Act
            var info = new DamageInfo(10f, UnityEngine.Vector3.zero, UnityEngine.Vector3.forward, null, DamageKind.Bullet);
            var crit = new DamageInfo(20f, UnityEngine.Vector3.zero, UnityEngine.Vector3.forward, null, DamageKind.Bullet, true);

            // Assert
            Assert.IsFalse(info.IsCrit);
            Assert.IsTrue(crit.IsCrit);
        }
    }
}
