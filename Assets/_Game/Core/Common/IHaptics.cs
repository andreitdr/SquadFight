namespace Game.Core
{
    public enum HapticKind { Light, Medium, Success, Warning }

    public interface IHaptics
    {
        void Play(HapticKind kind);
    }

    public sealed class NullHaptics : IHaptics
    {
        public static readonly NullHaptics Instance = new NullHaptics();
        public void Play(HapticKind kind) { }
    }
}
