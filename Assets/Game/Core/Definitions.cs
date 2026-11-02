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

    public sealed class AbilityDef : IDefinition
    {
        public string Id { get; set; }
        public float Cooldown { get; set; }
        public float Range { get; set; }
        public float Radius { get; set; }
        public float Damage { get; set; }
    }

    public sealed class ClassDef : IDefinition
    {
        public const int SkillSlots = 3;

        public string Id { get; set; }
        public Role Role { get; set; }
        public Stats Stats { get; set; }
        public string AutoAttack { get; set; }
        public string[] Skills { get; set; } = Array.Empty<string>();
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
