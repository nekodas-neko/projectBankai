using System.IO;
using Game.Core;
using NUnit.Framework;

namespace Game.Client.Tests
{
    // What a kill is worth comes from the dead enemy's class: its own XP, its own loot table and its own drop chance.
    public class EnemyRewardTests
    {
        static readonly string[] Starter = { "sword_and_shield", "staff", "healing_mace" };

        string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "bankai-rewards-" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        static MasteryRules Rules()
        {
            return new MasteryRules { XpPerLevel = new[] { 1000 }, XpPerKill = 10, XpPerBossKill = 150 };
        }

        static ClassDef Enemy(string id, int xp = 0, string table = "", float chance = 0f)
        {
            return new ClassDef { Id = id, Stats = new Stats { MaxHealth = 100f }, XpReward = xp, LootTable = table, DropChance = chance };
        }

        static LootTableDef Table(string id, string type, float power)
        {
            return new LootTableDef { Id = id, WeaponTypes = new[] { type }, PowerMin = power, PowerMax = power };
        }

        // A progress object with two enemy classes ("grunt": 25 XP, 100% from mob_drop; "brute": defaults, no table) and two tables.
        MasteryProgress Progress(LootTableDef fallback = null, float gruntChance = 1f)
        {
            var classes = new Registry<ClassDef>();
            classes.Add(Enemy("grunt", 25, "mob_drop", gruntChance));
            classes.Add(Enemy("brute"));
            classes.Add(Enemy("chief", 200, "boss_drop", 1f));
            var tables = new Registry<LootTableDef>();
            tables.Add(Table("mob_drop", "staff", 0.9f));
            tables.Add(Table("boss_drop", "healing_mace", 1.1f));
            return new MasteryProgress(new ProfileStore(directory, Starter), Rules(), fallback, 5, new RewardBook(classes, Rules()), tables);
        }

        static Snapshot Enemies(float health, string classId, int firstId = 1, int count = 1, bool boss = false)
        {
            var snapshot = new Snapshot();
            for (var i = 0; i < count; i++)
            {
                snapshot.Entities.Add(new EntityState
                {
                    Id = new EntityId(firstId + i),
                    Team = Team.Enemies,
                    Health = health,
                    MaxHealth = 100f,
                    ClassHash = StableId.From(classId),
                    Effects = boss ? EffectFlags.Boss : (byte)0,
                });
            }

            return snapshot;
        }

        static void Kill(MasteryProgress progress, string classId, int firstId = 1, int count = 1, bool boss = false)
        {
            progress.Observe(Enemies(50f, classId, firstId, count, boss));
            progress.Observe(Enemies(0f, classId, firstId, count, boss));
        }

        static int Xp(MasteryProgress progress)
        {
            return progress.Profile.MasteryFor(progress.Profile.EquippedType).Xp;
        }

        [Test]
        public void EachClassGivesItsOwnXp()
        {
            var progress = Progress();

            Kill(progress, "grunt");
            Assert.AreEqual(25, Xp(progress));

            Kill(progress, "brute", 2);
            Assert.AreEqual(35, Xp(progress), "no value of its own: the default of 10");

            Kill(progress, "chief", 3, boss: true);
            Assert.AreEqual(235, Xp(progress));
        }

        [Test]
        public void AMobDropsFromItsNamedTableWhenTheChanceHits()
        {
            var progress = Progress();

            Kill(progress, "grunt");

            Assert.AreEqual(4, progress.Profile.Weapons.Count);
            Assert.AreEqual("staff", progress.Profile.Weapons[3].Type);
            Assert.AreEqual(0.9f, progress.Profile.Weapons[3].Power, 0.001f);
        }

        [Test]
        public void ABossClassDropsFromItsOwnTableNotTheFallback()
        {
            var progress = Progress(Table("fallback", "staff", 1.3f));

            Kill(progress, "chief", boss: true);

            Assert.AreEqual("healing_mace", progress.Profile.Weapons[3].Type);
            Assert.AreEqual(1.1f, progress.Profile.Weapons[3].Power, 0.001f);
        }

        [Test]
        public void ABossWithoutATableUsesTheFallbackAndAMobWithoutOneDropsNothing()
        {
            var progress = Progress(Table("fallback", "staff", 1.3f));

            Kill(progress, "brute", 1);
            Assert.AreEqual(3, progress.Profile.Weapons.Count, "a plain mob has no table");

            Kill(progress, "brute", 2, boss: true);
            Assert.AreEqual(4, progress.Profile.Weapons.Count);
            Assert.AreEqual(1.3f, progress.Profile.Weapons[3].Power, 0.001f);
        }

        [Test]
        public void AZeroChanceNeverDropsAndAHalfChanceDropsAboutHalfTheTime()
        {
            var never = Progress(gruntChance: 0f);
            Kill(never, "grunt", 1, 20);
            Assert.AreEqual(3, never.Profile.Weapons.Count);

            var half = Progress(gruntChance: 0.5f);
            Kill(half, "grunt", 1, 40);
            var drops = half.Profile.Weapons.Count - 3;
            Assert.That(drops, Is.InRange(8, 27), "about half of 40, capped by the bag");
        }

        [Test]
        public void EveryShippedEnemyHasARewardAndItsTableExists()
        {
            DefinitionLoader.Load(out var classes, out _);
            var tables = LootCatalog.Load(classes);
            var enemies = 0;

            foreach (var def in classes.All)
            {
                if (def.Playable)
                {
                    continue;
                }

                enemies++;
                Assert.Greater(def.XpReward, 0, $"{def.Id} should set its own XP");
                if (def.DropChance > 0f)
                {
                    Assert.IsTrue(tables.TryGet(def.LootTable, out _), $"{def.Id} names the loot table '{def.LootTable}'");
                }
            }

            Assert.Greater(enemies, 0);
            Assert.IsTrue(tables.TryGet(LootTableAsset.MobDrop, out var mob));
            Assert.IsTrue(tables.TryGet(LootTableAsset.BossDrop, out var boss));
            Assert.Less(mob.PowerMax, boss.PowerMax, "a mob drop is never better than a boss drop");
        }
    }
}
