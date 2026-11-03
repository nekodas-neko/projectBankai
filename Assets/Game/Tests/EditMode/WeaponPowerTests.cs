using NUnit.Framework;

namespace Game.Core.Tests
{
    public class WeaponPowerTests
    {
        static float PowerOf(float requested)
        {
            var session = new LocalSession(new ServerConfig(), TestData.Classes(), TestData.Abilities(), 1);
            session.AddPlayer(new PlayerId(1), "fighter", 0, null, requested);
            return session.Server.FindPlayerEntity(new PlayerId(1)).Stats.Power;
        }

        [Test]
        public void ThePowerScaleMultipliesTheClassPower()
        {
            Assert.AreEqual(1f, PowerOf(1f), 0.0001f);
            Assert.AreEqual(1.1f, PowerOf(1.1f), 0.0001f);
            Assert.AreEqual(0.9f, PowerOf(0.9f), 0.0001f);
        }

        [Test]
        public void TheServerClampsWhatAClientSends()
        {
            Assert.AreEqual(GameServer.MaxWeaponPower, PowerOf(50f), 0.0001f);
            Assert.AreEqual(GameServer.MinWeaponPower, PowerOf(0f), 0.0001f);
            Assert.AreEqual(GameServer.MinWeaponPower, PowerOf(-3f), 0.0001f);
            Assert.AreEqual(1f, PowerOf(float.NaN), 0.0001f);
        }

        [Test]
        public void AStrongerWeaponHitsHarder()
        {
            float Damage(float power)
            {
                var session = new LocalSession(new ServerConfig(), TestData.Classes(), TestData.Abilities(), 1);
                var client = session.AddPlayer(new PlayerId(1), "fighter", 0, null, power);
                var player = session.Server.FindPlayerEntity(new PlayerId(1));
                var dummy = session.Server.SpawnNpc("dummy", Team.Enemies, player.Position + new Vec2(0f, 2f));
                var before = dummy.Health;
                client.Attack(new Vec2(0f, 1f));
                session.Advance(0.5f, 1f / 30f);
                return before - dummy.Health;
            }

            Assert.Greater(Damage(1.2f), Damage(1f) * 1.15f);
            Assert.Greater(Damage(1f), 0f);
        }

        [Test]
        public void PowerToWireRoundsToThousandthsAndNeverOverflows()
        {
            Assert.AreEqual(1000, Game.Net.Protocol.PowerToWire(1f));
            Assert.AreEqual(1150, Game.Net.Protocol.PowerToWire(1.15f));
            Assert.AreEqual(0, Game.Net.Protocol.PowerToWire(-2f));
            Assert.AreEqual(ushort.MaxValue, Game.Net.Protocol.PowerToWire(1e9f));
        }
    }
}
