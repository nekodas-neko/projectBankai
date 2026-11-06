using Game.Core;
using NUnit.Framework;

namespace Game.Client.Tests
{
    // After a wipe the party starts again from the room of the last boss it killed (issue 49). In the shipped cave that spot
    // must be a quiet one: no enemy of the sections still to do is close enough to notice the party straight away.
    public class CaveCheckpointTests
    {
        static Registry<ClassDef> classes;
        static Registry<AbilityDef> abilities;
        static Registry<DungeonDef> dungeons;

        [OneTimeSetUp]
        public void Load()
        {
            DefinitionLoader.Load(out classes, out abilities, out dungeons);
        }

        [Test]
        public void AfterEachBossTheCheckpointIsOutOfReachOfEveryEnemyStillToCome()
        {
            var cave = dungeons.Get("slime_cave");
            var session = new LocalSession(new ServerConfig(), classes, abilities, 1);
            var client = session.AddPlayer(new PlayerId(1), "sword_and_shield");
            session.AddPlayer(new PlayerId(2), "staff");
            session.AddPlayer(new PlayerId(3), "healing_mace");
            session.StartDungeon(cave);
            var server = session.Server;

            for (var boss = 0; boss < cave.Rooms.Length - 1; boss++)
            {
                foreach (var e in server.Entities)
                {
                    if (!e.Owner.IsValid && e.IsBoss && e.Section == boss)
                    {
                        e.Health = 0f;
                    }
                }

                session.Advance(cave.ClearDelaySeconds + 1f, 1f / 30f);
                Assert.AreEqual(boss + 1, server.Map.Progress, $"boss {boss} is down");

                foreach (var e in server.Entities)
                {
                    if (e.Owner.IsValid)
                    {
                        e.Health = 0f;
                    }
                }

                session.Advance(0.2f, 1f / 30f);
                Assert.AreEqual(DungeonPhase.Defeat, server.DungeonPhase);
                client.Restart();
                session.Advance(0.1f, 1f / 30f);

                Assert.AreEqual(DungeonPhase.Fighting, server.DungeonPhase);
                Assert.AreEqual(boss + 1, server.Map.Progress, "the gates stay open");
                foreach (var player in server.Entities)
                {
                    if (!player.Owner.IsValid)
                    {
                        continue;
                    }

                    Assert.IsTrue(player.IsAlive);
                    Assert.IsTrue(server.Map.Contains(player.Position), "the party stands on open ground");
                    foreach (var enemy in server.Entities)
                    {
                        if (enemy.Owner.IsValid || !enemy.IsAlive)
                        {
                            continue;
                        }

                        Assert.Greater(enemy.Section, boss, "nothing of the sections already won is put back");
                        var distance = (enemy.Position - player.Position).Length;
                        Assert.Greater(distance, enemy.AggroRange + 1f, $"{enemy.ClassId} in section {enemy.Section} would notice the party at once");
                    }
                }
            }
        }
    }
}
