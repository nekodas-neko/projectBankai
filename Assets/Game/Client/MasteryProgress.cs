using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // Spots enemies that fell between snapshots. A fallen enemy stays in the snapshot, so a missed packet loses nothing,
    // and ones already down when the client joins are not counted.
    public sealed class KillTracker
    {
        readonly HashSet<EntityId> counted = new HashSet<EntityId>();
        bool primed;

        public void Observe(Snapshot snapshot, out int kills, out int bosses)
        {
            kills = 0;
            bosses = 0;
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

                if ((entity.Effects & EffectFlags.Boss) != 0)
                {
                    bosses++;
                }
                else
                {
                    kills++;
                }
            }

            primed = true;
        }
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

        // The loot table is optional (no table, no drops). The seed varies the rolls between sessions; a test passes its own.
        public MasteryProgress(ProfileStore store, MasteryRules rules, LootTableDef loot = null, ulong lootSeed = 0)
        {
            this.store = store;
            this.rules = rules;
            this.loot = loot;
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

        // The power scale of the weapon in hand, sent when joining.
        public float WeaponPower => Profile.Equipped?.Power ?? 1f;

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
            kills.Observe(snapshot, out var normal, out var bosses);
            if (normal + bosses == 0)
            {
                return 0;
            }

            var gained = 0;
            for (var i = 0; i < normal; i++)
            {
                gained += Profile.AddKillXp(rules, false);
            }

            for (var i = 0; i < bosses; i++)
            {
                gained += Profile.AddKillXp(rules, true);
            }

            dirty = true;
            if (gained > 0)
            {
                notice = $"{Name(Profile.EquippedType)} mastery level {Level()}!";
                noticeLeft = NoticeSeconds;
            }

            for (var i = 0; i < bosses; i++)
            {
                Drop();
            }

            if (gained > 0 || bosses > 0)
            {
                Flush();
            }

            return gained;
        }

        // Personal loot: the boss that fell gives this player a weapon of their own (unless the bag is full).
        void Drop()
        {
            if (loot == null)
            {
                return;
            }

            if (Profile.Weapons.Count >= PlayerProfile.MaxWeapons)
            {
                notice = "Boss down. Your weapon bag is full, so the drop was lost.";
                noticeLeft = NoticeSeconds;
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
            return percent == 0 ? Name(weapon.Type) : $"{Name(weapon.Type)} {(percent > 0 ? "+" : string.Empty)}{percent}%";
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
            return $"{Describe(Profile.Equipped)} mastery {level} ({bar}){(points > 0 ? $", {points} {(points == 1 ? "point" : "points")}" : string.Empty)}";
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
