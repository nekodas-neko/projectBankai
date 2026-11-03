using NUnit.Framework;

namespace Game.Core.Tests
{
    // The snapshot tells clients how far each enemy notices a player, so the screen can show it.
    public class AggroSnapshotTests
    {
        [Test]
        public void TheAggroRangeSurvivesTheWire()
        {
            var snapshot = new Snapshot { Tick = 1 };
            snapshot.Entities.Add(new EntityState { Id = new EntityId(7), AggroQuarters = 38 });

            Assert.IsTrue(WireCodec.TryDecode(WireCodec.Encode(snapshot), out Snapshot decoded));

            Assert.AreEqual(38, decoded.Entities[0].AggroQuarters);
            Assert.AreEqual(9.5f, decoded.Entities[0].AggroRange, 0.001f);
        }

        [Test]
        public void AnEnemyReportsItsRangeAndAPlayerReportsNone()
        {
            var random = new SeededRandom(1);
            var server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
            var link = new LoopbackLink(random, NetworkConditions.None);
            server.AddPlayer(new PlayerId(1), "fighter", link.ServerEnd);
            server.SpawnNpc("grunt", Team.Enemies, new Vec2(0f, 30f));
            server.SpawnNpc("dummy", Team.Enemies, new Vec2(5f, 30f));

            server.StepTick();
            byte[] last = null;
            while (link.ClientEnd.TryReceive(out var data))
            {
                last = data;
            }

            Assert.IsTrue(WireCodec.TryDecode(last, out Snapshot snapshot));
            var ranges = new System.Collections.Generic.List<float>();
            foreach (var entity in snapshot.Entities)
            {
                ranges.Add(entity.AggroRange);
            }

            CollectionAssert.AreEquivalent(new[] { 0f, 8f, 0f }, ranges);
        }
    }
}
