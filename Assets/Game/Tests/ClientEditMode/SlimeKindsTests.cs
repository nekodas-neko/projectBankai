using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Client.Tests
{
    // The cave's slimes come in kinds: a plain one, a purple spitter that shoots, a red brute that is slow and tough, and a quick yellow dasher.
    public class SlimeKindsTests
    {
        static Registry<ClassDef> classes;
        static Registry<AbilityDef> abilities;

        [OneTimeSetUp]
        public void Load()
        {
            DefinitionLoader.Load(out classes, out abilities);
        }

        [Test]
        public void EachKindDiffersFromThePlainSlimeInTheWayItsNameSays()
        {
            var plain = classes.Get("slime");
            var spitter = classes.Get("slime_spitter");
            var brute = classes.Get("slime_brute");
            var dasher = classes.Get("slime_dasher");

            Assert.Greater(abilities.Get(spitter.AutoAttack).Range, 8f, "the spitter shoots from a distance");
            Assert.Less(spitter.Stats.MaxHealth, plain.Stats.MaxHealth, "and is fragile");
            Assert.GreaterOrEqual(brute.Stats.MaxHealth, plain.Stats.MaxHealth * 2.5f, "the brute is tough");
            Assert.Less(brute.Stats.MoveSpeed, plain.Stats.MoveSpeed, "and slow");
            Assert.Greater(abilities.Get(brute.AutoAttack).Radius, 2f, "and hits in a ring");
            Assert.Greater(dasher.Stats.MoveSpeed, plain.Stats.MoveSpeed * 1.4f, "the dasher is quick");
            Assert.Less(dasher.Stats.MaxHealth, plain.Stats.MaxHealth, "and fragile");
        }

        [Test]
        public void TheKindsPayMoreThanAPlainSlimeExceptTheDasher()
        {
            var plain = classes.Get("slime");

            Assert.Greater(classes.Get("slime_spitter").XpReward, plain.XpReward);
            Assert.Greater(classes.Get("slime_brute").XpReward, classes.Get("slime_spitter").XpReward);
            Assert.Greater(classes.Get("slime_brute").GoldReward, plain.GoldReward);
            Assert.Greater(classes.Get("slime_brute").PotionChance, plain.PotionChance);
            Assert.GreaterOrEqual(classes.Get("slime_dasher").XpReward, 1);
        }

        [Test]
        public void EverySectionOfTheCaveHasSeveralKindsInItsPacksWearingTheirColour()
        {
            DefinitionLoader.Load(out _, out _, out var dungeons);
            var cave = dungeons.Get("slime_cave");
            var colours = new System.Collections.Generic.Dictionary<string, string>
            {
                { "slime_spitter", "slime_purple" },
                { "slime_brute", "slime_red" },
                { "slime_dasher", "slime_yellow" },
            };

            foreach (var room in cave.Rooms)
            {
                var kinds = room.Spawns.Select(spawn => spawn.Class).Where(colours.ContainsKey).Distinct().Count();
                Assert.GreaterOrEqual(kinds, 2, room.Name + " mixes at least two kinds of slime");
                foreach (var spawn in room.Spawns.Where(spawn => colours.ContainsKey(spawn.Class)))
                {
                    Assert.AreEqual(colours[spawn.Class], spawn.Skin);
                    Assert.Greater(spawn.Pack, 0, "kinds stand in packs");
                }
            }

            Assert.GreaterOrEqual(cave.Rooms.SelectMany(room => room.Spawns).Count(spawn => spawn.Class == "slime_spitter"), 4);
            Assert.GreaterOrEqual(cave.Rooms.SelectMany(room => room.Spawns).Count(spawn => spawn.Class == "slime_brute"), 4);
            Assert.GreaterOrEqual(cave.Rooms.SelectMany(room => room.Spawns).Count(spawn => spawn.Class == "slime_dasher"), 3);
            Assert.IsTrue(cave.Rooms.SelectMany(room => room.Spawns).Any(spawn => spawn.Class == "slime" && spawn.Pack > 0), "plain slimes remain");
        }

        [Test]
        public void ASpitterShootsFromADistanceAndKeepsItWhileABruteComesClose()
        {
            var session = new LocalSession(new ServerConfig(), classes, abilities, 1);
            session.AddPlayer(new PlayerId(1), "sword_and_shield");
            var player = session.Server.FindPlayerEntity(new PlayerId(1));
            var spitter = session.Server.SpawnNpc("slime_spitter", Team.Enemies, player.Position + new Vec2(0f, 7f));
            var brute = session.Server.SpawnNpc("slime_brute", Team.Enemies, player.Position + new Vec2(4f, 0f));

            session.Advance(4f, 1f / 30f);

            Assert.Greater((spitter.Position - player.Position).Length, 5f, "the spitter stays back and shoots");
            Assert.Less((brute.Position - player.Position).Length, 3.5f, "the brute walks up to hit");
            Assert.Less(player.Health, player.Stats.MaxHealth, "and they hurt");
        }

        [Test]
        public void ADasherReachesAPlayerSoonerThanAPlainSlime()
        {
            float SecondsToReach(string kind)
            {
                var session = new LocalSession(new ServerConfig(), classes, abilities, 1);
                session.AddPlayer(new PlayerId(1), "staff");
                var player = session.Server.FindPlayerEntity(new PlayerId(1));
                var enemy = session.Server.SpawnNpc(kind, Team.Enemies, player.Position + new Vec2(0f, 4.4f));
                for (var i = 0; i < 300; i++)
                {
                    session.Advance(1f / 30f, 1f / 30f);
                    if ((enemy.Position - player.Position).Length < 2.5f)
                    {
                        return i / 30f;
                    }
                }

                return 10f;
            }

            Assert.Less(SecondsToReach("slime_dasher"), SecondsToReach("slime"));
        }
    }
}
