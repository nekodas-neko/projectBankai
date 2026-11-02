using System;
using System.Collections.Generic;

namespace Game.Core
{
    public enum Role : byte
    {
        None = 0,
        Tank = 1,
        Dps = 2,
        Healer = 3,
    }

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

    public sealed class AbilityDef : IDefinition
    {
        public string Id { get; set; }
        public AbilityKind Kind { get; set; }
        public AbilityShape Shape { get; set; }
        public float Angle { get; set; }
        public float Cooldown { get; set; }
        public float Range { get; set; }
        public float Radius { get; set; }
        public float Damage { get; set; }
        public float Windup { get; set; }
        public float ResourceCost { get; set; }
        public float Duration { get; set; }
        public float DashDistance { get; set; }
        public float Reduction { get; set; }
        public float Absorb { get; set; }
        public float PerfectWindow { get; set; }
        public float Heal { get; set; }
        public float Taunt { get; set; }
        public string Rider { get; set; }
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

    public sealed class PhaseDef
    {
        public float HealthBelow { get; set; } = 1f;
        public string[] Attacks { get; set; } = Array.Empty<string>();
        public float SpeedScale { get; set; } = 1f;
        public string SpawnClass { get; set; }
        public int SpawnCount { get; set; }
        public string SpawnSkin { get; set; }
    }

    public sealed class ClassDef : IDefinition
    {
        public const int SkillSlots = 3;

        public string Id { get; set; }
        public Role Role { get; set; }
        public Stats Stats { get; set; }
        public string AutoAttack { get; set; }
        public string[] Skills { get; set; } = Array.Empty<string>();
        public string Defense { get; set; }
        public ResourceDef Resource { get; set; }
        public float AggroRange { get; set; }
        public float ThreatMultiplier { get; set; } = 1f;
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
