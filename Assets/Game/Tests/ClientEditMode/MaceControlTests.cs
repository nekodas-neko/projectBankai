using Game.Core;
using NUnit.Framework;

namespace Game.Client.Tests
{
    // The healing mace is the one weapon that used to have no answer to adds and casts; its pulse stuns small enemies and breaks casts.
    public class MaceControlTests
    {
        [Test]
        public void TheMacePulseStunsASlimeButNotABoss()
        {
            DefinitionLoader.Load(out var classes, out var abilities);
            var session = new LocalSession(new ServerConfig(), classes, abilities, 1);
            var client = session.AddPlayer(new PlayerId(1), "healing_mace");
            var player = session.Server.FindPlayerEntity(new PlayerId(1));
            var slime = session.Server.SpawnNpc("slime", Team.Enemies, player.Position + new Vec2(0f, 2.5f));
            var other = session.Server.SpawnNpc("slime", Team.Enemies, player.Position + new Vec2(1f, 2.5f));
            other.IsBoss = true;

            client.Skill(1, new Vec2(0f, 1f));
            session.Advance(0.2f, 1f / 30f);

            Assert.IsTrue(slime.HasStatus(StatusType.Staggered), "the slime is stunned");
            Assert.IsFalse(other.HasStatus(StatusType.Staggered), "the boss is not");
        }

        [Test]
        public void TheMaceUltimateIsNotTheFastestToCharge()
        {
            DefinitionLoader.Load(out _, out var abilities);

            var mace = abilities.Get("radiant_burst");
            var tank = abilities.Get("bulwark_crash");

            Assert.LessOrEqual(mace.ChargeFromDealt, 1.3f);
            Assert.Less(mace.ChargeFromDealt / mace.ChargeRequired, tank.ChargeFromDealt / tank.ChargeRequired * 1.4f);
        }
    }
}
