namespace Game.Core
{
    // Dungeon progression: rooms, boss-gated clears, victory, defeat, restart, and the respawn rule that depends on it.
    // A dungeon without zones plays room by room: each room's enemies appear when the room starts and everyone is restored between rooms.
    // A dungeon with zones is one continuous map: everything is there from the start, a boss's death opens its gate, and nobody is moved.
    internal sealed class DungeonDirector
    {
        readonly ServerConfig config;
        readonly World world;
        readonly Session session;
        readonly Combat combat;
        DungeonDef dungeon;
        bool roomHasBoss;
        bool[] sectionHasBoss = System.Array.Empty<bool>();
        int cleared;
        float timer;

        public DungeonDirector(ServerConfig config, World world, Session session, Combat combat)
        {
            this.config = config;
            this.world = world;
            this.session = session;
            this.combat = combat;
        }

        public DungeonPhase Phase { get; private set; }

        public int Room { get; private set; }

        public bool Active => dungeon != null;

        public WorldMap Map { get; private set; }

        public int RoomCount => dungeon != null ? dungeon.Rooms.Length : 0;

        public float Timer => timer > 0f ? timer : 0f;

        public void Start(DungeonDef definition)
        {
            dungeon = definition;
            Map = WorldMap.From(definition);
            if (Map != null)
            {
                Map.Authoritative = true;
            }

            config.Simulation = config.Simulation.WithMap(Map);
            Reset();
        }

        public void RestartIfFinished()
        {
            if (dungeon != null && (Phase == DungeonPhase.Victory || Phase == DungeonPhase.Defeat))
            {
                Reset();
            }
        }

        public void Step(float dt)
        {
            if (dungeon == null)
            {
                return;
            }

            ScaleEnemiesToParty();

            if (Map != null)
            {
                StepMap(dt);
                return;
            }

            if (Phase == DungeonPhase.Cleared)
            {
                timer -= dt;
                if (timer <= 0f)
                {
                    Room++;
                    BeginRoom();
                }

                return;
            }

            if (Phase != DungeonPhase.Fighting)
            {
                return;
            }

            var playersAlive = 0;
            var playersTotal = 0;
            var enemiesAlive = 0;
            var bossesAlive = 0;
            var entities = world.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (e.Owner.IsValid)
                {
                    playersTotal++;
                    if (e.IsAlive)
                    {
                        playersAlive++;
                    }
                }
                else if (e.IsAlive && e.Team == Team.Enemies)
                {
                    enemiesAlive++;
                    if (e.IsBoss)
                    {
                        bossesAlive++;
                    }
                }
            }

            if (playersTotal > 0 && playersAlive == 0)
            {
                Phase = DungeonPhase.Defeat;
            }
            else if (roomHasBoss ? bossesAlive == 0 : enemiesAlive == 0)
            {
                if (enemiesAlive > 0)
                {
                    RemoveEnemies();
                }

                if (Room + 1 >= dungeon.Rooms.Length)
                {
                    Phase = DungeonPhase.Victory;
                }
                else
                {
                    Phase = DungeonPhase.Cleared;
                    timer = dungeon.ClearDelaySeconds;
                }
            }
        }

        // Enemies get tougher with each extra player. Anything spawned later (a boss's minions) or a player joining or leaving
        // is caught here too: an enemy keeps the share of its health it has left when its maximum changes.
        void ScaleEnemiesToParty()
        {
            var players = 0;
            var entities = world.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                if (entities[i].Owner.IsValid)
                {
                    players++;
                }
            }

            var factor = 1f + dungeon.HealthPerExtraPlayer * System.Math.Max(0, players - 1);
            for (var i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (e.Owner.IsValid || e.Team != Team.Enemies || e.PartyScale == factor)
                {
                    continue;
                }

                var ratio = factor / e.PartyScale;
                var stats = e.Stats;
                stats.MaxHealth *= ratio;
                e.Stats = stats;
                e.Health *= ratio;
                e.PartyScale = factor;
            }
        }

        // Outside a dungeon the dead come back after a delay; inside one they stay down until the next room.
        public void StepRespawn(Entity entity, float dt)
        {
            if (dungeon != null || entity.IsAlive || !entity.Owner.IsValid)
            {
                return;
            }

            entity.DeadSeconds += dt;
            if (entity.DeadSeconds < config.RespawnSeconds)
            {
                return;
            }

            var index = 0;
            for (var i = 0; i < session.Count; i++)
            {
                if (session[i].Entity == entity.Id)
                {
                    index = i;
                    break;
                }
            }

            entity.Respawn(World.SpawnPoint(index));
        }

        void Reset()
        {
            Room = 0;
            cleared = 0;
            for (var i = 0; i < session.Count; i++)
            {
                var player = world.Find(session[i].Entity);
                if (player != null)
                {
                    player.Charge = 0f;
                }
            }

            if (Map != null)
            {
                BeginMap();
                return;
            }

            BeginRoom();
        }

        void BeginRoom()
        {
            RemoveEnemies();
            for (var i = 0; i < session.Count; i++)
            {
                var player = world.Find(session[i].Entity);
                player?.Respawn(World.SpawnPoint(i));
            }

            var spawns = dungeon.Rooms[Room].Spawns;
            roomHasBoss = false;
            for (var i = 0; i < spawns.Length; i++)
            {
                var spawn = spawns[i];
                var npc = world.Spawn(PlayerId.None, Team.Enemies, spawn.Class, world.InsideArena(new Vec2(spawn.X, spawn.Y)), StableId.From(spawn.Skin));
                npc.Pack = spawn.Pack;
                npc.IsBoss = spawn.Boss;
                roomHasBoss |= spawn.Boss;
            }

            Phase = DungeonPhase.Fighting;
            timer = 0f;
        }

        // The continuous map: every section's enemies are placed at the start, in the zone they belong to.
        void BeginMap()
        {
            RemoveEnemies();
            Map.Progress = 0;
            for (var i = 0; i < session.Count; i++)
            {
                var player = world.Find(session[i].Entity);
                player?.Respawn(World.SpawnPoint(i));
            }

            sectionHasBoss = new bool[dungeon.Rooms.Length];
            for (var section = 0; section < dungeon.Rooms.Length; section++)
            {
                var room = dungeon.Rooms[section];
                for (var i = 0; i < room.Spawns.Length; i++)
                {
                    var spawn = room.Spawns[i];
                    var position = world.InsideArena(new Vec2(spawn.X + room.OffsetX, spawn.Y + room.OffsetY));
                    var npc = world.Spawn(PlayerId.None, Team.Enemies, spawn.Class, position, StableId.From(spawn.Skin));

                    // A pack number is only meaningful inside its own section.
                    npc.Pack = spawn.Pack == 0 ? 0 : (section + 1) * 1000 + spawn.Pack;
                    npc.IsBoss = spawn.Boss;
                    npc.Section = section;
                    sectionHasBoss[section] |= spawn.Boss;
                }
            }

            Phase = DungeonPhase.Fighting;
            timer = 0f;
        }

        void StepMap(float dt)
        {
            if (Phase != DungeonPhase.Fighting && Phase != DungeonPhase.Cleared)
            {
                return;
            }

            if (Phase == DungeonPhase.Cleared)
            {
                timer -= dt;
                if (timer <= 0f)
                {
                    Phase = DungeonPhase.Fighting;
                }
            }

            var playersAlive = 0;
            var playersTotal = 0;
            var enemiesAlive = 0;
            var bossesAlive = 0;
            var entities = world.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (e.Owner.IsValid)
                {
                    playersTotal++;
                    playersAlive += e.IsAlive ? 1 : 0;
                }
                else if (e.IsAlive && e.Team == Team.Enemies && e.Section == cleared)
                {
                    enemiesAlive++;
                    bossesAlive += e.IsBoss ? 1 : 0;
                }
            }

            if (playersTotal > 0 && playersAlive == 0)
            {
                Phase = DungeonPhase.Defeat;
                return;
            }

            if (Phase != DungeonPhase.Fighting)
            {
                return;
            }

            var done = cleared < sectionHasBoss.Length && sectionHasBoss[cleared] ? bossesAlive == 0 : enemiesAlive == 0;
            if (!done)
            {
                return;
            }

            // The boss is down: what it summoned goes with it, the fallen get back up, and the next gate opens.
            var section = cleared;
            world.Remove(e => !e.Owner.IsValid && e.Summoned && e.Section == section);
            for (var i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (e.Owner.IsValid && !e.IsAlive)
                {
                    e.Respawn(e.Position);
                    e.Health = e.Stats.MaxHealth * 0.5f;
                }
            }

            cleared++;
            Map.Progress = cleared;
            Room = System.Math.Min(cleared, dungeon.Rooms.Length - 1);
            if (cleared >= dungeon.Rooms.Length)
            {
                Phase = DungeonPhase.Victory;
            }
            else
            {
                Phase = DungeonPhase.Cleared;
                timer = dungeon.ClearDelaySeconds;
            }
        }

        void RemoveEnemies()
        {
            world.Remove(e => !e.Owner.IsValid);
            combat.CancelAllAttacks();
        }
    }
}
