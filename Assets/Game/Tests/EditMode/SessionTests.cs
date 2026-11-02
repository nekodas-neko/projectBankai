using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class SessionTests
    {
        const float Step = 1f / 30f;

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        public void EveryClient_SeesEveryPlayer(int count)
        {
            var session = TestData.Session(count);

            session.Advance(0.5f, Step);

            var seen = new HashSet<int>();
            foreach (var client in session.Clients)
            {
                Assert.IsTrue(client.HasLocalEntity);
                Assert.AreEqual(count, client.Latest.Entities.Count);
                seen.Add(client.LocalEntity.Value);
            }

            Assert.AreEqual(count, seen.Count);
        }

        [Test]
        public void Prediction_MovesLocalPlayerBeforeServerReplies()
        {
            var session = TestData.Session(1, conditions: new NetworkConditions { LatencySeconds = 0.2f });
            var client = session.Clients[0];
            session.Advance(0.6f, Step);
            var start = client.PredictedPosition;
            var serverStart = session.Server.FindPlayerEntity(client.Player).Position;

            client.SetMoveInput(new Vec2(1f, 0f));
            session.Advance(0.1f, Step);

            Assert.Greater(client.PredictedPosition.X - start.X, 0.4f);
            Assert.AreEqual(serverStart, session.Server.FindPlayerEntity(client.Player).Position);
        }

        [Test]
        public void Reconciliation_MatchesServerOnceInputStops()
        {
            var session = TestData.Session(1, conditions: new NetworkConditions { LatencySeconds = 0.1f });
            var client = session.Clients[0];
            session.Advance(0.5f, Step);

            client.SetMoveInput(new Vec2(1f, 0.5f));
            session.Advance(1.5f, Step);
            client.SetMoveInput(Vec2.Zero);
            session.Advance(1f, Step);

            var server = session.Server.FindPlayerEntity(client.Player).Position;
            Assert.AreEqual(server.X, client.PredictedPosition.X, 1e-3f);
            Assert.AreEqual(server.Y, client.PredictedPosition.Y, 1e-3f);
            Assert.AreEqual(0, client.PendingMoveCount);
        }

        [Test]
        public void Reconciliation_ConvergesUnderLatencyJitterAndLoss()
        {
            var conditions = new NetworkConditions { LatencySeconds = 0.1f, JitterSeconds = 0.05f, LossRate = 0.2f };
            var session = TestData.Session(2, seed: 11, conditions: conditions);
            session.Advance(1f, Step);

            session.Clients[0].SetMoveInput(new Vec2(1f, 0f));
            session.Clients[1].SetMoveInput(new Vec2(0f, 1f));
            session.Advance(2f, Step);
            session.Clients[0].SetMoveInput(Vec2.Zero);
            session.Clients[1].SetMoveInput(Vec2.Zero);
            session.SetConditions(NetworkConditions.None);
            session.Advance(2f, Step);

            foreach (var client in session.Clients)
            {
                var server = session.Server.FindPlayerEntity(client.Player).Position;
                Assert.AreEqual(server.X, client.PredictedPosition.X, 1e-3f);
                Assert.AreEqual(server.Y, client.PredictedPosition.Y, 1e-3f);
            }
        }

        [Test]
        public void Combat_RunsThroughTheServerAndReportsEvents()
        {
            var session = TestData.Session(1);
            var client = session.Clients[0];
            session.Advance(0.3f, Step);
            var origin = session.Server.FindPlayerEntity(client.Player).Position;
            var dummy = session.Server.SpawnNpc("dummy", Team.Enemies, origin + new Vec2(2f, 0f));

            client.Attack(Vec2.Zero);
            session.Advance(0.3f, Step);

            var events = new List<DamageEvent>();
            client.DrainEvents(events);
            Assert.AreEqual(90f, dummy.Health);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(dummy.Id, events[0].Target);
            Assert.IsTrue(client.Latest.TryFind(dummy.Id, out var state));
            Assert.AreEqual(90f, state.Health);
        }

        [Test]
        public void RemotePlayers_AreInterpolatedBetweenSnapshots()
        {
            var session = TestData.Session(2);
            session.Advance(0.5f, Step);
            var observer = session.Clients[0];
            var mover = session.Clients[1];
            var remote = mover.LocalEntity;

            mover.SetMoveInput(new Vec2(1f, 0f));
            session.Advance(0.5f, Step);

            Assert.IsTrue(observer.TrySampleRemote(remote, out var sampled));
            observer.Latest.TryFind(remote, out var latest);
            Assert.Less(sampled.X, latest.Position.X);
            Assert.Greater(sampled.X, latest.Position.X - 1f);
        }

        [Test]
        public void SameSeedAndInputs_ProduceIdenticalResults()
        {
            byte[] Run()
            {
                var conditions = new NetworkConditions { LatencySeconds = 0.08f, JitterSeconds = 0.04f, LossRate = 0.1f };
                var session = TestData.Session(2, seed: 99, conditions: conditions);
                session.Server.SpawnNpc("dummy", Team.Enemies, new Vec2(0f, 2f));
                session.Advance(0.5f, Step);
                session.Clients[0].SetMoveInput(new Vec2(0.5f, 1f));
                session.Clients[1].SetMoveInput(new Vec2(-1f, 0f));
                for (var i = 0; i < 60; i++)
                {
                    if (i % 10 == 0)
                    {
                        session.Clients[0].Attack(Vec2.Zero);
                        session.Clients[1].Skill(1, new Vec2(0f, 1f));
                    }

                    session.Advance(Step);
                }

                return WireCodec.Encode(session.Clients[0].Latest);
            }

            CollectionAssert.AreEqual(Run(), Run());
        }
    }
}
