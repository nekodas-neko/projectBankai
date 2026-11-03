using Game.Net;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // A network connection can only carry so many bytes in one message. A snapshot that does not fit is cut down
    // (the players stay, the nearest others stay) instead of being lost.
    public class SnapshotFitTests
    {
        sealed class Setup
        {
            public readonly GameServer Server;
            public readonly LoopbackLink Link;
            public readonly Entity Player;

            public Setup(int dummies)
            {
                var random = new SeededRandom(1);
                Server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
                Link = new LoopbackLink(random, NetworkConditions.None);
                Player = Server.AddPlayer(new PlayerId(1), "fighter", Link.ServerEnd);
                for (var i = 0; i < dummies; i++)
                {
                    Server.SpawnNpc("dummy", Team.Enemies, new Vec2(-8f + i * 0.5f, 10f + i));
                }
            }

            public int LastBytes;

            public Snapshot Step()
            {
                Server.StepTick();
                byte[] last = null;
                while (Link.ClientEnd.TryReceive(out var data))
                {
                    last = data;
                }

                LastBytes = last.Length;
                Assert.IsTrue(WireCodec.TryDecode(last, out Snapshot snapshot));
                return snapshot;
            }
        }

        [Test]
        public void TheEncodedSizeIsTheRealSize()
        {
            var snapshot = new Snapshot { Tick = 3 };
            snapshot.Entities.Add(new EntityState { Id = new EntityId(1) });
            snapshot.Entities.Add(new EntityState { Id = new EntityId(2) });
            snapshot.Events.Add(new DamageEvent());
            snapshot.Uses.Add(new UseEvent());
            snapshot.Windups.Add(new WindupState());

            Assert.AreEqual(WireCodec.Encode(snapshot).Length, WireCodec.EncodedSize(snapshot));
        }

        [Test]
        public void AConnectionThatCanCarryEverythingGetsEverything()
        {
            var setup = new Setup(30);

            var snapshot = setup.Step();

            Assert.AreEqual(31, snapshot.Entities.Count);
        }

        [Test]
        public void ASnapshotThatDoesNotFitIsCutDownToFit()
        {
            var setup = new Setup(30);
            setup.Link.MaxPayload = 700;

            var snapshot = setup.Step();

            Assert.LessOrEqual(setup.LastBytes, 700);
            Assert.Less(snapshot.Entities.Count, 31);
            Assert.Greater(snapshot.Entities.Count, 3);
        }

        [Test]
        public void ThePlayerIsAlwaysKeptAndTheNearestOthersGoFirst()
        {
            var setup = new Setup(30);
            setup.Player.Position = new Vec2(-8f, 9f);
            setup.Link.MaxPayload = 700;

            var snapshot = setup.Step();

            Assert.IsTrue(snapshot.TryFind(setup.Player.Id, out _));
            // The first dummy stands at (-8, 10), one unit from the player; the last at (6.5, 39).
            Assert.IsTrue(snapshot.Entities.Exists(e => !e.Owner.IsValid && System.Math.Abs(e.Position.Y - 10f) < 0.01f));
            Assert.IsFalse(snapshot.Entities.Exists(e => !e.Owner.IsValid && System.Math.Abs(e.Position.Y - 39f) < 0.01f));
        }

        [Test]
        public void TheSnapshotCarriesTheDungeonDetailsWhateverIsCut()
        {
            var setup = new Setup(30);
            setup.Server.StartDungeon(new DungeonDef { Id = "d", Rooms = new[] { new RoomDef { Spawns = new[] { new SpawnDef { Class = "dummy", X = 0f, Y = 10f } } } } });
            setup.Link.MaxPayload = 500;

            var snapshot = setup.Step();

            Assert.AreEqual(1, snapshot.Room);
            Assert.AreEqual(1, snapshot.RoomCount);
            Assert.AreEqual(DungeonPhase.Fighting, snapshot.Dungeon);
        }

        [Test]
        public void TwoConnectionsGetSnapshotsFittedToTheirOwnLimits()
        {
            var setup = new Setup(30);
            var second = new LoopbackLink(new SeededRandom(2), NetworkConditions.None);
            second.MaxPayload = 600;
            setup.Server.AddPlayer(new PlayerId(2), "fighter", second.ServerEnd);

            setup.Server.StepTick();
            byte[] unlimited = null;
            while (setup.Link.ClientEnd.TryReceive(out var data))
            {
                unlimited = data;
            }

            byte[] limited = null;
            while (second.ClientEnd.TryReceive(out var data))
            {
                limited = data;
            }

            Assert.IsTrue(WireCodec.TryDecode(unlimited, out Snapshot full));
            Assert.IsTrue(WireCodec.TryDecode(limited, out Snapshot cut));
            Assert.AreEqual(32, full.Entities.Count);
            Assert.Less(cut.Entities.Count, full.Entities.Count);
            Assert.LessOrEqual(limited.Length, 600);
        }

        [Test]
        public void EachConnectionStillGetsItsOwnAcknowledgedSequence()
        {
            var setup = new Setup(30);
            setup.Link.MaxPayload = 700;
            TestData.Send(setup.Link.ClientEnd, 7, CommandType.Move, new Vec2(1f, 0f));

            var snapshot = setup.Step();

            Assert.AreEqual(7, snapshot.AckedSequence);
        }

        [Test]
        public void TheUdpBudgetLeavesRoomForTheHeader()
        {
            Assert.AreEqual(1400 - Protocol.HeaderSize, new UdpClientTransport(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 1), "fighter", 0, "x").MaxPayload);
        }
    }
}
