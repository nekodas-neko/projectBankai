using System.IO;
using Game.Core;
using NUnit.Framework;

namespace Game.Client.Tests
{
    public class LootDropTests
    {
        static readonly string[] Starter = { "sword_and_shield", "staff", "healing_mace" };

        string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "bankai-loot-" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        static LootTableDef Table()
        {
            return new LootTableDef { Id = "boss_drop", WeaponTypes = new[] { "staff" }, PowerMin = 1.1f, PowerMax = 1.1f };
        }

        static Snapshot Boss(float health)
        {
            var snapshot = new Snapshot();
            snapshot.Entities.Add(new EntityState { Id = new EntityId(9), Team = Team.Enemies, Health = health, MaxHealth = 100f, Effects = EffectFlags.Boss });
            return snapshot;
        }

        static void Kill(MasteryProgress progress)
        {
            progress.Observe(Boss(50f));
            progress.Observe(Boss(0f));
        }

        [Test]
        public void TheShippedBossDropTableLoadsAndIsValid()
        {
            DefinitionLoader.Load(out var classes, out _);

            var table = LootCatalog.LoadBossDrop(classes);

            Assert.IsNotNull(table);
            CollectionAssert.AreEquivalent(new[] { "sword_and_shield", "staff", "healing_mace" }, table.WeaponTypes);
            Assert.GreaterOrEqual(table.PowerMin, GameServer.MinWeaponPower);
            Assert.LessOrEqual(table.PowerMax, GameServer.MaxWeaponPower);
        }

        [Test]
        public void ABossGivesThePlayerTheirOwnWeaponAndItIsSaved()
        {
            var progress = new MasteryProgress(new ProfileStore(directory, Starter), new MasteryRules(), Table(), 5);

            Kill(progress);

            Assert.AreEqual(4, progress.Profile.Weapons.Count);
            var drop = progress.Profile.Weapons[3];
            Assert.AreEqual("staff", drop.Type);
            Assert.AreEqual(1.1f, drop.Power, 0.001f);
            Assert.AreEqual("Dropped: Staff +10%", progress.Summary());
            Assert.AreEqual(4, new ProfileStore(directory, Starter).Load().Weapons.Count, "saved at once");
        }

        [Test]
        public void OrdinaryKillsDropNothing()
        {
            var progress = new MasteryProgress(new ProfileStore(directory, Starter), new MasteryRules(), Table(), 5);
            var snapshot = new Snapshot();
            snapshot.Entities.Add(new EntityState { Id = new EntityId(3), Team = Team.Enemies, Health = 10f });
            progress.Observe(snapshot);
            snapshot.Entities[0] = new EntityState { Id = new EntityId(3), Team = Team.Enemies, Health = 0f };

            progress.Observe(snapshot);

            Assert.AreEqual(3, progress.Profile.Weapons.Count);
        }

        [Test]
        public void EachBossRollIsANewWeaponWithItsOwnId()
        {
            var progress = new MasteryProgress(new ProfileStore(directory, Starter), new MasteryRules(), Table(), 5);
            Kill(progress);
            var second = new Snapshot();
            second.Entities.Add(new EntityState { Id = new EntityId(10), Team = Team.Enemies, Health = 50f, Effects = EffectFlags.Boss });
            progress.Observe(second);
            second.Entities[0] = new EntityState { Id = new EntityId(10), Team = Team.Enemies, Health = 0f, Effects = EffectFlags.Boss };
            progress.Observe(second);

            Assert.AreEqual(5, progress.Profile.Weapons.Count);
            Assert.AreNotEqual(progress.Profile.Weapons[3].Uid, progress.Profile.Weapons[4].Uid);
            Assert.AreEqual(2, progress.Profile.LootRolls);
        }

        [Test]
        public void AFullBagLosesTheDropAndSaysSo()
        {
            var store = new ProfileStore(directory, Starter);
            var seed = store.Load();
            for (var i = seed.Weapons.Count; i < PlayerProfile.MaxWeapons; i++)
            {
                seed.Add(new OwnedWeapon { Uid = "filler" + i, Type = "staff" });
            }

            store.Save(seed);
            var progress = new MasteryProgress(store, new MasteryRules(), Table(), 5);

            Kill(progress);

            Assert.AreEqual(PlayerProfile.MaxWeapons, progress.Profile.Weapons.Count);
            StringAssert.Contains("bag is full", progress.Summary());
        }

        [Test]
        public void NoLootTableMeansNoDrops()
        {
            var progress = new MasteryProgress(new ProfileStore(directory, Starter), new MasteryRules());

            Kill(progress);

            Assert.AreEqual(3, progress.Profile.Weapons.Count);
        }

        [Test]
        public void TheStrongestWeaponOfATypeIsCarriedAndItsPowerIsSentWhenJoining()
        {
            var progress = new MasteryProgress(new ProfileStore(directory, Starter), new MasteryRules(), Table(), 5);
            Kill(progress);

            progress.Equip("sword_and_shield");
            Assert.AreEqual(1f, progress.WeaponPower);
            progress.Equip("staff");

            Assert.AreEqual(1.1f, progress.WeaponPower, 0.001f);
        }

        [Test]
        public void PickingAWeaponFromTheBagEquipsItAndSaves()
        {
            var progress = new MasteryProgress(new ProfileStore(directory, Starter), new MasteryRules(), Table(), 5);
            Kill(progress);
            var drop = progress.Profile.Weapons[3];

            Assert.IsTrue(progress.EquipWeapon(drop.Uid));
            Assert.IsFalse(progress.EquipWeapon("nothing"));

            Assert.AreEqual(drop.Uid, new ProfileStore(directory, Starter).Load().EquippedUid);
        }

        [Test]
        public void TheBagRowsShowTheWeaponItsPowerAndItsMastery()
        {
            var rules = new MasteryRules();
            var profile = PlayerProfile.Starter(Starter);
            var weapon = new OwnedWeapon { Uid = "staff-drop1", Type = "staff", Power = 0.93f };
            profile.Add(weapon);
            profile.MasteryFor("staff").AddXp(rules, 250);

            Assert.AreEqual("Staff -7%   mastery 2", CharacterScreen.DescribeWeapon(weapon, profile, rules, false));
            Assert.AreEqual("[in hand] Staff   mastery 2", CharacterScreen.DescribeWeapon(profile.Weapons[1], profile, rules, true));
        }
    }
}
