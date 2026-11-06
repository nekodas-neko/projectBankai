using NUnit.Framework;

namespace Game.Core.Tests
{
    public class DungeonTests
    {
        static DungeonDef Cave()
        {
            return new DungeonDef
            {
                Id = "cave",
                ClearDelaySeconds = 1f,
                Rooms = new[]
                {
                    new RoomDef { Name = "One", Spawns = new[] { new SpawnDef { Class = "dummy", X = 4f, Y = 4f }, new SpawnDef { Class = "dummy", X = -4f, Y = 4f } } },
                    new RoomDef { Name = "Two", Spawns = new[] { new SpawnDef { Class = "dummy", X = 0f, Y = 8f } } },
                },
            };
        }

        sealed class Setup
        {
            public readonly GameServer Server;
            public readonly LoopbackLink Link;
            public readonly Entity Player;
            int sequence;

            public Setup(int players = 1)
            {
                var random = new SeededRandom(1);
                Server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
                Link = new LoopbackLink(random, NetworkConditions.None);
                Player = Server.AddPlayer(new PlayerId(1), "fighter", Link.ServerEnd);
                Server.StartDungeon(Cave());
            }

            public int Enemies()
            {
                var count = 0;
                foreach (var e in Server.Entities)
                {
                    if (!e.Owner.IsValid)
                    {
                        count++;
                    }
                }

                return count;
            }

            public void KillEnemies()
            {
                foreach (var e in Server.Entities)
                {
                    if (!e.Owner.IsValid)
                    {
                        e.Health = 0f;
                    }
                }
            }

            public void Tick(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    Server.StepTick();
                }
            }

            public void Send(CommandType type)
            {
                TestData.Send(Link.ClientEnd, ++sequence, type, Vec2.Zero);
            }

            public Snapshot Latest()
            {
                Snapshot latest = null;
                while (Link.ClientEnd.TryReceive(out var data))
                {
                    if (WireCodec.TryDecode(data, out Snapshot snapshot))
                    {
                        latest = snapshot;
                    }
                }

                return latest;
            }
        }

        [Test]
        public void StartingADungeonSpawnsTheFirstRoom()
        {
            var setup = new Setup();

            Assert.AreEqual(DungeonPhase.Fighting, setup.Server.DungeonPhase);
            Assert.AreEqual(2, setup.Enemies());
            Assert.AreEqual(0, setup.Server.Room);
        }

        [Test]
        public void ClearingARoomWaitsThenStartsTheNextAndRestoresThePlayers()
        {
            var setup = new Setup();
            setup.Player.Health = 10f;
            setup.Player.Cooldowns[0] = 5f;

            setup.KillEnemies();
            setup.Tick(1);
            Assert.AreEqual(DungeonPhase.Cleared, setup.Server.DungeonPhase);
            Assert.AreEqual(10f, setup.Player.Health);

            setup.Tick(20);
            Assert.AreEqual(DungeonPhase.Cleared, setup.Server.DungeonPhase);

            setup.Tick(15);
            Assert.AreEqual(DungeonPhase.Fighting, setup.Server.DungeonPhase);
            Assert.AreEqual(1, setup.Server.Room);
            Assert.AreEqual(1, setup.Enemies());
            Assert.AreEqual(100f, setup.Player.Health);
            Assert.AreEqual(0f, setup.Player.Cooldowns[0]);
        }

        [Test]
        public void ClearingTheLastRoomIsAVictoryThatStays()
        {
            var setup = new Setup();
            setup.KillEnemies();
            setup.Tick(60);
            setup.KillEnemies();
            setup.Tick(1);

            Assert.AreEqual(DungeonPhase.Victory, setup.Server.DungeonPhase);

            setup.Tick(300);
            Assert.AreEqual(DungeonPhase.Victory, setup.Server.DungeonPhase);
        }

        [Test]
        public void EveryPlayerDownIsADefeatAndNobodyRespawns()
        {
            var setup = new Setup();
            setup.Player.Health = 0f;

            setup.Tick(1);
            Assert.AreEqual(DungeonPhase.Defeat, setup.Server.DungeonPhase);

            setup.Tick(400);
            Assert.IsFalse(setup.Player.IsAlive);
            Assert.AreEqual(DungeonPhase.Defeat, setup.Server.DungeonPhase);
        }

        [Test]
        public void ADownedPlayerStaysDownWhileAnAllyKeepsFighting()
        {
            var setup = new Setup();
            var second = new LoopbackLink(new SeededRandom(2), NetworkConditions.None);
            var ally = setup.Server.AddPlayer(new PlayerId(2), "fighter", second.ServerEnd);
            setup.Player.Health = 0f;

            setup.Tick(300);

            Assert.IsFalse(setup.Player.IsAlive);
            Assert.IsTrue(ally.IsAlive);
            Assert.AreEqual(DungeonPhase.Fighting, setup.Server.DungeonPhase);

            setup.KillEnemies();
            setup.Tick(60);
            Assert.IsTrue(setup.Player.IsAlive);
        }

        [Test]
        public void RestartAfterADefeatStartsOverFromTheFirstRoom()
        {
            var setup = new Setup();
            setup.KillEnemies();
            setup.Tick(60);
            setup.Player.Health = 0f;
            setup.Tick(1);
            Assert.AreEqual(DungeonPhase.Defeat, setup.Server.DungeonPhase);

            setup.Send(CommandType.Restart);
            setup.Tick(1);

            Assert.AreEqual(DungeonPhase.Fighting, setup.Server.DungeonPhase);
            Assert.AreEqual(0, setup.Server.Room);
            Assert.AreEqual(2, setup.Enemies());
            Assert.IsTrue(setup.Player.IsAlive);
            Assert.AreEqual(100f, setup.Player.Health);
        }

        [Test]
        public void RestartIsIgnoredWhileTheFightIsOn()
        {
            var setup = new Setup();
            setup.Player.Health = 40f;

            setup.Send(CommandType.Restart);
            setup.Tick(2);

            Assert.AreEqual(40f, setup.Player.Health);
        }

        [Test]
        public void ASnapshotCarriesTheRoomAndPhase()
        {
            var setup = new Setup();
            setup.KillEnemies();
            setup.Tick(1);

            var snapshot = setup.Latest();

            Assert.AreEqual(1, snapshot.Room);
            Assert.AreEqual(2, snapshot.RoomCount);
            Assert.AreEqual(DungeonPhase.Cleared, snapshot.Dungeon);
            Assert.Greater(snapshot.DungeonTimer, 0.5f);
        }

        [Test]
        public void NoDungeonMeansNoRoomInTheSnapshot()
        {
            var random = new SeededRandom(1);
            var server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
            var link = new LoopbackLink(random, NetworkConditions.None);
            server.AddPlayer(new PlayerId(1), "fighter", link.ServerEnd);
            server.StepTick();

            Assert.IsTrue(WireCodec.TryDecode(Receive(link), out Snapshot snapshot));
            Assert.AreEqual(0, snapshot.Room);
            Assert.AreEqual(DungeonPhase.None, snapshot.Dungeon);
        }

        static byte[] Receive(LoopbackLink link)
        {
            link.ClientEnd.TryReceive(out var data);
            return data;
        }

        [Test]
        public void ThePlayersSurvivingIntoTheNextRoomIncludesALateJoiner()
        {
            var setup = new Setup();
            setup.Tick(5);
            var link = new LoopbackLink(new SeededRandom(3), NetworkConditions.None);
            var late = setup.Server.AddPlayer(new PlayerId(2), "fighter", link.ServerEnd);

            Assert.IsTrue(late.IsAlive);
            setup.KillEnemies();
            setup.Tick(60);
            Assert.AreEqual(1, setup.Server.Room);
            Assert.IsTrue(late.IsAlive);
        }

        [Test]
        public void Codec_RoundTripsTheDungeonFieldsAndTheRestartCommand()
        {
            var snapshot = new Snapshot { Tick = 3, Room = 2, RoomCount = 3, Dungeon = DungeonPhase.Cleared, DungeonTimer = 1.5f };
            Assert.IsTrue(WireCodec.TryDecode(WireCodec.Encode(snapshot), out Snapshot decoded));
            Assert.AreEqual(2, decoded.Room);
            Assert.AreEqual(3, decoded.RoomCount);
            Assert.AreEqual(DungeonPhase.Cleared, decoded.Dungeon);
            Assert.AreEqual(1.5f, decoded.DungeonTimer);

            var bytes = WireCodec.Encode(new Command(PlayerId.None, 4, CommandType.Restart, Vec2.Zero));
            Assert.IsTrue(WireCodec.TryDecode(bytes, out Command command));
            Assert.AreEqual(CommandType.Restart, command.Type);
            Assert.AreEqual(-1, command.AbilitySlot);
        }

        [Test]
        public void Validator_RejectsBrokenDungeons()
        {
            var dungeons = new Registry<DungeonDef>();
            dungeons.Add(new DungeonDef { Id = "empty" });
            dungeons.Add(new DungeonDef
            {
                Id = "bad",
                ClearDelaySeconds = -1f,
                Rooms = new[]
                {
                    new RoomDef { Spawns = new SpawnDef[0] },
                    new RoomDef { Spawns = new[] { new SpawnDef { Class = "ghost" }, new SpawnDef { Class = "fighter" } } },
                },
            });

            var errors = string.Join("\n", DefinitionValidator.ValidateDungeons(dungeons, TestData.Classes()));

            StringAssert.Contains("'empty': needs between 1 and 250 rooms", errors);
            StringAssert.Contains("'bad': clear delay", errors);
            StringAssert.Contains("room 1 has no enemies", errors);
            StringAssert.Contains("unknown class 'ghost'", errors);
            StringAssert.Contains("spawns player class 'fighter'", errors);
        }
    }
}
