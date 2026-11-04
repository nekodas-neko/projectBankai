using System.Collections.Generic;

namespace Game.Core
{
    // An armor set: what a character wears besides the weapon. For now it gives small scales on health, speed and power (sent
    // when joining, like a weapon modifier); Look names the outfit art for when there is some.
    public sealed class ArmorSetDef : IDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public float PowerScale { get; set; } = 1f;
        public float HealthScale { get; set; } = 1f;
        public float SpeedScale { get; set; } = 1f;
        public string Look { get; set; } = string.Empty;
    }

    // A set the character owns.
    public sealed class OwnedArmor
    {
        public string Uid { get; set; }
        public string Set { get; set; }
    }

    public static class ArmorRules
    {
        public static List<string> Validate(Registry<ArmorSetDef> sets)
        {
            var errors = new List<string>();
            foreach (var set in sets.All)
            {
                if (string.IsNullOrWhiteSpace(set.Name))
                {
                    errors.Add($"Armor set '{set.Id}': needs a name");
                }

                foreach (var scale in new[] { set.PowerScale, set.HealthScale, set.SpeedScale })
                {
                    if (float.IsNaN(scale) || scale < GameServer.MinWeaponPower || scale > GameServer.MaxWeaponPower)
                    {
                        errors.Add($"Armor set '{set.Id}': every scale must be between {GameServer.MinWeaponPower} and {GameServer.MaxWeaponPower}");
                        break;
                    }
                }
            }

            return errors;
        }
    }
}
