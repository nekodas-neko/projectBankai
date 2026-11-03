using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // A dungeon with zones is one continuous map: everything is placed at the start, a boss's death opens its gate.
    public class DungeonMapTests
    {
        static DungeonDef Cave()
        {
            return new DungeonDef
            {
                Id = "cave_map",
                ClearDelaySeconds = 1f,
                Zones = new[]
                {
                    new ZoneDef { Name = "A", MinX = -10f, MinY = -10f, MaxX = 10f, MaxY = 10f },
                    new ZoneDef { Name = "B", MinX = -10f, MinY = 16f, MaxX = 10f, MaxY = 36f },
                },
                Gates = new[] { new GateDef { Name = "door", MinX = -2f, MinY = 9f, MaxX = 2f, MaxY = 17f, OpenAfter = 0 } },
                Rooms = new[]
                {
                    new RoomDef
                    {
                        Name = "One",
                        Spawns = new[]
                        {
                            new SpawnDef { Class = "dummy", X = 0f, Y = 6f, Boss = true },
                            new SpawnDef { Class = "dummy", X = 6f, Y = 5f, Pack = 1 },
                            new SpawnDef { Class = "dummy", X = 6f, Y = 6f, Pack = 1 },
                        },
                    },
                    new RoomDef
                    {
                        Name = "Two",
                        OffsetY = 26f,
                        Spawns = new[]
                        {
                            new SpawnDef { Class = "dummy", X = 0f, Y = 5f, Boss = true },
                            new SpawnDef { Class = "dummy", X = -6f, Y = 4f, Pack = 1 },
                        },
                    },
                },
            };
        }

        sealed class Setup
        {
            public readonly LocalSession Session;
            public readonly GameClient Client;
            public readonly Entity Player;

            public Setup()
            {
                Session = new LocalSession(new ServerConfig(), TestData.Classes(), TestData.Abilities(), 1);
                Client = Session.AddPlayer(new PlayerId(1), "fighter");
                Session.StartDungeon(Cave());
                Player = Session.Server.FindPlayerEntity(new PlayerId(1));
            }

            public GameServer Server => Session.Server;

            public List<Entity> Enemies(int section = -1)
            {
                var list = new List<Entity>();
                foreach (var e in Server.Entities)
                {
                    if (!e.Owner.IsValid && (section < 0 || e.Section == section))
                    {
                        list.Add(e);
                    }
                }

                return list;
            }

            public Entity BossOf(int section)
            {
                return Enemies(section).Find(e => e.IsBoss);
            }

            public void Advance(float seconds)
            {
                Session.Advance(seconds, 1f / 30f);
            }
        }

        [Test]
        public void EverySectionIsPopulatedFromTheStartInTheZoneItBelongsTo()
        {
            var setup = new Setup();

            Assert.AreEqual(5, setup.Enemies().Count);
            Assert.AreEqual(3, setup.Enemies(0).Count);
            Assert.AreEqual(2, setup.Enemies(1).Count);
            Assert.AreEqual(31f, setup.BossOf(1).Position.Y, 0.001f);
            Assert.AreEqual(6f, setup.BossOf(0).Position.Y, 0.001f);
            foreach (var enemy in setup.Enemies())
            {
                Assert.IsTrue(setup.Server.Map.Contains(enemy.Position), $"{enemy.Id} is inside a zone");
            }
        }

        [Test]
        public void PackNumbersOnlyMeanSomethingInsideTheirOwnSection()
        {
            var setup = new Setup();

            var first = setup.Enemies(0).Find(e => e.Pack != 0).Pack;
            var second = setup.Enemies(1).Find(e => e.Pack != 0).Pack;

            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void TheSecondZoneIsOutOfReachUntilTheFirstBossIsDown()
        {
            var setup = new Setup();
            setup.Player.Position = new Vec2(0f, 8f);

            for (var i = 0; i < 60; i++)
            {
                setup.Client.SetMoveInput(new Vec2(0f, 1f));
                setup.Advance(0.1f);
            }

            Assert.Less(setup.Player.Position.Y, 10f);
            Assert.Less(setup.Client.PredictedPosition.Y, 10f);
        }

        [Test]
        public void KillingTheFirstBossOpensTheGateWithoutMovingAnyone()
        {
            var setup = new Setup();
            setup.Player.Position = new Vec2(3f, -4f);
            setup.Player.Health = 40f;
            setup.BossOf(0).Health = 0f;

            setup.Advance(0.2f);

            Assert.AreEqual(1, setup.Server.Map.Progress);
            Assert.IsTrue(setup.Server.Map.IsOpen(0));
            Assert.AreEqual(DungeonPhase.Cleared, setup.Server.DungeonPhase);
            Assert.AreEqual(1, setup.Server.Room);
            Assert.AreEqual(3f, setup.Player.Position.X, 0.5f);
            Assert.AreEqual(40f, setup.Player.Health, 0.001f);
        }

        [Test]
        public void TheTrashOfAClearedSectionStaysButWhatTheBossSummonedGoes()
        {
            var setup = new Setup();
            var minion = setup.Server.SpawnNpc("dummy", Team.Enemies, new Vec2(2f, 2f));
            minion.Summoned = true;
            minion.Section = 0;
            setup.BossOf(0).Health = 0f;

            setup.Advance(0.2f);

            Assert.IsFalse(setup.Enemies().Contains(minion));
            Assert.AreEqual(2, setup.Enemies(0).FindAll(e => e.IsAlive && !e.IsBoss).Count);
        }

        [Test]
        public void ThePartyWalksThroughTheOpenGateIntoTheNextZone()
        {
            var setup = new Setup();
            setup.BossOf(0).Health = 0f;
            setup.Advance(0.2f);
            setup.Player.Position = new Vec2(0f, 8f);

            for (var i = 0; i < 80; i++)
            {
                setup.Client.SetMoveInput(new Vec2(0f, 1f));
                setup.Advance(0.1f);
            }

            Assert.Greater(setup.Player.Position.Y, 20f);
        }

        [Test]
        public void TheBannerStateReturnsToFightingAfterTheClearDelay()
        {
            var setup = new Setup();
            setup.BossOf(0).Health = 0f;
            setup.Advance(0.2f);
            Assert.AreEqual(DungeonPhase.Cleared, setup.Server.DungeonPhase);

            setup.Advance(1.5f);

            Assert.AreEqual(DungeonPhase.Fighting, setup.Server.DungeonPhase);
        }

        [Test]
        public void ADownedPlayerGetsBackUpWithHalfHealthWhenABossFalls()
        {
            var setup = new Setup();
            var second = new LoopbackLink(new SeededRandom(2), NetworkConditions.None);
            var friend = setup.Server.AddPlayer(new PlayerId(2), "fighter", second.ServerEnd);
            friend.Position = new Vec2(-5f, -5f);
            friend.Health = 0f;
            setup.BossOf(0).Health = 0f;

            setup.Advance(0.2f);

            Assert.IsTrue(friend.IsAlive);
            Assert.AreEqual(50f, friend.Health, 0.001f);
            Assert.AreEqual(-5f, friend.Position.X, 0.5f);
        }

        [Test]
        public void TheLastBossIsTheVictoryAndTheRoomStaysOnTheLastSection()
        {
            var setup = new Setup();
            setup.BossOf(0).Health = 0f;
            setup.Advance(2f);
            setup.BossOf(1).Health = 0f;

            setup.Advance(0.2f);

            Assert.AreEqual(DungeonPhase.Victory, setup.Server.DungeonPhase);
            Assert.AreEqual(1, setup.Server.Room);
            Assert.AreEqual(2, setup.Server.Map.Progress);
        }

        [Test]
        public void OnlyTheCurrentSectionsBossCountsTowardsTheClear()
        {
            var setup = new Setup();
            setup.BossOf(1).Health = 0f;

            setup.Advance(0.5f);

            Assert.AreEqual(DungeonPhase.Fighting, setup.Server.DungeonPhase);
            Assert.AreEqual(0, setup.Server.Map.Progress);
        }

        [Test]
        public void EveryPlayerDownIsADefeatAndARestartPutsTheWholeCaveBack()
        {
            var setup = new Setup();
            setup.BossOf(0).Health = 0f;
            setup.Advance(0.2f);
            setup.Player.Health = 0f;
            setup.Advance(0.2f);
            Assert.AreEqual(DungeonPhase.Defeat, setup.Server.DungeonPhase);

            setup.Client.Restart();
            setup.Advance(0.2f);

            Assert.AreEqual(DungeonPhase.Fighting, setup.Server.DungeonPhase);
            Assert.AreEqual(0, setup.Server.Map.Progress);
            Assert.AreEqual(0, setup.Server.Room);
            Assert.AreEqual(5, setup.Enemies().Count);
            Assert.IsTrue(setup.Player.IsAlive);
        }

        [Test]
        public void ASnapshotCarriesTheSectionSoAClientCanOpenItsOwnCopyOfTheGate()
        {
            var setup = new Setup();
            var map = WorldMap.From(Cave());
            var clientConfig = new ClientConfig { Simulation = SimulationSettings.Default.WithMap(map) };
            var link = new LoopbackLink(new SeededRandom(5), NetworkConditions.None);
            var joiner = new GameClient(new PlayerId(2), link.ClientEnd, TestData.Classes().Get("fighter"), clientConfig);
            setup.Server.AddPlayer(new PlayerId(2), "fighter", link.ServerEnd);
            setup.BossOf(0).Health = 0f;

            for (var i = 0; i < 10; i++)
            {
                setup.Advance(0.1f);
                link.Advance(0.1f);
                joiner.Update(0.1f);
            }

            Assert.IsFalse(map.Authoritative);
            Assert.AreEqual(1, map.Progress);
            Assert.IsTrue(map.IsOpen(0));
        }

        [Test]
        public void AClientThatSharesTheServersMapNeverWritesToIt()
        {
            var setup = new Setup();

            setup.Advance(0.5f);

            Assert.IsTrue(setup.Server.Map.Authoritative);
            Assert.AreEqual(0, setup.Server.Map.Progress);
        }

        [Test]
        public void TheValidatorChecksTheMap()
        {
            var dungeons = new Registry<DungeonDef>();
            var broken = Cave();
            broken.Id = "broken";
            broken.Zones = new[]
            {
                new ZoneDef { Name = "tiny", MinX = -1f, MinY = -1f, MaxX = 1f, MaxY = 1f },
                new ZoneDef { Name = "far", MinX = 100f, MinY = 100f, MaxX = 120f, MaxY = 120f },
            };
            broken.Gates = new[]
            {
                new GateDef { Name = "nowhere", MinX = 50f, MinY = 50f, MaxX = 52f, MaxY = 52f, OpenAfter = 0 },
                new GateDef { Name = "late", MinX = -2f, MinY = 9f, MaxX = 2f, MaxY = 17f, OpenAfter = 5 },
                new GateDef { Name = "flat", MinX = 0f, MinY = 0f, MaxX = 0f, MaxY = 4f },
            };
            dungeons.Add(broken);

            var errors = string.Join("\n", DefinitionValidator.ValidateDungeons(dungeons, TestData.Classes()));

            StringAssert.Contains("zone 'tiny' must be at least", errors);
            StringAssert.Contains("gate 'nowhere' must overlap two zones", errors);
            StringAssert.Contains("gate 'late' opens after a section that does not exist", errors);
            StringAssert.Contains("gate 'flat' has no size", errors);
            StringAssert.Contains("start positions are outside the zones", errors);
            StringAssert.Contains("spawns a 'dummy' outside the zones", errors);
        }

        [Test]
        public void AGoodMapValidates()
        {
            var dungeons = new Registry<DungeonDef>();
            dungeons.Add(Cave());

            Assert.AreEqual(0, DefinitionValidator.ValidateDungeons(dungeons, TestData.Classes()).Count);
        }
    }
}
