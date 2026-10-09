namespace HandHero.Core
{
    // Seeded random stream (SplitMix64) that restarts in place: a pooled bot
    // reseeds its one stream per spawn instead of allocating a System.Random
    // (~300 B) each time (round 4, S3). Same seed, same sequence; the sequence
    // differs from System.Random's for that seed.
    public sealed class SeededRandom
    {
        private ulong _state;

        public SeededRandom(int seed)
        {
            Reseed(seed);
        }

        // Continues exactly like new SeededRandom(seed).
        public void Reseed(int seed)
        {
            _state = unchecked((ulong)(uint)seed);
        }

        // Uniform in [0, 1), 53 random bits.
        public double NextDouble()
        {
            return (Next64() >> 11) * (1.0 / 9007199254740992.0);
        }

        private ulong Next64()
        {
            unchecked
            {
                ulong z = _state += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
