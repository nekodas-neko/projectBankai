using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Core
{
    // A weapon the character owns: the type picks the kit and the mastery it trains, the rest is that item's own numbers.
    public sealed class OwnedWeapon
    {
        public string Uid { get; set; }
        public string Type { get; set; }
        public float Power { get; set; } = 1f;
        public string Modifier { get; set; } = string.Empty;
    }

    // Everything that belongs to the character and not to a session: mastery per weapon type, the weapons it owns, the one it carries.
    public sealed class PlayerProfile
    {
        public Dictionary<string, WeaponMastery> Mastery { get; } = new Dictionary<string, WeaponMastery>();
        public List<OwnedWeapon> Weapons { get; } = new List<OwnedWeapon>();
        public string EquippedUid { get; set; } = string.Empty;

        public OwnedWeapon Equipped
        {
            get
            {
                foreach (var weapon in Weapons)
                {
                    if (weapon.Uid == EquippedUid)
                    {
                        return weapon;
                    }
                }

                return null;
            }
        }

        // The weapon type in hand, or an empty string when nothing is equipped.
        public string EquippedType => Equipped?.Type ?? string.Empty;

        // A new character: one default weapon of each type, the first one equipped, no mastery yet.
        public static PlayerProfile Starter(IEnumerable<string> weaponTypes)
        {
            var profile = new PlayerProfile();
            foreach (var type in weaponTypes)
            {
                profile.Add(new OwnedWeapon { Uid = type, Type = type });
            }

            if (profile.Weapons.Count > 0)
            {
                profile.EquippedUid = profile.Weapons[0].Uid;
            }

            return profile;
        }

        public WeaponMastery MasteryFor(string weaponType)
        {
            if (!Mastery.TryGetValue(weaponType, out var mastery))
            {
                mastery = new WeaponMastery();
                Mastery[weaponType] = mastery;
            }

            return mastery;
        }

        public bool Add(OwnedWeapon weapon)
        {
            if (weapon == null || !ProfileText.IsId(weapon.Uid) || !ProfileText.IsId(weapon.Type) || Has(weapon.Uid))
            {
                return false;
            }

            Weapons.Add(weapon);
            return true;
        }

        public bool Has(string uid)
        {
            foreach (var weapon in Weapons)
            {
                if (weapon.Uid == uid)
                {
                    return true;
                }
            }

            return false;
        }

        // Only a weapon the character owns can be equipped.
        public bool Equip(string uid)
        {
            if (!Has(uid))
            {
                return false;
            }

            EquippedUid = uid;
            return true;
        }

        // Kills train the weapon type in hand. Returns the levels gained.
        public int AddKillXp(MasteryRules rules, bool boss)
        {
            var type = EquippedType;
            return type.Length == 0 ? 0 : MasteryFor(type).AddXp(rules, rules.XpForKill(boss));
        }
    }

    // The profile as a small text file: a version line, then one line per fact. Unknown lines are skipped so a newer build's file still loads.
    public static class ProfileText
    {
        public const int Version = 1;
        const string Header = "bankai-profile";

        public static bool IsId(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "-")
            {
                return false;
            }

            foreach (var c in value)
            {
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.'))
                {
                    return false;
                }
            }

            return true;
        }

        public static string Write(PlayerProfile profile)
        {
            var text = new StringBuilder();
            text.Append(Header).Append(' ').Append(Version).Append('\n');
            text.Append("equipped ").Append(profile.EquippedUid.Length == 0 ? "-" : profile.EquippedUid).Append('\n');
            foreach (var weapon in profile.Weapons)
            {
                text.Append("weapon ").Append(weapon.Uid).Append(' ').Append(weapon.Type).Append(' ')
                    .Append(weapon.Power.ToString("R", CultureInfo.InvariantCulture)).Append(' ')
                    .Append(string.IsNullOrEmpty(weapon.Modifier) ? "-" : weapon.Modifier).Append('\n');
            }

            var types = new List<string>(profile.Mastery.Keys);
            types.Sort(StringComparer.Ordinal);
            foreach (var type in types)
            {
                var mastery = profile.Mastery[type];
                text.Append("mastery ").Append(type).Append(' ').Append(mastery.Xp).Append(' ').Append(mastery.PointsSpent).Append('\n');
                foreach (var choice in mastery.Choices)
                {
                    text.Append("choice ").Append(type).Append(' ').Append(choice).Append('\n');
                }
            }

            return text.ToString();
        }

        // False with a reason when the text is not a profile this build can read; the profile is then empty.
        public static bool TryParse(string text, out PlayerProfile profile, out string error)
        {
            profile = new PlayerProfile();
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "empty";
                return false;
            }

            var lines = text.Split('\n');
            var head = lines[0].Trim().Split(' ');
            if (head.Length != 2 || head[0] != Header || !int.TryParse(head[1], NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            {
                error = "not a profile file";
                return false;
            }

            if (version > Version)
            {
                error = $"written by a newer build (version {version})";
                return false;
            }

            var equipped = string.Empty;
            for (var i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Trim().Split(' ');
                if (parts.Length == 0 || parts[0].Length == 0)
                {
                    continue;
                }

                if (!Apply(profile, parts, ref equipped, out error))
                {
                    error = $"line {i + 1}: {error}";
                    profile = new PlayerProfile();
                    return false;
                }
            }

            profile.EquippedUid = profile.Has(equipped) ? equipped : (profile.Weapons.Count > 0 ? profile.Weapons[0].Uid : string.Empty);
            return true;
        }

        static bool Apply(PlayerProfile profile, string[] parts, ref string equipped, out string error)
        {
            error = null;
            switch (parts[0])
            {
                case "equipped" when parts.Length == 2:
                    equipped = parts[1] == "-" ? string.Empty : parts[1];
                    return true;
                case "weapon" when parts.Length == 5:
                    if (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var power) || power <= 0f || float.IsInfinity(power))
                    {
                        error = "bad weapon power";
                        return false;
                    }

                    var weapon = new OwnedWeapon { Uid = parts[1], Type = parts[2], Power = power, Modifier = parts[4] == "-" ? string.Empty : parts[4] };
                    if (!profile.Add(weapon))
                    {
                        error = "bad or repeated weapon";
                        return false;
                    }

                    return true;
                case "mastery" when parts.Length == 4:
                    if (!IsId(parts[1]) || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var xp)
                        || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var spent))
                    {
                        error = "bad mastery";
                        return false;
                    }

                    var mastery = profile.MasteryFor(parts[1]);
                    mastery.Xp = xp;
                    mastery.PointsSpent = spent;
                    return true;
                case "choice" when parts.Length == 3:
                    if (!IsId(parts[1]) || !IsId(parts[2]))
                    {
                        error = "bad choice";
                        return false;
                    }

                    profile.MasteryFor(parts[1]).Choices.Add(parts[2]);
                    return true;
                case "equipped":
                case "weapon":
                case "mastery":
                case "choice":
                    error = $"'{parts[0]}' has the wrong number of fields";
                    return false;
                default:
                    return true;
            }
        }
    }
}
