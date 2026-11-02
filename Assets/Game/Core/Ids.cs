using System;

namespace Game.Core
{
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public static readonly EntityId None = new EntityId(0);

        public readonly int Value;

        public EntityId(int value)
        {
            Value = value;
        }

        public bool IsValid => Value != 0;

        public bool Equals(EntityId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is EntityId other && Equals(other);

        public override int GetHashCode() => Value;

        public override string ToString() => $"E{Value}";

        public static bool operator ==(EntityId a, EntityId b) => a.Value == b.Value;

        public static bool operator !=(EntityId a, EntityId b) => a.Value != b.Value;
    }

    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public static readonly PlayerId None = new PlayerId(0);

        public readonly int Value;

        public PlayerId(int value)
        {
            Value = value;
        }

        public bool IsValid => Value != 0;

        public bool Equals(PlayerId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);

        public override int GetHashCode() => Value;

        public override string ToString() => $"P{Value}";

        public static bool operator ==(PlayerId a, PlayerId b) => a.Value == b.Value;

        public static bool operator !=(PlayerId a, PlayerId b) => a.Value != b.Value;
    }
}
