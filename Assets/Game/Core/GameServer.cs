using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class ServerConfig
    {
        public SimulationSettings Simulation { get; set; } = SimulationSettings.Default;
        public int MaxCommandsPerTick { get; set; } = 2;
        public int MaxQueuedCommands { get; set; } = 16;
        public int SnapshotInterval { get; set; } = 1;
        public int MaxTicksPerUpdate { get; set; } = 8;
        public float RespawnSeconds { get; set; } = 5f;
        public float DefenseLeniencySeconds { get; set; } = 0.2f;
    }

    public sealed class GameServer
    {
        const double TickEpsilon = 1e-6;

        readonly ServerConfig config;
        readonly Registry<ClassDef> classes;
        readonly Registry<AbilityDef> abilities;
        readonly List<Entity> entities = new List<Entity>();
        readonly List<Connection> connections = new List<Connection>();
        readonly List<DamageEvent> pendingEvents = new List<DamageEvent>();
        readonly List<UseEvent> pendingUses = new List<UseEvent>();
        readonly List<PendingAttack> pendingAttacks = new List<PendingAttack>();
        double accumulator;
        int nextEntityId = 1;

        public GameServer(ServerConfig config, Registry<ClassDef> classes, Registry<AbilityDef> abilities, IRandom random)
        {
            this.config = config;
            this.classes = classes;
            this.abilities = abilities;
            Random = random;
        }

        public IRandom Random { get; }

        public int Tick { get; private set; }

        public IReadOnlyList<Entity> Entities => entities;

        public SimulationSettings Simulation => config.Simulation;

        public Entity FindEntity(EntityId id)
        {
            for (var i = 0; i < entities.Count; i++)
            {
                if (entities[i].Id == id)
                {
                    return entities[i];
                }
            }

            return null;
        }

        public Entity FindPlayerEntity(PlayerId player)
        {
            var connection = FindConnection(player);
            return connection == null ? null : FindEntity(connection.Entity);
        }

        public Entity AddPlayer(PlayerId player, string classId, ITransport transport, int appearance = 0)
        {
            if (!player.IsValid)
            {
                throw new ArgumentException("Player id must be non-zero");
            }

            if (FindConnection(player) != null)
            {
                throw new InvalidOperationException($"{player} is already connected");
            }

            var entity = Spawn(player, Team.Players, classes.Get(classId), SpawnPoint(connections.Count), appearance);
            connections.Add(new Connection { Player = player, Transport = transport, Entity = entity.Id });
            return entity;
        }

        public void RemovePlayer(PlayerId player)
        {
            var connection = FindConnection(player);
            if (connection == null)
            {
                return;
            }

            connections.Remove(connection);
            entities.RemoveAll(e => e.Id == connection.Entity);
            pendingAttacks.RemoveAll(a => a.User == connection.Entity);
        }

        public Entity SpawnNpc(string classId, Team team, Vec2 position, int appearance = 0)
        {
            return Spawn(PlayerId.None, team, classes.Get(classId), position, appearance);
        }

        public void Update(float dt)
        {
            accumulator += dt;
            var tickSeconds = (double)config.Simulation.TickSeconds;
            var steps = 0;
            while (accumulator + TickEpsilon >= tickSeconds)
            {
                accumulator -= tickSeconds;
                if (steps++ < config.MaxTicksPerUpdate)
                {
                    StepTick();
                }
            }
        }

        public void StepTick()
        {
            var dt = config.Simulation.TickSeconds;

            for (var i = 0; i < connections.Count; i++)
            {
                Receive(connections[i]);
            }

            for (var i = 0; i < connections.Count; i++)
            {
                ProcessCommands(connections[i]);
            }

            for (var i = 0; i < entities.Count; i++)
            {
                RunBrain(entities[i]);
            }

            for (var i = 0; i < entities.Count; i++)
            {
                StepDash(entities[i], dt);
            }

            ResolvePendingAttacks(dt);

            for (var i = 0; i < entities.Count; i++)
            {
                entities[i].TickTimers(dt);
                TickRespawn(entities[i], dt);
            }

            Tick++;

            if (Tick % config.SnapshotInterval == 0)
            {
                BroadcastSnapshot();
            }
        }

        Connection FindConnection(PlayerId player)
        {
            for (var i = 0; i < connections.Count; i++)
            {
                if (connections[i].Player == player)
                {
                    return connections[i];
                }
            }

            return null;
        }

        Entity Spawn(PlayerId owner, Team team, ClassDef def, Vec2 position, int appearance)
        {
            var entity = Entity.FromClass(new EntityId(nextEntityId++), owner, team, def, position, appearance);
            entities.Add(entity);
            return entity;
        }

        static Vec2 SpawnPoint(int index)
        {
            return new Vec2(((index % 4) - 1.5f) * 2f, 0f);
        }

        void Receive(Connection connection)
        {
            while (connection.Transport.TryReceive(out var data))
            {
                if (!WireCodec.TryDecode(data, out Command command))
                {
                    continue;
                }

                if (connection.Queue.Count >= config.MaxQueuedCommands)
                {
                    continue;
                }

                connection.Queue.Enqueue(command.WithPlayer(connection.Player));
            }
        }

        void ProcessCommands(Connection connection)
        {
            var entity = FindEntity(connection.Entity);
            var processed = 0;
            while (processed < config.MaxCommandsPerTick && connection.Queue.Count > 0)
            {
                var command = connection.Queue.Dequeue();
                if (command.Sequence <= connection.LastSequence)
                {
                    continue;
                }

                processed++;
                connection.LastSequence = command.Sequence;

                if (entity == null || !entity.IsAlive)
                {
                    continue;
                }

                if (command.Type == CommandType.Move)
                {
                    if (entity.HasStatus(StatusType.Dashing))
                    {
                        continue;
                    }

                    entity.Position = Movement.Step(entity.Position, command.Aim, entity.Stats.MoveSpeed, config.Simulation);
                    if (command.Aim.SqrLength > 1e-6f)
                    {
                        entity.Facing = command.Aim.Normalized;
                    }
                }
                else
                {
                    UseAbility(entity, command.AbilitySlot, command.Aim);
                }
            }
        }

        void UseAbility(Entity user, int slot, Vec2 aim)
        {
            if (slot < 0 || slot >= Entity.AbilitySlots || user.Cooldowns[slot] > 0f)
            {
                return;
            }

            if (!abilities.TryGet(user.AbilityIds[slot], out var ability) || !user.CanAfford(ability.ResourceCost))
            {
                return;
            }

            var direction = ChooseDirection(user, ability, aim);
            user.Cooldowns[slot] = ability.Cooldown;
            user.AddResource(-ability.ResourceCost);
            pendingUses.Add(new UseEvent { User = user.Id, Slot = (byte)slot, Ability = StableId.From(ability.Id), Direction = direction });

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
                    Strike(user, ability, slot, direction);
                    break;
            }
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

            var target = NearestHostile(user, ability.Range);
            return target != null ? (target.Position - user.Position).Normalized : facing;
        }

        void Strike(Entity user, AbilityDef ability, int slot, Vec2 direction)
        {
            if (ability.Windup <= 0f)
            {
                ResolveAttack(user, user.Position, direction, ability, slot);
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
                Remaining = ability.Windup,
            });
        }

        void ResolvePendingAttacks(float dt)
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
                var user = FindEntity(attack.User);
                if (user != null && user.IsAlive)
                {
                    ResolveAttack(user, attack.Origin, attack.Direction, attack.Ability, attack.Slot);
                }
            }
        }

        void ResolveAttack(Entity user, Vec2 origin, Vec2 direction, AbilityDef ability, int slot)
        {
            var damage = ability.Damage * user.Stats.Power;
            var heal = ability.Heal * user.Stats.Power;
            var abilityId = StableId.From(ability.Id);
            var hitsEnemies = ability.Damage > 0f || ability.Heal <= 0f;
            for (var i = 0; i < entities.Count; i++)
            {
                var other = entities[i];
                var hostile = Entity.AreHostile(user, other);
                var friendly = !hostile && other.Team != Team.Neutral && other.Team == user.Team;
                if (!other.IsAlive || !(hostile && hitsEnemies || friendly && heal > 0f))
                {
                    continue;
                }

                var offset = other.Position - origin;
                var along = Vec2.Dot(offset, direction);
                if (along < 0f || along > ability.Range)
                {
                    continue;
                }

                var across = (offset - direction * along).Length;
                if (across > ability.Radius)
                {
                    continue;
                }

                if (friendly)
                {
                    ApplyHeal(user, other, heal, slot, abilityId);
                    continue;
                }

                ApplyDamage(user, other, damage, slot, abilityId);
                if (ability.Taunt > 0f)
                {
                    other.ForcedTarget = user.Id;
                    other.ForcedSeconds = ability.Taunt;
                    other.RaiseThreatTo(user.Id, other.TopThreat() + 1f);
                }
            }
        }

        void ApplyHeal(Entity source, Entity target, float amount, int slot, int abilityId)
        {
            var healed = MathF.Min(amount, target.Stats.MaxHealth - target.Health);
            if (healed <= 0f)
            {
                return;
            }

            target.Health += healed;
            pendingEvents.Add(new DamageEvent
            {
                Source = source.Id,
                Target = target.Id,
                Amount = healed,
                Slot = (byte)slot,
                Ability = abilityId,
                Outcome = HitOutcome.Healed,
            });
        }

        Entity PickTarget(Entity npc, float aggroRange)
        {
            if (npc.ForcedSeconds > 0f)
            {
                var forced = FindEntity(npc.ForcedTarget);
                if (forced != null && forced.IsAlive && Entity.AreHostile(npc, forced))
                {
                    return forced;
                }
            }

            Entity best = null;
            var bestThreat = 0f;
            for (var i = 0; i < npc.Threat.Count; i++)
            {
                var candidate = FindEntity(npc.Threat[i].Source);
                if (candidate != null && candidate.IsAlive && Entity.AreHostile(npc, candidate) && npc.Threat[i].Amount > bestThreat)
                {
                    bestThreat = npc.Threat[i].Amount;
                    best = candidate;
                }
            }

            return best ?? NearestHostile(npc, aggroRange);
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

        void StepDash(Entity entity, float dt)
        {
            if (!entity.IsAlive || !entity.HasStatus(StatusType.Dashing))
            {
                return;
            }

            var moved = entity.Position + entity.DashDirection * (entity.DashSpeed * dt);
            entity.Position = moved.ClampLength(config.Simulation.ArenaRadius);
        }

        void ApplyDamage(Entity source, Entity target, float amount, int slot, int abilityId)
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

            pendingEvents.Add(new DamageEvent
            {
                Source = source.Id,
                Target = target.Id,
                Amount = health,
                Slot = (byte)slot,
                Ability = abilityId,
                Outcome = outcome,
            });
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

            var attacker = FindEntity(attackerId);
            if (ability.Kind == AbilityKind.Dodge && attacker != null)
            {
                TriggerRider(user, attacker);
            }

            pendingEvents.Add(new DamageEvent
            {
                Source = attackerId,
                Target = user.Id,
                Amount = 0f,
                Slot = (byte)Entity.DefenseSlot,
                Ability = StableId.From(ability.Id),
                Outcome = outcome,
            });
        }

        void TriggerRider(Entity dodger, Entity attacker)
        {
            if (dodger.PerfectRemaining <= 0f || string.IsNullOrEmpty(dodger.PerfectRider) || !abilities.TryGet(dodger.PerfectRider, out var rider))
            {
                return;
            }

            dodger.PerfectRemaining = 0f;
            var offset = attacker.Position - dodger.Position;
            var direction = offset.SqrLength > 1e-6f ? offset.Normalized : dodger.Facing;
            pendingUses.Add(new UseEvent { User = dodger.Id, Slot = (byte)Entity.DefenseSlot, Ability = StableId.From(rider.Id), Direction = direction });
            Strike(dodger, rider, Entity.DefenseSlot, direction);
        }

        void TickRespawn(Entity entity, float dt)
        {
            if (entity.IsAlive || !entity.Owner.IsValid)
            {
                return;
            }

            entity.DeadSeconds += dt;
            if (entity.DeadSeconds < config.RespawnSeconds)
            {
                return;
            }

            var index = connections.FindIndex(c => c.Entity == entity.Id);
            entity.Respawn(SpawnPoint(index < 0 ? 0 : index));
        }

        void RunBrain(Entity npc)
        {
            if (npc.Owner.IsValid || !npc.IsAlive || !classes.TryGet(npc.ClassId, out var def) || def.AggroRange <= 0f)
            {
                return;
            }

            if (npc.Winding > 0f || !abilities.TryGet(npc.AbilityIds[0], out var attack))
            {
                return;
            }

            var target = PickTarget(npc, def.AggroRange);
            if (target == null)
            {
                return;
            }

            var offset = target.Position - npc.Position;
            var distance = offset.Length;
            var direction = distance > 1e-4f ? offset * (1f / distance) : npc.Facing;
            npc.Facing = direction;

            if (distance <= attack.Range * 0.85f)
            {
                if (npc.Cooldowns[0] <= 0f)
                {
                    UseAbility(npc, 0, direction);
                }

                return;
            }

            npc.Position = Movement.Step(npc.Position, direction, npc.Stats.MoveSpeed, config.Simulation);
        }

        static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }

        Entity NearestHostile(Entity user, float range)
        {
            Entity best = null;
            var bestSqr = range * range;
            for (var i = 0; i < entities.Count; i++)
            {
                var other = entities[i];
                if (!other.IsAlive || !Entity.AreHostile(user, other))
                {
                    continue;
                }

                var sqr = (other.Position - user.Position).SqrLength;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = other;
                }
            }

            return best;
        }

        void BroadcastSnapshot()
        {
            var snapshot = new Snapshot { Tick = Tick };
            for (var i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                snapshot.Entities.Add(new EntityState
                {
                    Id = e.Id,
                    Owner = e.Owner,
                    Team = e.Team,
                    Role = e.Role,
                    Appearance = e.Appearance,
                    Position = e.Position,
                    Facing = e.Facing,
                    Health = e.Health,
                    MaxHealth = e.Stats.MaxHealth,
                    Cooldown0 = e.Cooldowns[0],
                    Cooldown1 = e.Cooldowns[1],
                    Cooldown2 = e.Cooldowns[2],
                    Cooldown3 = e.Cooldowns[3],
                    Cooldown4 = e.Cooldowns[4],
                    Resource = e.Resource,
                    MaxResource = e.MaxResource,
                    ResourceId = e.ResourceSpec != null ? StableId.From(e.ResourceSpec.Id) : 0,
                    Effects = e.EffectFlagsNow(),
                });
            }

            snapshot.Events.AddRange(pendingEvents);
            pendingEvents.Clear();
            snapshot.Uses.AddRange(pendingUses);
            pendingUses.Clear();

            for (var i = 0; i < connections.Count; i++)
            {
                snapshot.AckedSequence = connections[i].LastSequence;
                connections[i].Transport.Send(WireCodec.Encode(snapshot));
            }
        }

        struct PendingAttack
        {
            public EntityId User;
            public Vec2 Origin;
            public Vec2 Direction;
            public AbilityDef Ability;
            public int Slot;
            public float Remaining;
        }

        sealed class Connection
        {
            public PlayerId Player;
            public ITransport Transport;
            public EntityId Entity;
            public int LastSequence;
            public readonly Queue<Command> Queue = new Queue<Command>();
        }
    }
}
