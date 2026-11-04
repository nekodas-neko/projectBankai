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

        // A health potion heals this fraction of the drinker's maximum health, then needs this long before the next one.
        public float PotionHealFraction { get; set; } = 0.4f;
        public float PotionCooldownSeconds { get; set; } = 20f;
    }

    // The authoritative simulation. It owns the tick and the public API and coordinates focused systems:
    // World (entities), Session (connections), Combat (abilities), NpcBrain (enemy decisions) and DungeonDirector (progression).
    public sealed class GameServer
    {
        const double TickEpsilon = 1e-6;

        readonly ServerConfig config;
        readonly World world;
        readonly Session session = new Session();
        readonly AbilityBook book;
        readonly Combat combat;
        readonly NpcBrain brain;
        readonly DungeonDirector dungeon;
        double accumulator;

        public GameServer(ServerConfig config, Registry<ClassDef> classes, Registry<AbilityDef> abilities, IRandom random)
        {
            this.config = config;
            Random = random;
            world = new World(config, classes);
            book = new AbilityBook(abilities);
            combat = new Combat(config, world, book);
            brain = new NpcBrain(config, classes, book, world, combat, random);
            dungeon = new DungeonDirector(config, world, session, combat);
        }

        public IRandom Random { get; }

        public int Tick { get; private set; }

        public IReadOnlyList<Entity> Entities => world.Entities;

        public DungeonPhase DungeonPhase => dungeon.Phase;

        public int Room => dungeon.Room;

        public WorldMap Map => dungeon.Map;

        public SimulationSettings Simulation => config.Simulation;

        public void StartDungeon(DungeonDef definition)
        {
            dungeon.Start(definition);
        }

        public Entity FindEntity(EntityId id)
        {
            return world.Find(id);
        }

        public Entity FindPlayerEntity(PlayerId player)
        {
            var connection = session.Find(player);
            return connection == null ? null : world.Find(connection.Entity);
        }

        // What a weapon's power scale can be, whatever a client sends: a drop rolls inside this range.
        public const float MinWeaponPower = 0.8f;
        public const float MaxWeaponPower = 1.3f;

        public Entity AddPlayer(PlayerId player, string classId, ITransport transport, int appearance = 0, int[] variants = null, float weaponPower = 1f)
        {
            if (!player.IsValid)
            {
                throw new ArgumentException("Player id must be non-zero");
            }

            if (session.Find(player) != null)
            {
                throw new InvalidOperationException($"{player} is already connected");
            }

            var entity = world.Spawn(player, Team.Players, classId, World.SpawnPoint(session.Count), appearance);
            book.ApplyLoadout(entity, variants);
            book.SetUpCharge(entity);
            var scaled = entity.Stats;
            scaled.Power *= float.IsNaN(weaponPower) ? 1f : MathF.Max(MinWeaponPower, MathF.Min(MaxWeaponPower, weaponPower));
            entity.Stats = scaled;
            session.Add(player, transport, entity.Id);
            return entity;
        }

        public void RemovePlayer(PlayerId player)
        {
            var connection = session.Find(player);
            if (connection == null)
            {
                return;
            }

            session.Remove(connection);
            world.Remove(e => e.Id == connection.Entity);
            combat.CancelAttacksOf(connection.Entity);
        }

        public Entity SpawnNpc(string classId, Team team, Vec2 position, int appearance = 0)
        {
            return world.Spawn(PlayerId.None, team, classId, position, appearance);
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
            var entities = world.Entities;

            for (var i = 0; i < session.Count; i++)
            {
                session.Receive(session[i], config.MaxQueuedCommands);
            }

            for (var i = 0; i < session.Count; i++)
            {
                ProcessCommands(session[i]);
            }

            for (var i = 0; i < entities.Count; i++)
            {
                brain.Run(entities[i]);
            }

            for (var i = 0; i < entities.Count; i++)
            {
                combat.StepDash(entities[i], dt);
            }

            world.Separate();
            combat.StepWindups(dt);

            for (var i = 0; i < entities.Count; i++)
            {
                entities[i].TickTimers(dt);
                dungeon.StepRespawn(entities[i], dt);
            }

            dungeon.Step(dt);
            Tick++;

            if (Tick % config.SnapshotInterval == 0)
            {
                session.Broadcast(SnapshotBuilder.Build(Tick, world, dungeon, combat));
            }
        }

        void ProcessCommands(Session.Connection connection)
        {
            var entity = world.Find(connection.Entity);
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

                if (command.Type == CommandType.Restart)
                {
                    dungeon.RestartIfFinished();
                    continue;
                }

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
                else if (command.Type == CommandType.UsePotion)
                {
                    combat.DrinkPotion(entity);
                }
                else
                {
                    combat.Use(entity, command.AbilitySlot, command.Aim, command.Type == CommandType.HeavyAttack, command.Sequence);
                }
            }
        }
    }
}
