using System.Collections.Generic;

namespace Game.Core
{
    // What one kill is worth to the player who sees it.
    public readonly struct KillReward
    {
        public KillReward(int xp, string lootTable, float dropChance)
        {
            Xp = xp;
            LootTable = lootTable ?? string.Empty;
            DropChance = dropChance;
        }

        public int Xp { get; }

        public string LootTable { get; }

        public float DropChance { get; }
    }

    // Looks up the reward of a fallen enemy from the class data, using the class hash that every snapshot entity carries.
    // An enemy the book does not know gives the default XP and no drop.
    public sealed class RewardBook
    {
        readonly Dictionary<int, ClassDef> byHash = new Dictionary<int, ClassDef>();
        readonly MasteryRules rules;

        public RewardBook(Registry<ClassDef> classes, MasteryRules rules)
        {
            this.rules = rules;
            if (classes == null)
            {
                return;
            }

            foreach (var def in classes.All)
            {
                byHash[StableId.From(def.Id)] = def;
            }
        }

        public KillReward For(int classHash, bool boss)
        {
            if (!byHash.TryGetValue(classHash, out var def))
            {
                return new KillReward(rules.XpForKill(boss), string.Empty, 0f);
            }

            return new KillReward(def.XpReward > 0 ? def.XpReward : rules.XpForKill(boss), def.LootTable, def.DropChance);
        }
    }
}
