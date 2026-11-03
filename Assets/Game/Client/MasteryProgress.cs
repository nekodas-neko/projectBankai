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

        public MasteryProgress(ProfileStore store, MasteryRules rules)
        {
            this.store = store;
            this.rules = rules;
            Profile = store.Load();
        }

        public PlayerProfile Profile { get; }

        public bool Dirty => dirty;

        // Starter weapons are owned under their type name, so the weapon in hand is equipped by that name.
        public void Equip(string weaponType)
        {
            if (Profile.Equip(weaponType) && Profile.EquippedUid == weaponType)
            {
                dirty = true;
            }
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

            if (gained > 0 || bosses > 0)
            {
                Flush();
            }

            return gained;
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
            return $"{Name(type)} mastery {level} ({bar}){(points > 0 ? $", {points} {(points == 1 ? "point" : "points")}" : string.Empty)}";
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
