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

                if (!string.IsNullOrEmpty(ability.Rider) && !abilities.TryGet(ability.Rider, out _))
                {
                    errors.Add($"Ability '{ability.Id}': unknown rider '{ability.Rider}'");
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

                if (def.ThreatMultiplier < 0f)
                {
                    errors.Add($"Class '{def.Id}': threat multiplier must not be negative");
                }

                if (def.AggroRange > 0f && string.IsNullOrEmpty(def.AutoAttack))
                {
                    errors.Add($"Class '{def.Id}': aggro range needs an auto attack");
                }

                ValidateResource(def, abilities, errors);
                ValidatePhases(def, classes, abilities, errors);
            }

            return errors;
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
