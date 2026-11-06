using NUnit.Framework;

namespace Game.Core.Tests
{
    public class MasteryTests
    {
        static MasteryRules Rules()
        {
            return new MasteryRules { XpPerLevel = new[] { 100, 200, 300 }, PointsPerLevel = 1, XpPerKill = 10, XpPerBossKill = 100 };
        }

        [Test]
        public void LevelsComeFromTotalXp()
        {
            var rules = Rules();

            Assert.AreEqual(0, rules.LevelFor(0));
            Assert.AreEqual(0, rules.LevelFor(99));
            Assert.AreEqual(1, rules.LevelFor(100));
            Assert.AreEqual(2, rules.LevelFor(300));
            Assert.AreEqual(3, rules.LevelFor(600));
            Assert.AreEqual(3, rules.LevelFor(100000), "capped at the last level");
            Assert.AreEqual(0, rules.LevelFor(-5));
        }

        [Test]
        public void ProgressShowsTheBarTowardTheNextLevel()
        {
            var rules = Rules();

            rules.Progress(150, out var into, out var needed);
            Assert.AreEqual(50, into);
            Assert.AreEqual(200, needed);

            rules.Progress(600, out into, out needed);
            Assert.AreEqual(0, needed, "nothing left to earn at the cap");
        }

        [Test]
        public void XpIsCappedAndReportsLevelsGained()
        {
            var rules = Rules();
            var mastery = new WeaponMastery();

            Assert.AreEqual(0, mastery.AddXp(rules, 90));
            Assert.AreEqual(1, mastery.AddXp(rules, 20));
            Assert.AreEqual(2, mastery.AddXp(rules, 100000));
            Assert.AreEqual(600, mastery.Xp);
            Assert.AreEqual(0, mastery.AddXp(rules, 0));
            Assert.AreEqual(0, mastery.AddXp(rules, -10));
        }

        [Test]
        public void EveryLevelGivesPointsThatTreeChoicesSpend()
        {
            var rules = Rules();
            var mastery = new WeaponMastery();
            mastery.AddXp(rules, 300);

            Assert.AreEqual(2, mastery.PointsAvailable(rules));
            Assert.IsTrue(mastery.TryChoose(rules, "block_to_dodge", 1));
            Assert.AreEqual(1, mastery.PointsAvailable(rules));
            Assert.IsFalse(mastery.TryChoose(rules, "block_to_dodge", 1), "the same node twice does nothing");
            Assert.IsFalse(mastery.TryChoose(rules, "big_node", 5), "not enough points");
            Assert.IsFalse(mastery.TryChoose(rules, "", 1));
            Assert.IsTrue(mastery.TryChoose(rules, "red_bolt", 1));
            Assert.AreEqual(0, mastery.PointsAvailable(rules));
        }

        [Test]
        public void ResetReturnsEveryPointAndTheDefaultKit()
        {
            var rules = Rules();
            var mastery = new WeaponMastery();
            mastery.AddXp(rules, 300);
            mastery.TryChoose(rules, "block_to_dodge", 1);
            mastery.TryChoose(rules, "red_bolt", 1);

            mastery.Reset();

            Assert.AreEqual(2, mastery.PointsAvailable(rules));
            Assert.IsEmpty(mastery.Choices);
            Assert.AreEqual(300, mastery.Xp, "the XP stays, only the spending is undone");
        }

        [Test]
        public void KillsAndBossesGiveXpFromTheRules()
        {
            var rules = Rules();

            Assert.AreEqual(10, rules.XpForKill(false));
            Assert.AreEqual(100, rules.XpForKill(true));
        }

        [Test]
        public void TheDefaultRulesHaveTwentyLevelsAndAreValid()
        {
            var rules = new MasteryRules();

            Assert.AreEqual(20, rules.MaxLevel);
            Assert.IsEmpty(rules.Validate());
            Assert.AreEqual(20, rules.LevelFor(rules.MaxXp));
        }

        [Test]
        public void TheValidatorRefusesBrokenRules()
        {
            Assert.IsNotEmpty(new MasteryRules { XpPerLevel = new int[0] }.Validate());
            Assert.IsNotEmpty(new MasteryRules { XpPerLevel = new[] { 100, 0 } }.Validate());
            Assert.IsNotEmpty(new MasteryRules { PointsPerLevel = 50 }.Validate());
            Assert.IsNotEmpty(new MasteryRules { XpPerKill = -1 }.Validate());
        }
    }
}
