using System;

namespace Game.Core
{
    /// <summary>
    /// Deterministic xoshiro128** generator seeded through SplitMix64.
    /// Identical output on every runtime (Mono, IL2CPP, .NET), unlike System.Random.
    /// </summary>
    public sealed class SeededRandom : IRandom
    {
        private uint _s0, _s1, _s2, _s3;

        public SeededRandom(ulong seed)
        {
            var sm = seed;
            ulong a = SplitMix64(ref sm), b = SplitMix64(ref sm);
            _s0 = (uint)a; _s1 = (uint)(a >> 32); _s2 = (uint)b; _s3 = (uint)(b >> 32);
            if ((_s0 | _s1 | _s2 | _s3) == 0) _s0 = 1;
        }

        /// <summary>Seed from the current time; for non-reproducible play.</summary>
        public static SeededRandom FromTime() => new SeededRandom((ulong)DateTime.UtcNow.Ticks);

        private static ulong SplitMix64(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            var z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private static uint Rotl(uint x, int k) => (x << k) | (x >> (32 - k));

        public uint NextUInt()
        {
            var result = Rotl(_s1 * 5, 7) * 9;
            var t = _s1 << 9;
            _s2 ^= _s0; _s3 ^= _s1; _s1 ^= _s2; _s0 ^= _s3;
            _s2 ^= t;
            _s3 = Rotl(_s3, 11);
            return result;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            var range = (uint)((long)maxExclusive - minInclusive);
            // Lemire's nearly-divisionless unbiased bounded integer.
            ulong m = (ulong)NextUInt() * range;
            var low = (uint)m;
            if (low < range)
            {
                var threshold = (uint)(-range) % range;
                while (low < threshold)
                {
                    m = (ulong)NextUInt() * range;
                    low = (uint)m;
                }
            }
            return (int)(minInclusive + (long)(m >> 32));
        }

        public double NextDouble() => (NextUInt() >> 8) * (1.0 / 16777216.0);

        public ulong[] GetState() => new[] { ((ulong)_s1 << 32) | _s0, ((ulong)_s3 << 32) | _s2 };

        public void SetState(ulong[] state)
        {
            if (state == null || state.Length != 2) throw new ArgumentException("State must have 2 elements.", nameof(state));
            _s0 = (uint)state[0]; _s1 = (uint)(state[0] >> 32); _s2 = (uint)state[1]; _s3 = (uint)(state[1] >> 32);
            if ((_s0 | _s1 | _s2 | _s3) == 0) _s0 = 1;
        }
    }
}
