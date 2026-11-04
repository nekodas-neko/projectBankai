using NUnit.Framework;

namespace Game.Core.Tests
{
    // A dropped weapon can carry a small modifier. It is numbers only, worked out on the client and clamped by the server.
    public class ModifierTests
    {
        static Registry<WeaponModifierDef> Modifiers()
        {
            var registry = new Registry<WeaponModifierDef>();
            registry.Add(new WeaponModifierDef { Id = "sturdy", Name = "Sturdy", HealthScale = 1.1f });
            registry.Add(new WeaponModifierDef { Id = "swift", Name = "Swift", SpeedScale = 1.1f });
            registry.Add(new WeaponModifierDef { Id = "keen", Name = "Keen", PowerScale = 1.08f });
            return registry;
        }

        [Test]
        public void TheGearOfAWeaponIsItsPowerTimesItsModifier()
        {
            var plain = Gear.Of(new OwnedWeapon { Uid = "a", Type = "staff", Power = 1.1f }, Modifiers());
            var keen = Gear.Of(new OwnedWeapon { Uid = "b", Type = "staff", Power = 1.1f, Modifier = "keen" }, Modifiers());
            var sturdy = Gear.Of(new OwnedWeapon { Uid = "c", Type = "staff", Power = 1f, Modifier = "sturdy" }, Modifiers());

            Assert.AreEqual(1.1f, plain.Power, 0.0001f);
            Assert.AreEqual(1f, plain.Health);
            Assert.AreEqual(1.188f, keen.Power, 0.0001f);
            Assert.AreEqual(1.1f, sturdy.Health, 0.0001f);
            Assert.AreEqual(1f, Gear.Of(null, Modifiers()).Power);
            Assert.AreEqual(1f, Gear.Of(new OwnedWeapon { Uid = "d", Type = "staff", Modifier = "ghost" }, Modifiers()).Health, "an unknown modifier counts as none");
        }

        [Test]
        public void ModifierScalesMustStayCloseToOne()
        {
            Assert.IsEmpty(Gear.Validate(Modifiers()));

            var bad = new Registry<WeaponModifierDef>();
            bad.Add(new WeaponModifierDef { Id = "huge", Name = "Huge", HealthScale = 3f });
            bad.Add(new WeaponModifierDef { Id = "unnamed", Name = "", PowerScale = 1.1f });
            Assert.AreEqual(2, Gear.Validate(bad).Count);
        }

        [Test]
        public void ALootTableRollsAModifierWithItsChance()
        {
            var always = new LootTableDef { Id = "t", WeaponTypes = new[] { "staff" }, Modifiers = new[] { "keen" }, ModifierChance = 1f };
            var never = new LootTableDef { Id = "t", WeaponTypes = new[] { "staff" }, Modifiers = new[] { "keen" }, ModifierChance = 0f };

            Assert.AreEqual("keen", LootRoller.Roll(always, new SeededRandom(3), 1).Modifier);
            Assert.AreEqual(string.Empty, LootRoller.Roll(never, new SeededRandom(3), 1).Modifier);

            var half = new LootTableDef { Id = "t", WeaponTypes = new[] { "staff" }, Modifiers = new[] { "keen", "swift", "sturdy" }, ModifierChance = 0.5f };
            var random = new SeededRandom(11);
            var with = 0;
            for (var i = 0; i < 200; i++)
            {
                with += LootRoller.Roll(half, random, i).Modifier.Length > 0 ? 1 : 0;
            }

            Assert.That(with, Is.InRange(70, 130), "about half carry one");
        }

        [Test]
        public void ALootTableNamingAnUnknownModifierIsRefused()
        {
            var tables = new Registry<LootTableDef>();
            tables.Add(new LootTableDef { Id = "t", WeaponTypes = new[] { "fighter" }, Modifiers = new[] { "ghost" }, ModifierChance = 0.5f });
            tables.Add(new LootTableDef { Id = "u", WeaponTypes = new[] { "fighter" }, ModifierChance = 2f });

            var errors = LootRoller.Validate(tables, TestData.Classes(), Modifiers());

            Assert.AreEqual(2, errors.Count);
        }

        [Test]
        public void TheServerAppliesHealthAndSpeedScalesAndClampsThem()
        {
            float MaxHealthFor(float scale)
            {
                var session = new LocalSession(new ServerConfig(), TestData.Classes(), TestData.Abilities(), 1);
                session.AddPlayer(new PlayerId(1), "fighter", 0, null, 1f, scale, 1f);
                var player = session.Server.FindPlayerEntity(new PlayerId(1));
                Assert.AreEqual(player.Stats.MaxHealth, player.Health, 0.001f, "starts at full health");
                return player.Stats.MaxHealth;
            }

            Assert.AreEqual(100f, MaxHealthFor(1f), 0.001f);
            Assert.AreEqual(110f, MaxHealthFor(1.1f), 0.001f);
            Assert.AreEqual(130f, MaxHealthFor(50f), 0.001f, "clamped to 1.3");
            Assert.AreEqual(80f, MaxHealthFor(0f), 0.001f, "and to 0.8");
            Assert.AreEqual(100f, MaxHealthFor(float.NaN), 0.001f);
        }

        [Test]
        public void ASwiftPlayerMovesFasterOnTheServerAndOnTheClientPrediction()
        {
            (float server, float client) Walk(float speed)
            {
                var session = new LocalSession(new ServerConfig(), TestData.Classes(), TestData.Abilities(), 1);
                var client = session.AddPlayer(new PlayerId(1), "fighter", 0, null, 1f, 1f, speed);
                var player = session.Server.FindPlayerEntity(new PlayerId(1));
                session.Advance(0.5f, 1f / 30f);
                var startServer = player.Position.Y;
                var startClient = client.PredictedPosition.Y;
                client.SetMoveInput(new Vec2(0f, 1f));
                session.Advance(1f, 1f / 30f);
                return (player.Position.Y - startServer, client.PredictedPosition.Y - startClient);
            }

            var normal = Walk(1f);
            var swift = Walk(1.1f);

            Assert.Greater(swift.server, normal.server * 1.05f);
            Assert.Greater(swift.client, normal.client * 1.05f);
            Assert.AreEqual(swift.server, swift.client, 0.3f, "the client predicted the same distance as the server walked");
        }
    }
}
