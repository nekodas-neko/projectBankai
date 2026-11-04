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

        // The attack button held: damage scale of the heavy swing (0 = no heavy version) and how much longer the wait after it is.
        [Min(0f)] public float heavyDamageScale = 2.4f;
        [Min(0.5f)] public float heavyCooldownScale = 1.8f;
        public float resourceCost;
        [Min(0f)] public float duration;
        [Min(0f)] public float dashDistance;
        [Range(0f, 1f)] public float reduction;
        [Min(0f)] public float absorb;
        [Min(0f)] public float perfectWindow;
        [Min(0f)] public float heal;
        [Min(0f)] public float taunt;
        public AbilityAsset rider;
        public bool avoidable = true;
        public bool atTarget;
        [Min(0f)] public float chargeRequired;
        [Min(0f)] public float chargeFromDealt;
        [Min(0f)] public float chargeFromTaken;
        [Min(0f)] public float chargeFromBlocked;
        [Min(0f)] public float chargeFromHealing;
        [Tooltip("A cast: the caster stands still while it winds up and an Interrupt hit cancels it.")] public bool interruptible;
        [Min(0f)] [Tooltip("Seconds an enemy is staggered when this hits it while it casts something interruptible. 0 = cannot interrupt.")] public float interrupt;
        [Min(0f)] [Tooltip("Seconds a small enemy is stunned when this hits it (bosses are immune). 0 = no stun.")] public float stun;
        [Tooltip("Summons adds around the caster when it resolves; they go for the farthest player.")] public ClassAsset summonClass;
        public string summonSkin = "";
        [Range(0, 8)] public int summonCount;
        [Tooltip("Hits every enemy in the room wherever it stands (needs damage and a windup of at least 0.5 s).")] public bool everyone;
        [Tooltip("Damage is cut for a target standing on a dungeon patch with this tag (for example water).")] public string shelterTag = "";
        [Range(0f, 1f)] [Tooltip("How much of the damage a sheltered target does not take.")] public float shelterReduction;

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
                HeavyDamageScale = heavyDamageScale,
                HeavyCooldownScale = heavyCooldownScale,
                ResourceCost = resourceCost,
                Duration = duration,
                DashDistance = dashDistance,
                Reduction = reduction,
                Absorb = absorb,
                PerfectWindow = perfectWindow,
                Heal = heal,
                Taunt = taunt,
                Rider = rider != null ? rider.Id : null,
                Avoidable = avoidable,
                AtTarget = atTarget,
                ChargeRequired = chargeRequired,
                ChargeFromDealt = chargeFromDealt,
                ChargeFromTaken = chargeFromTaken,
                ChargeFromBlocked = chargeFromBlocked,
                ChargeFromHealing = chargeFromHealing,
                Interruptible = interruptible,
                Interrupt = interrupt,
                Stun = stun,
                SummonClass = summonClass != null ? summonClass.Id : null,
                SummonSkin = summonSkin,
                SummonCount = summonCount,
                Everyone = everyone,
                ShelterTag = shelterTag,
                ShelterReduction = shelterReduction,
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
