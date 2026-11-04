using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // Spots enemies that fell between snapshots. A fallen enemy stays in the snapshot, so a missed packet loses nothing,
    // and ones already down when the client joins are not counted.
    public sealed class KillTracker
    {
        readonly HashSet<EntityId> counted = new HashSet<EntityId>();
        readonly List<FallenEnemy> scratch = new List<FallenEnemy>();
        bool primed;

        public void Observe(Snapshot snapshot, out int kills, out int bosses)
        {
            kills = 0;
            bosses = 0;
            scratch.Clear();
            Collect(snapshot, scratch);
            foreach (var fallen in scratch)
            {
                if (fallen.Boss)
                {
                    bosses++;
                }
                else
                {
                    kills++;
                }
            }
        }

        // Adds every enemy that fell since the last call, with its class so the caller can look up what it was worth.
        public void Collect(Snapshot snapshot, List<FallenEnemy> into)
        {
            if (snapshot == null)
            {
                return;
            }

            for (var i = 0; i < snapshot.Entities.Count; i++)
            {
                var entity = snapshot.Entities[i];
                if (entity.Team != Team.Enemies || entity.Health > 0f || !counted.Add(entity.Id) || !primed)
                {
                    continue;
                }

                if ((entity.Effects & EffectFlags.Summoned) != 0)
                {
                    continue;
                }

                into.Add(new FallenEnemy(entity.ClassHash, (entity.Effects & EffectFlags.Boss) != 0));
            }

            primed = true;
        }
    }

    public readonly struct FallenEnemy
    {
        public FallenEnemy(int classHash, bool boss)
        {
            ClassHash = classHash;
            Boss = boss;
        }

        public int ClassHash { get; }

        public bool Boss { get; }
    }

    // Trains the weapon type in hand from what the client sees die, and keeps the profile saved.
    public sealed class MasteryProgress
    {
        const float SaveEverySeconds = 20f;
        const float NoticeSeconds = 5f;

        readonly ProfileStore store;
        readonly MasteryRules rules;
        readonly KillTracker kills = new KillTracker();
        bool dirty;
        float sinceSave;
        string notice = string.Empty;
        float noticeLeft;
        readonly LootTableDef loot;
        readonly ulong lootSeed;
        readonly RewardBook rewards;
        readonly Registry<LootTableDef> tables;
        Registry<WeaponModifierDef> modifiers = new Registry<WeaponModifierDef>();
        readonly List<FallenEnemy> fallen = new List<FallenEnemy>();
        ulong chanceRolls;

        // What an enemy is worth comes from its class (rewards, and the tables it names). A boss whose class names no table drops
        // from the fallback table `loot` (optional), always; any other enemy without one drops nothing. The seed varies the rolls
        // between sessions; a test passes its own.
        public MasteryProgress(ProfileStore store, MasteryRules rules, LootTableDef loot = null, ulong lootSeed = 0, RewardBook rewards = null, Registry<LootTableDef> tables = null)
        {
            this.store = store;
            this.rules = rules;
            this.loot = loot;
            this.rewards = rewards ?? new RewardBook(null, rules);
            this.tables = tables;
            this.lootSeed = lootSeed != 0 ? lootSeed : (ulong)System.Environment.TickCount * 2654435761UL;
            Profile = store.Load();
        }

        public PlayerProfile Profile { get; }

        public MasteryRules Rules => rules;

        public bool Dirty => dirty;

        // Carries a weapon of that type: the one already in hand if it matches, otherwise the strongest owned.
        public void Equip(string weaponType)
        {
            var before = Profile.EquippedUid;
            if (Profile.EquipType(weaponType) && Profile.EquippedUid != before)
            {
                dirty = true;
            }
        }

        // Picks a specific owned weapon (from the inventory screen) and saves.
        public bool EquipWeapon(string uid)
        {
            if (!Profile.Equip(uid))
            {
                return false;
            }

            dirty = true;
            Flush();
            return true;
        }

        public int Potions => Profile.Potions;

        public int Gold => Profile.Gold;

        // Drinks one from the bag (the caller sends the command) and saves at once.
        public bool TryUsePotion()
        {
            if (!Profile.TryUsePotion())
            {
                return false;
            }

            dirty = true;
            Flush();
            return true;
        }

        // The modifiers a dropped weapon can carry, to work out what the weapon in hand adds up to.
        public void UseModifiers(Registry<WeaponModifierDef> known)
        {
            modifiers = known ?? new Registry<WeaponModifierDef>();
        }

        // What the weapon in hand and its modifier give: the power, health and speed scales sent when joining.
        public GearStats Gear => Game.Core.Gear.Of(Profile.Equipped, modifiers);

        // The power scale of the weapon in hand (its own, with its modifier), sent when joining.
        public float WeaponPower => Gear.Power;

        // Spends points on a tree node for a weapon type and saves at once (the loadout is sent when the session starts).
        public bool TryChoose(SkillTreeDef tree, string weaponType, string nodeId, out string reason)
        {
            if (!SkillTreeRules.TryChoose(tree, Profile.MasteryFor(weaponType), rules, nodeId, out reason))
            {
                return false;
            }

            dirty = true;
            Flush();
            return true;
        }

        public void ResetSkills(string weaponType)
        {
            Profile.MasteryFor(weaponType).Reset();
            dirty = true;
            Flush();
        }

        // Returns the levels gained from what fell since the last call.
        public int Observe(Snapshot snapshot)
        {
            fallen.Clear();
            kills.Collect(snapshot, fallen);
            if (fallen.Count == 0)
            {
                return 0;
            }

            var gained = 0;
            foreach (var enemy in fallen)
            {
                gained += Profile.AddXp(rules, rewards.For(enemy.ClassHash, enemy.Boss).Xp);
            }

            dirty = true;
            if (gained > 0)
            {
                notice = $"{Name(Profile.EquippedType)} mastery level {Level()}!";
                noticeLeft = NoticeSeconds;
            }

            var dropped = false;
            foreach (var enemy in fallen)
            {
                var reward = rewards.For(enemy.ClassHash, enemy.Boss);
                if (reward.Gold > 0)
                {
                    Profile.AddGold(reward.Gold);
                }

                if (reward.PotionChance > 0f && Roll(reward.PotionChance) && Profile.AddPotion())
                {
                    notice = "Found a health potion";
                    noticeLeft = NoticeSeconds;
                    dropped = true;
                }

                var table = TableFor(reward, enemy.Boss, out var chance);
                if (table != null && Roll(chance))
                {
                    Drop(table, enemy.Boss);
                    dropped = true;
                }
            }

            if (gained > 0 || dropped)
            {
                Flush();
            }

            return gained;
        }

        LootTableDef TableFor(KillReward reward, bool boss, out float chance)
        {
            chance = reward.DropChance;
            if (reward.LootTable.Length > 0 && tables != null && tables.TryGet(reward.LootTable, out var named))
            {
                return named;
            }

            chance = 1f;
            return boss && reward.LootTable.Length == 0 ? loot : null;
        }

        bool Roll(float chance)
        {
            if (chance >= 1f)
            {
                return true;
            }

            return chance > 0f && new SeededRandom(lootSeed ^ (++chanceRolls * 104729UL)).NextFloat() < chance;
        }

        // Personal loot: the enemy that fell gives this player a weapon of their own (unless the bag is full).
        void Drop(LootTableDef loot, bool boss)
        {
            if (Profile.Weapons.Count >= PlayerProfile.MaxWeapons)
            {
                if (boss)
                {
                    notice = "Boss down. Your weapon bag is full, so the drop was lost.";
                    noticeLeft = NoticeSeconds;
                }

                return;
            }

            var rolls = Profile.LootRolls++;
            var weapon = LootRoller.Roll(loot, new SeededRandom(lootSeed + (ulong)rolls * 7919UL), rolls + 1);
            if (weapon != null && Profile.Add(weapon))
            {
                notice = "Dropped: " + Describe(weapon);
                noticeLeft = NoticeSeconds;
            }
        }

        // "Staff +8%": the weapon type and how far its power is from a starter weapon.
        public static string Describe(OwnedWeapon weapon)
        {
            var percent = (int)System.Math.Round((weapon.Power - 1f) * 100f);
            var text = percent == 0 ? Name(weapon.Type) : $"{Name(weapon.Type)} {(percent > 0 ? "+" : string.Empty)}{percent}%";
            return string.IsNullOrEmpty(weapon.Modifier) ? text : $"{text} ({Name(weapon.Modifier)})";
        }

        public void Tick(float dt)
        {
            noticeLeft = System.Math.Max(0f, noticeLeft - dt);
            if (!dirty)
            {
                sinceSave = 0f;
                return;
            }

            sinceSave += dt;
            if (sinceSave >= SaveEverySeconds)
            {
                Flush();
            }
        }

        public void Flush()
        {
            if (dirty && store.Save(Profile))
            {
                dirty = false;
            }

            sinceSave = 0f;
        }

        public int Level()
        {
            var type = Profile.EquippedType;
            return type.Length == 0 ? 0 : Profile.MasteryFor(type).Level(rules);
        }

        // One line for the screen: the weapon, its mastery level and the bar toward the next one; the level-up notice replaces it for a few seconds.
        public string Summary()
        {
            if (noticeLeft > 0f)
            {
                return notice;
            }

            var type = Profile.EquippedType;
            if (type.Length == 0)
            {
                return string.Empty;
            }

            var mastery = Profile.MasteryFor(type);
            rules.Progress(mastery.Xp, out var into, out var needed);
            var level = mastery.Level(rules);
            var bar = needed > 0 ? $"{into}/{needed}" : "max";
            var points = mastery.PointsAvailable(rules);
            return $"{Describe(Profile.Equipped)} mastery {level} ({bar}){(points > 0 ? $", {points} {(points == 1 ? "point" : "points")}" : string.Empty)} - {Profile.Gold} gold";
        }

        public static string Name(string weaponType)
        {
            if (string.IsNullOrEmpty(weaponType))
            {
                return string.Empty;
            }

            var words = weaponType.Replace('_', ' ');
            return char.ToUpperInvariant(words[0]) + words.Substring(1);
        }
    }
}
