using NUnit.Framework;

namespace Game.Core.Tests
{
    public class ArmorTests
    {
        static Registry<ArmorSetDef> Sets()
        {
            var sets = new Registry<ArmorSetDef>();
            sets.Add(new ArmorSetDef { Id = "wanderer", Name = "Wanderer" });
            sets.Add(new ArmorSetDef { Id = "stone", Name = "Stone", HealthScale = 1.1f, SpeedScale = 0.95f });
            return sets;
        }

        [Test]
        public void ANewCharacterWearsTheStarterSetAndCanSwapToAnOwnedOne()
        {
            var profile = PlayerProfile.Starter(new[] { "staff" });

            Assert.AreEqual("wanderer", profile.EquippedArmor.Set);
            Assert.IsTrue(profile.AddArmor(new OwnedArmor { Uid = "stone-drop1", Set = "stone" }));
            Assert.IsTrue(profile.EquipArmor("stone-drop1"));
            Assert.AreEqual("stone", profile.EquippedArmor.Set);
            Assert.IsFalse(profile.EquipArmor("nothing"));
            Assert.IsFalse(profile.AddArmor(new OwnedArmor { Uid = "stone-drop1", Set = "stone" }), "ids are unique");
        }

        [Test]
        public void TheBagHoldsALimitedNumberOfSets()
        {
            var profile = new PlayerProfile();
            for (var i = 0; i < PlayerProfile.MaxArmors + 5; i++)
            {
                profile.AddArmor(new OwnedArmor { Uid = "a" + i, Set = "stone" });
            }

            Assert.AreEqual(PlayerProfile.MaxArmors, profile.Armors.Count);
        }

        [Test]
        public void ArmorAndTheWornSetSurviveTheProfileFile()
        {
            var profile = PlayerProfile.Starter(new[] { "staff" });
            profile.AddArmor(new OwnedArmor { Uid = "stone-drop3", Set = "stone" });
            profile.EquipArmor("stone-drop3");

            Assert.IsTrue(ProfileText.TryParse(ProfileText.Write(profile), out var loaded, out _));

            Assert.AreEqual(2, loaded.Armors.Count);
            Assert.AreEqual("stone-drop3", loaded.EquippedArmorUid);
            Assert.IsTrue(ProfileText.TryParse("bankai-profile 1\nweapon w staff 1 -\nequipped w\n", out var old, out _), "a profile from before armor still loads");
            Assert.AreEqual(0, old.Armors.Count);
            Assert.IsNull(old.EquippedArmor);
        }

        [Test]
        public void ArmorMultipliesWithTheWeaponGear()
        {
            var modifiers = new Registry<WeaponModifierDef>();
            modifiers.Add(new WeaponModifierDef { Id = "sturdy", Name = "Sturdy", HealthScale = 1.1f });
            var weapon = new OwnedWeapon { Uid = "w", Type = "staff", Power = 1.05f, Modifier = "sturdy" };

            var gear = Gear.Of(weapon, new OwnedArmor { Uid = "s", Set = "stone" }, modifiers, Sets());

            Assert.AreEqual(1.05f, gear.Power, 0.0001f);
            Assert.AreEqual(1.21f, gear.Health, 0.0001f);
            Assert.AreEqual(0.95f, gear.Speed, 0.0001f);
            Assert.AreEqual(1.1f, Gear.Of(weapon, null, modifiers, Sets()).Health, 0.0001f, "no armor, just the weapon");
        }

        [Test]
        public void ALootTableRollsAnArmorDropWithItsChance()
        {
            var always = new LootTableDef { Id = "t", ArmorSets = new[] { "stone" }, ArmorChance = 1f };
            var never = new LootTableDef { Id = "t", ArmorSets = new[] { "stone" }, ArmorChance = 0f };

            var armor = LootRoller.RollArmor(always, new SeededRandom(1), 4);

            Assert.AreEqual("stone", armor.Set);
            Assert.AreEqual("stone-drop4", armor.Uid);
            Assert.IsNull(LootRoller.RollArmor(never, new SeededRandom(1), 4));
        }

        [Test]
        public void ArmorSetsAndLootTablesAreValidated()
        {
            Assert.IsEmpty(ArmorRules.Validate(Sets()));
            var bad = new Registry<ArmorSetDef>();
            bad.Add(new ArmorSetDef { Id = "huge", Name = "Huge", HealthScale = 2f });
            bad.Add(new ArmorSetDef { Id = "unnamed", Name = "" });
            Assert.AreEqual(2, ArmorRules.Validate(bad).Count);

            var tables = new Registry<LootTableDef>();
            tables.Add(new LootTableDef { Id = "t", WeaponTypes = new[] { "fighter" }, ArmorSets = new[] { "ghost" }, ArmorChance = 0.5f });
            Assert.AreEqual(1, LootRoller.Validate(tables, TestData.Classes(), null, Sets()).Count);
        }
    }
}
