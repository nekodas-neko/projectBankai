using System.Collections.Generic;

namespace Game.Core
{
    public struct StatusEffect
    {
        public int TypeId;
        public float Remaining;
        public float Magnitude;
    }

    public sealed class Entity
    {
        public const int AbilitySlots = 1 + ClassDef.SkillSlots;

        public EntityId Id;
        public PlayerId Owner;
        public Team Team;
        public string ClassId;
        public Role Role;
        public Vec2 Position;
        public Vec2 Facing;
        public Stats Stats;
        public float Health;
        public readonly List<StatusEffect> Statuses = new List<StatusEffect>();
        public readonly string[] AbilityIds = new string[AbilitySlots];
        public readonly float[] Cooldowns = new float[AbilitySlots];

        public bool IsAlive => Health > 0f;

        public static Entity FromClass(EntityId id, PlayerId owner, Team team, ClassDef def, Vec2 position)
        {
            var entity = new Entity
            {
                Id = id,
                Owner = owner,
                Team = team,
                ClassId = def.Id,
                Role = def.Role,
                Position = position,
                Facing = new Vec2(0f, 1f),
                Stats = def.Stats,
                Health = def.Stats.MaxHealth,
            };

            entity.AbilityIds[0] = def.AutoAttack;
            for (var i = 0; i < ClassDef.SkillSlots && i < def.Skills.Length; i++)
            {
                entity.AbilityIds[i + 1] = def.Skills[i];
            }

            return entity;
        }

        public void TickTimers(float dt)
        {
            for (var i = 0; i < Cooldowns.Length; i++)
            {
                if (Cooldowns[i] > 0f)
                {
                    Cooldowns[i] = Cooldowns[i] > dt ? Cooldowns[i] - dt : 0f;
                }
            }

            for (var i = Statuses.Count - 1; i >= 0; i--)
            {
                var status = Statuses[i];
                status.Remaining -= dt;
                if (status.Remaining <= 0f)
                {
                    Statuses.RemoveAt(i);
                }
                else
                {
                    Statuses[i] = status;
                }
            }
        }

        public static bool AreHostile(Entity a, Entity b)
        {
            return a.Team != Team.Neutral && b.Team != Team.Neutral && a.Team != b.Team;
        }
    }
}
