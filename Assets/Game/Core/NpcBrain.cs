using System;

namespace Game.Core
{
    // What enemies decide each tick: who to attack, chasing, leashing, pack calls, boss phases and minions.
    internal sealed class NpcBrain
    {
        const float LeashArrival = 0.5f;
        const float LeashSpeedScale = 2f;
        const float PackCallThreat = 1f;

        readonly ServerConfig config;
        readonly Registry<ClassDef> classes;
        readonly AbilityBook book;
        readonly World world;
        readonly Combat combat;

        public NpcBrain(ServerConfig config, Registry<ClassDef> classes, AbilityBook book, World world, Combat combat)
        {
            this.config = config;
            this.classes = classes;
            this.book = book;
            this.world = world;
            this.combat = combat;
        }

        public void Run(Entity npc)
        {
            if (npc.Owner.IsValid || !npc.IsAlive || !classes.TryGet(npc.ClassId, out var def) || def.AggroRange <= 0f)
            {
                return;
            }

            if (def.Phases.Length > 0)
            {
                EnterPhases(npc, def);
            }

            if (npc.Winding > 0f)
            {
                return;
            }

            if (def.LeashRange > 0f && !npc.Leashing && (npc.Position - npc.Home).Length > def.LeashRange)
            {
                npc.Leashing = true;
                npc.Threat.Clear();
                npc.ForcedSeconds = 0f;
            }

            if (npc.Leashing)
            {
                ReturnHome(npc);
                return;
            }

            var target = PickTarget(npc, def.AggroRange);
            if (target == null)
            {
                return;
            }

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
                SpawnPhaseMinions(npc, def.Phases[npc.Phase]);
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
                world.Spawn(PlayerId.None, boss.Team, phase.SpawnClass, world.InsideArena(spot), appearance);
            }
        }
    }
}
