using Game.Core;
using NUnit.Framework;

namespace Game.Client.Tests
{
    public class DefinitionLoaderTests
    {
        [Test]
        public void ShippedDefinitions_LoadAndValidate()
        {
            DefinitionLoader.Load(out var classes, out var abilities);

            Assert.IsTrue(classes.TryGet("fighter", out var fighter));
            Assert.AreEqual(Role.Dps, fighter.Role);
            Assert.AreEqual("slash", fighter.AutoAttack);
            CollectionAssert.AreEqual(new[] { "bolt", "shockwave" }, fighter.Skills);
            Assert.IsTrue(abilities.TryGet("bolt", out var bolt));
            Assert.AreEqual(25f, bolt.Damage);
            Assert.IsTrue(classes.TryGet("training_dummy", out _));
        }

        [Test]
        public void LoadedDefinitions_DriveAServer()
        {
            DefinitionLoader.Load(out var classes, out var abilities);
            var session = new LocalSession(new ServerConfig(), classes, abilities, 1);
            var client = session.AddPlayer(new PlayerId(1), "fighter");
            var dummy = session.Server.SpawnNpc("training_dummy", Team.Enemies, new Vec2(-3f, 2f));

            session.Advance(0.2f, 1f / 30f);
            client.Attack(Vec2.Zero);
            session.Advance(0.2f, 1f / 30f);

            Assert.AreEqual(390f, dummy.Health);
        }
    }
}
