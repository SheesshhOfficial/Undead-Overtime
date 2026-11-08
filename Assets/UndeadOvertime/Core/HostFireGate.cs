using System;
using System.Collections.Generic;

namespace UndeadOvertime.Core
{
    /// <summary>Engine-independent finite vector; adapters convert to/from Unity's Vector3.</summary>
    public readonly struct AimVector
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public AimVector(double x, double y, double z) { X = x; Y = y; Z = z; }
        public bool IsFinite => Finite(X) && Finite(Y) && Finite(Z);
        public double LengthSquared => X * X + Y * Y + Z * Z;
        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static AimVector operator -(AimVector a, AimVector b) => new AimVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static double Dot(AimVector a, AimVector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public bool IsUnit => IsFinite && Math.Abs(LengthSquared - 1d) <= 0.001d;
        public AimVector Normalized()
        {
            if (!IsUnit) throw new ArgumentException("A finite near-unit direction is required.");
            double length = Math.Sqrt(LengthSquared);
            return new AimVector(X / length, Y / length, Z / length);
        }
    }

    public enum FireResult
    {
        Accepted, StaleRound, UnknownPlayer, NoHostPose, InvalidTime, InvalidSequence,
        InvalidAim, OriginMismatch, AimMismatch, Downed, Reloading, RateLimited, Empty
    }

    /// <summary>Only intent is accepted; no target, damage, hit position, range or client clock.</summary>
    public readonly struct FireRequest
    {
        public long SessionId { get; }
        public long RoundId { get; }
        public ulong Sequence { get; }
        public AimVector ClaimedOrigin { get; }
        public AimVector Direction { get; }
        public FireRequest(long sessionId, long roundId, ulong sequence, AimVector claimedOrigin, AimVector direction)
        { SessionId = sessionId; RoundId = roundId; Sequence = sequence; ClaimedOrigin = claimedOrigin; Direction = direction; }
    }

    /// <summary>Host-created shot; raycast THIS origin/direction on the host and apply damage there.</summary>
    public readonly struct AuthorizedShot
    {
        public PlayerTicket Shooter { get; }
        public AimVector Origin { get; }
        public AimVector Direction { get; }
        public double Range { get; }
        public int Damage { get; }
        public AuthorizedShot(PlayerTicket shooter, AimVector origin, AimVector direction, double range, int damage)
        { Shooter = shooter; Origin = origin; Direction = direction; Range = range; Damage = damage; }
    }

    /// <summary>
    /// Bounded, single-threaded fire admission for one host-owned round. This is NOT networking
    /// or anti-cheat by itself. Only trusted host code may update pose/alive state, grant reloads,
    /// or supply time; adapters must map transport senders to current host-held PlayerTickets.
    /// Construct a new gate for every round. All movement, occlusion, target selection and damage
    /// remain host work outside this gate. No client hit result is accepted.
    /// </summary>
    public sealed class HostFireGate
    {
        public const int MagazineSize = 12;
        public const int Damage = 25;
        public const double Range = 100d;
        public const double FireInterval = 0.18d;
        public const double ReloadDuration = 1.5d;
        public const double OriginTolerance = 0.35d;
        // Well beyond any round lifetime while retaining sub-millisecond double precision.
        public const double MaximumHostTime = 3155760000d;
        // Request direction must be within five degrees of last host-accepted aim.
        private const double MinimumAimDot = 0.9961946980917455d;
        private readonly LanSession session;
        private readonly long sessionId;
        private readonly long roundId;
        private readonly Dictionary<PlayerTicket, WeaponState> weapons = new Dictionary<PlayerTicket, WeaponState>();
        private double lastHostTime;

        private sealed class WeaponState
        {
            public AimVector Origin;
            public AimVector Direction;
            public bool HasPose;
            public bool Alive = true;
            public ulong LastSequence;
            public int Ammo = MagazineSize;
            public double NextShotAt;
            public bool IsReloading;
            public double ReloadEndsAt;
        }

        public HostFireGate(LanSession session, double hostTime)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            if (session.Phase != SessionPhase.Playing) throw new InvalidOperationException("Begin a round before constructing its fire gate.");
            if (!AimVector.Finite(hostTime) || hostTime < 0d || hostTime > MaximumHostTime) throw new ArgumentOutOfRangeException(nameof(hostTime));
            sessionId = session.SessionId;
            roundId = session.RoundId;
            lastHostTime = hostTime;
            foreach (PlayerTicket member in session.SnapshotMembers()) weapons.Add(member, new WeaponState());
        }

        public bool IsCurrentRound => session.Phase == SessionPhase.Playing && session.SessionId == sessionId && session.RoundId == roundId;

        /// <summary>Read-only identity check for host snapshot assembly; equal numeric IDs in another owner are insufficient.</summary>
        public bool IsBoundToCurrentRound(LanSession owner) => ReferenceEquals(session, owner) && IsCurrentRound;

        public bool SetPoseFromHost(PlayerTicket player, AimVector origin, AimVector aim)
        {
            if (!TryState(player, out WeaponState state) || !origin.IsFinite || !aim.IsUnit) return false;
            state.Origin = origin;
            state.Direction = aim.Normalized();
            state.HasPose = true;
            return true;
        }

        public bool SetAliveFromHost(PlayerTicket player, bool alive)
        {
            if (!TryState(player, out WeaponState state)) return false;
            state.Alive = alive;
            if (!alive) state.IsReloading = false;
            return true;
        }

        public int Ammo(PlayerTicket player) => TryState(player, out WeaponState state) ? state.Ammo : 0;

        public bool IsReloading(PlayerTicket player) => TryState(player, out WeaponState state) && state.IsReloading;

        /// <summary>Trusted host tick: completes reloads even when no further command arrives.</summary>
        public bool AdvanceReloadsFromHost(double hostTime)
        {
            if (!IsCurrentRound || !AdvanceTime(hostTime)) return false;
            foreach (WeaponState state in weapons.Values) CompleteReload(state, hostTime);
            return true;
        }

        public bool TryBeginReloadFromHost(PlayerTicket player, double hostTime)
        {
            if (!TryState(player, out WeaponState state) || !AdvanceTime(hostTime)) return false;
            CompleteReload(state, hostTime);
            if (!state.Alive || state.IsReloading || state.Ammo == MagazineSize) return false;
            state.IsReloading = true;
            state.ReloadEndsAt = hostTime + ReloadDuration;
            return true;
        }

        public FireResult TryAuthorize(PlayerTicket transportSender, FireRequest request, double hostTime, out AuthorizedShot shot)
        {
            shot = default;
            if (!IsCurrentRound || request.SessionId != sessionId || request.RoundId != roundId) return FireResult.StaleRound;
            if (!TryState(transportSender, out WeaponState state)) return FireResult.UnknownPlayer;
            if (!AdvanceTime(hostTime)) return FireResult.InvalidTime;
            if (request.Sequence == 0 || request.Sequence <= state.LastSequence) return FireResult.InvalidSequence;
            // Every fresh request consumes its sequence, including rejected attempts. A held/rate-limited
            // packet cannot become a delayed valid shot when retransmitted after cooldown or revival.
            state.LastSequence = request.Sequence;
            if (!request.ClaimedOrigin.IsFinite || !request.Direction.IsUnit) return FireResult.InvalidAim;
            if (!state.HasPose) return FireResult.NoHostPose;
            if ((request.ClaimedOrigin - state.Origin).LengthSquared > OriginTolerance * OriginTolerance) return FireResult.OriginMismatch;
            AimVector direction = request.Direction.Normalized();
            if (AimVector.Dot(direction, state.Direction) < MinimumAimDot) return FireResult.AimMismatch;
            if (!state.Alive) return FireResult.Downed;
            CompleteReload(state, hostTime);
            if (state.IsReloading) return FireResult.Reloading;
            if (hostTime < state.NextShotAt) return FireResult.RateLimited;
            if (state.Ammo == 0) return FireResult.Empty;
            state.Ammo--;
            state.NextShotAt = hostTime + FireInterval;
            // Resolve from authoritative pose, not the client's claimed origin (even within tolerance).
            shot = new AuthorizedShot(transportSender, state.Origin, direction, Range, Damage);
            return FireResult.Accepted;
        }

        private bool TryState(PlayerTicket player, out WeaponState state)
        {
            state = null;
            return IsCurrentRound && session.IsCurrent(player) && weapons.TryGetValue(player, out state);
        }

        private bool AdvanceTime(double hostTime)
        {
            if (!AimVector.Finite(hostTime) || hostTime < lastHostTime || hostTime > MaximumHostTime) return false;
            lastHostTime = hostTime;
            return true;
        }

        private static void CompleteReload(WeaponState state, double hostTime)
        {
            if (!state.IsReloading || hostTime < state.ReloadEndsAt) return;
            state.IsReloading = false;
            state.Ammo = MagazineSize; // Unlimited reserve in this isolated prototype rule only.
        }
    }
}
