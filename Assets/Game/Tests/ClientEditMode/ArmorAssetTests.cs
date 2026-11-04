using System.IO;
using Game.Core;
using NUnit.Framework;
using EntityId = Game.Core.EntityId;

namespace Game.Client.Tests
{
    public class ArmorAssetTests
    {
        string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "bankai-armor-" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void TheShippedSetsLoadAndTheLootTablesUseThem()
        {
            DefinitionLoader.Load(out var classes, out _);

            var sets = ArmorCatalog.Load();
            var tables = LootCatalog.Load(classes, ModifierCatalog.Load(), sets);

            Assert.IsTrue(sets.TryGet("wanderer", out var starter));
            Assert.AreEqual(1f, starter.HealthScale, "the starter set gives nothing");
            Assert.GreaterOrEqual(sets.Count, 4);
            Assert.Greater(tables.Get(LootTableAsset.BossDrop).ArmorChance, tables.Get(LootTableAsset.MobDrop).ArmorChance);
        }

        [Test]
        public void ABossCanDropArmorWhichIsNamedWithWhatItGivesAndSaved()
        {
            var sets = new Registry<ArmorSetDef>();
            sets.Add(new ArmorSetDef { Id = "stone", Name = "Stone Warden", HealthScale = 1.1f });
            var table = new LootTableDef { Id = "boss_drop", WeaponTypes = new[] { "staff" }, ArmorSets = new[] { "stone" }, ArmorChance = 1f };
            var progress = new MasteryProgress(new ProfileStore(directory, new[] { "staff" }), new MasteryRules(), table, 5);
            progress.UseArmorSets(sets);
            var snapshot = new Snapshot();
            snapshot.Entities.Add(new EntityState { Id = new EntityId(9), Team = Team.Enemies, Health = 50f, Effects = EffectFlags.Boss });
            progress.Observe(snapshot);
            snapshot.Entities[0] = new EntityState { Id = new EntityId(9), Team = Team.Enemies, Health = 0f, Effects = EffectFlags.Boss };

            progress.Observe(snapshot);

            Assert.AreEqual(2, progress.Profile.Armors.Count, "the starter set and the drop");
            Assert.AreEqual("Dropped: Stone Warden (health +10%)", progress.Summary());
            Assert.AreEqual(2, new ProfileStore(directory, new[] { "staff" }).Load().Armors.Count);

            Assert.IsTrue(progress.EquipArmor(progress.Profile.Armors[1].Uid));
            Assert.AreEqual(1.1f, progress.Gear.Health, 0.0001f, "wearing it counts in the gear sent when joining");
        }

        [Test]
        public void AnUnknownSetIsStillNamedFromItsId()
        {
            var progress = new MasteryProgress(new ProfileStore(directory, new[] { "staff" }), new MasteryRules());

            Assert.AreEqual("Moss Cloak", progress.DescribeArmor(new OwnedArmor { Uid = "x", Set = "moss_cloak" }));
        }
    }
}
