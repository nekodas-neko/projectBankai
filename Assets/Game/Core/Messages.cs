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
        Defend = 6,
        Restart = 7,

        // The attack button held: the same slot-0 attack as a heavy swing.
        HeavyAttack = 8,

        // Drink a health potion (the count lives in the player's own profile; the server limits how often).
        UsePotion = 9,
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

        public int AbilitySlot => Type == CommandType.Move || Type == CommandType.Restart || Type == CommandType.UsePotion ? -1 : Type == CommandType.HeavyAttack ? 0 : (int)Type - (int)CommandType.Attack;
    }

    public struct EntityState
    {
        public EntityId Id;
        public PlayerId Owner;
        public Team Team;
        public int Appearance;
        public Vec2 Position;
        public Vec2 Facing;
        public float Health;
        public float MaxHealth;
        public float Cooldown0;
        public float Cooldown1;
        public float Cooldown2;
        public float Cooldown3;
        public float Cooldown4;
        public float Resource;
        public float MaxResource;
        public int ResourceId;
        public float Charge;
        public float ChargeMax;
        public EntityId Target;
        public byte Effects;

        // The distance (in quarter units, so up to 63.75) at which this enemy notices a player; 0 for anything that never aggro.
        public byte AggroQuarters;

        // StableId of the class definition (the weapon for a player, the mob or boss for an enemy), so a client can look up what it is worth.
        public int ClassHash;

        public float AggroRange => AggroQuarters * 0.25f;

        public float GetCooldown(int slot)
        {
            switch (slot)
            {
                case 0: return Cooldown0;
                case 1: return Cooldown1;
                case 2: return Cooldown2;
                case 3: return Cooldown3;
                case 4: return Cooldown4;
                default: return 0f;
            }
        }
    }

    public enum HitOutcome : byte
    {
        Hit = 0,
        Dodged = 1,
        Blocked = 2,
        Absorbed = 3,
        Healed = 4,
        Interrupted = 5,
        Stunned = 6,
    }

    public struct DamageEvent
    {
        public EntityId Source;
        public EntityId Target;
        public float Amount;
        public byte Slot;
        public int Ability;
        public int Variant;
        public HitOutcome Outcome;
    }

    public struct UseEvent
    {
        public EntityId User;
        public byte Slot;
        public int Ability;
        public int Variant;
        public Vec2 Direction;
    }

    // An attack that has started but not landed yet. Sent every snapshot until it resolves, so a late or joining client
    // still shows the warning with the time that is actually left.
    public struct WindupState
    {
        public EntityId User;
        public int Ability;
        public int Variant;
        public Vec2 Origin;
        public Vec2 Direction;
        public float Remaining;
    }

    public static class StableId
    {
        public static int From(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return 0;
            }

            unchecked
            {
                var hash = (uint)2166136261;
                for (var i = 0; i < id.Length; i++)
                {
                    hash = (hash ^ id[i]) * 16777619;
                }

                return (int)hash;
            }
        }
    }

    public sealed class Snapshot
    {
        public int Tick;
        public int AckedSequence;
        public byte Room;
        public byte RoomCount;
        public DungeonPhase Dungeon;
        public float DungeonTimer;
        public readonly List<EntityState> Entities = new List<EntityState>();
        public readonly List<DamageEvent> Events = new List<DamageEvent>();
        public readonly List<UseEvent> Uses = new List<UseEvent>();
        public readonly List<WindupState> Windups = new List<WindupState>();

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
