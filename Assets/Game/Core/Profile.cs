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
        public List<OwnedArmor> Armors { get; } = new List<OwnedArmor>();
        public string EquippedArmorUid { get; set; } = string.Empty;
        public const int MaxArmors = 20;

        public OwnedArmor EquippedArmor
        {
            get
            {
                foreach (var armor in Armors)
                {
                    if (armor.Uid == EquippedArmorUid)
                    {
                        return armor;
                    }
                }

                return null;
            }
        }

        public bool AddArmor(OwnedArmor armor)
        {
            if (armor == null || !ProfileText.IsId(armor.Uid) || !ProfileText.IsId(armor.Set) || Armors.Count >= MaxArmors || Armors.Exists(a => a.Uid == armor.Uid))
            {
                return false;
            }

            Armors.Add(armor);
            return true;
        }

        public bool EquipArmor(string uid)
        {
            if (!Armors.Exists(a => a.Uid == uid))
            {
                return false;
            }

            EquippedArmorUid = uid;
            return true;
        }
        public string EquippedUid { get; set; } = string.Empty;

        // Gold: what kills, chests and dismantled weapons give, to be spent on things later.
        public const int MaxGold = 999999;
        public int Gold { get; set; }

        public void AddGold(int amount)
        {
            if (amount > 0)
            {
                Gold = (int)Math.Min((long)Gold + amount, MaxGold);
            }
        }

        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || amount > Gold)
            {
                return false;
            }

            Gold -= amount;
            return true;
        }

        // Health potions carried between runs.
        public const int MaxPotions = 5;
        public int Potions { get; set; }

        public bool AddPotion()
        {
            if (Potions >= MaxPotions)
            {
                return false;
            }

            Potions++;
            return true;
        }

        public bool TryUsePotion()
        {
            if (Potions <= 0)
            {
                return false;
            }

            Potions--;
            return true;
        }

        // How many drops have been rolled; it seeds the next roll and numbers the weapon's id.
        public int LootRolls { get; set; }

        // The inventory holds this many weapons; a drop that would not fit is lost.
        public const int MaxWeapons = 30;

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

            profile.AddArmor(new OwnedArmor { Uid = "wanderer", Set = "wanderer" });
            profile.EquippedArmorUid = "wanderer";

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
            if (weapon == null || Weapons.Count >= MaxWeapons || !ProfileText.IsId(weapon.Uid) || !ProfileText.IsId(weapon.Type) || Has(weapon.Uid))
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
        // The weapon of that type to carry: the one already in hand if it is that type, otherwise the strongest one owned.
        public bool EquipType(string weaponType)
        {
            if (EquippedType == weaponType)
            {
                return true;
            }

            OwnedWeapon best = null;
            foreach (var weapon in Weapons)
            {
                if (weapon.Type == weaponType && (best == null || weapon.Power > best.Power))
                {
                    best = weapon;
                }
            }

            return best != null && Equip(best.Uid);
        }

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
            return AddXp(rules, rules.XpForKill(boss));
        }

        // Trains the weapon type in hand by an amount (a kill's reward). Returns the levels gained.
        public int AddXp(MasteryRules rules, int xp)
        {
            var type = EquippedType;
            return type.Length == 0 || xp <= 0 ? 0 : MasteryFor(type).AddXp(rules, xp);
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
            text.Append("gold ").Append(profile.Gold).Append('\n');
            text.Append("potions ").Append(profile.Potions).Append('\n');
            text.Append("equipped ").Append(profile.EquippedUid.Length == 0 ? "-" : profile.EquippedUid).Append('\n');
            foreach (var armor in profile.Armors)
            {
                text.Append("armor ").Append(armor.Uid).Append(' ').Append(armor.Set).Append('\n');
            }

            text.Append("armor_equipped ").Append(profile.EquippedArmorUid.Length == 0 ? "-" : profile.EquippedArmorUid).Append('\n');
            foreach (var weapon in profile.Weapons)
            {
                text.Append("weapon ").Append(weapon.Uid).Append(' ').Append(weapon.Type).Append(' ')
                    .Append(weapon.Power.ToString("R", CultureInfo.InvariantCulture)).Append(' ')
                    .Append(string.IsNullOrEmpty(weapon.Modifier) ? "-" : weapon.Modifier).Append('\n');
            }

            text.Append("loot ").Append(profile.LootRolls).Append('\n');
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
            var wornArmor = string.Empty;
            for (var i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Trim().Split(' ');
                if (parts.Length == 0 || parts[0].Length == 0)
                {
                    continue;
                }

                if (!Apply(profile, parts, ref equipped, ref wornArmor, out error))
                {
                    error = $"line {i + 1}: {error}";
                    profile = new PlayerProfile();
                    return false;
                }
            }

            profile.EquippedArmorUid = profile.Armors.Exists(a => a.Uid == wornArmor) ? wornArmor : string.Empty;
            profile.EquippedUid = profile.Has(equipped) ? equipped : (profile.Weapons.Count > 0 ? profile.Weapons[0].Uid : string.Empty);
            return true;
        }

        static bool Apply(PlayerProfile profile, string[] parts, ref string equipped, ref string wornArmor, out string error)
        {
            error = null;
            switch (parts[0])
            {
                case "armor" when parts.Length == 3:
                    if (!profile.AddArmor(new OwnedArmor { Uid = parts[1], Set = parts[2] }))
                    {
                        error = "bad or repeated armor";
                        return false;
                    }

                    return true;
                case "armor_equipped" when parts.Length == 2:
                    wornArmor = parts[1] == "-" ? string.Empty : parts[1];
                    return true;
                case "gold" when parts.Length == 2:
                    if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var gold))
                    {
                        error = "bad gold";
                        return false;
                    }

                    profile.Gold = Math.Min(gold, PlayerProfile.MaxGold);
                    return true;
                case "potions" when parts.Length == 2:
                    if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var potions))
                    {
                        error = "bad potions";
                        return false;
                    }

                    profile.Potions = Math.Min(potions, PlayerProfile.MaxPotions);
                    return true;
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
                case "loot" when parts.Length == 2:
                    if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var rolls))
                    {
                        error = "bad loot count";
                        return false;
                    }

                    profile.LootRolls = rolls;
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
                case "armor":
                case "armor_equipped":
                case "gold":
                case "potions":
                case "equipped":
                case "weapon":
                case "loot":
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
