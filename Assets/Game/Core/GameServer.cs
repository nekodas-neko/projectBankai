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
                entities[i].TickTimers(dt);
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

            if (!abilities.TryGet(user.AbilityIds[slot], out var ability))
            {
                return;
            }

            Vec2 direction;
            if (aim.SqrLength > 1e-6f)
            {
                direction = aim.Normalized;
            }
            else
            {
                var target = NearestHostile(user, ability.Range);
                if (target != null)
                {
                    direction = (target.Position - user.Position).Normalized;
                }
                else
                {
                    direction = user.Facing.SqrLength > 1e-6f ? user.Facing : new Vec2(0f, 1f);
                }
            }

            user.Facing = direction;
            user.Cooldowns[slot] = ability.Cooldown;
            pendingUses.Add(new UseEvent { User = user.Id, Slot = (byte)slot, Direction = direction });

            var damage = ability.Damage * user.Stats.Power;
            for (var i = 0; i < entities.Count; i++)
            {
                var other = entities[i];
                if (!other.IsAlive || !Entity.AreHostile(user, other))
                {
                    continue;
                }

                var offset = other.Position - user.Position;
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

                other.Health = MathF.Max(0f, other.Health - damage);
                pendingEvents.Add(new DamageEvent { Source = user.Id, Target = other.Id, Amount = damage, Slot = (byte)slot });
            }
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
