namespace Game.Core
{
    public readonly struct SimulationSettings
    {
        public readonly int TickRate;
        public readonly float ArenaRadius;
        public readonly WorldMap Map;

        public SimulationSettings(int tickRate, float arenaRadius, WorldMap map = null)
        {
            TickRate = tickRate;
            ArenaRadius = arenaRadius;
            Map = map;
        }

        public SimulationSettings WithMap(WorldMap map)
        {
            return new SimulationSettings(TickRate, ArenaRadius, map);
        }

        // Keeps a position where something can stand: inside the dungeon's open zones, or inside the round arena when there is no map.
        public Vec2 Constrain(Vec2 position)
        {
            return Map != null ? Map.Constrain(position) : position.ClampLength(ArenaRadius);
        }

        public static SimulationSettings Default => new SimulationSettings(30, 20f);

        public float TickSeconds => 1f / TickRate;
    }

    public static class Movement
    {
        public static Vec2 Step(Vec2 position, Vec2 input, float speed, in SimulationSettings settings)
        {
            var moved = position + input.ClampLength(1f) * (speed * settings.TickSeconds);
            return settings.Constrain(moved);
        }
    }
}
