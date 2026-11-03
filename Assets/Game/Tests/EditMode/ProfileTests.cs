using NUnit.Framework;

namespace Game.Core.Tests
{
    public class ProfileTests
    {
        static readonly string[] Types = { "sword_and_shield", "staff", "healing_mace" };

        static MasteryRules Rules()
        {
            return new MasteryRules { XpPerLevel = new[] { 100, 200 }, XpPerKill = 10, XpPerBossKill = 100 };
        }

        [Test]
        public void ANewCharacterOwnsOneOfEachWeaponAndCarriesTheFirst()
        {
            var profile = PlayerProfile.Starter(Types);

            Assert.AreEqual(3, profile.Weapons.Count);
            Assert.AreEqual("sword_and_shield", profile.EquippedType);
            Assert.AreEqual(0, profile.MasteryFor("staff").Xp);
        }

        [Test]
        public void OnlyAnOwnedWeaponCanBeEquipped()
        {
            var profile = PlayerProfile.Starter(Types);

            Assert.IsTrue(profile.Equip("staff"));
            Assert.AreEqual("staff", profile.EquippedType);
            Assert.IsFalse(profile.Equip("rocket_launcher"));
            Assert.AreEqual("staff", profile.EquippedType);
        }

        [Test]
        public void TwoWeaponsOfOneTypeShareTheTypesMastery()
        {
            var profile = PlayerProfile.Starter(Types);
            Assert.IsTrue(profile.Add(new OwnedWeapon { Uid = "staff_boss_drop", Type = "staff", Power = 1.1f, Modifier = "swift" }));
            profile.Equip("staff_boss_drop");

            profile.AddKillXp(Rules(), false);
            profile.Equip("staff");
            profile.AddKillXp(Rules(), false);

            Assert.AreEqual(20, profile.MasteryFor("staff").Xp);
            Assert.AreEqual(0, profile.MasteryFor("healing_mace").Xp);
        }

        [Test]
        public void BossKillsTrainTheWeaponInHandAndReportLevelUps()
        {
            var profile = PlayerProfile.Starter(Types);
            profile.Equip("healing_mace");

            var gained = profile.AddKillXp(Rules(), true);

            Assert.AreEqual(1, gained);
            Assert.AreEqual(100, profile.MasteryFor("healing_mace").Xp);
            Assert.AreEqual(0, profile.MasteryFor("staff").Xp);
        }

        [Test]
        public void AWeaponNeedsAUniqueIdWithoutSpaces()
        {
            var profile = PlayerProfile.Starter(Types);

            Assert.IsFalse(profile.Add(new OwnedWeapon { Uid = "staff", Type = "staff" }), "repeated");
            Assert.IsFalse(profile.Add(new OwnedWeapon { Uid = "has space", Type = "staff" }));
            Assert.IsFalse(profile.Add(new OwnedWeapon { Uid = "", Type = "staff" }));
            Assert.IsFalse(profile.Add(null));
        }

        [Test]
        public void TheTextFormRoundTripsEverything()
        {
            var rules = Rules();
            var profile = PlayerProfile.Starter(Types);
            profile.Add(new OwnedWeapon { Uid = "mace_drop_1", Type = "healing_mace", Power = 1.25f, Modifier = "swift" });
            profile.Equip("mace_drop_1");
            profile.MasteryFor("healing_mace").AddXp(rules, 300);
            profile.MasteryFor("healing_mace").TryChoose(rules, "block_to_shield", 1);
            profile.MasteryFor("staff").AddXp(rules, 40);

            var text = ProfileText.Write(profile);
            Assert.IsTrue(ProfileText.TryParse(text, out var loaded, out var error), error);

            Assert.AreEqual("mace_drop_1", loaded.EquippedUid);
            Assert.AreEqual(4, loaded.Weapons.Count);
            Assert.AreEqual(1.25f, loaded.Equipped.Power);
            Assert.AreEqual("swift", loaded.Equipped.Modifier);
            Assert.AreEqual(300, loaded.MasteryFor("healing_mace").Xp);
            Assert.AreEqual(1, loaded.MasteryFor("healing_mace").PointsSpent);
            CollectionAssert.AreEqual(new[] { "block_to_shield" }, loaded.MasteryFor("healing_mace").Choices);
            Assert.AreEqual(40, loaded.MasteryFor("staff").Xp);
            Assert.AreEqual(text, ProfileText.Write(loaded), "writing it again gives the same text");
        }

        [Test]
        public void UnknownLinesAreSkippedSoANewerFileStillLoads()
        {
            var text = "bankai-profile 1\nequipped staff\nweapon staff staff 1 -\nfuture_thing 1 2 3\nmastery staff 50 0\n";

            Assert.IsTrue(ProfileText.TryParse(text, out var profile, out var error), error);
            Assert.AreEqual(50, profile.MasteryFor("staff").Xp);
        }

        [Test]
        public void AFileFromANewerVersionIsRefusedNotMisread()
        {
            Assert.IsFalse(ProfileText.TryParse("bankai-profile 99\nequipped staff\n", out var profile, out var error));
            StringAssert.Contains("newer", error);
            Assert.AreEqual(0, profile.Weapons.Count);
        }

        [TestCase("")]
        [TestCase("hello world")]
        [TestCase("bankai-profile x\n")]
        [TestCase("bankai-profile 1\nweapon staff staff nope -\n")]
        [TestCase("bankai-profile 1\nweapon staff\n")]
        [TestCase("bankai-profile 1\nmastery staff -5 0\n")]
        [TestCase("bankai-profile 1\nweapon staff staff 1 -\nweapon staff staff 1 -\n")]
        public void DamagedFilesAreRefusedWithAReason(string text)
        {
            Assert.IsFalse(ProfileText.TryParse(text, out var profile, out var error));
            Assert.IsNotEmpty(error);
            Assert.AreEqual(0, profile.Weapons.Count, "a refused file leaves an empty profile");
        }

        [Test]
        public void AnEquippedWeaponThatIsNotOwnedFallsBackToTheFirst()
        {
            var text = "bankai-profile 1\nequipped ghost\nweapon staff staff 1 -\nweapon healing_mace healing_mace 1 -\n";

            Assert.IsTrue(ProfileText.TryParse(text, out var profile, out var error), error);
            Assert.AreEqual("staff", profile.EquippedUid);
        }
    }
}
