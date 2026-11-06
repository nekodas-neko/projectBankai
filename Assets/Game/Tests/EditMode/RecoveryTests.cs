using NUnit.Framework;

namespace Game.Core.Tests
{
    // A boss with a recovery time stays planted after each attack instead of chasing and swinging without a break.
    public class RecoveryTests
    {
        static (GameServer server, Entity player, Entity boss) Start(string boss)
        {
            var random = new SeededRandom(1);
            var server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
            var player = server.AddPlayer(new PlayerId(1), "fighter", new LoopbackLink(random, NetworkConditions.None).ServerEnd);
            player.Position = new Vec2(0f, 0f);
            var npc = server.SpawnNpc(boss, Team.Enemies, new Vec2(0f, 2f));
            return (server, player, npc);
        }

        [Test]
        public void ABossStaysPlantedAfterAnAttackAndThenChasesAgain()
        {
            var (server, player, boss) = Start("lurcher");
            server.StepTick();
            Assert.Greater(player.Stats.MaxHealth - player.Health, 0f, "the first swing should have landed");

            player.Position = new Vec2(0f, 12f);
            var planted = boss.Position;
            for (var i = 0; i < 40; i++)
            {
                player.Position = new Vec2(0f, 12f);
                server.StepTick();
            }

            Assert.AreEqual(planted.Y, boss.Position.Y, 0.001f, "still recovering after about 1.3 s");

            for (var i = 0; i < 60; i++)
            {
                player.Position = new Vec2(0f, 12f);
                server.StepTick();
            }

            Assert.Greater(boss.Position.Y, planted.Y + 0.5f, "recovery is over, so it walks after the player");
        }

        [Test]
        public void AnEnemyWithNoRecoveryChasesAtOnce()
        {
            var (server, player, boss) = Start("grunt");
            player.Position = new Vec2(0f, 10f);
            var start = boss.Position;

            for (var i = 0; i < 10; i++)
            {
                server.StepTick();
            }

            Assert.Greater(boss.Position.Y, start.Y + 0.5f);
        }
    }
}
