namespace Game.Core
{
    public readonly struct SimulationSettings
    {
        public readonly int TickRate;
        public readonly float ArenaRadius;

        public SimulationSettings(int tickRate, float arenaRadius)
        {
            TickRate = tickRate;
            ArenaRadius = arenaRadius;
        }

        public static SimulationSettings Default => new SimulationSettings(30, 20f);

        public float TickSeconds => 1f / TickRate;
    }

    public static class Movement
    {
        public static Vec2 Step(Vec2 position, Vec2 input, float speed, in SimulationSettings settings)
        {
            var moved = position + input.ClampLength(1f) * (speed * settings.TickSeconds);
            return moved.ClampLength(settings.ArenaRadius);
        }
    }
}
