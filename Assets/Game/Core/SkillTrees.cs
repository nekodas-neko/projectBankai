using System;
using System.Collections.Generic;

namespace Game.Core
{
    // One choice in a weapon's tree: spend points to swap the skill in a slot for one of its variants.
    public sealed class SkillNodeDef
    {
        public string Id { get; set; }
        public string Name { get; set; }

        // The ability slot it changes: 0 auto attack, 1 and 2 skills (2 is the ultimate), 4 defence.
        public int Slot { get; set; }

        // The variant id on that slot's ability.
        public string Variant { get; set; }
        public int Cost { get; set; } = 1;

        // Another node of the same tree that must be taken first (empty for none).
        public string Requires { get; set; } = string.Empty;
    }

    // A weapon type's skill tree. The id is the weapon type (the playable class id).
    public sealed class SkillTreeDef : IDefinition
    {
        public string Id { get; set; }
        public SkillNodeDef[] Nodes { get; set; } = Array.Empty<SkillNodeDef>();

        public SkillNodeDef Find(string nodeId)
        {
            foreach (var node in Nodes)
            {
                if (node.Id == nodeId)
                {
                    return node;
                }
            }

            return null;
        }
    }

    public static class SkillTreeRules
    {
        // Takes a node. A node in a slot that already has one replaces it and gives its points back, so changing your mind costs nothing extra.
        public static bool TryChoose(SkillTreeDef tree, WeaponMastery mastery, MasteryRules rules, string nodeId, out string reason)
        {
            reason = null;
            var node = tree.Find(nodeId);
            if (node == null)
            {
                reason = "no such node";
                return false;
            }

            Sanitize(tree, mastery);
            if (mastery.Choices.Contains(nodeId))
            {
                reason = "already chosen";
                return false;
            }

            if (node.Requires.Length > 0 && !mastery.Choices.Contains(node.Requires))
            {
                reason = "needs " + (tree.Find(node.Requires)?.Name ?? node.Requires);
                return false;
            }

            var replaced = ChosenInSlot(tree, mastery, node.Slot);
            if (replaced != null)
            {
                foreach (var other in mastery.Choices)
                {
                    var dependent = tree.Find(other);
                    if (dependent != null && dependent.Requires == replaced.Id && dependent.Id != node.Id)
                    {
                        reason = $"{dependent.Name} depends on {replaced.Name}";
                        return false;
                    }
                }
            }

            var refund = replaced?.Cost ?? 0;
            if (node.Cost > mastery.PointsAvailable(rules) + refund)
            {
                reason = "not enough points";
                return false;
            }

            if (replaced != null)
            {
                mastery.Choices.Remove(replaced.Id);
                mastery.PointsSpent -= replaced.Cost;
            }

            mastery.Choices.Add(node.Id);
            mastery.PointsSpent += node.Cost;
            return true;
        }

        // The variant to use in each ability slot (0 for the default skill), in the form the join message carries.
        public static int[] Loadout(SkillTreeDef tree, WeaponMastery mastery)
        {
            var variants = new int[Entity.AbilitySlots];
            if (tree == null)
            {
                return variants;
            }

            foreach (var choice in mastery.Choices)
            {
                var node = tree.Find(choice);
                if (node != null && node.Slot >= 0 && node.Slot < variants.Length)
                {
                    variants[node.Slot] = StableId.From(node.Variant);
                }
            }

            return variants;
        }

        // Drops choices the tree no longer has (the data changed since the save) and recounts the points they cost.
        public static void Sanitize(SkillTreeDef tree, WeaponMastery mastery)
        {
            var spent = 0;
            for (var i = mastery.Choices.Count - 1; i >= 0; i--)
            {
                var node = tree.Find(mastery.Choices[i]);
                if (node == null)
                {
                    mastery.Choices.RemoveAt(i);
                }
                else
                {
                    spent += node.Cost;
                }
            }

            mastery.PointsSpent = spent;
        }

        static SkillNodeDef ChosenInSlot(SkillTreeDef tree, WeaponMastery mastery, int slot)
        {
            foreach (var choice in mastery.Choices)
            {
                var node = tree.Find(choice);
                if (node != null && node.Slot == slot)
                {
                    return node;
                }
            }

            return null;
        }
    }

    public static class SkillTreeValidator
    {
        public static List<string> Validate(Registry<SkillTreeDef> trees, Registry<ClassDef> classes, Registry<AbilityDef> abilities)
        {
            var errors = new List<string>();
            foreach (var tree in trees.All)
            {
                if (!classes.TryGet(tree.Id, out var weapon) || !weapon.Playable)
                {
                    errors.Add($"Skill tree '{tree.Id}': not a playable weapon");
                    continue;
                }

                if (tree.Nodes.Length == 0 || tree.Nodes.Length > 40)
                {
                    errors.Add($"Skill tree '{tree.Id}': needs between 1 and 40 nodes");
                }

                var seen = new HashSet<string>();
                foreach (var node in tree.Nodes)
                {
                    var name = $"Skill tree '{tree.Id}' node '{node.Id}'";
                    if (!ProfileText.IsId(node.Id) || !seen.Add(node.Id))
                    {
                        errors.Add($"Skill tree '{tree.Id}': node id '{node.Id}' is empty, has a space or is repeated");
                        continue;
                    }

                    if (string.IsNullOrEmpty(node.Name))
                    {
                        errors.Add($"{name}: needs a name");
                    }

                    if (node.Cost < 0 || node.Cost > 10)
                    {
                        errors.Add($"{name}: cost {node.Cost} must be 0 to 10");
                    }

                    var abilityId = AbilityIn(weapon, node.Slot);
                    if (abilityId == null || !abilities.TryGet(abilityId, out var ability))
                    {
                        errors.Add($"{name}: the weapon has no skill in slot {node.Slot}");
                    }
                    else if (!Array.Exists(ability.Variants, v => v.Id == node.Variant))
                    {
                        errors.Add($"{name}: '{abilityId}' has no variant '{node.Variant}'");
                    }
                }

                foreach (var node in tree.Nodes)
                {
                    if (node.Requires.Length == 0)
                    {
                        continue;
                    }

                    var required = tree.Find(node.Requires);
                    if (required == null || required == node)
                    {
                        errors.Add($"Skill tree '{tree.Id}' node '{node.Id}': requires unknown node '{node.Requires}'");
                    }
                    else if (required.Slot == node.Slot)
                    {
                        errors.Add($"Skill tree '{tree.Id}' node '{node.Id}': cannot require a node in the same slot");
                    }
                    else if (Loops(tree, node))
                    {
                        errors.Add($"Skill tree '{tree.Id}' node '{node.Id}': its requirements loop");
                    }
                }
            }

            return errors;
        }

        static bool Loops(SkillTreeDef tree, SkillNodeDef start)
        {
            var current = start;
            for (var steps = 0; steps <= tree.Nodes.Length; steps++)
            {
                if (current == null || current.Requires.Length == 0)
                {
                    return false;
                }

                current = tree.Find(current.Requires);
                if (current == start)
                {
                    return true;
                }
            }

            return true;
        }

        public static string AbilityIn(ClassDef weapon, int slot)
        {
            if (slot == 0)
            {
                return weapon.AutoAttack;
            }

            if (slot >= 1 && slot <= ClassDef.SkillSlots)
            {
                return slot - 1 < weapon.Skills.Length ? weapon.Skills[slot - 1] : null;
            }

            return slot == Entity.DefenseSlot ? weapon.Defense : null;
        }
    }
}
