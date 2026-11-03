using NUnit.Framework;

namespace Game.Core.Tests
{
    // The attack button: a tap is the light attack, holding it is the heavy one (same slot, same ability, harder and slower).
    public class HeavyAttackTests
    {
        static (float damage, float cooldown) Swing(bool heavy, Registry<AbilityDef> abilities = null)
        {
            var session = new LocalSession(new ServerConfig(), TestData.Classes(), abilities ?? TestData.Abilities(), 1);
            var client = session.AddPlayer(new PlayerId(1), "fighter");
            var player = session.Server.FindPlayerEntity(new PlayerId(1));
            var dummy = session.Server.SpawnNpc("dummy", Team.Enemies, player.Position + new Vec2(0f, 2f));
            var before = dummy.Health;
            if (heavy)
            {
                client.HeavyAttack(new Vec2(0f, 1f));
            }
            else
            {
                client.Attack(new Vec2(0f, 1f));
            }

            session.Advance(0.1f, 1f / 30f);
            return (before - dummy.Health, player.Cooldowns[0]);
        }

        [Test]
        public void AHeavyAttackHitsHarderAndRecoversSlowerThanALightOne()
        {
            var light = Swing(false);
            var heavy = Swing(true);

            Assert.AreEqual(10f, light.damage, 0.001f);
            Assert.AreEqual(24f, heavy.damage, 0.001f, "2.4 times the light hit");
            Assert.Greater(heavy.cooldown, light.cooldown * 1.5f, "and the next attack comes later");
        }

        [Test]
        public void TurningTheHeavyVersionOffMakesItALightAttack()
        {
            var abilities = TestData.Abilities();
            abilities.Get("basic").HeavyDamageScale = 0f;

            var heavy = Swing(true, abilities);

            Assert.AreEqual(10f, heavy.damage, 0.001f);
        }

        [Test]
        public void AHeavyAttackIsStillBlockedByTheCooldown()
        {
            var session = new LocalSession(new ServerConfig(), TestData.Classes(), TestData.Abilities(), 1);
            var client = session.AddPlayer(new PlayerId(1), "fighter");
            var player = session.Server.FindPlayerEntity(new PlayerId(1));
            var dummy = session.Server.SpawnNpc("dummy", Team.Enemies, player.Position + new Vec2(0f, 2f));
            client.HeavyAttack(new Vec2(0f, 1f));
            session.Advance(0.1f, 1f / 30f);
            var afterFirst = dummy.Health;

            client.HeavyAttack(new Vec2(0f, 1f));
            session.Advance(0.3f, 1f / 30f);

            Assert.AreEqual(afterFirst, dummy.Health, 0.001f);
        }

        [Test]
        public void TheHeavyCommandSurvivesTheWireAndUsesTheAttackSlot()
        {
            var command = new Command(new PlayerId(1), 7, CommandType.HeavyAttack, new Vec2(0f, 1f));

            Assert.IsTrue(WireCodec.TryDecode(WireCodec.Encode(command), out Command decoded));

            Assert.AreEqual(CommandType.HeavyAttack, decoded.Type);
            Assert.AreEqual(0, decoded.AbilitySlot);
        }

        [Test]
        public void ABadHeavyScaleIsRefusedByTheValidator()
        {
            var abilities = TestData.Abilities();
            abilities.Get("basic").HeavyDamageScale = 50f;

            Assert.IsNotEmpty(DefinitionValidator.Validate(TestData.Classes(), abilities));
        }
    }
}
