using System;
using System.Text;

namespace Game.Core
{
    /// <summary>Value per <see cref="StatType"/>. Sized from the enum so new stats need no code change here.</summary>
    public sealed class StatBlock : IEquatable<StatBlock>
    {
        public static readonly int Count = Enum.GetValues(typeof(StatType)).Length;
        public static readonly StatType[] All = (StatType[])Enum.GetValues(typeof(StatType));

        private readonly float[] _values = new float[Count];

        public StatBlock() { }

        public StatBlock(params (StatType stat, float value)[] entries)
        {
            foreach (var (stat, value) in entries) this[stat] = value;
        }

        public float this[StatType stat]
        {
            get => _values[(int)stat];
            set => _values[(int)stat] = value;
        }

        public StatBlock Clone()
        {
            var c = new StatBlock();
            Array.Copy(_values, c._values, Count);
            return c;
        }

        public void CopyFrom(StatBlock other) => Array.Copy(other._values, _values, Count);

        public StatBlock Add(StatBlock other)
        {
            for (var i = 0; i < Count; i++) _values[i] += other._values[i];
            return this;
        }

        public StatBlock AddScaled(StatBlock other, float factor)
        {
            for (var i = 0; i < Count; i++) _values[i] += other._values[i] * factor;
            return this;
        }

        public StatBlock Scale(float factor)
        {
            for (var i = 0; i < Count; i++) _values[i] *= factor;
            return this;
        }

        /// <summary>Component-wise multiply.</summary>
        public StatBlock Multiply(StatBlock other)
        {
            for (var i = 0; i < Count; i++) _values[i] *= other._values[i];
            return this;
        }

        public void Clear() => Array.Clear(_values, 0, Count);

        public static StatBlock operator +(StatBlock a, StatBlock b) => a.Clone().Add(b);
        public static StatBlock operator *(StatBlock a, float k) => a.Clone().Scale(k);

        public bool ApproximatelyEquals(StatBlock other, float epsilon = 1e-4f)
        {
            for (var i = 0; i < Count; i++)
                if (Math.Abs(_values[i] - other._values[i]) > epsilon) return false;
            return true;
        }

        public bool Equals(StatBlock other)
        {
            if (other is null) return false;
            for (var i = 0; i < Count; i++)
                if (!_values[i].Equals(other._values[i])) return false;
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as StatBlock);

        public override int GetHashCode()
        {
            var h = 17;
            for (var i = 0; i < Count; i++) h = h * 31 + _values[i].GetHashCode();
            return h;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (var i = 0; i < Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(All[i]).Append('=').Append(_values[i].ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }

    [Serializable]
    public struct StatModifier
    {
        public StatType Stat;
        public float Value;
        public ModifierKind Kind;

        public StatModifier(StatType stat, float value, ModifierKind kind = ModifierKind.Flat)
        {
            Stat = stat;
            Value = value;
            Kind = kind;
        }

        public override string ToString() => Kind == ModifierKind.Percent ? $"{Stat}+{Value * 100:0.#}%" : $"{Stat}+{Value:0.##}";
    }
}
