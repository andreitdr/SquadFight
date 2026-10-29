namespace Game.Core
{
    /// <summary>All game randomness goes through this so tests and replays can be seeded.</summary>
    public interface IRandom
    {
        /// <summary>Uniform integer in [minInclusive, maxExclusive). Returns minInclusive when the range is empty.</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>Uniform double in [0, 1).</summary>
        double NextDouble();

        /// <summary>Opaque generator state; restoring it reproduces the same sequence.</summary>
        ulong[] GetState();

        void SetState(ulong[] state);
    }
}
