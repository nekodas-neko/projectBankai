using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    [System.Serializable]
    public class MechanicData
    {
        public AbilityAsset ability;
        [Min(0.1f)] public float intervalSeconds = 6f;
        [Min(0f)] public float firstDelaySeconds = 3f;
        public MechanicTarget target;
        [Range(1, 4)] public int count = 1;
    }

    [System.Serializable]
    public class PhaseData
    {
        [Range(0.01f, 1f)] public float healthBelow = 1f;
        public AbilityAsset[] attacks = new AbilityAsset[0];
        [Min(0.1f)] public float speedScale = 1f;
        public ClassAsset spawnClass;
        [Min(0)] public int spawnCount;
        public string spawnSkin = "";
        public MechanicData[] mechanics = new MechanicData[0];
    }

    [CreateAssetMenu(menuName = "Game/Class", fileName = "new_class")]
    public class ClassAsset : DefinitionAsset
    {
        public bool playable;
        [Min(1f)] public float maxHealth = 100f;
        [Min(0f)] public float moveSpeed = 6f;
        [Min(0f)] public float power = 1f;
        public AbilityAsset autoAttack;
        public AbilityAsset[] skills = new AbilityAsset[0];
        public AbilityAsset defense;
        [Min(0f)] public float aggroRange;
        [Min(0f)] public float threatMultiplier = 1f;
        [Min(0f)] public float bodyRadius;
        [Min(0f)] public float leashRange;
        public string displayName = "";
        public string skinPrefix = "";
        public PhaseData[] phases = new PhaseData[0];
        public bool hasResource;
        public string resourceId = "";
        [Min(1f)] public float resourceMax = 100f;
        [Min(0f)] public float resourceStart;
        public float resourceRegen;
        [Min(0f)] public float resourceGainDealt;
        [Min(0f)] public float resourceGainTaken;
        [Min(0f)] public float resourceGainBlocked;

        // What the player sees ("Healing mace"), and which skins belong to this weapon (skin assets are named <prefix>_<skin>).
        public string DisplayLabel => string.IsNullOrEmpty(displayName) ? Id : displayName;

        public string SkinPrefix => string.IsNullOrEmpty(skinPrefix) ? Id : skinPrefix;

        public ClassDef ToDefinition()
        {
            return new ClassDef
            {
                Id = Id,
                Playable = playable,
                Stats = new Stats { MaxHealth = maxHealth, MoveSpeed = moveSpeed, Power = power },
                AutoAttack = autoAttack != null ? autoAttack.Id : null,
                Skills = skills.Where(skill => skill != null).Select(skill => skill.Id).ToArray(),
                Defense = defense != null ? defense.Id : null,
                AggroRange = aggroRange,
                ThreatMultiplier = threatMultiplier,
                BodyRadius = bodyRadius,
                LeashRange = leashRange,
                Phases = phases.Select(phase => new PhaseDef
                {
                    HealthBelow = phase.healthBelow,
                    Attacks = phase.attacks.Where(attack => attack != null).Select(attack => attack.Id).ToArray(),
                    SpeedScale = phase.speedScale,
                    SpawnClass = phase.spawnClass != null ? phase.spawnClass.Id : null,
                    SpawnCount = phase.spawnCount,
                    SpawnSkin = phase.spawnSkin,
                    Mechanics = phase.mechanics.Where(mechanic => mechanic.ability != null).Select(mechanic => new MechanicDef
                    {
                        Ability = mechanic.ability.Id,
                        IntervalSeconds = mechanic.intervalSeconds,
                        FirstDelaySeconds = mechanic.firstDelaySeconds,
                        Target = mechanic.target,
                        Count = mechanic.count,
                    }).ToArray(),
                }).ToArray(),
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
