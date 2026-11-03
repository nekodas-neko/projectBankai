using NUnit.Framework;

namespace Game.Core.Tests
{
    // What an enemy is worth is data on its class, and the snapshot carries the class so a client can look it up.
    public class RewardTests
    {
        static ClassDef Enemy(string id, int xp = 0, string table = "", float chance = 0f)
        {
            return new ClassDef
            {
                Id = id,
                Stats = new Stats { MaxHealth = 100f, MoveSpeed = 3f, Power = 1f },
                AutoAttack = "basic",
                XpReward = xp,
                LootTable = table,
                DropChance = chance,
            };
        }

        static RewardBook Book(params ClassDef[] defs)
        {
            var classes = new Registry<ClassDef>();
            foreach (var def in defs)
            {
                classes.Add(def);
            }

            return new RewardBook(classes, new MasteryRules { XpPerKill = 10, XpPerBossKill = 150 });
        }

        static System.Collections.Generic.List<string> Errors(ClassDef def)
        {
            var classes = TestData.Classes();
            classes.Add(def);
            return DefinitionValidator.Validate(classes, TestData.Abilities());
        }

        [Test]
        public void AClassWithItsOwnRewardGivesIt()
        {
            var book = Book(Enemy("elite", xp: 40, table: "mob_drop", chance: 0.25f));

            var reward = book.For(StableId.From("elite"), false);

            Assert.AreEqual(40, reward.Xp);
            Assert.AreEqual("mob_drop", reward.LootTable);
            Assert.AreEqual(0.25f, reward.DropChance, 0.0001f);
        }

        [Test]
        public void AClassWithoutAnXpValueUsesTheRulesForItsKind()
        {
            var book = Book(Enemy("plain"));

            Assert.AreEqual(10, book.For(StableId.From("plain"), false).Xp);
            Assert.AreEqual(150, book.For(StableId.From("plain"), true).Xp);
            Assert.AreEqual(0f, book.For(StableId.From("plain"), false).DropChance);
        }

        [Test]
        public void AnUnknownEnemyGivesTheDefaultXpAndNoDrop()
        {
            var book = Book(Enemy("known"));

            var reward = book.For(StableId.From("stranger"), true);

            Assert.AreEqual(150, reward.Xp);
            Assert.AreEqual(string.Empty, reward.LootTable);
            Assert.AreEqual(0f, reward.DropChance);
        }

        [Test]
        public void TheClassHashSurvivesTheWire()
        {
            var snapshot = new Snapshot { Tick = 1 };
            snapshot.Entities.Add(new EntityState { Id = new EntityId(7), ClassHash = StableId.From("slime") });

            Assert.IsTrue(WireCodec.TryDecode(WireCodec.Encode(snapshot), out Snapshot decoded));

            Assert.AreEqual(StableId.From("slime"), decoded.Entities[0].ClassHash);
        }

        [Test]
        public void TheServerFillsInTheClassOfEveryEntity()
        {
            var random = new SeededRandom(1);
            var server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
            var link = new LoopbackLink(random, NetworkConditions.None);
            server.AddPlayer(new PlayerId(1), "fighter", link.ServerEnd);
            server.SpawnNpc("grunt", Team.Enemies, new Vec2(0f, 30f));

            server.StepTick();
            byte[] last = null;
            while (link.ClientEnd.TryReceive(out var data))
            {
                last = data;
            }

            Assert.IsTrue(WireCodec.TryDecode(last, out Snapshot snapshot));
            var hashes = new System.Collections.Generic.List<int>();
            foreach (var entity in snapshot.Entities)
            {
                hashes.Add(entity.ClassHash);
            }

            CollectionAssert.AreEquivalent(new[] { StableId.From("fighter"), StableId.From("grunt") }, hashes);
        }

        [Test]
        public void AValidRewardPassesAndBadOnesAreCaught()
        {
            Assert.IsEmpty(Errors(Enemy("ok_elite", xp: 40, table: "mob_drop", chance: 0.5f)));
            Assert.IsNotEmpty(Errors(Enemy("neg", xp: -1)));
            Assert.IsNotEmpty(Errors(Enemy("over", table: "t", chance: 1.5f)));
            Assert.IsNotEmpty(Errors(Enemy("nan", table: "t", chance: float.NaN)));
            Assert.IsNotEmpty(Errors(Enemy("no_table", chance: 0.5f)));
        }

        [Test]
        public void AWeaponCannotCarryAReward()
        {
            var weapon = Enemy("rewarded_weapon", xp: 10);
            weapon.Playable = true;

            Assert.IsNotEmpty(Errors(weapon));
        }

        [Test]
        public void AddXpTrainsTheWeaponInHandByThatAmount()
        {
            var profile = PlayerProfile.Starter(new[] { "staff" });
            var rules = new MasteryRules { XpPerLevel = new[] { 20, 40 }, XpPerKill = 10, XpPerBossKill = 100 };

            Assert.AreEqual(0, profile.AddXp(rules, 15));
            Assert.AreEqual(1, profile.AddXp(rules, 5), "20 XP is level 1");
            Assert.AreEqual(0, profile.AddXp(rules, 0));
            Assert.AreEqual(20, profile.MasteryFor("staff").Xp);
        }
    }
}
