using System;

namespace Game.Core
{
    public interface IClock
    {
        DateTime UtcNow { get; }

        /// <summary>Monotonic seconds since an arbitrary origin (for debouncing; unaffected by device clock changes).</summary>
        double RealtimeSeconds { get; }
    }

    public sealed class SystemClock : IClock
    {
        private readonly System.Diagnostics.Stopwatch _watch = System.Diagnostics.Stopwatch.StartNew();
        public DateTime UtcNow => DateTime.UtcNow;
        public double RealtimeSeconds => _watch.Elapsed.TotalSeconds;
    }

    /// <summary>Manually advanced clock for tests and simulations.</summary>
    public sealed class ManualClock : IClock
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public double RealtimeSeconds { get; set; }

        public void Advance(double seconds)
        {
            RealtimeSeconds += seconds;
            UtcNow = UtcNow.AddSeconds(seconds);
        }
    }
}
