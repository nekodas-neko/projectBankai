using NUnit.Framework;

namespace Game.Core.Tests
{
    public class LootTests
    {
        static LootTableDef Table()
        {
            return new LootTableDef { Id = "boss_drop", WeaponTypes = new[] { "staff", "healing_mace" }, PowerMin = 0.95f, PowerMax = 1.15f };
        }

        static Registry<ClassDef> Classes()
        {
            var classes = new Registry<ClassDef>();
            classes.Add(new ClassDef { Id = "staff", Playable = true });
            classes.Add(new ClassDef { Id = "healing_mace", Playable = true });
            classes.Add(new ClassDef { Id = "slime" });
            return classes;
        }

        static Registry<LootTableDef> Tables(params LootTableDef[] tables)
        {
            var registry = new Registry<LootTableDef>();
            foreach (var table in tables)
            {
                registry.Add(table);
            }

            return registry;
        }

        [Test]
        public void ARollPicksAListedTypeAndAPowerInsideTheRange()
        {
            var random = new SeededRandom(7);
            var types = new System.Collections.Generic.HashSet<string>();
            for (var i = 0; i < 200; i++)
            {
                var weapon = LootRoller.Roll(Table(), random, i + 1);

                types.Add(weapon.Type);
                Assert.GreaterOrEqual(weapon.Power, 0.95f);
                Assert.LessOrEqual(weapon.Power, 1.15f);
                Assert.IsTrue(ProfileText.IsId(weapon.Uid), weapon.Uid);
            }

            CollectionAssert.AreEquivalent(new[] { "staff", "healing_mace" }, types);
        }

        [Test]
        public void TheSameSeedGivesTheSameDropAndTheRollNumberMakesItsIdUnique()
        {
            var first = LootRoller.Roll(Table(), new SeededRandom(3), 1);
            var again = LootRoller.Roll(Table(), new SeededRandom(3), 1);
            var second = LootRoller.Roll(Table(), new SeededRandom(3), 2);

            Assert.AreEqual(first.Type, again.Type);
            Assert.AreEqual(first.Power, again.Power);
            Assert.AreNotEqual(first.Uid, second.Uid);
        }

        [Test]
        public void AnEmptyTableDropsNothing()
        {
            Assert.IsNull(LootRoller.Roll(new LootTableDef { Id = "none" }, new SeededRandom(1), 1));
        }

        [Test]
        public void TheValidatorChecksTypesAndThePowerRange()
        {
            Assert.IsEmpty(LootRoller.Validate(Tables(Table()), Classes()));
            Assert.IsNotEmpty(LootRoller.Validate(Tables(new LootTableDef { Id = "a" }), Classes()), "no weapon types");
            Assert.IsNotEmpty(LootRoller.Validate(Tables(new LootTableDef { Id = "a", WeaponTypes = new[] { "slime" } }), Classes()), "not a playable weapon");
            Assert.IsNotEmpty(LootRoller.Validate(Tables(new LootTableDef { Id = "a", WeaponTypes = new[] { "ghost" } }), Classes()), "unknown");
            Assert.IsNotEmpty(LootRoller.Validate(Tables(new LootTableDef { Id = "a", WeaponTypes = new[] { "staff" }, PowerMin = 1.2f, PowerMax = 1.1f }), Classes()), "min above max");
            Assert.IsNotEmpty(LootRoller.Validate(Tables(new LootTableDef { Id = "a", WeaponTypes = new[] { "staff" }, PowerMin = 1f, PowerMax = 2f }), Classes()), "above what the server accepts");
            Assert.IsNotEmpty(LootRoller.Validate(Tables(new LootTableDef { Id = "a", WeaponTypes = new[] { "staff" }, PowerMin = 0.1f, PowerMax = 1f }), Classes()), "below what the server accepts");
        }

        [Test]
        public void TheLootCountSurvivesTheSaveFile()
        {
            var profile = PlayerProfile.Starter(new[] { "staff" });
            profile.LootRolls = 12;

            Assert.IsTrue(ProfileText.TryParse(ProfileText.Write(profile), out var loaded, out var error), error);
            Assert.AreEqual(12, loaded.LootRolls);
        }

        [Test]
        public void TheBagHoldsAtMostThirtyWeapons()
        {
            var profile = new PlayerProfile();
            for (var i = 0; i < PlayerProfile.MaxWeapons; i++)
            {
                Assert.IsTrue(profile.Add(new OwnedWeapon { Uid = "w" + i, Type = "staff" }));
            }

            Assert.IsFalse(profile.Add(new OwnedWeapon { Uid = "one_too_many", Type = "staff" }));
        }

        [Test]
        public void CarryingATypeKeepsTheWeaponInHandOtherwiseTakesTheStrongest()
        {
            var profile = PlayerProfile.Starter(new[] { "sword_and_shield", "staff" });
            profile.Add(new OwnedWeapon { Uid = "staff-drop1", Type = "staff", Power = 1.1f });
            profile.Add(new OwnedWeapon { Uid = "staff-drop2", Type = "staff", Power = 1.2f });

            Assert.IsTrue(profile.EquipType("staff"));
            Assert.AreEqual("staff-drop2", profile.EquippedUid, "the strongest staff");

            profile.Equip("staff-drop1");
            Assert.IsTrue(profile.EquipType("staff"));
            Assert.AreEqual("staff-drop1", profile.EquippedUid, "already holding a staff, so it stays");

            Assert.IsFalse(profile.EquipType("healing_mace"), "none owned");
            Assert.AreEqual("staff-drop1", profile.EquippedUid);
        }
    }
}
