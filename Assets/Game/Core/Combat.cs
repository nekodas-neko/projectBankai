using System;
using System.Collections.Generic;

namespace Game.Core
{
    // Using abilities and resolving them: windups, hit areas, damage, healing and the defensive skills.
    internal sealed class Combat
    {
        readonly ServerConfig config;
        readonly World world;
        readonly AbilityBook book;
        readonly List<PendingAttack> pendingAttacks = new List<PendingAttack>();

        public Combat(ServerConfig config, World world, AbilityBook book)
        {
            this.config = config;
            this.world = world;
            this.book = book;
        }

        public static float Reach(AbilityDef ability)
        {
            return ability.Shape == AbilityShape.Circle ? ability.Radius : ability.Range;
        }

        public void Use(Entity user, int slot, Vec2 aim)
        {
            if (slot < 0 || slot >= Entity.AbilitySlots || user.Cooldowns[slot] > 0f)
            {
                return;
            }

            if (!book.TryGet(user.AbilityIds[slot], out var baseAbility))
            {
                return;
            }

            var variantId = user.Variants[slot];
            var ability = book.Resolve(baseAbility, variantId);
            if (!user.CanAfford(ability.ResourceCost))
            {
                return;
            }

            var direction = ChooseDirection(user, ability, aim);
            user.Cooldowns[slot] = ability.Cooldown;
            user.AddResource(-ability.ResourceCost);
            world.Uses.Add(new UseEvent { User = user.Id, Slot = (byte)slot, Ability = StableId.From(ability.Id), Variant = variantId, Direction = direction });

            switch (ability.Kind)
            {
                case AbilityKind.Dodge:
                    StartDodge(user, ability, direction);
                    ForgiveRecentHits(user, ability);
                    break;
                case AbilityKind.Block:
                    user.AddStatus(StatusType.Blocking, ability.Duration, ability.Reduction);
                    ForgiveRecentHits(user, ability);
                    break;
                case AbilityKind.Shield:
                    user.AddStatus(StatusType.Shield, ability.Duration, ability.Absorb * user.Stats.Power);
                    ForgiveRecentHits(user, ability);
                    break;
                default:
                    user.Facing = direction;
                    Strike(user, ability, slot, direction, variantId);
                    break;
            }
        }

        public void StepWindups(float dt)
        {
            for (var i = pendingAttacks.Count - 1; i >= 0; i--)
            {
                var attack = pendingAttacks[i];
                attack.Remaining -= dt;
                if (attack.Remaining > 1e-4f)
                {
                    pendingAttacks[i] = attack;
                    continue;
                }

                pendingAttacks.RemoveAt(i);
                var user = world.Find(attack.User);
                if (user != null && user.IsAlive)
                {
                    ResolveAttack(user, attack.Origin, attack.Direction, attack.Ability, attack.Slot, attack.Variant);
                }
            }
        }

        public void StepDash(Entity entity, float dt)
        {
            if (!entity.IsAlive || !entity.HasStatus(StatusType.Dashing))
            {
                return;
            }

            entity.Position = world.InsideArena(entity.Position + entity.DashDirection * (entity.DashSpeed * dt));
        }

        public void CollectWindups(List<WindupState> into)
        {
            for (var i = 0; i < pendingAttacks.Count; i++)
            {
                var attack = pendingAttacks[i];
                var user = world.Find(attack.User);
                if (user == null || !user.IsAlive)
                {
                    continue;
                }

                into.Add(new WindupState
                {
                    User = attack.User,
                    Ability = StableId.From(attack.Ability.Id),
                    Variant = attack.Variant,
                    Origin = attack.Origin,
                    Direction = attack.Direction,
                    Remaining = attack.Remaining > 0f ? attack.Remaining : 0f,
                });
            }
        }

        public void CancelAttacksOf(EntityId user)
        {
            pendingAttacks.RemoveAll(a => a.User == user);
        }

        public void CancelAllAttacks()
        {
            pendingAttacks.Clear();
        }

        Vec2 ChooseDirection(Entity user, AbilityDef ability, Vec2 aim)
        {
            if (aim.SqrLength > 1e-6f)
            {
                return aim.Normalized;
            }

            var facing = user.Facing.SqrLength > 1e-6f ? user.Facing : new Vec2(0f, 1f);
            if (ability.Kind == AbilityKind.Dodge)
            {
                return facing * -1f;
            }

            if (ability.Kind != AbilityKind.Attack)
            {
                return facing;
            }

            var target = world.NearestHostile(user, ability.Range);
            return target != null ? (target.Position - user.Position).Normalized : facing;
        }

        void Strike(Entity user, AbilityDef ability, int slot, Vec2 direction, int variantId)
        {
            if (ability.Windup <= 0f)
            {
                ResolveAttack(user, user.Position, direction, ability, slot, variantId);
                return;
            }

            user.Winding = ability.Windup;
            pendingAttacks.Add(new PendingAttack
            {
                User = user.Id,
                Origin = user.Position,
                Direction = direction,
                Ability = ability,
                Slot = slot,
                Variant = variantId,
                Remaining = ability.Windup,
            });
        }

        void ResolveAttack(Entity user, Vec2 origin, Vec2 direction, AbilityDef ability, int slot, int variantId)
        {
            var damage = ability.Damage * user.Stats.Power;
            var heal = ability.Heal * user.Stats.Power;
            var abilityId = StableId.From(ability.Id);
            var hitsEnemies = ability.Damage > 0f || ability.Heal <= 0f;
            var entities = world.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                var other = entities[i];
                var hostile = Entity.AreHostile(user, other);
                var friendly = !hostile && other.Team != Team.Neutral && other.Team == user.Team;
                if (!other.IsAlive || !(hostile && hitsEnemies || friendly && heal > 0f))
                {
                    continue;
                }

                if (!InArea(ability, origin, direction, other.Position, other.Radius))
                {
                    continue;
                }

                if (friendly)
                {
                    ApplyHeal(user, other, heal, slot, abilityId, variantId);
                    continue;
                }

                ApplyDamage(user, other, damage, slot, abilityId, variantId);
                if (ability.Taunt > 0f)
                {
                    other.ForcedTarget = user.Id;
                    other.ForcedSeconds = ability.Taunt;
                    other.RaiseThreatTo(user.Id, other.TopThreat() + 1f);
                }
            }
        }

        static bool InArea(AbilityDef ability, Vec2 origin, Vec2 direction, Vec2 point, float bodyRadius)
        {
            var offset = point - origin;
            switch (ability.Shape)
            {
                case AbilityShape.Circle:
                    return offset.Length <= ability.Radius + bodyRadius;
                case AbilityShape.Cone:
                    var distance = offset.Length;
                    if (distance - bodyRadius > ability.Range)
                    {
                        return false;
                    }

                    if (distance < 1e-4f)
                    {
                        return true;
                    }

                    return Vec2.Dot(offset * (1f / distance), direction) >= MathF.Cos(ability.Angle * 0.5f * (MathF.PI / 180f));
                default:
                    var along = Vec2.Dot(offset, direction);
                    if (along + bodyRadius < 0f || along - bodyRadius > ability.Range)
                    {
                        return false;
                    }

                    return (offset - direction * along).Length <= ability.Radius + bodyRadius;
            }
        }

        void ApplyHeal(Entity source, Entity target, float amount, int slot, int abilityId, int variantId)
        {
            var healed = MathF.Min(amount, target.Stats.MaxHealth - target.Health);
            if (healed <= 0f)
            {
                return;
            }

            target.Health += healed;
            world.Events.Add(new DamageEvent
            {
                Source = source.Id,
                Target = target.Id,
                Amount = healed,
                Slot = (byte)slot,
                Ability = abilityId,
                Variant = variantId,
                Outcome = HitOutcome.Healed,
            });
        }

        void ApplyDamage(Entity source, Entity target, float amount, int slot, int abilityId, int variantId)
        {
            var outcome = HitOutcome.Hit;
            var health = amount;
            var spec = target.ResourceSpec;

            if (target.HasStatus(StatusType.Invulnerable))
            {
                health = 0f;
                outcome = HitOutcome.Dodged;
                TriggerRider(target, source);
            }
            else
            {
                if (target.HasStatus(StatusType.Blocking))
                {
                    var blocked = health * Clamp01(target.StatusMagnitude(StatusType.Blocking));
                    health -= blocked;
                    outcome = HitOutcome.Blocked;
                    if (spec != null)
                    {
                        target.AddResource(blocked * spec.GainBlocked);
                    }
                }

                var absorbed = target.AbsorbWithShield(health);
                if (absorbed > 0f)
                {
                    health -= absorbed;
                    outcome = health > 0f ? outcome : HitOutcome.Absorbed;
                    if (spec != null)
                    {
                        target.AddResource(absorbed * spec.GainBlocked);
                    }
                }
            }

            if (health > 0f)
            {
                target.Health = MathF.Max(0f, target.Health - health);
                target.RecentHits.Add(new RecentHit { Amount = health, Source = source.Id });
                target.AddThreat(source.Id, health * source.ThreatMultiplier);
                if (spec != null)
                {
                    target.AddResource(health * spec.GainTaken);
                }

                var dealt = source.ResourceSpec;
                if (dealt != null)
                {
                    source.AddResource(health * dealt.GainDealt);
                }
            }

            world.Events.Add(new DamageEvent
            {
                Source = source.Id,
                Target = target.Id,
                Amount = health,
                Slot = (byte)slot,
                Ability = abilityId,
                Variant = variantId,
                Outcome = outcome,
            });
        }

        void StartDodge(Entity user, AbilityDef ability, Vec2 direction)
        {
            var duration = MathF.Max(ability.Duration, config.Simulation.TickSeconds);
            user.Facing = direction;
            user.DashDirection = direction;
            user.DashSpeed = ability.DashDistance / duration;
            user.AddStatus(StatusType.Dashing, duration, 0f);
            user.AddStatus(StatusType.Invulnerable, duration, 0f);
            user.PerfectRemaining = ability.PerfectWindow;
            user.PerfectRider = ability.Rider;
        }

        void ForgiveRecentHits(Entity user, AbilityDef ability)
        {
            if (!user.IsAlive)
            {
                return;
            }

            var total = user.TakeRecentHits(config.DefenseLeniencySeconds, out var attackerId);
            if (total <= 0f)
            {
                return;
            }

            var refund = 0f;
            var outcome = HitOutcome.Hit;
            switch (ability.Kind)
            {
                case AbilityKind.Dodge:
                    refund = total;
                    outcome = HitOutcome.Dodged;
                    break;
                case AbilityKind.Block:
                    refund = total * Clamp01(ability.Reduction);
                    outcome = HitOutcome.Blocked;
                    break;
                case AbilityKind.Shield:
                    refund = user.AbsorbWithShield(total);
                    outcome = HitOutcome.Absorbed;
                    break;
            }

            if (refund <= 0f)
            {
                return;
            }

            user.Health = MathF.Min(user.Stats.MaxHealth, user.Health + refund);
            if (user.ResourceSpec != null && ability.Kind != AbilityKind.Dodge)
            {
                user.AddResource(refund * user.ResourceSpec.GainBlocked);
            }

            var attacker = world.Find(attackerId);
            if (ability.Kind == AbilityKind.Dodge && attacker != null)
            {
                TriggerRider(user, attacker);
            }

            world.Events.Add(new DamageEvent
            {
                Source = attackerId,
                Target = user.Id,
                Amount = 0f,
                Slot = (byte)Entity.DefenseSlot,
                Ability = StableId.From(ability.Id),
                Variant = user.Variants[Entity.DefenseSlot],
                Outcome = outcome,
            });
        }

        void TriggerRider(Entity dodger, Entity attacker)
        {
            if (dodger.PerfectRemaining <= 0f || string.IsNullOrEmpty(dodger.PerfectRider) || !book.TryGet(dodger.PerfectRider, out var rider))
            {
                return;
            }

            dodger.PerfectRemaining = 0f;
            var offset = attacker.Position - dodger.Position;
            var direction = offset.SqrLength > 1e-6f ? offset.Normalized : dodger.Facing;
            world.Uses.Add(new UseEvent { User = dodger.Id, Slot = (byte)Entity.DefenseSlot, Ability = StableId.From(rider.Id), Direction = direction });
            Strike(dodger, rider, Entity.DefenseSlot, direction, 0);
        }

        static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }

        struct PendingAttack
        {
            public EntityId User;
            public Vec2 Origin;
            public Vec2 Direction;
            public AbilityDef Ability;
            public int Slot;
            public int Variant;
            public float Remaining;
        }
    }
}
