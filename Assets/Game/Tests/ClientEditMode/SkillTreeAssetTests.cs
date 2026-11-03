using System.IO;
using Game.Client;
using Game.Core;
using NUnit.Framework;

namespace Game.Client.Tests
{
    public class SkillTreeAssetTests
    {
        static readonly string[] Weapons = { "sword_and_shield", "staff", "healing_mace" };

        string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "bankai-tree-" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        static Registry<SkillTreeDef> Shipped(out Registry<ClassDef> classes, out Registry<AbilityDef> abilities)
        {
            DefinitionLoader.Load(out classes, out abilities);
            return SkillTreeCatalog.Load(classes, abilities);
        }

        [Test]
        public void EveryWeaponHasAValidShippedTree()
        {
            var trees = Shipped(out var classes, out var abilities);

            Assert.IsEmpty(SkillTreeCatalog.Errors(trees, classes, abilities));
            foreach (var weapon in Weapons)
            {
                Assert.IsTrue(trees.TryGet(weapon, out var tree), weapon);
                Assert.GreaterOrEqual(tree.Nodes.Length, 4, weapon);
                Assert.LessOrEqual(tree.Nodes.Length, 6, "the panel shows six rows");
            }
        }

        [Test]
        public void EveryNodeCanBeTakenWithEnoughMasteryAndChangesItsSlot()
        {
            var trees = Shipped(out _, out _);
            var rules = MasteryCatalog.Load();
            foreach (var weapon in Weapons)
            {
                var tree = trees.Get(weapon);
                foreach (var node in tree.Nodes)
                {
                    var mastery = new WeaponMastery();
                    mastery.AddXp(rules, rules.MaxXp);

                    var chain = new System.Collections.Generic.List<SkillNodeDef> { node };
                    for (var parent = node.Requires; parent.Length > 0; parent = chain[0].Requires)
                    {
                        chain.Insert(0, tree.Find(parent));
                    }

                    foreach (var step in chain)
                    {
                        Assert.IsTrue(SkillTreeRules.TryChoose(tree, mastery, rules, step.Id, out var reason), $"{weapon}/{step.Id}: {reason}");
                    }

                    Assert.AreEqual(StableId.From(node.Variant), SkillTreeRules.Loadout(tree, mastery)[node.Slot], $"{weapon}/{node.Id}");
                }
            }
        }

        [Test]
        public void TheWholeTreeFitsWithinTheMasteryPoints()
        {
            var trees = Shipped(out _, out _);
            var rules = MasteryCatalog.Load();
            foreach (var weapon in Weapons)
            {
                var total = 0;
                foreach (var node in trees.Get(weapon).Nodes)
                {
                    total += node.Cost;
                }

                Assert.LessOrEqual(total, rules.MaxLevel * rules.PointsPerLevel, $"{weapon}: every node costs {total} points");
                Assert.Greater(total, 3, $"{weapon}: the tree should take a while to fill");
            }
        }

        [Test]
        public void SpendingPointsThroughTheProgressHolderSavesAndResetGivesThemBack()
        {
            var trees = Shipped(out _, out _);
            var rules = MasteryCatalog.Load();
            var store = new ProfileStore(directory, Weapons);
            var progress = new MasteryProgress(store, rules);
            progress.Profile.MasteryFor("staff").AddXp(rules, rules.MaxXp);

            Assert.IsTrue(progress.TryChoose(trees.Get("staff"), "staff", "crimson_bolt", out var reason), reason);
            Assert.IsFalse(progress.Dirty, "saved at once");
            CollectionAssert.AreEqual(new[] { "crimson_bolt" }, new ProfileStore(directory, Weapons).Load().MasteryFor("staff").Choices);

            progress.ResetSkills("staff");
            Assert.IsEmpty(new ProfileStore(directory, Weapons).Load().MasteryFor("staff").Choices);
        }

        [Test]
        public void ThePanelDescribesANodeWithItsSlotCostAndRequirement()
        {
            var tree = Shipped(out _, out _).Get("sword_and_shield");

            Assert.AreEqual("Heavy bash   Skill   1 pt", SkillTreePanel.Describe(tree, tree.Find("heavy_bash"), false));
            Assert.AreEqual("[x] Crushing crash   Ultimate   2 pt  (needs Heavy bash)", SkillTreePanel.Describe(tree, tree.Find("crushing_crash"), true));
            Assert.AreEqual("Defence", SkillTreePanel.SlotName(Entity.DefenseSlot));
        }
    }
}
