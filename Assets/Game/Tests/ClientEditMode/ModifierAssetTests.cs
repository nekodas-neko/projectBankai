using System.IO;
using Game.Core;
using NUnit.Framework;

namespace Game.Client.Tests
{
    public class ModifierAssetTests
    {
        string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "bankai-modifier-" + System.Guid.NewGuid().ToString("N"));
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
        public void TheShippedModifiersLoadAndTheBossAndMobTablesUseThem()
        {
            DefinitionLoader.Load(out var classes, out _);

            var modifiers = ModifierCatalog.Load();
            var tables = LootCatalog.Load(classes, modifiers);

            Assert.GreaterOrEqual(modifiers.Count, 3);
            Assert.Greater(tables.Get(LootTableAsset.BossDrop).ModifierChance, tables.Get(LootTableAsset.MobDrop).ModifierChance, "bosses give modifiers more often");
            foreach (var id in tables.Get(LootTableAsset.BossDrop).Modifiers)
            {
                Assert.IsTrue(modifiers.TryGet(id, out _), id);
            }
        }

        [Test]
        public void TheWeaponInHandAddsUpWithItsModifier()
        {
            var store = new ProfileStore(directory, new[] { "staff" });
            var seed = store.Load();
            seed.Add(new OwnedWeapon { Uid = "staff-drop1", Type = "staff", Power = 1.1f, Modifier = "keen" });
            seed.Equip("staff-drop1");
            store.Save(seed);
            var progress = new MasteryProgress(store, new MasteryRules());
            var modifiers = new Registry<WeaponModifierDef>();
            modifiers.Add(new WeaponModifierDef { Id = "keen", Name = "Keen", PowerScale = 1.08f, HealthScale = 1.05f });
            progress.UseModifiers(modifiers);

            Assert.AreEqual(1.188f, progress.WeaponPower, 0.0001f);
            Assert.AreEqual(1.05f, progress.Gear.Health, 0.0001f);
        }

        [Test]
        public void TheWeaponNameShowsItsModifier()
        {
            var weapon = new OwnedWeapon { Uid = "a", Type = "healing_mace", Power = 1.05f, Modifier = "swift" };

            Assert.AreEqual("Healing mace +5% (Swift)", MasteryProgress.Describe(weapon));
            Assert.AreEqual("Staff", MasteryProgress.Describe(new OwnedWeapon { Uid = "b", Type = "staff" }));
        }
    }
}
