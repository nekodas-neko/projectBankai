using NUnit.Framework;

namespace Game.Core.Tests
{
    public class DismantleTests
    {
        static PlayerProfile Profile()
        {
            var profile = PlayerProfile.Starter(new[] { "sword_and_shield", "staff" });
            profile.Add(new OwnedWeapon { Uid = "staff-drop1", Type = "staff", Power = 1.1f });
            profile.Add(new OwnedWeapon { Uid = "staff-drop2", Type = "staff", Power = 1.0f, Modifier = "keen" });
            profile.AddArmor(new OwnedArmor { Uid = "stone-drop1", Set = "stone_warden" });
            return profile;
        }

        [Test]
        public void AStrongerWeaponAndOneWithAModifierAreWorthMore()
        {
            var plain = PlayerProfile.DismantleValue(new OwnedWeapon { Uid = "a", Type = "staff", Power = 1f });
            var strong = PlayerProfile.DismantleValue(new OwnedWeapon { Uid = "b", Type = "staff", Power = 1.15f });
            var modified = PlayerProfile.DismantleValue(new OwnedWeapon { Uid = "c", Type = "staff", Power = 1f, Modifier = "keen" });

            Assert.AreEqual(22, plain);
            Assert.Greater(strong, plain);
            Assert.AreEqual(plain + 10, modified);
            Assert.AreEqual(20, PlayerProfile.DismantleValue(new OwnedArmor { Uid = "x", Set = "stone" }));
        }

        [Test]
        public void ASpareWeaponCanBeTakenApartForGold()
        {
            var profile = Profile();

            Assert.IsTrue(profile.TryDismantleWeapon("staff-drop1", out var gold));

            Assert.AreEqual(PlayerProfile.DismantleValue(new OwnedWeapon { Uid = "a", Type = "staff", Power = 1.1f }), gold);
            Assert.AreEqual(gold, profile.Gold);
            Assert.IsFalse(profile.Has("staff-drop1"));
        }

        [Test]
        public void TheWeaponInHandAndTheLastOfItsTypeStay()
        {
            var profile = Profile();
            profile.Equip("staff-drop2");

            Assert.IsFalse(profile.TryDismantleWeapon("staff-drop2", out _), "in hand");
            Assert.IsFalse(profile.TryDismantleWeapon("sword_and_shield", out _), "the only sword and shield");
            Assert.IsTrue(profile.TryDismantleWeapon("staff", out _), "a spare staff, so one staff is still left");
            Assert.IsTrue(profile.TryDismantleWeapon("staff-drop1", out _));
            Assert.IsFalse(profile.TryDismantleWeapon("staff-drop2", out _), "the last staff");
            Assert.IsFalse(profile.TryDismantleWeapon("nothing", out _));
            Assert.AreEqual(0, new PlayerProfile().Gold);
        }

        [Test]
        public void ArmorThatIsNotWornCanBeTakenApart()
        {
            var profile = Profile();

            Assert.IsFalse(profile.TryDismantleArmor("wanderer", out _), "worn");
            Assert.IsTrue(profile.TryDismantleArmor("stone-drop1", out var gold));
            Assert.AreEqual(20, gold);
            Assert.AreEqual(20, profile.Gold);
            Assert.IsFalse(profile.TryDismantleArmor("stone-drop1", out _), "already gone");
        }
    }
}
