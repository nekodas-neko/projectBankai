using NUnit.Framework;

namespace Game.Core.Tests
{
    // The health potion: a share of maximum health, with a cooldown, drunk through a command like any other.
    public class PotionTests
    {
        static (LocalSession session, GameClient client, Entity player) Start(ServerConfig config = null)
        {
            var session = new LocalSession(config ?? new ServerConfig(), TestData.Classes(), TestData.Abilities(), 1);
            var client = session.AddPlayer(new PlayerId(1), "fighter");
            return (session, client, session.Server.FindPlayerEntity(new PlayerId(1)));
        }

        [Test]
        public void ADrinkHealsAShareOfMaximumHealth()
        {
            var (session, client, player) = Start();
            player.Health = 20f;

            client.UsePotion();
            session.Advance(0.1f, 1f / 30f);

            Assert.AreEqual(60f, player.Health, 0.001f, "40% of 100");
        }

        [Test]
        public void ADrinkNeverHealsPastFullHealth()
        {
            var (session, client, player) = Start();
            player.Health = 90f;

            client.UsePotion();
            session.Advance(0.1f, 1f / 30f);

            Assert.AreEqual(100f, player.Health, 0.001f);
        }

        [Test]
        public void ThePotionNeedsItsCooldownBetweenDrinks()
        {
            var (session, client, player) = Start();
            player.Health = 10f;
            client.UsePotion();
            session.Advance(0.1f, 1f / 30f);
            Assert.AreEqual(50f, player.Health, 0.001f);

            player.Health = 10f;
            client.UsePotion();
            session.Advance(0.5f, 1f / 30f);
            Assert.AreEqual(10f, player.Health, 0.001f, "too soon");

            session.Advance(20f, 1f / 30f);
            client.UsePotion();
            session.Advance(0.1f, 1f / 30f);
            Assert.AreEqual(50f, player.Health, 0.001f, "ready again");
        }

        [Test]
        public void AFullHealthDrinkIsRefusedAndKeepsThePotionReady()
        {
            var (session, client, player) = Start();

            client.UsePotion();
            session.Advance(0.1f, 1f / 30f);
            player.Health = 10f;
            client.UsePotion();
            session.Advance(0.1f, 1f / 30f);

            Assert.AreEqual(50f, player.Health, 0.001f, "the refused first drink did not start the cooldown");
        }

        [Test]
        public void TheFractionAndCooldownAreServerSettings()
        {
            var (session, client, player) = Start(new ServerConfig { PotionHealFraction = 0.75f, PotionCooldownSeconds = 1f });
            player.Health = 10f;

            client.UsePotion();
            session.Advance(0.1f, 1f / 30f);
            Assert.AreEqual(85f, player.Health, 0.001f);
        }

        [Test]
        public void ThePotionCommandSurvivesTheWireAndUsesNoAbilitySlot()
        {
            var command = new Command(new PlayerId(1), 3, CommandType.UsePotion, Vec2.Zero);

            Assert.IsTrue(WireCodec.TryDecode(WireCodec.Encode(command), out Command decoded));

            Assert.AreEqual(CommandType.UsePotion, decoded.Type);
            Assert.AreEqual(-1, decoded.AbilitySlot);
        }

        [Test]
        public void PotionsAreSavedWithTheProfileAndCapped()
        {
            var profile = PlayerProfile.Starter(new[] { "staff" });
            for (var i = 0; i < 9; i++)
            {
                profile.AddPotion();
            }

            Assert.AreEqual(PlayerProfile.MaxPotions, profile.Potions);
            Assert.IsTrue(ProfileText.TryParse(ProfileText.Write(profile), out var loaded, out _));
            Assert.AreEqual(PlayerProfile.MaxPotions, loaded.Potions);

            Assert.IsTrue(loaded.TryUsePotion());
            Assert.AreEqual(PlayerProfile.MaxPotions - 1, loaded.Potions);
            Assert.IsTrue(ProfileText.TryParse("bankai-profile 1\npotions 99\n", out var big, out _));
            Assert.AreEqual(PlayerProfile.MaxPotions, big.Potions, "a bigger number in a file is capped");
            Assert.IsFalse(new PlayerProfile().TryUsePotion());
        }

        [Test]
        public void ARewardCarriesThePotionChanceAndTheValidatorChecksIt()
        {
            var classes = new Registry<ClassDef>();
            classes.Add(new ClassDef { Id = "carrier", Stats = new Stats { MaxHealth = 10f }, AutoAttack = "basic", PotionChance = 0.25f });
            var book = new RewardBook(classes, new MasteryRules());

            Assert.AreEqual(0.25f, book.For(StableId.From("carrier"), false).PotionChance, 0.0001f);
            Assert.AreEqual(0f, book.For(StableId.From("stranger"), false).PotionChance);

            var bad = TestData.Classes();
            bad.Add(new ClassDef { Id = "greedy", Stats = new Stats { MaxHealth = 10f }, AutoAttack = "basic", PotionChance = 2f });
            Assert.IsNotEmpty(DefinitionValidator.Validate(bad, TestData.Abilities()));
        }
    }
}
