using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // Actions the player takes are shown at once on their own screen; the server still decides, and its echo is matched so an
    // effect is never played twice.
    public class PredictionTests
    {
        const float Step = 1f / 30f;

        static (LocalSession session, GameClient client, Entity player) Start(float latency = 0f, string weapon = "rogue")
        {
            var session = new LocalSession(new ServerConfig(), TestData.Classes(), TestData.Abilities(), 1, new NetworkConditions { LatencySeconds = latency });
            var client = session.AddPlayer(new PlayerId(1), weapon);
            var player = session.Server.FindPlayerEntity(new PlayerId(1));
            session.Advance(1f, Step);
            return (session, client, player);
        }

        static List<UseEvent> Uses(GameClient client)
        {
            var list = new List<UseEvent>();
            client.DrainUses(list);
            return list;
        }

        [Test]
        public void TheServersUseEventCarriesTheActionIdOfTheCommand()
        {
            var (session, client, _) = Start();

            var id = client.Attack(new Vec2(0f, 1f));
            session.Advance(0.2f, Step);

            var uses = Uses(client);
            Assert.AreEqual(1, uses.Count);
            Assert.AreEqual(id, uses[0].Sequence);
        }

        [Test]
        public void AnActionThePlayerAlreadyShowedIsNotPlayedAgainWhenTheServerEchoesIt()
        {
            var (session, client, _) = Start();

            client.ExpectEcho(client.Attack(new Vec2(0f, 1f)));
            session.Advance(0.2f, Step);

            Assert.IsEmpty(Uses(client), "the echo is swallowed");
            var rejected = new List<int>();
            client.DrainRejected(rejected);
            Assert.IsEmpty(rejected, "and it was not rejected");
        }

        [Test]
        public void AnActionTheServerRefusesIsReportedAsRejected()
        {
            var (session, client, _) = Start();
            client.ExpectEcho(client.Attack(new Vec2(0f, 1f)));
            var second = client.Attack(new Vec2(0f, 1f));
            client.ExpectEcho(second);

            session.Advance(0.2f, Step);

            var rejected = new List<int>();
            client.DrainRejected(rejected);
            CollectionAssert.AreEqual(new[] { second }, rejected, "the second attack was still on cooldown");
        }

        [Test]
        public void AnActionThatGetsNoAnswerAtAllTimesOut()
        {
            var (session, client, _) = Start();
            client.ExpectEcho(9999);

            session.Advance(1.5f, Step);

            var rejected = new List<int>();
            client.DrainRejected(rejected);
            CollectionAssert.AreEqual(new[] { 9999 }, rejected);
        }

        [Test]
        public void SomeoneElsesUseWithTheSameNumberIsNotMistakenForOurEcho()
        {
            var session = new LocalSession(new ServerConfig(), TestData.Classes(), TestData.Abilities(), 1);
            var mine = session.AddPlayer(new PlayerId(1), "fighter");
            var theirs = session.AddPlayer(new PlayerId(2), "fighter");
            session.Advance(0.3f, Step);

            mine.ExpectEcho(theirs.Attack(new Vec2(0f, 1f)));
            session.Advance(0.2f, Step);

            var seen = Uses(mine);
            Assert.AreEqual(1, seen.Count, "the other player's attack still shows on my screen");
            Assert.AreNotEqual(mine.LocalEntity, seen[0].User);
        }

        [Test]
        public void ADodgeStartsMovingOnTheClientBeforeTheServerHasHeardOfIt()
        {
            var (session, client, player) = Start(0.3f);
            var start = client.PredictedPosition;
            var serverStart = player.Position;

            client.Defend(new Vec2(1f, 0f), 1f, 0.5f);
            session.Advance(0.2f, Step);

            Assert.Greater(client.PredictedPosition.X - start.X, 0.3f, "the client is already dashing");
            Assert.AreEqual(serverStart.X, player.Position.X, 0.001f, "the command is still on its way");
        }

        [Test]
        public void ThePredictedDodgeSettlesOnTheServersPosition()
        {
            var (session, client, player) = Start(0.2f);
            var startX = player.Position.X;

            client.Defend(new Vec2(1f, 0f), 1f, 0.5f);
            session.Advance(3f, Step);

            Assert.IsFalse(client.IsDashing, "settled");
            Assert.Greater(player.Position.X - startX, 0.5f, "the server dashed");
            Assert.AreEqual(player.Position.X, client.PredictedPosition.X, 0.1f);
            Assert.AreEqual(player.Position.Y, client.PredictedPosition.Y, 0.1f);
        }

        [Test]
        public void TheClientDoesNotJumpBackWhileAPredictedDodgeWaitsForTheServer()
        {
            var (session, client, _) = Start(0.2f);
            var previous = client.PredictedPosition.X;
            client.Defend(new Vec2(1f, 0f), 1f, 0.5f);

            for (var i = 0; i < 90; i++)
            {
                session.Advance(Step, Step);
                Assert.GreaterOrEqual(client.PredictedPosition.X, previous - 0.001f, $"stepped back at tick {i}");
                previous = client.PredictedPosition.X;
            }
        }

        [Test]
        public void ARefusedDodgeSnapsBackToWhereTheServerIs()
        {
            var (session, client, player) = Start(0.1f);
            client.Defend(new Vec2(1f, 0f), 1f, 0.5f);
            session.Advance(1f, Step);
            session.Advance(3f, Step);
            var firstEnd = player.Position;

            player.Cooldowns[Entity.DefenseSlot] = 100f;
            client.Defend(new Vec2(-1f, 0f), 1f, 0.5f);
            session.Advance(0.15f, Step);
            Assert.Less(client.PredictedPosition.X, firstEnd.X - 0.1f, "it started moving at once");

            session.Advance(2f, Step);

            Assert.IsFalse(client.IsDashing);
            Assert.AreEqual(firstEnd.X, player.Position.X, 0.001f, "the server refused");
            Assert.AreEqual(firstEnd.X, client.PredictedPosition.X, 0.1f, "so the client came back");
        }

        [Test]
        public void TheUseSequenceSurvivesTheWire()
        {
            var snapshot = new Snapshot { Tick = 1 };
            snapshot.Uses.Add(new UseEvent { User = new EntityId(2), Slot = 1, Ability = 5, Variant = 6, Direction = new Vec2(0f, 1f), Sequence = 77 });

            Assert.IsTrue(WireCodec.TryDecode(WireCodec.Encode(snapshot), out Snapshot decoded));

            Assert.AreEqual(77, decoded.Uses[0].Sequence);
        }
    }
}
