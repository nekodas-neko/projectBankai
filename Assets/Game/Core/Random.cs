namespace Game.Core
{
    public interface IRandom
    {
        uint NextUInt();
        float NextFloat();
    }

    public sealed class SeededRandom : IRandom
    {
        ulong state;

        public SeededRandom(ulong seed)
        {
            state = seed;
        }

        public uint NextUInt()
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                var z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return (uint)((z ^ (z >> 31)) >> 32);
            }
        }

        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / (1 << 24));
        }
    }
}
