using NUnit.Framework;

namespace Game.Core.Tests
{
    public class GoldTests
    {
        [Test]
        public void GoldAddsUpIsCappedAndCanBeSpent()
        {
            var profile = new PlayerProfile();

            profile.AddGold(40);
            profile.AddGold(-5);
            profile.AddGold(PlayerProfile.MaxGold);

            Assert.AreEqual(PlayerProfile.MaxGold, profile.Gold, "capped, and a negative amount adds nothing");
            profile.Gold = 100;
            Assert.IsFalse(profile.TrySpendGold(101));
            Assert.IsFalse(profile.TrySpendGold(-1));
            Assert.IsTrue(profile.TrySpendGold(60));
            Assert.AreEqual(40, profile.Gold);
        }

        [Test]
        public void GoldIsSavedWithTheProfile()
        {
            var profile = PlayerProfile.Starter(new[] { "staff" });
            profile.AddGold(1234);

            Assert.IsTrue(ProfileText.TryParse(ProfileText.Write(profile), out var loaded, out _));
            Assert.AreEqual(1234, loaded.Gold);

            Assert.IsTrue(ProfileText.TryParse("bankai-profile 1\ngold 99999999\n", out var big, out _));
            Assert.AreEqual(PlayerProfile.MaxGold, big.Gold, "a bigger number in a file is capped");
            Assert.IsFalse(ProfileText.TryParse("bankai-profile 1\ngold x\n", out _, out _));
        }

        [Test]
        public void ARewardCarriesTheGoldAndTheValidatorChecksIt()
        {
            var classes = new Registry<ClassDef>();
            classes.Add(new ClassDef { Id = "payer", Stats = new Stats { MaxHealth = 10f }, AutoAttack = "basic", GoldReward = 12 });
            var book = new RewardBook(classes, new MasteryRules());

            Assert.AreEqual(12, book.For(StableId.From("payer"), false).Gold);
            Assert.AreEqual(0, book.For(StableId.From("stranger"), false).Gold);

            var bad = TestData.Classes();
            bad.Add(new ClassDef { Id = "robber", Stats = new Stats { MaxHealth = 10f }, AutoAttack = "basic", GoldReward = -1 });
            Assert.IsNotEmpty(DefinitionValidator.Validate(bad, TestData.Abilities()));
        }
    }
}
