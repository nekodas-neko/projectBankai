using System;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    [Serializable]
    public class VariantData
    {
        public string id = "";
        [Min(0f)] public float damageScale = 1f;
        [Min(0.1f)] public float rangeScale = 1f;
        [Min(0.1f)] public float cooldownScale = 1f;
        [Min(0f)] public float costScale = 1f;
        public GameObject castEffect;
        public GameObject hitEffect;
    }

    [CreateAssetMenu(menuName = "Game/Ability", fileName = "new_ability")]
    public class AbilityAsset : DefinitionAsset
    {
        public AbilityKind kind = AbilityKind.Attack;
        public AbilityShape shape = AbilityShape.Line;
        [Range(0f, 360f)] public float angle;
        [Min(0f)] public float cooldown = 1f;
        [Min(0.1f)] public float range = 3f;
        [Min(0f)] public float radius = 0.5f;
        [Min(0f)] public float damage = 10f;
        [Min(0f)] public float windup;
        public float resourceCost;
        [Min(0f)] public float duration;
        [Min(0f)] public float dashDistance;
        [Range(0f, 1f)] public float reduction;
        [Min(0f)] public float absorb;
        [Min(0f)] public float perfectWindow;
        [Min(0f)] public float heal;
        [Min(0f)] public float taunt;
        public AbilityAsset rider;
        public VariantData[] variants = new VariantData[0];
        public GameObject castEffect;
        public GameObject hitEffect;

        public bool TryGetVariant(int variantId, out VariantData variant)
        {
            variant = null;
            if (variantId == 0)
            {
                return false;
            }

            for (var i = 0; i < variants.Length; i++)
            {
                if (StableId.From(variants[i].id) == variantId)
                {
                    variant = variants[i];
                    return true;
                }
            }

            return false;
        }

        public AbilityDef ToDefinition()
        {
            return new AbilityDef
            {
                Id = Id,
                Kind = kind,
                Shape = shape,
                Angle = angle,
                Cooldown = cooldown,
                Range = range,
                Radius = radius,
                Damage = damage,
                Windup = windup,
                ResourceCost = resourceCost,
                Duration = duration,
                DashDistance = dashDistance,
                Reduction = reduction,
                Absorb = absorb,
                PerfectWindow = perfectWindow,
                Heal = heal,
                Taunt = taunt,
                Rider = rider != null ? rider.Id : null,
                Variants = variants.Select(variant => new VariantDef
                {
                    Id = variant.id,
                    DamageScale = variant.damageScale,
                    RangeScale = variant.rangeScale,
                    CooldownScale = variant.cooldownScale,
                    CostScale = variant.costScale,
                }).ToArray(),
            };
        }
    }
}
