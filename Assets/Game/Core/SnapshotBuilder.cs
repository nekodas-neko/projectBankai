namespace Game.Core
{
    // Turns the current world and dungeon state into the snapshot sent to every client.
    internal static class SnapshotBuilder
    {
        public static Snapshot Build(int tick, World world, DungeonDirector dungeon)
        {
            var snapshot = new Snapshot
            {
                Tick = tick,
                Room = dungeon.Active ? (byte)(dungeon.Room + 1) : (byte)0,
                RoomCount = (byte)dungeon.RoomCount,
                Dungeon = dungeon.Active ? dungeon.Phase : DungeonPhase.None,
                DungeonTimer = dungeon.Timer,
            };

            var entities = world.Entities;
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

            snapshot.Events.AddRange(world.Events);
            world.Events.Clear();
            snapshot.Uses.AddRange(world.Uses);
            world.Uses.Clear();
            return snapshot;
        }
    }
}
