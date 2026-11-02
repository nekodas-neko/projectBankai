using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    [CreateAssetMenu(menuName = "Game/Class", fileName = "new_class")]
    public class ClassAsset : DefinitionAsset
    {
        public Role role = Role.Dps;
        [Min(1f)] public float maxHealth = 100f;
        [Min(0f)] public float moveSpeed = 6f;
        [Min(0f)] public float power = 1f;
        public AbilityAsset autoAttack;
        public AbilityAsset[] skills = new AbilityAsset[0];
        public AbilityAsset defense;
        [Min(0f)] public float aggroRange;
        [Min(0f)] public float threatMultiplier = 1f;
        public bool hasResource;
        public string resourceId = "";
        [Min(1f)] public float resourceMax = 100f;
        [Min(0f)] public float resourceStart;
        public float resourceRegen;
        [Min(0f)] public float resourceGainDealt;
        [Min(0f)] public float resourceGainTaken;
        [Min(0f)] public float resourceGainBlocked;

        public ClassDef ToDefinition()
        {
            return new ClassDef
            {
                Id = Id,
                Role = role,
                Stats = new Stats { MaxHealth = maxHealth, MoveSpeed = moveSpeed, Power = power },
                AutoAttack = autoAttack != null ? autoAttack.Id : null,
                Skills = skills.Where(skill => skill != null).Select(skill => skill.Id).ToArray(),
                Defense = defense != null ? defense.Id : null,
                AggroRange = aggroRange,
                ThreatMultiplier = threatMultiplier,
                Resource = hasResource
                    ? new ResourceDef
                    {
                        Id = resourceId,
                        Max = resourceMax,
                        Start = resourceStart,
                        Regen = resourceRegen,
                        GainDealt = resourceGainDealt,
                        GainTaken = resourceGainTaken,
                        GainBlocked = resourceGainBlocked,
                    }
                    : null,
            };
        }
    }
}
