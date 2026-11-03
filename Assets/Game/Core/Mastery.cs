using System;
using System.Collections.Generic;

namespace Game.Core
{
    // How mastery is earned and spent. Data: the client reads it from a MasteryAsset, and a test can build its own.
    public sealed class MasteryRules
    {
        // XP needed to go from level n to level n + 1, one entry per level; the length is the level cap.
        public int[] XpPerLevel { get; set; } = DefaultXpPerLevel();
        public int PointsPerLevel { get; set; } = 1;
        public int XpPerKill { get; set; } = 10;
        public int XpPerBossKill { get; set; } = 150;

        public int MaxLevel => XpPerLevel.Length;

        public int MaxXp
        {
            get
            {
                var total = 0;
                foreach (var step in XpPerLevel)
                {
                    total += step;
                }

                return total;
            }
        }

        public static int[] DefaultXpPerLevel()
        {
            var steps = new int[20];
            for (var i = 0; i < steps.Length; i++)
            {
                steps[i] = 100 + 25 * i;
            }

            return steps;
        }

        public int LevelFor(int xp)
        {
            var level = 0;
            var remaining = Math.Max(0, xp);
            while (level < XpPerLevel.Length && remaining >= XpPerLevel[level])
            {
                remaining -= XpPerLevel[level];
                level++;
            }

            return level;
        }

        // XP already earned toward the next level, and what that level costs (0 at the cap).
        public void Progress(int xp, out int into, out int needed)
        {
            var level = LevelFor(xp);
            var spent = 0;
            for (var i = 0; i < level; i++)
            {
                spent += XpPerLevel[i];
            }

            into = Math.Max(0, xp) - spent;
            needed = level < XpPerLevel.Length ? XpPerLevel[level] : 0;
        }

        public int XpForKill(bool boss)
        {
            return boss ? XpPerBossKill : XpPerKill;
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (XpPerLevel.Length == 0 || XpPerLevel.Length > 100)
            {
                errors.Add($"Mastery: {XpPerLevel.Length} levels, expected 1 to 100");
            }

            for (var i = 0; i < XpPerLevel.Length; i++)
            {
                if (XpPerLevel[i] <= 0)
                {
                    errors.Add($"Mastery: level {i + 1} needs {XpPerLevel[i]} XP, must be above 0");
                }
            }

            if (PointsPerLevel < 0 || PointsPerLevel > 10)
            {
                errors.Add($"Mastery: {PointsPerLevel} points per level, expected 0 to 10");
            }

            if (XpPerKill < 0 || XpPerBossKill < 0)
            {
                errors.Add("Mastery: kill XP cannot be negative");
            }

            return errors;
        }
    }

    // What a character has earned with one weapon type (staff, healing mace, ...). It belongs to the character, not to a weapon item.
    public sealed class WeaponMastery
    {
        public int Xp { get; set; }
        public List<string> Choices { get; } = new List<string>();
        public int PointsSpent { get; set; }

        public int Level(MasteryRules rules)
        {
            return rules.LevelFor(Xp);
        }

        public int PointsEarned(MasteryRules rules)
        {
            return Level(rules) * rules.PointsPerLevel;
        }

        public int PointsAvailable(MasteryRules rules)
        {
            return Math.Max(0, PointsEarned(rules) - PointsSpent);
        }

        // Adds XP (capped at the last level) and returns how many levels it gained.
        public int AddXp(MasteryRules rules, int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            var before = Level(rules);
            Xp = (int)Math.Min((long)Xp + amount, rules.MaxXp);
            return Level(rules) - before;
        }

        // Takes a tree node: it costs points, and picking it twice does nothing.
        public bool TryChoose(MasteryRules rules, string node, int cost)
        {
            if (string.IsNullOrEmpty(node) || cost < 0 || Choices.Contains(node) || cost > PointsAvailable(rules))
            {
                return false;
            }

            Choices.Add(node);
            PointsSpent += cost;
            return true;
        }

        // Free for now: every point goes back to the pool and the default kit returns.
        public void Reset()
        {
            Choices.Clear();
            PointsSpent = 0;
        }
    }
}
