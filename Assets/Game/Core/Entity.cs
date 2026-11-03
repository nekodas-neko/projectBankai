using System.Collections.Generic;

namespace Game.Core
{
    public static class StatusType
    {
        public const int Invulnerable = 1;
        public const int Blocking = 2;
        public const int Shield = 3;
        public const int Dashing = 4;
    }

    public static class EffectFlags
    {
        public const byte Invulnerable = 1;
        public const byte Blocking = 2;
        public const byte Shielded = 4;
        public const byte Dashing = 8;
    }

    public struct StatusEffect
    {
        public int TypeId;
        public float Remaining;
        public float Magnitude;
    }

    public struct RecentHit
    {
        public float Amount;
        public float Age;
        public EntityId Source;
    }

    public struct ThreatEntry
    {
        public EntityId Source;
        public float Amount;
    }

    public sealed class Entity
    {
        const float HitMemorySeconds = 1f;

        public const int UltimateSlot = 2;
        public const int DefenseSlot = 1 + ClassDef.SkillSlots;
        public const int AbilitySlots = DefenseSlot + 1;

        public EntityId Id;
        public PlayerId Owner;
        public Team Team;
        public int Appearance;
        public string ClassId;
        public Vec2 Position;
        public Vec2 Facing;
        public Stats Stats;
        public float Health;
        public ResourceDef ResourceSpec;
        public float Resource;
        public float Charge;
        public float ChargeMax;
        public Vec2 DashDirection;
        public float DashSpeed;
        public float PerfectRemaining;
        public string PerfectRider;
        public float Winding;
        public float DeadSeconds;
        public float ThreatMultiplier = 1f;
        public float Radius;
        public Vec2 Home;
        public int Pack;
        public bool IsBoss;
        public bool Leashing;
        public int Phase = -1;
        public EntityId ForcedTarget;
        public float ForcedSeconds;
        public readonly List<StatusEffect> Statuses = new List<StatusEffect>();
        public readonly List<RecentHit> RecentHits = new List<RecentHit>();
        public readonly List<ThreatEntry> Threat = new List<ThreatEntry>();
        public readonly string[] AbilityIds = new string[AbilitySlots];
        public readonly float[] Cooldowns = new float[AbilitySlots];
        public readonly int[] Variants = new int[AbilitySlots];

        public bool IsAlive => Health > 0f;

        public bool HasResource => ResourceSpec != null;

        public float MaxResource => ResourceSpec != null ? ResourceSpec.Max : 0f;

        public static Entity FromClass(EntityId id, PlayerId owner, Team team, ClassDef def, Vec2 position, int appearance = 0)
        {
            var entity = new Entity
            {
                Id = id,
                Owner = owner,
                Team = team,
                Appearance = appearance,
                ClassId = def.Id,
                Position = position,
                Home = position,
                Facing = new Vec2(0f, 1f),
                Stats = def.Stats,
                Health = def.Stats.MaxHealth,
                ResourceSpec = def.Resource,
                ThreatMultiplier = def.ThreatMultiplier,
                Radius = def.BodyRadius,
                Resource = def.Resource != null ? System.Math.Min(def.Resource.Start, def.Resource.Max) : 0f,
            };

            entity.AbilityIds[0] = def.AutoAttack;
            for (var i = 0; i < ClassDef.SkillSlots && i < def.Skills.Length; i++)
            {
                entity.AbilityIds[i + 1] = def.Skills[i];
            }

            entity.AbilityIds[DefenseSlot] = def.Defense;
            return entity;
        }

        public void Respawn(Vec2 position)
        {
            Health = Stats.MaxHealth;
            Position = position;
            Resource = ResourceSpec != null ? System.Math.Min(ResourceSpec.Start, ResourceSpec.Max) : 0f;
            Statuses.Clear();
            RecentHits.Clear();
            Threat.Clear();
            ForcedSeconds = 0f;
            Phase = -1;
            Leashing = false;
            System.Array.Clear(Cooldowns, 0, Cooldowns.Length);
            DashSpeed = 0f;
            PerfectRemaining = 0f;
            Winding = 0f;
            DeadSeconds = 0f;
        }

        public float TakeRecentHits(float window, out EntityId lastSource)
        {
            var total = 0f;
            lastSource = default;
            for (var i = RecentHits.Count - 1; i >= 0; i--)
            {
                if (RecentHits[i].Age > window)
                {
                    continue;
                }

                total += RecentHits[i].Amount;
                lastSource = RecentHits[i].Source;
                RecentHits.RemoveAt(i);
            }

            return total;
        }

        public void AddThreat(EntityId source, float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            for (var i = 0; i < Threat.Count; i++)
            {
                if (Threat[i].Source == source)
                {
                    var entry = Threat[i];
                    entry.Amount += amount;
                    Threat[i] = entry;
                    return;
                }
            }

            Threat.Add(new ThreatEntry { Source = source, Amount = amount });
        }

        public float ThreatOf(EntityId source)
        {
            for (var i = 0; i < Threat.Count; i++)
            {
                if (Threat[i].Source == source)
                {
                    return Threat[i].Amount;
                }
            }

            return 0f;
        }

        public int SlotOf(string abilityId)
        {
            for (var i = 0; i < AbilityIds.Length; i++)
            {
                if (AbilityIds[i] == abilityId)
                {
                    return i;
                }
            }

            return -1;
        }

        public float TopThreat()
        {
            var top = 0f;
            for (var i = 0; i < Threat.Count; i++)
            {
                top = System.Math.Max(top, Threat[i].Amount);
            }

            return top;
        }

        public void RaiseThreatTo(EntityId source, float value)
        {
            var missing = value - ThreatOf(source);
            if (missing > 0f)
            {
                AddThreat(source, missing);
            }
        }

        public bool HasStatus(int type)
        {
            return IndexOfStatus(type) >= 0;
        }

        public float StatusMagnitude(int type)
        {
            var index = IndexOfStatus(type);
            return index >= 0 ? Statuses[index].Magnitude : 0f;
        }

        public void AddStatus(int type, float duration, float magnitude)
        {
            var index = IndexOfStatus(type);
            var status = new StatusEffect { TypeId = type, Remaining = duration, Magnitude = magnitude };
            if (index >= 0)
            {
                Statuses[index] = status;
            }
            else
            {
                Statuses.Add(status);
            }
        }

        public void RemoveStatus(int type)
        {
            var index = IndexOfStatus(type);
            if (index >= 0)
            {
                Statuses.RemoveAt(index);
            }
        }

        public float AbsorbWithShield(float amount)
        {
            var index = IndexOfStatus(StatusType.Shield);
            if (index < 0 || amount <= 0f)
            {
                return 0f;
            }

            var status = Statuses[index];
            var absorbed = System.Math.Min(status.Magnitude, amount);
            status.Magnitude -= absorbed;
            if (status.Magnitude <= 0f)
            {
                Statuses.RemoveAt(index);
            }
            else
            {
                Statuses[index] = status;
            }

            return absorbed;
        }

        public bool CanAfford(float cost)
        {
            return cost <= 0f || (ResourceSpec != null && Resource >= cost);
        }

        public void AddCharge(float amount)
        {
            if (ChargeMax <= 0f || amount <= 0f)
            {
                return;
            }

            Charge = System.Math.Min(ChargeMax, Charge + amount);
        }

        public void AddResource(float amount)
        {
            if (ResourceSpec == null || amount == 0f)
            {
                return;
            }

            Resource = System.Math.Max(0f, System.Math.Min(ResourceSpec.Max, Resource + amount));
        }

        public byte EffectFlagsNow()
        {
            byte flags = 0;
            if (HasStatus(StatusType.Invulnerable))
            {
                flags |= EffectFlags.Invulnerable;
            }

            if (HasStatus(StatusType.Blocking))
            {
                flags |= EffectFlags.Blocking;
            }

            if (HasStatus(StatusType.Shield))
            {
                flags |= EffectFlags.Shielded;
            }

            if (HasStatus(StatusType.Dashing))
            {
                flags |= EffectFlags.Dashing;
            }

            return flags;
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

            for (var i = RecentHits.Count - 1; i >= 0; i--)
            {
                var hit = RecentHits[i];
                hit.Age += dt;
                if (hit.Age > HitMemorySeconds)
                {
                    RecentHits.RemoveAt(i);
                }
                else
                {
                    RecentHits[i] = hit;
                }
            }

            PerfectRemaining = PerfectRemaining > dt ? PerfectRemaining - dt : 0f;
            Winding = Winding > dt ? Winding - dt : 0f;
            ForcedSeconds = ForcedSeconds > dt ? ForcedSeconds - dt : 0f;

            if (ResourceSpec != null && ResourceSpec.Regen != 0f)
            {
                AddResource(ResourceSpec.Regen * dt);
            }
        }

        public static bool AreHostile(Entity a, Entity b)
        {
            return a.Team != Team.Neutral && b.Team != Team.Neutral && a.Team != b.Team;
        }

        int IndexOfStatus(int type)
        {
            for (var i = 0; i < Statuses.Count; i++)
            {
                if (Statuses[i].TypeId == type)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
