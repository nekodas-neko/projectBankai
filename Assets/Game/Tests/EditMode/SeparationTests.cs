using NUnit.Framework;

namespace Game.Core.Tests
{
    public class SeparationTests
    {
        static (GameServer server, LoopbackLink link, Entity player) Setup()
        {
            var random = new SeededRandom(1);
            var server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
            var link = new LoopbackLink(random, NetworkConditions.None);
            return (server, link, server.AddPlayer(new PlayerId(1), "fighter", link.ServerEnd));
        }

        static void Tick(GameServer server, int count)
        {
            for (var i = 0; i < count; i++)
            {
                server.StepTick();
            }
        }

        [Test]
        public void OverlappingBodiesArePushedApart()
        {
            var (server, _, _) = Setup();
            var a = server.SpawnNpc("bulky", Team.Enemies, new Vec2(5f, 5f));
            var b = server.SpawnNpc("bulky", Team.Enemies, new Vec2(5.2f, 5f));

            Tick(server, 2);

            Assert.GreaterOrEqual((b.Position - a.Position).Length, 1f - 1e-3f);
        }

        [Test]
        public void ExactlyOverlappingBodiesStillSeparate()
        {
            var (server, _, _) = Setup();
            var a = server.SpawnNpc("bulky", Team.Enemies, new Vec2(5f, 5f));
            var b = server.SpawnNpc("bulky", Team.Enemies, new Vec2(5f, 5f));

            Tick(server, 2);

            Assert.GreaterOrEqual((b.Position - a.Position).Length, 1f - 1e-3f);
        }

        [Test]
        public void BodiesWithNoRadiusAreLeftAlone()
        {
            var (server, _, player) = Setup();
            var other = server.SpawnNpc("dummy", Team.Enemies, player.Position);

            Tick(server, 2);

            Assert.AreEqual(player.Position, other.Position);
        }

        [Test]
        public void ADashingEntityPassesThrough()
        {
            var (server, _, _) = Setup();
            var a = server.SpawnNpc("bulky", Team.Enemies, new Vec2(5f, 5f));
            var b = server.SpawnNpc("bulky", Team.Enemies, new Vec2(5.2f, 5f));
            a.AddStatus(StatusType.Dashing, 1f, 0f);

            Tick(server, 2);

            Assert.AreEqual(new Vec2(5f, 5f), a.Position);
            Assert.AreEqual(new Vec2(5.2f, 5f), b.Position);
        }

        [Test]
        public void ABigEnemyStopsAtItsBodyAndStillHitsTheTarget()
        {
            var (server, _, player) = Setup();
            var giant = server.SpawnNpc("giant", Team.Enemies, new Vec2(-3f, 14f));

            Tick(server, 240);

            Assert.GreaterOrEqual((giant.Position - player.Position).Length, 1.8f - 1e-3f);
            Assert.Less(player.Health, 100f);
        }

        [Test]
        public void AttacksReachTheEdgeOfABody()
        {
            var (server, link, player) = Setup();
            var near = server.SpawnNpc("bulky", Team.Enemies, player.Position + new Vec2(0f, 3.4f));
            var far = server.SpawnNpc("bulky", Team.Enemies, player.Position + new Vec2(3.5f, 3.5f));

            TestData.Send(link.ClientEnd, 1, CommandType.Attack, new Vec2(0f, 1f));
            Tick(server, 1);

            Assert.Less(near.Health, 100f);
            Assert.AreEqual(100f, far.Health);
        }

        [Test]
        public void ACircleReachesTheEdgeOfABody()
        {
            var random = new SeededRandom(1);
            var server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
            var link = new LoopbackLink(random, NetworkConditions.None);
            var caster = server.AddPlayer(new PlayerId(1), "shaman", link.ServerEnd);
            var inside = server.SpawnNpc("bulky", Team.Enemies, caster.Position + new Vec2(4.4f, 0f));
            var outside = server.SpawnNpc("bulky", Team.Enemies, caster.Position + new Vec2(-4.6f, 0f));

            TestData.Send(link.ClientEnd, 1, CommandType.Attack, new Vec2(1f, 0f));
            Tick(server, 12);

            Assert.Less(inside.Health, 100f);
            Assert.AreEqual(100f, outside.Health);
        }
    }
}
