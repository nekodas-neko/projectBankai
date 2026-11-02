using System.Collections.Generic;

namespace Game.Core
{
    public enum CommandType : byte
    {
        Move = 1,
        Attack = 2,
        Skill1 = 3,
        Skill2 = 4,
        Skill3 = 5,
    }

    public readonly struct Command
    {
        public readonly PlayerId Player;
        public readonly int Sequence;
        public readonly CommandType Type;
        public readonly Vec2 Aim;

        public Command(PlayerId player, int sequence, CommandType type, Vec2 aim)
        {
            Player = player;
            Sequence = sequence;
            Type = type;
            Aim = aim;
        }

        public Command WithPlayer(PlayerId player) => new Command(player, Sequence, Type, Aim);

        public int AbilitySlot => Type == CommandType.Move ? -1 : (int)Type - (int)CommandType.Attack;
    }

    public struct EntityState
    {
        public EntityId Id;
        public PlayerId Owner;
        public Team Team;
        public Role Role;
        public Vec2 Position;
        public Vec2 Facing;
        public float Health;
        public float MaxHealth;
        public float Cooldown0;
        public float Cooldown1;
        public float Cooldown2;
        public float Cooldown3;

        public float GetCooldown(int slot)
        {
            switch (slot)
            {
                case 0: return Cooldown0;
                case 1: return Cooldown1;
                case 2: return Cooldown2;
                case 3: return Cooldown3;
                default: return 0f;
            }
        }
    }

    public struct DamageEvent
    {
        public EntityId Source;
        public EntityId Target;
        public float Amount;
        public byte Slot;
    }

    public sealed class Snapshot
    {
        public int Tick;
        public int AckedSequence;
        public readonly List<EntityState> Entities = new List<EntityState>();
        public readonly List<DamageEvent> Events = new List<DamageEvent>();

        public bool TryFind(EntityId id, out EntityState state)
        {
            for (var i = 0; i < Entities.Count; i++)
            {
                if (Entities[i].Id == id)
                {
                    state = Entities[i];
                    return true;
                }
            }

            state = default;
            return false;
        }
    }
}
