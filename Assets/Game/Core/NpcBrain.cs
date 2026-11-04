using System;
using System.Collections.Generic;

namespace Game.Core
{
    // What enemies decide each tick: who to attack, chasing, leashing, pack calls, boss phases and minions.
    internal sealed class NpcBrain
    {
        const float LeashArrival = 0.5f;
        const float LeashSpeedScale = 2f;
        const float PackCallThreat = 1f;
        const float AddFocusSeconds = 12f;
        const float WanderSpeedScale = 0.45f;
        const float WanderArrival = 0.3f;
        const float WanderPauseMin = 1.5f;
        const float WanderPauseSpan = 3f;

        readonly ServerConfig config;
        readonly Registry<ClassDef> classes;
        readonly AbilityBook book;
        readonly World world;
        readonly Combat combat;
        readonly IRandom random;
        readonly List<Entity> candidates = new List<Entity>();
        readonly List<EntityId> chosen = new List<EntityId>();

        public NpcBrain(ServerConfig config, Registry<ClassDef> classes, AbilityBook book, World world, Combat combat, IRandom random)
        {
            this.config = config;
            this.classes = classes;
            this.book = book;
            this.world = world;
            this.combat = combat;
            this.random = random;
        }

        public void Run(Entity npc)
        {
            if (npc.Owner.IsValid || !npc.IsAlive || !classes.TryGet(npc.ClassId, out var def) || def.AggroRange <= 0f)
            {
                return;
            }

            var staggered = npc.HasStatus(StatusType.Staggered);
            if (def.Phases.Length > 0)
            {
                EnterPhases(npc, def);
                if (!staggered && IsEngaged(npc, def))
                {
                    StepMechanics(npc, def);
                }
            }

            if (npc.Winding > 0f || staggered)
            {
                return;
            }

            if (def.LeashRange > 0f && !npc.Leashing && !npc.Summoned && (npc.Position - npc.Home).Length > def.LeashRange)
            {
                npc.Leashing = true;
                npc.Threat.Clear();
                npc.ForcedSeconds = 0f;
            }

            if (npc.Leashing)
            {
                npc.Target = default;
                ReturnHome(npc);
                return;
            }

            if (npc.Recovery > 0f)
            {
                return;
            }

            var target = PickTarget(npc, def.AggroRange);
            if (target == null)
            {
                npc.Target = default;
                Wander(npc, def);
                return;
            }

            npc.Wandering = false;
            npc.Target = target.Id;

            if (npc.Pack != 0)
            {
                CallPack(npc, target);
            }

            var offset = target.Position - npc.Position;
            var distance = offset.Length;
            var direction = distance > 1e-4f ? offset * (1f / distance) : npc.Facing;
            npc.Facing = direction;

            var phase = npc.Phase >= 0 && npc.Phase < def.Phases.Length ? def.Phases[npc.Phase] : null;
            var stopAt = float.PositiveInfinity;
            var count = phase != null ? phase.Attacks.Length : 1;
            for (var i = 0; i < count; i++)
            {
                var attackId = phase != null ? phase.Attacks[i] : def.AutoAttack;
                var slot = npc.SlotOf(attackId);
                if (slot < 0 || !book.TryGet(attackId, out var attack))
                {
                    continue;
                }

                var reach = (Combat.Reach(attack) + target.Radius) * 0.85f;
                stopAt = MathF.Min(stopAt, reach);
                if (npc.Cooldowns[slot] <= 0f && distance <= reach)
                {
                    combat.Use(npc, slot, direction);
                    npc.Recovery = def.RecoverySeconds > 0f ? attack.Windup + def.RecoverySeconds : 0f;
                    return;
                }
            }

            stopAt = MathF.Max(stopAt, npc.Radius + target.Radius + 0.05f);
            if (distance > stopAt)
            {
                var speed = npc.Stats.MoveSpeed * (phase != null ? phase.SpeedScale : 1f);
                npc.Position = Movement.Step(npc.Position, direction, speed, config.Simulation);
            }
        }

        // An idle enemy with a wander radius strolls to random spots near where it stands, resting between them.
        void Wander(Entity npc, ClassDef def)
        {
            if (def.WanderRadius <= 0f || npc.WanderPause > 0f)
            {
                return;
            }

            var offset = npc.WanderTarget - npc.Position;
            var distance = offset.Length;
            if (!npc.Wandering || distance <= WanderArrival)
            {
                var angle = random.NextFloat() * MathF.PI * 2f;
                var reach = def.WanderRadius * MathF.Sqrt(random.NextFloat());
                npc.WanderTarget = world.InsideArena(npc.Home + new Vec2(MathF.Cos(angle), MathF.Sin(angle)) * reach);
                npc.WanderPause = WanderPauseMin + random.NextFloat() * WanderPauseSpan;
                npc.Wandering = true;
                return;
            }

            var direction = offset * (1f / distance);
            npc.Facing = direction;
            npc.Position = Movement.Step(npc.Position, direction, npc.Stats.MoveSpeed * WanderSpeedScale, config.Simulation);
        }

        Entity PickTarget(Entity npc, float aggroRange)
        {
            if (npc.ForcedSeconds > 0f)
            {
                var forced = world.Find(npc.ForcedTarget);
                if (forced != null && forced.IsAlive && Entity.AreHostile(npc, forced))
                {
                    return forced;
                }
            }

            Entity best = null;
            var bestThreat = 0f;
            for (var i = 0; i < npc.Threat.Count; i++)
            {
                var candidate = world.Find(npc.Threat[i].Source);
                if (candidate != null && candidate.IsAlive && Entity.AreHostile(npc, candidate) && npc.Threat[i].Amount > bestThreat)
                {
                    bestThreat = npc.Threat[i].Amount;
                    best = candidate;
                }
            }

            return best ?? world.NearestHostile(npc, aggroRange);
        }

        void ReturnHome(Entity npc)
        {
            var offset = npc.Home - npc.Position;
            var distance = offset.Length;
            if (distance <= LeashArrival)
            {
                npc.Position = npc.Home;
                npc.Health = npc.Stats.MaxHealth;
                npc.Threat.Clear();
                npc.Leashing = false;
                return;
            }

            var direction = offset * (1f / distance);
            npc.Facing = direction;
            npc.Position = Movement.Step(npc.Position, direction, npc.Stats.MoveSpeed * LeashSpeedScale, config.Simulation);
        }

        void CallPack(Entity caller, Entity target)
        {
            var entities = world.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                var other = entities[i];
                if (other != caller && other.Pack == caller.Pack && !other.Owner.IsValid && other.IsAlive && !other.Leashing && other.Threat.Count == 0)
                {
                    other.AddThreat(target.Id, PackCallThreat);
                }
            }
        }

        void EnterPhases(Entity npc, ClassDef def)
        {
            var fraction = npc.Health / npc.Stats.MaxHealth;
            var reached = npc.Phase;
            for (var i = 0; i < def.Phases.Length; i++)
            {
                if (fraction <= def.Phases[i].HealthBelow)
                {
                    reached = i;
                }
            }

            while (npc.Phase < reached)
            {
                npc.Phase++;
                ResetMechanics(npc, def.Phases[npc.Phase]);
                SpawnPhaseMinions(npc, def.Phases[npc.Phase]);
            }
        }

        // A boss works its mechanics only once it is in a fight: someone is on its threat list or inside its aggro range.
        bool IsEngaged(Entity npc, ClassDef def)
        {
            return npc.Threat.Count > 0 || world.NearestHostile(npc, def.AggroRange) != null;
        }

        static EntityId TopThreatId(Entity npc)
        {
            var best = default(EntityId);
            var amount = 0f;
            for (var i = 0; i < npc.Threat.Count; i++)
            {
                if (npc.Threat[i].Amount > amount)
                {
                    amount = npc.Threat[i].Amount;
                    best = npc.Threat[i].Source;
                }
            }

            return best;
        }

        static void ResetMechanics(Entity npc, PhaseDef phase)
        {
            for (var i = 0; i < Entity.MaxMechanics; i++)
            {
                npc.MechanicTimers[i] = i < phase.Mechanics.Length ? phase.Mechanics[i].FirstDelaySeconds : 0f;
            }
        }

        void StepMechanics(Entity npc, ClassDef def)
        {
            if (npc.Phase < 0 || npc.Phase >= def.Phases.Length)
            {
                return;
            }

            var mechanics = def.Phases[npc.Phase].Mechanics;
            var dt = config.Simulation.TickSeconds;
            for (var i = 0; i < mechanics.Length && i < Entity.MaxMechanics; i++)
            {
                npc.MechanicTimers[i] -= dt;
                if (npc.MechanicTimers[i] > 0f)
                {
                    continue;
                }

                // A cast needs the caster standing free: if it is busy, try again next tick instead of losing the turn.
                if (npc.Winding > 0f && book.TryGet(mechanics[i].Ability, out var wanted) && !wanted.AtTarget)
                {
                    continue;
                }

                npc.MechanicTimers[i] = mechanics[i].IntervalSeconds;
                Fire(npc, mechanics[i]);
            }
        }

        void Fire(Entity npc, MechanicDef mechanic)
        {
            var slot = npc.SlotOf(mechanic.Ability);
            if (slot < 0)
            {
                return;
            }

            chosen.Clear();
            for (var i = 0; i < mechanic.Count; i++)
            {
                var target = PickMechanicTarget(npc, mechanic.Target);
                if (target == null)
                {
                    break;
                }

                chosen.Add(target.Id);
                npc.Cooldowns[slot] = 0f;
                combat.UseAtPoint(npc, slot, target.Position);
            }
        }

        // The players the mechanic can land on, not counting anyone it already picked in this cast (unless there is nobody else).
        Entity PickMechanicTarget(Entity npc, MechanicTarget rule)
        {
            candidates.Clear();
            var entities = world.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                var other = entities[i];
                if (other.Owner.IsValid && other.IsAlive && Entity.AreHostile(npc, other) && !chosen.Contains(other.Id))
                {
                    candidates.Add(other);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            switch (rule)
            {
                case MechanicTarget.Farthest:
                    var farthest = candidates[0];
                    var best = -1f;
                    for (var i = 0; i < candidates.Count; i++)
                    {
                        var distance = (candidates[i].Position - npc.Position).SqrLength;
                        if (distance > best)
                        {
                            best = distance;
                            farthest = candidates[i];
                        }
                    }

                    return farthest;
                case MechanicTarget.NotTopThreat:
                    for (var i = candidates.Count - 1; i >= 0; i--)
                    {
                        if (candidates[i].Id == TopThreatId(npc) && candidates.Count > 1)
                        {
                            candidates.RemoveAt(i);
                        }
                    }

                    return candidates[(int)(random.NextFloat() * candidates.Count) % candidates.Count];
                default:
                    return candidates[(int)(random.NextFloat() * candidates.Count) % candidates.Count];
            }
        }

        void SpawnPhaseMinions(Entity boss, PhaseDef phase)
        {
            if (phase.SpawnCount <= 0 || string.IsNullOrEmpty(phase.SpawnClass))
            {
                return;
            }

            var appearance = StableId.From(phase.SpawnSkin);
            for (var i = 0; i < phase.SpawnCount; i++)
            {
                var angle = (float)i / phase.SpawnCount * MathF.PI * 2f;
                var spot = boss.Position + new Vec2(MathF.Cos(angle), MathF.Sin(angle)) * 3.5f;
                var minion = world.Spawn(PlayerId.None, boss.Team, phase.SpawnClass, world.InsideArena(spot), appearance);
                minion.Summoned = true;
                minion.Section = boss.Section;

                // Adds join the fight at once, on whoever the boss is fighting, instead of waiting to notice someone.
                if (boss.Target.IsValid)
                {
                    minion.ForcedTarget = boss.Target;
                    minion.ForcedSeconds = AddFocusSeconds;
                }
            }
        }
    }
}
