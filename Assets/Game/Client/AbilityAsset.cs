using Game.Core;
using UnityEngine;

namespace Game.Client
{
    [CreateAssetMenu(menuName = "Game/Ability", fileName = "new_ability")]
    public class AbilityAsset : DefinitionAsset
    {
        public AbilityKind kind = AbilityKind.Attack;
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
        public GameObject castEffect;
        public GameObject hitEffect;

        public AbilityDef ToDefinition()
        {
            return new AbilityDef
            {
                Id = Id,
                Kind = kind,
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
            };
        }
    }
}
