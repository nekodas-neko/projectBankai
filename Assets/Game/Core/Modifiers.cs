using System;
using System.Collections.Generic;

namespace Game.Core
{
    // A small bonus a dropped weapon can carry: a few percent on power, health or movement speed. Kept to numbers so a new one is a
    // data file. Scales stay close to 1; the server clamps what a client claims to the same limits as a drop can roll.
    public sealed class WeaponModifierDef : IDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public float PowerScale { get; set; } = 1f;
        public float HealthScale { get; set; } = 1f;
        public float SpeedScale { get; set; } = 1f;
    }

    // What the gear a player carries adds up to: scales on top of the class numbers, sent when joining.
    public readonly struct GearStats
    {
        public GearStats(float power, float health, float speed)
        {
            Power = power;
            Health = health;
            Speed = speed;
        }

        public float Power { get; }

        public float Health { get; }

        public float Speed { get; }

        public static GearStats None => new GearStats(1f, 1f, 1f);
    }

    public static class Gear
    {
        // The weapon's own power times its modifier's scales. A modifier id the registry does not know counts as none.
        public static GearStats Of(OwnedWeapon weapon, Registry<WeaponModifierDef> modifiers)
        {
            if (weapon == null)
            {
                return GearStats.None;
            }

            var power = weapon.Power;
            var health = 1f;
            var speed = 1f;
            if (!string.IsNullOrEmpty(weapon.Modifier) && modifiers != null && modifiers.TryGet(weapon.Modifier, out var modifier))
            {
                power *= modifier.PowerScale;
                health *= modifier.HealthScale;
                speed *= modifier.SpeedScale;
            }

            return new GearStats(power, health, speed);
        }

        // The weapon in hand and the armor worn together: their scales multiply.
        public static GearStats Of(OwnedWeapon weapon, OwnedArmor armor, Registry<WeaponModifierDef> modifiers, Registry<ArmorSetDef> sets)
        {
            var stats = Of(weapon, modifiers);
            if (armor == null || sets == null || !sets.TryGet(armor.Set, out var set))
            {
                return stats;
            }

            return new GearStats(stats.Power * set.PowerScale, stats.Health * set.HealthScale, stats.Speed * set.SpeedScale);
        }

        public static List<string> Validate(Registry<WeaponModifierDef> modifiers)
        {
            var errors = new List<string>();
            foreach (var modifier in modifiers.All)
            {
                foreach (var scale in new[] { modifier.PowerScale, modifier.HealthScale, modifier.SpeedScale })
                {
                    if (float.IsNaN(scale) || scale < GameServer.MinWeaponPower || scale > GameServer.MaxWeaponPower)
                    {
                        errors.Add($"Modifier '{modifier.Id}': every scale must be between {GameServer.MinWeaponPower} and {GameServer.MaxWeaponPower}");
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(modifier.Name))
                {
                    errors.Add($"Modifier '{modifier.Id}': needs a name");
                }
            }

            return errors;
        }
    }
}
