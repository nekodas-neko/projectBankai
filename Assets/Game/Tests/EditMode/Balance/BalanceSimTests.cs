using NUnit.Framework;

namespace Game.Core.Tests
{
    public class BalanceSimTests
    {
        static BalanceResult Play(string weapon, string boss, IBotPolicy policy, ulong seed = 1)
        {
            return BalanceSim.Run(TestData.Classes(), TestData.Abilities(), weapon, boss, policy, 120f, seed);
        }

        [Test]
        public void ARunIsTheSameEveryTimeForTheSameSeed()
        {
            var first = Play("knight", "brute", new TankBlocker());
            var second = Play("knight", "brute", new TankBlocker());

            Assert.AreEqual(first.Seconds, second.Seconds);
            Assert.AreEqual(first.DamageTaken, second.DamageTaken);
            Assert.AreEqual(first.DamageDealt, second.DamageDealt);
        }

        [Test]
        public void AKiterWithALongRangeBeatsAMeleeBossWithoutBeingTouchedMuch()
        {
            var result = Play("caster", "brute", new Kiter());

            Assert.IsTrue(result.BossDead, BalanceSim.Table(new[] { result }));
            Assert.IsTrue(result.Survived);
            Assert.Greater(result.HealthLeft, 0.5f, BalanceSim.Table(new[] { result }));
        }

        [Test]
        public void TheSameLongRangeWeaponStandingStillIsHitFarMoreThanTheKiter()
        {
            var kite = Play("caster", "brute", new Kiter());
            var stand = Play("caster", "brute", new StandStill());

            Assert.Greater(stand.DamageTaken, kite.DamageTaken, BalanceSim.Table(new[] { kite, stand }));
        }

        [Test]
        public void AMeleeFighterThatStandsStillAndDoesNothingLosesToTheBoss()
        {
            var result = Play("knight", "brute", new StandStill());

            Assert.IsFalse(result.Survived, BalanceSim.Table(new[] { result }));
        }

        [Test]
        public void UsingTheBlockOnTheWarningsMakesTheTankLastLonger()
        {
            var still = Play("knight", "brute", new StandStill());
            var block = Play("knight", "brute", new TankBlocker());

            Assert.Greater(block.Seconds, still.Seconds, BalanceSim.Table(new[] { still, block }));
            Assert.Less(block.DamageTaken / block.Seconds, still.DamageTaken / still.Seconds, BalanceSim.Table(new[] { still, block }));
        }

        // Prints the table for the small test dataset; run by name when you want to see the numbers.
        [Test, Explicit("prints a report")]
        public void ReportOnTheTestData()
        {
            var results = new System.Collections.Generic.List<BalanceResult>();
            foreach (var weapon in new[] { "knight", "caster", "cleric" })
            {
                foreach (var policy in new IBotPolicy[] { new StandStill(), new TankBlocker(), new Kiter() })
                {
                    results.Add(Play(weapon, "brute", policy));
                }
            }

            TestContext.Out.WriteLine(BalanceSim.Table(results));
        }

        [Test]
        public void TheTableListsEveryRun()
        {
            var table = BalanceSim.Table(new[] { Play("caster", "brute", new Kiter()), Play("knight", "brute", new StandStill()) });

            StringAssert.Contains("caster", table);
            StringAssert.Contains("knight", table);
            StringAssert.Contains("kite", table);
            StringAssert.Contains("stand still", table);
        }
    }
}
