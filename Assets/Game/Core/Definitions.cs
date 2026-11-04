using System;
using System.Collections.Generic;

namespace Game.Core
{
    public enum Team : byte
    {
        Neutral = 0,
        Players = 1,
        Enemies = 2,
    }

    public struct Stats
    {
        public float MaxHealth;
        public float MoveSpeed;
        public float Power;
    }

    public interface IDefinition
    {
        string Id { get; }
    }

    public enum AbilityKind : byte
    {
        Attack = 0,
        Dodge = 1,
        Block = 2,
        Shield = 3,
    }

    public enum AbilityShape : byte
    {
        Line = 0,
        Circle = 1,
        Cone = 2,
    }

    public sealed class VariantDef
    {
        public string Id { get; set; }
        public float DamageScale { get; set; } = 1f;
        public float RangeScale { get; set; } = 1f;
        public float CooldownScale { get; set; } = 1f;
        public float CostScale { get; set; } = 1f;
    }

    public sealed class AbilityDef : IDefinition
    {
        public VariantDef[] Variants { get; set; } = Array.Empty<VariantDef>();

        public AbilityDef Clone()
        {
            return (AbilityDef)MemberwiseClone();
        }

        public string Id { get; set; }
        public AbilityKind Kind { get; set; }
        public AbilityShape Shape { get; set; }
        public float Angle { get; set; }
        public float Cooldown { get; set; }
        public float Range { get; set; }
        public float Radius { get; set; }
        public float Damage { get; set; }
        public float Windup { get; set; }

        // The heavy version of an attack (the attack button held): damage, healing and the resource it gives are scaled by
        // HeavyDamageScale, and the wait before the next attack by HeavyCooldownScale. 0 for the damage scale turns it off.
        public float HeavyDamageScale { get; set; } = 2.4f;
        public float HeavyCooldownScale { get; set; } = 1.8f;
        public float ResourceCost { get; set; }
        public float Duration { get; set; }
        public float DashDistance { get; set; }
        public float Reduction { get; set; }
        public float Absorb { get; set; }
        public float PerfectWindow { get; set; }
        public float Heal { get; set; }
        public float Taunt { get; set; }
        public string Rider { get; set; }
        public bool Avoidable { get; set; } = true;
        public bool AtTarget { get; set; }
        public float ChargeRequired { get; set; }
        public float ChargeFromDealt { get; set; }
        public float ChargeFromTaken { get; set; }
        public float ChargeFromBlocked { get; set; }
        public float ChargeFromHealing { get; set; }

        // A cast: while this ability winds up the caster stands still, and a hit from an ability with Interrupt cancels it.
        // Needs a windup and cannot be a ground circle (At Target), since only a standing caster can be interrupted.
        public bool Interruptible { get; set; }

        // Seconds an enemy is staggered (cannot move, attack or cast) when this ability hits it while it casts something
        // interruptible. The cast is cancelled and does nothing. 0 means this ability cannot interrupt.
        public float Interrupt { get; set; }

        // Seconds a small enemy is stunned (staggered: it cannot move, attack or cast, and its attack in progress is cancelled)
        // when this ability hits it. Bosses are immune. 0 means this ability does not stun.
        public float Stun { get; set; }

        // When the ability resolves it also summons SummonCount of SummonClass around the caster. The adds are fixed on the
        // farthest player for a while; a taunt pulls them off.
        public string SummonClass { get; set; }
        public string SummonSkin { get; set; }
        public int SummonCount { get; set; }
    }

    public sealed class ResourceDef
    {
        public string Id { get; set; }
        public float Max { get; set; } = 100f;
        public float Start { get; set; }
        public float Regen { get; set; }
        public float GainDealt { get; set; }
        public float GainTaken { get; set; }
        public float GainBlocked { get; set; }
    }

    public enum MechanicTarget : byte
    {
        Random = 0,
        Farthest = 1,
        NotTopThreat = 2,
    }

    // Something a boss does on a timer while a phase lasts, on top of its normal attacks: for example drop a
    // ground circle on the player who is farthest from it, so ranged players have to keep moving.
    public sealed class MechanicDef
    {
        public string Ability { get; set; }
        public float IntervalSeconds { get; set; } = 6f;
        public float FirstDelaySeconds { get; set; } = 3f;
        public MechanicTarget Target { get; set; }
        public int Count { get; set; } = 1;
    }

    public sealed class PhaseDef
    {
        public float HealthBelow { get; set; } = 1f;
        public string[] Attacks { get; set; } = Array.Empty<string>();
        public float SpeedScale { get; set; } = 1f;
        public string SpawnClass { get; set; }
        public int SpawnCount { get; set; }
        public string SpawnSkin { get; set; }
        public MechanicDef[] Mechanics { get; set; } = Array.Empty<MechanicDef>();
    }

    public sealed class SpawnDef
    {
        public string Class { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public string Skin { get; set; }
        public bool Boss { get; set; }
        public int Pack { get; set; }
    }

    public sealed class RoomDef
    {
        public string Name { get; set; }
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }
        public SpawnDef[] Spawns { get; set; } = Array.Empty<SpawnDef>();
    }

    public sealed class DungeonDef : IDefinition
    {
        public string Id { get; set; }
        public float ClearDelaySeconds { get; set; } = 3f;

        // Every enemy's health is multiplied by 1 + this x (players - 1), so a party of three meets a tougher boss than a solo player.
        // 0 keeps health the same whatever the party size.
        public float HealthPerExtraPlayer { get; set; }
        public RoomDef[] Rooms { get; set; } = Array.Empty<RoomDef>();
        public ZoneDef[] Zones { get; set; } = Array.Empty<ZoneDef>();
        public GateDef[] Gates { get; set; } = Array.Empty<GateDef>();
    }

    public enum DungeonPhase : byte
    {
        None = 0,
        Fighting = 1,
        Cleared = 2,
        Victory = 3,
        Defeat = 4,
    }

    public sealed class ClassDef : IDefinition
    {
        public const int SkillSlots = 3;

        public string Id { get; set; }
        public bool Playable { get; set; }
        public Stats Stats { get; set; }
        public string AutoAttack { get; set; }
        public string[] Skills { get; set; } = Array.Empty<string>();
        public string Defense { get; set; }
        public ResourceDef Resource { get; set; }
        public float AggroRange { get; set; }
        public float ThreatMultiplier { get; set; } = 1f;
        public float BodyRadius { get; set; }
        public float LeashRange { get; set; }

        // An enemy that has not noticed a player walks slowly about inside this radius of where it stands, pausing between
        // steps, so it can be avoided or sneaked past. 0 keeps it standing still.
        public float WanderRadius { get; set; }

        // What killing this enemy gives. XpReward is mastery XP for the weapon in hand (0 = the default in the mastery rules, which
        // depends on whether it is a boss). LootTable names a loot table; DropChance (0 to 1) is the chance each player gets a drop.
        public int XpReward { get; set; }
        public string LootTable { get; set; } = string.Empty;
        public float DropChance { get; set; }

        // After an attack lands, the enemy stays planted this long before it moves or attacks again, so a boss does not
        // chase and swing without a break. 0 keeps the old behaviour.
        public float RecoverySeconds { get; set; }
        public PhaseDef[] Phases { get; set; } = Array.Empty<PhaseDef>();
    }

    public sealed class Registry<T> where T : class, IDefinition
    {
        readonly Dictionary<string, T> items = new Dictionary<string, T>();

        public int Count => items.Count;

        public IEnumerable<T> All => items.Values;

        public void Add(T item)
        {
            if (item == null || string.IsNullOrEmpty(item.Id))
            {
                throw new ArgumentException("Definition needs an id");
            }

            if (items.ContainsKey(item.Id))
            {
                throw new ArgumentException($"Duplicate {typeof(T).Name} id '{item.Id}'");
            }

            items.Add(item.Id, item);
        }

        public bool TryGet(string id, out T item)
        {
            if (id == null)
            {
                item = null;
                return false;
            }

            return items.TryGetValue(id, out item);
        }

        public T Get(string id)
        {
            if (!TryGet(id, out var item))
            {
                throw new KeyNotFoundException($"No {typeof(T).Name} with id '{id}'");
            }

            return item;
        }
    }
}
