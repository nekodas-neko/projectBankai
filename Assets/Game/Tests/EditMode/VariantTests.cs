using NUnit.Framework;

namespace Game.Core.Tests
{
    public class VariantTests
    {
        static readonly int Hot = StableId.From("hot");
        static readonly int Cold = StableId.From("cold");

        static (GameServer server, LoopbackLink link, Entity player) Setup(params int[] variants)
        {
            var random = new SeededRandom(1);
            var server = new GameServer(new ServerConfig(), TestData.Classes(), TestData.Abilities(), random);
            var link = new LoopbackLink(random, NetworkConditions.None);
            var player = server.AddPlayer(new PlayerId(1), "sparker", link.ServerEnd, 0, variants);
            return (server, link, player);
        }

        static void Fire(GameServer server, LoopbackLink link, Vec2 aim)
        {
            TestData.Send(link.ClientEnd, 1, CommandType.Attack, aim);
            server.StepTick();
        }

        static Snapshot Latest(LoopbackLink link)
        {
            Snapshot latest = null;
            while (link.ClientEnd.TryReceive(out var data))
            {
                if (WireCodec.TryDecode(data, out Snapshot snapshot))
                {
                    latest = snapshot;
                }
            }

            return latest;
        }

        [Test]
        public void ABaseAbilityIsUnchanged()
        {
            var (server, link, player) = Setup();
            var target = server.SpawnNpc("dummy", Team.Enemies, player.Position + new Vec2(0f, 4f));

            Fire(server, link, new Vec2(0f, 1f));

            Assert.AreEqual(90f, target.Health, 0.001f);
            Assert.AreEqual(90f, player.Resource, 0.001f);
            Assert.AreEqual(2f - 1f / 30f, player.Cooldowns[0], 0.01f);
        }

        [Test]
        public void AVariantScalesDamageCooldownAndCost()
        {
            var (server, link, player) = Setup(Hot);
            var target = server.SpawnNpc("dummy", Team.Enemies, player.Position + new Vec2(0f, 4f));

            Fire(server, link, new Vec2(0f, 1f));

            Assert.AreEqual(80f, target.Health, 0.001f);
            Assert.AreEqual(80f, player.Resource, 0.001f);
            Assert.AreEqual(1f - 1f / 30f, player.Cooldowns[0], 0.01f);
        }

        [Test]
        public void AVariantCanReachFurther()
        {
            var baseSetup = Setup();
            var baseTarget = baseSetup.server.SpawnNpc("dummy", Team.Enemies, baseSetup.player.Position + new Vec2(0f, 7f));
            Fire(baseSetup.server, baseSetup.link, new Vec2(0f, 1f));

            var hotSetup = Setup(Hot);
            var hotTarget = hotSetup.server.SpawnNpc("dummy", Team.Enemies, hotSetup.player.Position + new Vec2(0f, 7f));
            Fire(hotSetup.server, hotSetup.link, new Vec2(0f, 1f));

            Assert.AreEqual(100f, baseTarget.Health);
            Assert.Less(hotTarget.Health, 100f);
        }

        [Test]
        public void ADifferentVariantScalesDifferently()
        {
            var (server, link, player) = Setup(Cold);
            var target = server.SpawnNpc("dummy", Team.Enemies, player.Position + new Vec2(0f, 4f));

            Fire(server, link, new Vec2(0f, 1f));

            Assert.AreEqual(95f, target.Health, 0.001f);
            Assert.AreEqual(90f, player.Resource, 0.001f);
        }

        [Test]
        public void AnUnknownVariantFallsBackToTheBaseAbility()
        {
            var (server, link, player) = Setup(StableId.From("nonsense"));
            var target = server.SpawnNpc("dummy", Team.Enemies, player.Position + new Vec2(0f, 4f));

            Fire(server, link, new Vec2(0f, 1f));

            Assert.AreEqual(90f, target.Health, 0.001f);
            Assert.AreEqual(0, player.Variants[0]);
        }

        [Test]
        public void AVariantForAnAbilityThatDoesNotHaveItIsIgnored()
        {
            var (_, _, player) = Setup(0, Hot, Hot, Hot, Hot);

            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0 }, player.Variants);
        }

        [Test]
        public void ExtraLoadoutEntriesAreIgnoredSafely()
        {
            var (_, _, player) = Setup(Hot, 1, 2, 3, 4, 5, 6, 7);

            Assert.AreEqual(Hot, player.Variants[0]);
        }

        [Test]
        public void UseAndDamageEventsCarryTheVariant()
        {
            var (server, link, player) = Setup(Hot);
            server.SpawnNpc("dummy", Team.Enemies, player.Position + new Vec2(0f, 4f));

            Fire(server, link, new Vec2(0f, 1f));
            var snapshot = Latest(link);

            Assert.AreEqual(Hot, snapshot.Uses[0].Variant);
            Assert.AreEqual(Hot, snapshot.Events[0].Variant);
            Assert.AreEqual(StableId.From("spark"), snapshot.Uses[0].Ability);
        }

        [Test]
        public void ABaseUseCarriesNoVariant()
        {
            var (server, link, player) = Setup();
            server.SpawnNpc("dummy", Team.Enemies, player.Position + new Vec2(0f, 4f));

            Fire(server, link, new Vec2(0f, 1f));

            Assert.AreEqual(0, Latest(link).Uses[0].Variant);
        }

        [Test]
        public void Codec_RoundTripsVariantFields()
        {
            var snapshot = new Snapshot { Tick = 1 };
            snapshot.Uses.Add(new UseEvent { User = new EntityId(1), Slot = 1, Ability = 5, Variant = 77, Direction = new Vec2(0f, 1f) });
            snapshot.Events.Add(new DamageEvent { Source = new EntityId(1), Target = new EntityId(2), Amount = 3f, Ability = 5, Variant = 88 });

            Assert.IsTrue(WireCodec.TryDecode(WireCodec.Encode(snapshot), out Snapshot decoded));

            Assert.AreEqual(77, decoded.Uses[0].Variant);
            Assert.AreEqual(88, decoded.Events[0].Variant);
        }

        [Test]
        public void Validator_RejectsBrokenVariants()
        {
            var abilities = TestData.Abilities();
            abilities.Add(new AbilityDef
            {
                Id = "bad_variants",
                Range = 1f,
                Variants = new[]
                {
                    new VariantDef { Id = "a" },
                    new VariantDef { Id = "a" },
                    new VariantDef { Id = "" },
                    new VariantDef { Id = "slow", CooldownScale = 0f },
                },
            });

            var errors = string.Join("\n", DefinitionValidator.Validate(TestData.Classes(), abilities));

            StringAssert.Contains("'bad_variants': variant ids must be non-empty and unique", errors);
            StringAssert.Contains("variant 'slow' has a non-positive scale", errors);
        }
    }
}
