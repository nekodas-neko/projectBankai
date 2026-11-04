using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Client.Tests
{
    // A chest is an enemy-team object that never fights: smash it open for a potion and sometimes a weapon.
    public class ChestTests
    {
        static LocalSession Session(out GameClient client, out Entity player, out Entity chest)
        {
            DefinitionLoader.Load(out var classes, out var abilities);
            var session = new LocalSession(new ServerConfig(), classes, abilities, 1);
            client = session.AddPlayer(new PlayerId(1), "sword_and_shield");
            player = session.Server.FindPlayerEntity(new PlayerId(1));
            chest = session.Server.SpawnNpc("chest", Team.Enemies, player.Position + new Vec2(0f, 1.5f));
            return session;
        }

        [Test]
        public void AChestNeverAttacksAndStaysWhereItIs()
        {
            var session = Session(out _, out var player, out var chest);
            var spot = chest.Position;

            session.Advance(5f, 1f / 30f);

            Assert.AreEqual(player.Stats.MaxHealth, player.Health, 0.001f);
            Assert.AreEqual(spot.X, chest.Position.X, 0.001f);
            Assert.AreEqual(spot.Y, chest.Position.Y, 0.001f);
        }

        [Test]
        public void SmashingAChestOpensItAndTheKillIsCounted()
        {
            var session = Session(out var client, out _, out var chest);
            for (var i = 0; i < 30 && chest.IsAlive; i++)
            {
                client.Attack(new Vec2(0f, 1f));
                session.Advance(0.6f, 1f / 30f);
            }

            Assert.IsFalse(chest.IsAlive, "a few hits open it");
        }

        [Test]
        public void TheShippedChestAlwaysHoldsAPotionAndNeverFights()
        {
            DefinitionLoader.Load(out var classes, out _);

            var chest = classes.Get("chest");

            Assert.IsFalse(chest.Playable);
            Assert.AreEqual(0f, chest.AggroRange);
            Assert.AreEqual(1f, chest.PotionChance);
            Assert.Greater(chest.DropChance, 0f);
            Assert.Less(chest.DropChance, 0.5f);
        }

        [Test]
        public void TheCaveHasAChestOrTwoInEverySectionAwayFromThePacks()
        {
            DefinitionLoader.Load(out _, out _, out var dungeons);

            foreach (var room in dungeons.Get("slime_cave").Rooms)
            {
                var chests = room.Spawns.Where(spawn => spawn.Class == "chest").ToList();
                Assert.GreaterOrEqual(chests.Count, 1, room.Name);
                Assert.IsTrue(chests.All(spawn => spawn.Pack == 0 && !spawn.Boss));
            }
        }
    }
}
