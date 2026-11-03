using System.Collections.Generic;

namespace Game.Core
{
    // Looks abilities up and resolves the version a player actually uses (a variant applied).
    internal sealed class AbilityBook
    {
        readonly Registry<AbilityDef> abilities;
        readonly Dictionary<long, AbilityDef> effective = new Dictionary<long, AbilityDef>();

        public AbilityBook(Registry<AbilityDef> abilities)
        {
            this.abilities = abilities;
        }

        public bool TryGet(string id, out AbilityDef ability)
        {
            return abilities.TryGet(id, out ability);
        }

        public void ApplyLoadout(Entity entity, int[] variants)
        {
            if (variants == null)
            {
                return;
            }

            for (var slot = 0; slot < variants.Length && slot < Entity.AbilitySlots; slot++)
            {
                if (variants[slot] != 0 && abilities.TryGet(entity.AbilityIds[slot], out var ability) && FindVariant(ability, variants[slot]) != null)
                {
                    entity.Variants[slot] = variants[slot];
                }
            }
        }

        // The ultimate decides how big the charge meter is and how it fills.
        public AbilityDef UltimateOf(Entity entity)
        {
            return entity.ChargeMax > 0f && abilities.TryGet(entity.AbilityIds[Entity.UltimateSlot], out var ultimate) ? ultimate : null;
        }

        public void SetUpCharge(Entity entity)
        {
            if (abilities.TryGet(entity.AbilityIds[Entity.UltimateSlot], out var ultimate))
            {
                entity.ChargeMax = ultimate.ChargeRequired;
            }
        }

        public AbilityDef Resolve(AbilityDef ability, int variantId)
        {
            if (variantId == 0)
            {
                return ability;
            }

            var key = ((long)StableId.From(ability.Id) << 32) | (uint)variantId;
            if (effective.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var variant = FindVariant(ability, variantId);
            if (variant == null)
            {
                return ability;
            }

            var scaled = ability.Clone();
            scaled.Damage *= variant.DamageScale;
            scaled.Heal *= variant.DamageScale;
            scaled.Range *= variant.RangeScale;
            if (scaled.Shape == AbilityShape.Circle)
            {
                scaled.Radius *= variant.RangeScale;
            }

            scaled.Cooldown *= variant.CooldownScale;
            scaled.ResourceCost *= variant.CostScale;
            effective.Add(key, scaled);
            return scaled;
        }

        static VariantDef FindVariant(AbilityDef ability, int variantId)
        {
            for (var i = 0; i < ability.Variants.Length; i++)
            {
                if (StableId.From(ability.Variants[i].Id) == variantId)
                {
                    return ability.Variants[i];
                }
            }

            return null;
        }
    }
}
