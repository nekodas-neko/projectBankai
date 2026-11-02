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
            }

            return errors;
        }
    }
}
