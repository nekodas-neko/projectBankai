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
        // How far from the caster adds appear, and how long they stay on the player they were sent at.
        const float SummonRing = 1.5f;
        const float AddFocusSeconds = 12f;

        readonly List<PendingAttack> pendingAttacks = new List<PendingAttack>();
        readonly List<PendingAttack> due = new List<PendingAttack>();

        public Combat(ServerConfig config, World world, AbilityBook book)
        {
            this.config = config;
            this.world = world;
            this.book = book;
        }

        public static float Reach(AbilityDef ability)
        {
            return HitAreas.Reach(ability);
        }

        public void Use(Entity user, int slot, Vec2 aim, bool heavy = false)
        {
            UseAt(user, slot, aim, false, default, heavy);
        }

        public const string PotionId = "potion";

        // Heals the drinker by a share of its maximum health. Refused while the potion is cooling down or at full health, so a
        // client cannot drink faster than the config allows or waste one.
        public void DrinkPotion(Entity user)
        {
            if (!user.IsAlive || user.PotionCooldown > 0f || user.Health >= user.Stats.MaxHealth)
            {
                return;
            }

            user.PotionCooldown = config.PotionCooldownSeconds;
            ApplyHeal(user, user, user.Stats.MaxHealth * config.PotionHealFraction, 0, StableId.From(PotionId), 0);
        }

        // An enemy mechanic: an ability whose area is centred on a point on the ground (where a player stands), not on the caster.
        public void UseAtPoint(Entity user, int slot, Vec2 point)
        {
            var offset = point - user.Position;
            UseAt(user, slot, offset.SqrLength > 1e-6f ? offset.Normalized : user.Facing, true, point);
        }

        void UseAt(Entity user, int slot, Vec2 aim, bool grounded, Vec2 point, bool heavy = false)
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
            if (heavy)
            {
                ability = book.Heavy(ability);
            }

            if (!user.CanAfford(ability.ResourceCost) || user.Charge < ability.ChargeRequired)
            {
                return;
            }

            var direction = ChooseDirection(user, ability, aim);
            user.Cooldowns[slot] = ability.Cooldown;
            user.AddResource(-ability.ResourceCost);
            user.Charge = System.Math.Max(0f, user.Charge - ability.ChargeRequired);
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
                    Strike(user, ability, slot, direction, variantId, grounded && ability.AtTarget ? point : user.Position);
                    break;
            }
        }

        // Attacks whose windup ran out are taken off the list first and then resolved, since a landing hit can cancel other
        // attacks (a stun or an interrupt). An attacker that was stunned or staggered meanwhile does not finish its swing.
        public void StepWindups(float dt)
        {
            due.Clear();
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
                due.Add(attack);
            }

            for (var i = 0; i < due.Count; i++)
            {
                var attack = due[i];
                var user = world.Find(attack.User);
                if (user != null && user.IsAlive && !user.HasStatus(StatusType.Staggered))
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

            var target = world.PreferredTarget(user, ability.Range);
            return target != null ? (target.Position - user.Position).Normalized : facing;
        }

        void Strike(Entity user, AbilityDef ability, int slot, Vec2 direction, int variantId, Vec2 origin)
        {
            if (ability.Windup <= 0f)
            {
                ResolveAttack(user, origin, direction, ability, slot, variantId);
                return;
            }

            // A ground circle does not hold its caster still: the boss keeps fighting while it winds up.
            if (!ability.AtTarget)
            {
                user.Winding = ability.Windup;
            }

            pendingAttacks.Add(new PendingAttack
            {
                User = user.Id,
                Origin = origin,
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
            if (ability.SummonCount > 0 && !string.IsNullOrEmpty(ability.SummonClass))
            {
                SummonAdds(user, ability);
            }

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

                if (!HitAreas.Contains(ability, origin, direction, other.Position, other.Radius))
                {
                    continue;
                }

                if (friendly)
                {
                    ApplyHeal(user, other, heal, slot, abilityId, variantId);
                    continue;
                }

                ApplyDamage(user, other, damage, slot, abilityId, variantId, ability.Avoidable);
                if (ability.Interrupt > 0f)
                {
                    TryInterrupt(user, other, ability, slot, abilityId, variantId);
                }

                if (ability.Stun > 0f)
                {
                    TryStun(user, other, ability, slot, abilityId, variantId);
                }

                if (ability.Taunt > 0f)
                {
                    other.ForcedTarget = user.Id;
                    other.ForcedSeconds = ability.Taunt;
                    other.RaiseThreatTo(user.Id, other.TopThreat() + 1f);
                }
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
            ChargeFrom(source, slot, healed, ultimate => ultimate.ChargeFromHealing);
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

        // A hit that carries Stun freezes a small enemy for a few seconds and cancels any attack it was winding up. Bosses ignore it.
        void TryStun(Entity user, Entity target, AbilityDef ability, int slot, int abilityId, int variantId)
        {
            if (!target.IsAlive || target.IsBoss || target.Owner.IsValid)
            {
                return;
            }

            CancelAttacksOf(target.Id);
            target.Winding = 0f;
            target.AddStatus(StatusType.Staggered, ability.Stun, 0f);
            world.Events.Add(new DamageEvent
            {
                Source = user.Id,
                Target = target.Id,
                Amount = 0f,
                Slot = (byte)slot,
                Ability = abilityId,
                Variant = variantId,
                Outcome = HitOutcome.Stunned,
            });
        }

        // A hit that carries Interrupt cancels an interruptible cast the target is winding up, and staggers it.
        void TryInterrupt(Entity user, Entity target, AbilityDef ability, int slot, int abilityId, int variantId)
        {
            if (!target.IsAlive || target.Winding <= 0f)
            {
                return;
            }

            var interrupted = false;
            for (var i = pendingAttacks.Count - 1; i >= 0; i--)
            {
                if (pendingAttacks[i].User == target.Id && pendingAttacks[i].Ability.Interruptible)
                {
                    pendingAttacks.RemoveAt(i);
                    interrupted = true;
                }
            }

            if (!interrupted)
            {
                return;
            }

            target.Winding = 0f;
            target.AddStatus(StatusType.Staggered, ability.Interrupt, 0f);
            world.Events.Add(new DamageEvent
            {
                Source = user.Id,
                Target = target.Id,
                Amount = 0f,
                Slot = (byte)slot,
                Ability = abilityId,
                Variant = variantId,
                Outcome = HitOutcome.Interrupted,
            });
        }

        // Adds appear in a ring around the caster and go for the farthest player (a ranged or healing one), the way a classic boss
        // sends adds at the people who cannot take a hit. The tank's taunt, which sets the forced target, pulls them back.
        void SummonAdds(Entity caster, AbilityDef ability)
        {
            Entity target = null;
            var farthest = -1f;
            var entities = world.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                var other = entities[i];
                if (other.Owner.IsValid && other.IsAlive && Entity.AreHostile(caster, other))
                {
                    var distance = (other.Position - caster.Position).SqrLength;
                    if (distance > farthest)
                    {
                        farthest = distance;
                        target = other;
                    }
                }
            }

            var appearance = StableId.From(ability.SummonSkin);
            for (var i = 0; i < ability.SummonCount; i++)
            {
                var angle = (float)i / ability.SummonCount * MathF.PI * 2f;
                var spot = caster.Position + new Vec2(MathF.Cos(angle), MathF.Sin(angle)) * (caster.Radius + SummonRing);
                var add = world.Spawn(PlayerId.None, caster.Team, ability.SummonClass, world.InsideArena(spot), appearance);
                add.Summoned = true;
                add.Section = caster.Section;
                if (target != null)
                {
                    add.ForcedTarget = target.Id;
                    add.ForcedSeconds = AddFocusSeconds;
                    add.AddThreat(target.Id, 1f);
                }
            }
        }

        // An unavoidable (red) hit ignores dodge, block and shield: the only answer is not to be there.
        void ApplyDamage(Entity source, Entity target, float amount, int slot, int abilityId, int variantId, bool avoidable)
        {
            var outcome = HitOutcome.Hit;
            var health = amount;
            var spec = target.ResourceSpec;

            if (avoidable && target.HasStatus(StatusType.Invulnerable))
            {
                health = 0f;
                outcome = HitOutcome.Dodged;
                TriggerRider(target, source);
            }
            else
            {
                if (avoidable && target.HasStatus(StatusType.Blocking))
                {
                    var blocked = health * Clamp01(target.StatusMagnitude(StatusType.Blocking));
                    health -= blocked;
                    outcome = HitOutcome.Blocked;
                    ChargeFrom(target, -1, blocked, ultimate => ultimate.ChargeFromBlocked);
                    if (spec != null)
                    {
                        target.AddResource(blocked * spec.GainBlocked);
                    }
                }

                var absorbed = avoidable ? target.AbsorbWithShield(health) : 0f;
                if (absorbed > 0f)
                {
                    health -= absorbed;
                    outcome = health > 0f ? outcome : HitOutcome.Absorbed;
                    ChargeFrom(target, -1, absorbed, ultimate => ultimate.ChargeFromBlocked);
                    if (spec != null)
                    {
                        target.AddResource(absorbed * spec.GainBlocked);
                    }
                }
            }

            if (health > 0f)
            {
                target.Health = MathF.Max(0f, target.Health - health);
                target.RecentHits.Add(new RecentHit { Amount = health, Source = source.Id, Unavoidable = !avoidable });
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

                ChargeFrom(source, slot, health, ultimate => ultimate.ChargeFromDealt);
                ChargeFrom(target, -1, health, ultimate => ultimate.ChargeFromTaken);
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
            Strike(dodger, rider, Entity.DefenseSlot, direction, 0, dodger.Position);
        }

        // The ultimate never charges itself: hits made by the ultimate slot are ignored (slot -1 means "not a hit of mine").
        void ChargeFrom(Entity entity, int slot, float amount, Func<AbilityDef, float> rate)
        {
            if (slot == Entity.UltimateSlot || amount <= 0f)
            {
                return;
            }

            var ultimate = book.UltimateOf(entity);
            if (ultimate != null)
            {
                entity.AddCharge(amount * rate(ultimate));
            }
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
