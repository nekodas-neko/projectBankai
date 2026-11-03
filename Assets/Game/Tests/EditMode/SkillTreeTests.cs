using NUnit.Framework;

namespace Game.Core.Tests
{
    public class SkillTreeTests
    {
        static readonly MasteryRules Rules = new MasteryRules { XpPerLevel = new[] { 10, 10, 10, 10, 10, 10 }, PointsPerLevel = 1 };

        static VariantDef[] Variants(params string[] ids)
        {
            var list = new VariantDef[ids.Length];
            for (var i = 0; i < ids.Length; i++)
            {
                list[i] = new VariantDef { Id = ids[i] };
            }

            return list;
        }

        static Registry<AbilityDef> Abilities()
        {
            var registry = new Registry<AbilityDef>();
            registry.Add(new AbilityDef { Id = "slash", Variants = Variants("ember") });
            registry.Add(new AbilityDef { Id = "bash", Variants = Variants("heavy", "swift") });
            registry.Add(new AbilityDef { Id = "crash", Variants = Variants("crushing") });
            registry.Add(new AbilityDef { Id = "wall", Kind = AbilityKind.Block, Variants = Variants("quick") });
            registry.Add(new AbilityDef { Id = "plain" });
            return registry;
        }

        static Registry<ClassDef> Classes()
        {
            var registry = new Registry<ClassDef>();
            registry.Add(new ClassDef { Id = "knight", Playable = true, AutoAttack = "slash", Skills = new[] { "bash", "crash" }, Defense = "wall" });
            registry.Add(new ClassDef { Id = "goblin", AutoAttack = "slash" });
            return registry;
        }

        static SkillTreeDef Tree()
        {
            return new SkillTreeDef
            {
                Id = "knight",
                Nodes = new[]
                {
                    new SkillNodeDef { Id = "ember_slash", Name = "Ember slash", Slot = 0, Variant = "ember" },
                    new SkillNodeDef { Id = "heavy_bash", Name = "Heavy bash", Slot = 1, Variant = "heavy" },
                    new SkillNodeDef { Id = "swift_bash", Name = "Swift bash", Slot = 1, Variant = "swift" },
                    new SkillNodeDef { Id = "crushing_crash", Name = "Crushing crash", Slot = 2, Variant = "crushing", Cost = 2, Requires = "heavy_bash" },
                    new SkillNodeDef { Id = "quick_wall", Name = "Quick wall", Slot = Entity.DefenseSlot, Variant = "quick" },
                },
            };
        }

        static WeaponMastery WithPoints(int points)
        {
            var mastery = new WeaponMastery();
            mastery.AddXp(Rules, points * 10);
            return mastery;
        }

        static Registry<SkillTreeDef> Trees(params SkillTreeDef[] trees)
        {
            var registry = new Registry<SkillTreeDef>();
            foreach (var tree in trees)
            {
                registry.Add(tree);
            }

            return registry;
        }

        [Test]
        public void ANodeCostsPointsAndSetsItsSlotsVariant()
        {
            var mastery = WithPoints(3);

            Assert.IsTrue(SkillTreeRules.TryChoose(Tree(), mastery, Rules, "ember_slash", out var reason), reason);

            Assert.AreEqual(2, mastery.PointsAvailable(Rules));
            var loadout = SkillTreeRules.Loadout(Tree(), mastery);
            Assert.AreEqual(StableId.From("ember"), loadout[0]);
            Assert.AreEqual(0, loadout[1], "other slots keep their default skill");
            Assert.AreEqual(Entity.AbilitySlots, loadout.Length);
        }

        [Test]
        public void ANodeNeedsItsPrerequisiteAndEnoughPoints()
        {
            var mastery = WithPoints(2);

            Assert.IsFalse(SkillTreeRules.TryChoose(Tree(), mastery, Rules, "crushing_crash", out var reason));
            StringAssert.Contains("Heavy bash", reason);

            SkillTreeRules.TryChoose(Tree(), mastery, Rules, "heavy_bash", out _);
            Assert.IsFalse(SkillTreeRules.TryChoose(Tree(), mastery, Rules, "crushing_crash", out reason), "2 points needed, 1 left");
            StringAssert.Contains("points", reason);
        }

        [Test]
        public void ChoosingAnotherNodeInASlotReplacesItAndRefundsThePoints()
        {
            var mastery = WithPoints(2);
            SkillTreeRules.TryChoose(Tree(), mastery, Rules, "heavy_bash", out _);

            Assert.IsTrue(SkillTreeRules.TryChoose(Tree(), mastery, Rules, "swift_bash", out var reason), reason);

            CollectionAssert.AreEqual(new[] { "swift_bash" }, mastery.Choices);
            Assert.AreEqual(1, mastery.PointsSpent);
            Assert.AreEqual(StableId.From("swift"), SkillTreeRules.Loadout(Tree(), mastery)[1]);
        }

        [Test]
        public void ASwapIsWorthItEvenWithOnlyTheRefundToPayWith()
        {
            var mastery = WithPoints(1);
            SkillTreeRules.TryChoose(Tree(), mastery, Rules, "heavy_bash", out _);

            Assert.AreEqual(0, mastery.PointsAvailable(Rules));
            Assert.IsTrue(SkillTreeRules.TryChoose(Tree(), mastery, Rules, "swift_bash", out _));
        }

        [Test]
        public void ANodeAnotherNodeDependsOnCannotBeReplaced()
        {
            var mastery = WithPoints(4);
            SkillTreeRules.TryChoose(Tree(), mastery, Rules, "heavy_bash", out _);
            SkillTreeRules.TryChoose(Tree(), mastery, Rules, "crushing_crash", out _);

            Assert.IsFalse(SkillTreeRules.TryChoose(Tree(), mastery, Rules, "swift_bash", out var reason));
            StringAssert.Contains("depends", reason);
            CollectionAssert.Contains(mastery.Choices, "heavy_bash");
        }

        [Test]
        public void TakingTheSameNodeTwiceOrAnUnknownNodeIsRefused()
        {
            var mastery = WithPoints(3);
            SkillTreeRules.TryChoose(Tree(), mastery, Rules, "ember_slash", out _);

            Assert.IsFalse(SkillTreeRules.TryChoose(Tree(), mastery, Rules, "ember_slash", out _));
            Assert.IsFalse(SkillTreeRules.TryChoose(Tree(), mastery, Rules, "nope", out _));
            Assert.AreEqual(1, mastery.PointsSpent);
        }

        [Test]
        public void AResetClearsTheLoadoutBackToTheDefaultKit()
        {
            var mastery = WithPoints(3);
            SkillTreeRules.TryChoose(Tree(), mastery, Rules, "ember_slash", out _);
            SkillTreeRules.TryChoose(Tree(), mastery, Rules, "quick_wall", out _);

            mastery.Reset();

            CollectionAssert.AreEqual(new int[Entity.AbilitySlots], SkillTreeRules.Loadout(Tree(), mastery));
            Assert.AreEqual(3, mastery.PointsAvailable(Rules));
        }

        [Test]
        public void ChoicesTheTreeNoLongerHasAreDroppedAndTheirPointsReturned()
        {
            var mastery = WithPoints(3);
            SkillTreeRules.TryChoose(Tree(), mastery, Rules, "ember_slash", out _);
            mastery.Choices.Add("removed_node");
            mastery.PointsSpent += 2;

            SkillTreeRules.Sanitize(Tree(), mastery);

            CollectionAssert.AreEqual(new[] { "ember_slash" }, mastery.Choices);
            Assert.AreEqual(1, mastery.PointsSpent);
            Assert.AreEqual(new int[Entity.AbilitySlots].Length, SkillTreeRules.Loadout(null, mastery).Length);
        }

        [Test]
        public void AValidTreePassesTheValidator()
        {
            Assert.IsEmpty(SkillTreeValidator.Validate(Trees(Tree()), Classes(), Abilities()));
        }

        [Test]
        public void TheValidatorFindsEveryKindOfMistake()
        {
            SkillTreeDef Make(string id, params SkillNodeDef[] nodes) => new SkillTreeDef { Id = id, Nodes = nodes };
            SkillNodeDef Node(string id, int slot, string variant, string requires = "", int cost = 1) =>
                new SkillNodeDef { Id = id, Name = id, Slot = slot, Variant = variant, Requires = requires, Cost = cost };

            Assert.IsNotEmpty(SkillTreeValidator.Validate(Trees(Make("goblin", Node("a", 0, "ember"))), Classes(), Abilities()), "not a playable weapon");
            Assert.IsNotEmpty(SkillTreeValidator.Validate(Trees(Make("ghost", Node("a", 0, "ember"))), Classes(), Abilities()), "unknown weapon");
            Assert.IsNotEmpty(SkillTreeValidator.Validate(Trees(Make("knight")), Classes(), Abilities()), "empty tree");
            Assert.IsNotEmpty(SkillTreeValidator.Validate(Trees(Make("knight", Node("a", 0, "nope"))), Classes(), Abilities()), "unknown variant");
            Assert.IsNotEmpty(SkillTreeValidator.Validate(Trees(Make("knight", Node("a", 3, "ember"))), Classes(), Abilities()), "no skill in that slot");
            Assert.IsNotEmpty(SkillTreeValidator.Validate(Trees(Make("knight", Node("a", 0, "ember", cost: 11))), Classes(), Abilities()), "cost too high");
            Assert.IsNotEmpty(SkillTreeValidator.Validate(Trees(Make("knight", Node("a", 0, "ember"), Node("a", 1, "heavy"))), Classes(), Abilities()), "repeated id");
            Assert.IsNotEmpty(SkillTreeValidator.Validate(Trees(Make("knight", Node("has space", 0, "ember"))), Classes(), Abilities()), "bad id");
            Assert.IsNotEmpty(SkillTreeValidator.Validate(Trees(Make("knight", Node("a", 0, "ember", "ghost"))), Classes(), Abilities()), "unknown requirement");
            Assert.IsNotEmpty(SkillTreeValidator.Validate(Trees(Make("knight", Node("a", 1, "heavy"), Node("b", 1, "swift", "a"))), Classes(), Abilities()), "requires its own slot");
            Assert.IsNotEmpty(SkillTreeValidator.Validate(Trees(Make("knight", Node("a", 0, "ember", "b"), Node("b", 1, "heavy", "a"))), Classes(), Abilities()), "loop");
        }
    }
}
