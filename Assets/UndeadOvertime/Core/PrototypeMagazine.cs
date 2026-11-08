using System;

namespace UndeadOvertime.Core
{
    /// <summary>Solo weapon timing only. Never use this client-side object as network authority.</summary>
    public sealed class PrototypeMagazine
    {
        public const int Capacity = 12;
        public const double ShotInterval = 0.18d;
        public const double ReloadSeconds = 1.5d;
        public int Ammo { get; private set; }
        public bool IsReloading { get; private set; }
        public double ReloadEndsAt { get; private set; }
        private double lastTime, nextShotAt;

        public PrototypeMagazine() => Reset();
        public void Reset()
        {
            Ammo = Capacity;
            IsReloading = false;
            lastTime = nextShotAt = ReloadEndsAt = 0d;
        }

        public bool Advance(double time)
        {
            if (!AimVector.Finite(time) || time < lastTime || time > HostFireGate.MaximumHostTime) return false;
            lastTime = time;
            if (IsReloading && time >= ReloadEndsAt)
            {
                Ammo = Capacity;
                IsReloading = false;
            }
            return true;
        }

        public bool TryFire(double time)
        {
            if (!Advance(time) || IsReloading || Ammo == 0 || time < nextShotAt) return false;
            Ammo--;
            nextShotAt = time + ShotInterval;
            return true;
        }

        public bool TryReload(double time)
        {
            if (!Advance(time) || IsReloading || Ammo == Capacity) return false;
            IsReloading = true;
            ReloadEndsAt = time + ReloadSeconds;
            return true;
        }
    }
}
