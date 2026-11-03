using NUnit.Framework;

namespace Game.Core.Tests
{
    public class BossTargetTests
    {
        sealed class Setup
        {
            public readonly GameServer Server;
            public readonly LoopbackLink Link;
            public readonly Entity First;
            public readonly Entity Second;
            public readonly Entity Boss;
            int sequence;

            public Setup()
            {
                var random = new SeededRandom(1);
                Server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
                Link = new LoopbackLink(random, NetworkConditions.None);
                First = Server.AddPlayer(new PlayerId(1), "fighter", Link.ServerEnd);
                var other = new LoopbackLink(random, NetworkConditions.None);
                Second = Server.AddPlayer(new PlayerId(2), "warden", other.ServerEnd);
                Boss = Server.SpawnNpc("brute", Team.Enemies, new Vec2(0f, 9f));
                Boss.IsBoss = true;
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
                TestData.Send(Link.ClientEnd, ++sequence, type, new Vec2(0f, 1f));
                Server.StepTick();
            }

            public EntityState StateOf(Entity entity)
            {
                Snapshot latest = null;
                while (Link.ClientEnd.TryReceive(out var data))
                {
                    if (WireCodec.TryDecode(data, out Snapshot snapshot))
                    {
                        latest = snapshot;
                    }
                }

                Assert.IsNotNull(latest);
                Assert.IsTrue(latest.TryFind(entity.Id, out var state));
                return state;
            }
        }

        [Test]
        public void AnIdleBossHasNoTarget()
        {
            var setup = new Setup();
            setup.First.Position = new Vec2(-19f, -5f);
            setup.Second.Position = new Vec2(19f, -5f);

            setup.Tick(3);

            Assert.IsFalse(setup.StateOf(setup.Boss).Target.IsValid);
        }

        [Test]
        public void ABossReportsWhoItIsFightingAndItIsMarkedAsABoss()
        {
            var setup = new Setup();
            setup.First.Position = new Vec2(0f, 6f);
            setup.Second.Position = new Vec2(19f, -5f);

            setup.Tick(3);
            var state = setup.StateOf(setup.Boss);

            Assert.AreEqual(setup.First.Id, state.Target);
            Assert.AreNotEqual(0, state.Effects & EffectFlags.Boss);
        }

        [Test]
        public void AnOrdinaryEnemyIsNotMarkedAsABoss()
        {
            var setup = new Setup();
            setup.Boss.IsBoss = false;

            setup.Tick(2);

            Assert.AreEqual(0, setup.StateOf(setup.Boss).Effects & EffectFlags.Boss);
        }

        [Test]
        public void ThreatMovesTheBossToTheOtherPlayer()
        {
            var setup = new Setup();
            setup.First.Position = new Vec2(0f, 6f);
            setup.Second.Position = new Vec2(2f, 6f);
            setup.Tick(2);
            Assert.AreEqual(setup.First.Id, setup.StateOf(setup.Boss).Target);

            setup.Boss.AddThreat(setup.Second.Id, 500f);
            setup.Tick(2);

            Assert.AreEqual(setup.Second.Id, setup.StateOf(setup.Boss).Target);
        }

        [Test]
        public void ADeadBossHasNoTarget()
        {
            var setup = new Setup();
            setup.First.Position = new Vec2(0f, 6f);
            setup.Tick(2);
            setup.Boss.Health = 0f;

            setup.Tick(1);

            Assert.IsFalse(setup.StateOf(setup.Boss).Target.IsValid);
        }

        [Test]
        public void Codec_RoundTripsTheTarget()
        {
            var snapshot = new Snapshot { Tick = 1 };
            snapshot.Entities.Add(new EntityState { Id = new EntityId(4), Target = new EntityId(9), Effects = EffectFlags.Boss });

            Assert.IsTrue(WireCodec.TryDecode(WireCodec.Encode(snapshot), out Snapshot decoded));

            Assert.AreEqual(new EntityId(9), decoded.Entities[0].Target);
            Assert.AreEqual(EffectFlags.Boss, decoded.Entities[0].Effects);
        }
    }
}
