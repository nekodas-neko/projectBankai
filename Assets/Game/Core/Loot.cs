using System;
using System.Collections.Generic;

namespace Game.Core
{
    // What a boss can drop: one of these weapon types with a power scale rolled inside a small range.
    public sealed class LootTableDef : IDefinition
    {
        public string Id { get; set; }
        public string[] WeaponTypes { get; set; } = Array.Empty<string>();
        public float PowerMin { get; set; } = 0.95f;
        public float PowerMax { get; set; } = 1.15f;
    }

    public static class LootRoller
    {
        // Personal loot: every player rolls their own. The uid is unique inside the profile (type plus the roll number).
        public static OwnedWeapon Roll(LootTableDef table, IRandom random, int rollNumber)
        {
            if (table.WeaponTypes.Length == 0)
            {
                return null;
            }

            var type = table.WeaponTypes[(int)(random.NextFloat() * table.WeaponTypes.Length) % table.WeaponTypes.Length];
            var power = table.PowerMin + (table.PowerMax - table.PowerMin) * random.NextFloat();
            return new OwnedWeapon { Uid = $"{type}-drop{rollNumber}", Type = type, Power = (float)Math.Round(power, 2) };
        }

        public static List<string> Validate(Registry<LootTableDef> tables, Registry<ClassDef> classes)
        {
            var errors = new List<string>();
            foreach (var table in tables.All)
            {
                if (table.WeaponTypes.Length == 0)
                {
                    errors.Add($"Loot table '{table.Id}': needs at least one weapon type");
                }

                foreach (var type in table.WeaponTypes)
                {
                    if (!classes.TryGet(type, out var weapon) || !weapon.Playable)
                    {
                        errors.Add($"Loot table '{table.Id}': '{type}' is not a playable weapon");
                    }
                }

                if (table.PowerMin > table.PowerMax || table.PowerMin < GameServer.MinWeaponPower || table.PowerMax > GameServer.MaxWeaponPower)
                {
                    errors.Add($"Loot table '{table.Id}': power must satisfy {GameServer.MinWeaponPower} <= min <= max <= {GameServer.MaxWeaponPower}");
                }
            }

            return errors;
        }
    }
}
