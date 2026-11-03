using NUnit.Framework;

namespace Game.Core.Tests
{
    // A dungeon makes its enemies tougher with every extra player, so a party does not finish a boss in seconds.
    public class PartyScaleTests
    {
        static DungeonDef Dungeon(float perExtraPlayer)
        {
            return new DungeonDef
            {
                Id = "scaled",
                HealthPerExtraPlayer = perExtraPlayer,
                Rooms = new[] { new RoomDef { Spawns = new[] { new SpawnDef { Class = "grunt", X = 0f, Y = 20f } } } },
            };
        }

        static GameServer Start(float perExtraPlayer, int players)
        {
            var random = new SeededRandom(1);
            var server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
            for (var i = 0; i < players; i++)
            {
                server.AddPlayer(new PlayerId(i + 1), "fighter", new LoopbackLink(random, NetworkConditions.None).ServerEnd);
            }

            server.StartDungeon(Dungeon(perExtraPlayer));
            server.StepTick();
            return server;
        }

        static Entity Grunt(GameServer server)
        {
            foreach (var entity in server.Entities)
            {
                if (!entity.Owner.IsValid && entity.ClassId == "grunt")
                {
                    return entity;
                }
            }

            return null;
        }

        [Test]
        public void ASoloPlayerMeetsTheNormalHealth()
        {
            var server = Start(0.8f, 1);

            Assert.AreEqual(100f, Grunt(server).Stats.MaxHealth, 0.01f);
        }

        [Test]
        public void EachExtraPlayerAddsToTheEnemyHealth()
        {
            var server = Start(0.8f, 3);

            var grunt = Grunt(server);
            Assert.AreEqual(100f * (1f + 0.8f * 2f), grunt.Stats.MaxHealth, 0.01f);
            Assert.AreEqual(grunt.Stats.MaxHealth, grunt.Health, 0.01f);
        }

        [Test]
        public void ADungeonThatAsksForNoScalingKeepsTheHealth()
        {
            var server = Start(0f, 3);

            Assert.AreEqual(100f, Grunt(server).Stats.MaxHealth, 0.01f);
        }

        [Test]
        public void SomeoneJoiningLaterRaisesTheHealthAndKeepsTheShareLeft()
        {
            var server = Start(1f, 1);
            var grunt = Grunt(server);
            grunt.Health = 40f;

            var random = new SeededRandom(2);
            server.AddPlayer(new PlayerId(2), "fighter", new LoopbackLink(random, NetworkConditions.None).ServerEnd);
            server.StepTick();

            Assert.AreEqual(200f, grunt.Stats.MaxHealth, 0.01f);
            Assert.AreEqual(80f, grunt.Health, 0.01f, "40% of the old maximum is still 40% of the new one");
        }

        [Test]
        public void SomeoneLeavingLowersItAgain()
        {
            var server = Start(1f, 2);
            var grunt = Grunt(server);
            Assert.AreEqual(200f, grunt.Stats.MaxHealth, 0.01f);

            server.RemovePlayer(new PlayerId(2));
            server.StepTick();

            Assert.AreEqual(100f, grunt.Stats.MaxHealth, 0.01f);
        }

        [Test]
        public void ThingsSpawnedLaterAreScaledToo()
        {
            var server = Start(1f, 2);

            var late = server.SpawnNpc("grunt", Team.Enemies, new Vec2(5f, 20f));
            server.StepTick();

            Assert.AreEqual(200f, late.Stats.MaxHealth, 0.01f);
        }
    }
}
