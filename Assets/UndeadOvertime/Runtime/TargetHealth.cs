using System;

namespace UndeadOvertime.Prototype
{
    /// <summary>Small, deterministic damage model. No input, scene, or network ownership.</summary>
    public sealed class TargetHealth
    {
        public int Maximum { get; }
        public int Current { get; private set; }
        public bool IsAlive => Current > 0;

        public TargetHealth(int maximum)
        {
            if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            Maximum = maximum;
            Reset();
        }

        /// <returns>True only for the hit that changes this target from alive to dead.</returns>
        public bool ApplyDamage(int damage)
        {
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (!IsAlive || damage == 0) return false;
            Current -= Math.Min(Current, damage);
            return !IsAlive;
        }

        public void Reset() => Current = Maximum;
    }
}
