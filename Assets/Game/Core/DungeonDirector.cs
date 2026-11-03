namespace Game.Core
{
    // Dungeon progression: rooms, boss-gated clears, victory, defeat, restart, and the respawn rule that depends on it.
    internal sealed class DungeonDirector
    {
        readonly ServerConfig config;
        readonly World world;
        readonly Session session;
        readonly Combat combat;
        DungeonDef dungeon;
        bool roomHasBoss;
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

        public int RoomCount => dungeon != null ? dungeon.Rooms.Length : 0;

        public float Timer => timer > 0f ? timer : 0f;

        public void Start(DungeonDef definition)
        {
            dungeon = definition;
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
            for (var i = 0; i < session.Count; i++)
            {
                var player = world.Find(session[i].Entity);
                if (player != null)
                {
                    player.Charge = 0f;
                }
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

        void RemoveEnemies()
        {
            world.Remove(e => !e.Owner.IsValid);
            combat.CancelAllAttacks();
        }
    }
}
