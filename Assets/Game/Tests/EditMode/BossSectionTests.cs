using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class BossSectionTests
    {
        static DungeonDef Keep(params RoomDef[] rooms)
        {
            return new DungeonDef { Id = "keep", ClearDelaySeconds = 1f, Rooms = rooms };
        }

        static SpawnDef Grunt(float x, float y, int pack = 0)
        {
            return new SpawnDef { Class = "grunt", X = x, Y = y, Pack = pack };
        }

        static SpawnDef Boss(float x, float y)
        {
            return new SpawnDef { Class = "dummy", X = x, Y = y, Boss = true };
        }

        sealed class Setup
        {
            public readonly GameServer Server;
            public readonly Entity Player;

            public Setup(DungeonDef dungeon)
            {
                var random = new SeededRandom(1);
                Server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
                var link = new LoopbackLink(random, NetworkConditions.None);
                Player = Server.AddPlayer(new PlayerId(1), "fighter", link.ServerEnd);
                Server.StartDungeon(dungeon);
            }

            public List<Entity> Enemies()
            {
                var list = new List<Entity>();
                foreach (var e in Server.Entities)
                {
                    if (!e.Owner.IsValid)
                    {
                        list.Add(e);
                    }
                }

                return list;
            }

            public void Tick(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    Server.StepTick();
                }
            }
        }

        [Test]
        public void TheBossDyingClearsTheSectionAndTheTrashCollapses()
        {
            var setup = new Setup(Keep(
                new RoomDef { Spawns = new[] { Boss(0f, 12f), Grunt(8f, 12f), Grunt(-8f, 12f) } },
                new RoomDef { Spawns = new[] { Boss(0f, 12f) } }));
            setup.Enemies()[0].Health = 0f;

            setup.Tick(1);

            Assert.AreEqual(DungeonPhase.Cleared, setup.Server.DungeonPhase);
            Assert.AreEqual(0, setup.Enemies().Count);
        }

        [Test]
        public void KillingOnlyTheTrashDoesNotClearASectionWithABoss()
        {
            var setup = new Setup(Keep(new RoomDef { Spawns = new[] { Boss(0f, 12f), Grunt(8f, 12f) } }));
            foreach (var e in setup.Enemies())
            {
                if (!e.IsBoss)
                {
                    e.Health = 0f;
                }
            }

            setup.Tick(30);

            Assert.AreEqual(DungeonPhase.Fighting, setup.Server.DungeonPhase);
        }

        [Test]
        public void EveryBossInASectionMustDie()
        {
            var setup = new Setup(Keep(new RoomDef { Spawns = new[] { Boss(-6f, 12f), Boss(6f, 12f) } }));
            setup.Enemies()[0].Health = 0f;

            setup.Tick(5);
            Assert.AreEqual(DungeonPhase.Fighting, setup.Server.DungeonPhase);

            setup.Enemies()[1].Health = 0f;
            setup.Tick(1);
            Assert.AreEqual(DungeonPhase.Victory, setup.Server.DungeonPhase);
        }

        [Test]
        public void TheFinalBossIsTheVictory()
        {
            var setup = new Setup(Keep(
                new RoomDef { Spawns = new[] { Boss(0f, 12f) } },
                new RoomDef { Spawns = new[] { Boss(0f, 12f), Grunt(5f, 12f) } }));
            setup.Enemies()[0].Health = 0f;
            setup.Tick(40);
            Assert.AreEqual(1, setup.Server.Room);

            foreach (var e in setup.Enemies())
            {
                if (e.IsBoss)
                {
                    e.Health = 0f;
                }
            }

            setup.Tick(1);
            Assert.AreEqual(DungeonPhase.Victory, setup.Server.DungeonPhase);
        }

        [Test]
        public void AMobIgnoresPlayersOutsideItsAggroRange()
        {
            var setup = new Setup(Keep(new RoomDef { Spawns = new[] { Grunt(0f, 18f) } }));
            var grunt = setup.Enemies()[0];

            setup.Tick(60);

            Assert.AreEqual(18f, grunt.Position.Y, 0.001f);
        }

        [Test]
        public void AMobAggrosWhenAPlayerComesIntoRange()
        {
            var setup = new Setup(Keep(new RoomDef { Spawns = new[] { Grunt(0f, 18f) } }));
            var grunt = setup.Enemies()[0];
            setup.Player.Position = new Vec2(0f, 12f);

            setup.Tick(30);

            Assert.Less(grunt.Position.Y, 17f);
        }

        [Test]
        public void PullingOneMobOfAPackPullsTheRest()
        {
            var setup = new Setup(Keep(new RoomDef { Spawns = new[] { Grunt(0f, 12f, 1), Grunt(12f, 12f, 1) } }));
            var far = setup.Enemies()[1];
            setup.Player.Position = new Vec2(0f, 5f);

            setup.Tick(30);

            Assert.Less(far.Position.X, 11f);
        }

        [Test]
        public void MobsWithoutAPackDoNotPullEachOther()
        {
            var setup = new Setup(Keep(new RoomDef { Spawns = new[] { Grunt(0f, 12f), Grunt(12f, 12f) } }));
            var far = setup.Enemies()[1];
            setup.Player.Position = new Vec2(0f, 5f);

            setup.Tick(30);

            Assert.AreEqual(12f, far.Position.X, 0.001f);
        }

        [Test]
        public void DifferentPacksStayApart()
        {
            var setup = new Setup(Keep(new RoomDef { Spawns = new[] { Grunt(0f, 12f, 1), Grunt(12f, 12f, 2) } }));
            var other = setup.Enemies()[1];
            setup.Player.Position = new Vec2(0f, 5f);

            setup.Tick(30);

            Assert.AreEqual(12f, other.Position.X, 0.001f);
        }

        [Test]
        public void AMobDraggedTooFarGivesUpWalksHomeAndHeals()
        {
            var setup = new Setup(Keep(new RoomDef { Spawns = new[] { Grunt(0f, 3f) } }));
            var grunt = setup.Enemies()[0];
            grunt.Health = 30f;
            grunt.AddThreat(setup.Player.Id, 5f);
            grunt.Position = new Vec2(0f, 18f);
            setup.Player.Position = new Vec2(0f, -19f);

            setup.Tick(1);
            Assert.IsTrue(grunt.Leashing);
            Assert.AreEqual(0, grunt.Threat.Count);

            var ticks = 0;
            while (grunt.Leashing && ticks++ < 300)
            {
                setup.Tick(1);
            }

            Assert.IsFalse(grunt.Leashing);
            Assert.AreEqual(100f, grunt.Health);
            Assert.Less((grunt.Position - new Vec2(0f, 3f)).Length, 0.6f);
        }

        [Test]
        public void AMobWithoutALeashChasesAsFarAsItLikes()
        {
            var setup = new Setup(Keep(new RoomDef { Spawns = new[] { new SpawnDef { Class = "brute", X = 0f, Y = 3f } } }));
            var brute = setup.Enemies()[0];
            brute.AddThreat(setup.Player.Id, 5f);
            brute.Position = new Vec2(0f, 18f);
            setup.Player.Position = new Vec2(0f, -19f);

            setup.Tick(5);

            Assert.IsFalse(brute.Leashing);
        }

        [Test]
        public void Validator_RejectsBadLeashAndPackValues()
        {
            var classes = TestData.Classes();
            classes.Add(new ClassDef
            {
                Id = "tethered",
                Role = Role.None,
                Stats = new Stats { MaxHealth = 10f, MoveSpeed = 1f, Power = 1f },
                AutoAttack = "basic",
                AggroRange = 10f,
                LeashRange = 8f,
            });
            var dungeons = new Registry<DungeonDef>();
            dungeons.Add(Keep(new RoomDef { Spawns = new[] { new SpawnDef { Class = "grunt", Pack = -1 } } }));

            var classErrors = string.Join("\n", DefinitionValidator.Validate(classes, TestData.Abilities()));
            var dungeonErrors = string.Join("\n", DefinitionValidator.ValidateDungeons(dungeons, classes));

            StringAssert.Contains("'tethered': leash range must be 0 (none) or larger than the aggro range", classErrors);
            StringAssert.Contains("negative pack number", dungeonErrors);
        }
    }
}
