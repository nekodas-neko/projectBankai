using NUnit.Framework;

namespace Game.Core.Tests
{
    // A map-wide attack hits everyone in the room; standing on a tagged patch of the map (a pool of water) cuts the damage.
    public class ShelterTests
    {
        static DungeonDef Hall(bool withPool = true)
        {
            return new DungeonDef
            {
                Id = "hall",
                Zones = new[] { new ZoneDef { Name = "hall", MinX = -20f, MinY = -20f, MaxX = 20f, MaxY = 30f } },
                Patches = withPool ? new[] { new PatchDef { Name = "pool", Tag = "water", MinX = 6f, MinY = -4f, MaxX = 12f, MaxY = 2f } } : new PatchDef[0],
                Rooms = new[]
                {
                    new RoomDef { Name = "One", Spawns = new[] { new SpawnDef { Class = "flooder", X = 0f, Y = 24f, Boss = true } } },
                },
            };
        }

        sealed class Setup
        {
            public readonly LocalSession Session;
            public readonly Entity Dry;
            public readonly Entity Wet;

            public Setup(bool withPool = true)
            {
                Session = new LocalSession(new ServerConfig(), TestData.Classes(), TestData.Abilities(), 1);
                Session.AddPlayer(new PlayerId(1), "fighter");
                Session.AddPlayer(new PlayerId(2), "fighter");
                Session.StartDungeon(Hall(withPool));
                Dry = Session.Server.FindPlayerEntity(new PlayerId(1));
                Wet = Session.Server.FindPlayerEntity(new PlayerId(2));
                Dry.Position = new Vec2(-10f, -10f);
                Wet.Position = new Vec2(9f, -1f);
            }

            public void Tick(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    Session.Advance(1f / 30f);
                }
            }
        }

        [Test]
        public void AMapWideAttackHitsEveryoneWhereverTheyStand()
        {
            var setup = new Setup(withPool: false);

            setup.Tick(80);

            Assert.AreEqual(100f - 40f, setup.Dry.Health, 0.01f, "34 units from the boss, far outside the 2 unit circle");
            Assert.AreEqual(100f - 40f, setup.Wet.Health, 0.01f);
        }

        [Test]
        public void StandingInThePoolCutsTheDamageBySheltersAmount()
        {
            var setup = new Setup();

            setup.Tick(80);

            Assert.AreEqual(60f, setup.Dry.Health, 0.01f);
            Assert.AreEqual(100f - 40f * 0.2f, setup.Wet.Health, 0.01f, "80% less in the water");
        }

        [Test]
        public void TheWarningShowsBeforeItLandsAndNothingHappensEarly()
        {
            var setup = new Setup();

            setup.Tick(40);

            Assert.AreEqual(100f, setup.Dry.Health);
            Assert.AreEqual(100f, setup.Wet.Health);
        }

        [Test]
        public void ARedAttackIgnoresADodgeEvenInsideTheShelterRule()
        {
            var setup = new Setup();
            setup.Tick(10);
            setup.Dry.AddStatus(StatusType.Invulnerable, 5f, 0f);

            setup.Tick(70);

            Assert.AreEqual(60f, setup.Dry.Health, 0.01f, "red attacks ignore invulnerability, so the only answer is the water");
        }

        [Test]
        public void ThePatchOnlyShelterFromTheTagTheAbilityNames()
        {
            var map = new WorldMap(Hall().Zones, null, new[] { new PatchDef { Name = "rock", Tag = "cover", MinX = 0f, MinY = 0f, MaxX = 4f, MaxY = 4f } });

            Assert.IsTrue(map.OnPatch(new Vec2(2f, 2f), "cover"));
            Assert.IsFalse(map.OnPatch(new Vec2(2f, 2f), "water"));
            Assert.IsFalse(map.OnPatch(new Vec2(2f, 2f), null));
            Assert.IsFalse(map.OnPatch(new Vec2(10f, 10f), "cover"));
        }

        [Test]
        public void BrokenShelterDataIsRejected()
        {
            var abilities = TestData.Abilities();
            abilities.Add(new AbilityDef { Id = "bad_shelter", Cooldown = 1f, Range = 2f, Radius = 2f, Damage = 5f, ShelterReduction = 1.5f });
            abilities.Add(new AbilityDef { Id = "bad_wave", Cooldown = 1f, Range = 2f, Radius = 2f, Damage = 5f, Everyone = true });
            var errors = string.Join("\n", DefinitionValidator.Validate(TestData.Classes(), abilities));

            StringAssert.Contains("Ability 'bad_shelter': shelter reduction must be between 0 and 1", errors);
            StringAssert.Contains("Ability 'bad_shelter': a shelter reduction needs a shelter tag", errors);
            StringAssert.Contains("Ability 'bad_wave': a map-wide attack needs damage, a windup", errors);

            var dungeons = new Registry<DungeonDef>();
            var broken = Hall();
            broken.Id = "broken";
            broken.Patches = new[]
            {
                new PatchDef { Name = "flat", Tag = "water", MinX = 0f, MinY = 0f, MaxX = 0f, MaxY = 3f },
                new PatchDef { Name = "away", Tag = "water", MinX = 100f, MinY = 100f, MaxX = 104f, MaxY = 104f },
                new PatchDef { Name = "untagged", MinX = 0f, MinY = 0f, MaxX = 2f, MaxY = 2f },
            };
            dungeons.Add(broken);
            var dungeonErrors = string.Join("\n", DefinitionValidator.ValidateDungeons(dungeons, TestData.Classes()));

            StringAssert.Contains("patch 'flat' needs a size and a tag", dungeonErrors);
            StringAssert.Contains("the middle of patch 'away' is outside the zones", dungeonErrors);
            StringAssert.Contains("patch 'untagged' needs a size and a tag", dungeonErrors);
        }
    }
}
