using System.Collections.Generic;

namespace Game.Core
{
    public static class DefinitionValidator
    {
        public static List<string> Validate(Registry<ClassDef> classes, Registry<AbilityDef> abilities)
        {
            var errors = new List<string>();

            foreach (var ability in abilities.All)
            {
                if (ability.Cooldown < 0f)
                {
                    errors.Add($"Ability '{ability.Id}': cooldown must not be negative");
                }

                if (ability.HeavyDamageScale < 0f || ability.HeavyDamageScale > 10f || (ability.HeavyDamageScale > 0f && (ability.HeavyCooldownScale < 0.5f || ability.HeavyCooldownScale > 10f)))
                {
                    errors.Add($"Ability '{ability.Id}': heavy damage scale must be 0 to 10 (0 turns it off) and the heavy cooldown scale 0.5 to 10");
                }

                if (ability.Range <= 0f)
                {
                    errors.Add($"Ability '{ability.Id}': range must be above zero");
                }

                if (ability.Radius < 0f)
                {
                    errors.Add($"Ability '{ability.Id}': radius must not be negative");
                }

                if (ability.Damage < 0f)
                {
                    errors.Add($"Ability '{ability.Id}': damage must not be negative");
                }

                var seenVariants = new HashSet<string>();
                foreach (var variant in ability.Variants)
                {
                    if (string.IsNullOrEmpty(variant.Id) || !seenVariants.Add(variant.Id))
                    {
                        errors.Add($"Ability '{ability.Id}': variant ids must be non-empty and unique");
                    }

                    if (variant.DamageScale < 0f || variant.RangeScale <= 0f || variant.CooldownScale <= 0f || variant.CostScale < 0f)
                    {
                        errors.Add($"Ability '{ability.Id}': variant '{variant.Id}' has a non-positive scale");
                    }
                }

                if (ability.Heal < 0f || ability.Taunt < 0f)
                {
                    errors.Add($"Ability '{ability.Id}': heal and taunt must not be negative");
                }

                if (ability.Shape == AbilityShape.Circle && ability.Radius <= 0f)
                {
                    errors.Add($"Ability '{ability.Id}': a circle needs a radius");
                }

                if (ability.Shape == AbilityShape.Cone && (ability.Angle <= 0f || ability.Angle > 360f))
                {
                    errors.Add($"Ability '{ability.Id}': a cone needs an angle between 0 and 360");
                }

                if (ability.Windup < 0f)
                {
                    errors.Add($"Ability '{ability.Id}': windup must not be negative");
                }

                if (ability.ChargeRequired < 0f || ability.ChargeFromDealt < 0f || ability.ChargeFromTaken < 0f || ability.ChargeFromBlocked < 0f || ability.ChargeFromHealing < 0f)
                {
                    errors.Add($"Ability '{ability.Id}': charge numbers must not be negative");
                }

                if (ability.ChargeRequired > 0f && ability.ChargeFromDealt + ability.ChargeFromTaken + ability.ChargeFromBlocked + ability.ChargeFromHealing <= 0f)
                {
                    errors.Add($"Ability '{ability.Id}': needs charge but nothing charges it");
                }

                if (!string.IsNullOrEmpty(ability.Rider) && !abilities.TryGet(ability.Rider, out _))
                {
                    errors.Add($"Ability '{ability.Id}': unknown rider '{ability.Rider}'");
                }

                if (ability.Stun < 0f || ability.Stun > 10f)
                {
                    errors.Add($"Ability '{ability.Id}': stun must be between 0 and 10 seconds");
                }

                if (ability.Interrupt < 0f || ability.Interrupt > 10f)
                {
                    errors.Add($"Ability '{ability.Id}': interrupt must be between 0 and 10 seconds");
                }

                if (ability.Interruptible && (ability.Windup < 0.5f || ability.AtTarget))
                {
                    errors.Add($"Ability '{ability.Id}': an interruptible cast needs a windup of at least 0.5 s and cannot be a ground circle");
                }

                if (ability.SummonCount < 0 || ability.SummonCount > 8 || (ability.SummonCount > 0 && !classes.TryGet(ability.SummonClass ?? string.Empty, out _)))
                {
                    errors.Add($"Ability '{ability.Id}': summons between 0 and 8 of an existing class");
                }

                ValidateKind(ability, errors);
            }

            foreach (var def in classes.All)
            {
                if (def.Stats.MaxHealth <= 0f)
                {
                    errors.Add($"Class '{def.Id}': max health must be above zero");
                }

                if (def.Stats.MoveSpeed < 0f)
                {
                    errors.Add($"Class '{def.Id}': move speed must not be negative");
                }

                if (!string.IsNullOrEmpty(def.AutoAttack) && !abilities.TryGet(def.AutoAttack, out _))
                {
                    errors.Add($"Class '{def.Id}': unknown auto attack '{def.AutoAttack}'");
                }

                if (def.Skills.Length > ClassDef.SkillSlots)
                {
                    errors.Add($"Class '{def.Id}': {def.Skills.Length} skills, the limit is {ClassDef.SkillSlots}");
                }

                foreach (var skill in def.Skills)
                {
                    if (!abilities.TryGet(skill, out _))
                    {
                        errors.Add($"Class '{def.Id}': unknown skill '{skill}'");
                    }
                }

                if (!string.IsNullOrEmpty(def.Defense))
                {
                    if (!abilities.TryGet(def.Defense, out var defense))
                    {
                        errors.Add($"Class '{def.Id}': unknown defense '{def.Defense}'");
                    }
                    else if (defense.Kind == AbilityKind.Attack)
                    {
                        errors.Add($"Class '{def.Id}': defense '{def.Defense}' must be a dodge, block or shield");
                    }
                }

                if (def.BodyRadius < 0f)
                {
                    errors.Add($"Class '{def.Id}': body radius must not be negative");
                }

                if (def.ThreatMultiplier < 0f)
                {
                    errors.Add($"Class '{def.Id}': threat multiplier must not be negative");
                }

                if (def.AggroRange > 0f && string.IsNullOrEmpty(def.AutoAttack))
                {
                    errors.Add($"Class '{def.Id}': aggro range needs an auto attack");
                }

                if (def.LeashRange < 0f || (def.LeashRange > 0f && def.LeashRange <= def.AggroRange))
                {
                    errors.Add($"Class '{def.Id}': leash range must be 0 (none) or larger than the aggro range");
                }

                if (def.WanderRadius < 0f || def.WanderRadius > 30f || (def.WanderRadius > 0f && def.AggroRange <= 0f))
                {
                    errors.Add($"Class '{def.Id}': wander radius must be between 0 and 30, and a wanderer needs an aggro range");
                }

                if (def.RecoverySeconds < 0f || def.RecoverySeconds > 10f)
                {
                    errors.Add($"Class '{def.Id}': recovery seconds must be between 0 and 10");
                }

                if (def.XpReward < 0 || def.XpReward > 100000)
                {
                    errors.Add($"Class '{def.Id}': XP reward must be between 0 and 100000");
                }

                if (float.IsNaN(def.DropChance) || def.DropChance < 0f || def.DropChance > 1f)
                {
                    errors.Add($"Class '{def.Id}': drop chance must be between 0 and 1");
                }
                else if (def.DropChance > 0f && string.IsNullOrEmpty(def.LootTable))
                {
                    errors.Add($"Class '{def.Id}': a drop chance needs a loot table");
                }

                if (def.GoldReward < 0 || def.GoldReward > 100000 || (def.Playable && def.GoldReward != 0))
                {
                    errors.Add($"Class '{def.Id}': gold reward must be between 0 and 100000 and a weapon cannot carry one");
                }

                if (float.IsNaN(def.PotionChance) || def.PotionChance < 0f || def.PotionChance > 1f)
                {
                    errors.Add($"Class '{def.Id}': potion chance must be between 0 and 1");
                }

                if (def.Playable && (def.XpReward != 0 || def.DropChance != 0f || !string.IsNullOrEmpty(def.LootTable)))
                {
                    errors.Add($"Class '{def.Id}': a weapon cannot carry a reward");
                }

                ValidateResource(def, abilities, errors);
                ValidatePhases(def, classes, abilities, errors);
            }

            return errors;
        }

        public static List<string> ValidateDungeons(Registry<DungeonDef> dungeons, Registry<ClassDef> classes)
        {
            var errors = new List<string>();
            foreach (var dungeon in dungeons.All)
            {
                if (dungeon.Rooms.Length == 0 || dungeon.Rooms.Length > 250)
                {
                    errors.Add($"Dungeon '{dungeon.Id}': needs between 1 and 250 rooms");
                }

                if (dungeon.ClearDelaySeconds < 0f)
                {
                    errors.Add($"Dungeon '{dungeon.Id}': clear delay must not be negative");
                }

                if (dungeon.HealthPerExtraPlayer < 0f || dungeon.HealthPerExtraPlayer > 5f)
                {
                    errors.Add($"Dungeon '{dungeon.Id}': health per extra player must be between 0 and 5");
                }

                ValidateMap(dungeon, errors);

                for (var i = 0; i < dungeon.Rooms.Length; i++)
                {
                    var room = dungeon.Rooms[i];
                    if (room.Spawns.Length == 0)
                    {
                        errors.Add($"Dungeon '{dungeon.Id}': room {i + 1} has no enemies");
                    }

                    foreach (var spawn in room.Spawns)
                    {
                        if (spawn.Pack < 0)
                        {
                            errors.Add($"Dungeon '{dungeon.Id}': room {i + 1} has a negative pack number");
                        }

                        if (!classes.TryGet(spawn.Class, out var def))
                        {
                            errors.Add($"Dungeon '{dungeon.Id}': room {i + 1} spawns unknown class '{spawn.Class}'");
                        }
                        else if (def.Playable)
                        {
                            errors.Add($"Dungeon '{dungeon.Id}': room {i + 1} spawns player class '{spawn.Class}'");
                        }
                    }
                }
            }

            return errors;
        }

        static void ValidateMap(DungeonDef dungeon, List<string> errors)
        {
            if (dungeon.Zones.Length == 0)
            {
                if (dungeon.Gates.Length > 0)
                {
                    errors.Add($"Dungeon '{dungeon.Id}': gates need zones");
                }

                return;
            }

            const float MinSize = 4f;
            foreach (var zone in dungeon.Zones)
            {
                if (zone.MaxX - zone.MinX < MinSize || zone.MaxY - zone.MinY < MinSize)
                {
                    errors.Add($"Dungeon '{dungeon.Id}': zone '{zone.Name}' must be at least {MinSize} units wide and tall");
                }
            }

            foreach (var gate in dungeon.Gates)
            {
                if (gate.MaxX <= gate.MinX || gate.MaxY <= gate.MinY)
                {
                    errors.Add($"Dungeon '{dungeon.Id}': gate '{gate.Name}' has no size");
                    continue;
                }

                var touching = 0;
                foreach (var zone in dungeon.Zones)
                {
                    touching += gate.MinX < zone.MaxX && gate.MaxX > zone.MinX && gate.MinY < zone.MaxY && gate.MaxY > zone.MinY ? 1 : 0;
                }

                if (touching < 2)
                {
                    errors.Add($"Dungeon '{dungeon.Id}': gate '{gate.Name}' must overlap two zones to connect them");
                }

                if (gate.OpenAfter < 0 || gate.OpenAfter >= dungeon.Rooms.Length - 1)
                {
                    errors.Add($"Dungeon '{dungeon.Id}': gate '{gate.Name}' opens after a section that does not exist or is the last one");
                }
            }

            var zonesOnly = new WorldMap(dungeon.Zones, null);
            for (var i = 0; i < 4; i++)
            {
                if (!zonesOnly.Contains(World.SpawnPoint(i)))
                {
                    errors.Add($"Dungeon '{dungeon.Id}': the players' start positions are outside the zones");
                    break;
                }
            }

            for (var r = 0; r < dungeon.Rooms.Length; r++)
            {
                var room = dungeon.Rooms[r];
                foreach (var spawn in room.Spawns)
                {
                    if (!zonesOnly.Contains(new Vec2(spawn.X + room.OffsetX, spawn.Y + room.OffsetY)))
                    {
                        errors.Add($"Dungeon '{dungeon.Id}': room {r + 1} spawns a '{spawn.Class}' outside the zones");
                    }
                }
            }
        }

        static void ValidatePhases(ClassDef def, Registry<ClassDef> classes, Registry<AbilityDef> abilities, List<string> errors)
        {
            var previous = float.PositiveInfinity;
            for (var i = 0; i < def.Phases.Length; i++)
            {
                var phase = def.Phases[i];
                if (phase.HealthBelow <= 0f || phase.HealthBelow > 1f || phase.HealthBelow >= previous)
                {
                    errors.Add($"Class '{def.Id}': phase {i} health threshold must be between 0 and 1 and lower than the phase before it");
                }

                previous = phase.HealthBelow;

                if (phase.Attacks.Length == 0 || phase.SpeedScale <= 0f || phase.SpawnCount < 0)
                {
                    errors.Add($"Class '{def.Id}': phase {i} needs attacks, a positive speed scale and a non-negative spawn count");
                }

                foreach (var attack in phase.Attacks)
                {
                    var owned = attack == def.AutoAttack || System.Array.IndexOf(def.Skills, attack) >= 0;
                    if (!owned || !abilities.TryGet(attack, out _))
                    {
                        errors.Add($"Class '{def.Id}': phase {i} uses '{attack}', which is not one of its attacks");
                    }
                }

                if (phase.Mechanics.Length > Entity.MaxMechanics)
                {
                    errors.Add($"Class '{def.Id}': phase {i} has {phase.Mechanics.Length} mechanics, the limit is {Entity.MaxMechanics}");
                }

                foreach (var mechanic in phase.Mechanics)
                {
                    var owned = mechanic.Ability == def.AutoAttack || System.Array.IndexOf(def.Skills, mechanic.Ability) >= 0;
                    if (!owned || !abilities.TryGet(mechanic.Ability, out _))
                    {
                        errors.Add($"Class '{def.Id}': phase {i} mechanic uses '{mechanic.Ability}', which is not one of its attacks");
                    }

                    if (mechanic.IntervalSeconds <= 0f || mechanic.FirstDelaySeconds < 0f || mechanic.Count < 1 || mechanic.Count > 4)
                    {
                        errors.Add($"Class '{def.Id}': phase {i} mechanic '{mechanic.Ability}' needs a positive interval, a non-negative first delay and a count of 1 to 4");
                    }
                }

                if (phase.SpawnCount > 0 && !classes.TryGet(phase.SpawnClass, out _))
                {
                    errors.Add($"Class '{def.Id}': phase {i} spawns unknown class '{phase.SpawnClass}'");
                }
            }
        }

        static void ValidateKind(AbilityDef ability, List<string> errors)
        {
            switch (ability.Kind)
            {
                case AbilityKind.Dodge:
                    if (ability.Duration <= 0f)
                    {
                        errors.Add($"Ability '{ability.Id}': a dodge needs a duration");
                    }

                    if (ability.DashDistance < 0f || ability.PerfectWindow < 0f)
                    {
                        errors.Add($"Ability '{ability.Id}': dash distance and perfect window must not be negative");
                    }

                    break;
                case AbilityKind.Block:
                    if (ability.Duration <= 0f || ability.Reduction <= 0f || ability.Reduction > 1f)
                    {
                        errors.Add($"Ability '{ability.Id}': a block needs a duration and a reduction between 0 and 1");
                    }

                    break;
                case AbilityKind.Shield:
                    if (ability.Duration <= 0f || ability.Absorb <= 0f)
                    {
                        errors.Add($"Ability '{ability.Id}': a shield needs a duration and an absorb amount");
                    }

                    break;
            }
        }

        static void ValidateResource(ClassDef def, Registry<AbilityDef> abilities, List<string> errors)
        {
            var resource = def.Resource;
            if (resource != null)
            {
                if (string.IsNullOrEmpty(resource.Id))
                {
                    errors.Add($"Class '{def.Id}': resource needs an id");
                }

                if (resource.Max <= 0f || resource.Start < 0f || resource.Start > resource.Max)
                {
                    errors.Add($"Class '{def.Id}': resource max must be above zero and start must fit inside it");
                }

                if (resource.GainDealt < 0f || resource.GainTaken < 0f || resource.GainBlocked < 0f)
                {
                    errors.Add($"Class '{def.Id}': resource gains must not be negative");
                }
            }

            if (resource != null)
            {
                return;
            }

            var ids = new List<string> { def.AutoAttack, def.Defense };
            ids.AddRange(def.Skills);
            foreach (var id in ids)
            {
                if (abilities.TryGet(id, out var ability) && ability.ResourceCost > 0f)
                {
                    errors.Add($"Class '{def.Id}': ability '{id}' costs a resource but the class has none");
                }
            }
        }
    }
}
