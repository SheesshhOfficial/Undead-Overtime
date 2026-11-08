using System;
using System.Collections.Generic;

namespace UndeadOvertime.Core
{
    public readonly struct MoveStep
    {
        public readonly PlayerTicket Player;
        public readonly AimVector Displacement;
        public MoveStep(PlayerTicket player, AimVector displacement) { Player = player; Displacement = displacement; }
    }

    /// <summary>
    /// One host-owned round of planar walking intent. Packets never move a player: only a trusted
    /// host tick produces a bounded displacement, which the Unity adapter must collision-resolve.
    /// No client position, speed, delta time, timestamp, vertical input or identity is accepted.
    /// </summary>
    public sealed class HostMoveGate
    {
        public const double Speed = 4d, MaximumStep = .05d, InputLifetime = .25d;
        public const double EyeHeight = 1.6d, ArenaLimit = 15.3d;
        private readonly LanSession session;
        private readonly long sessionId, roundId;
        private readonly Dictionary<PlayerTicket, InputState> inputs = new Dictionary<PlayerTicket, InputState>();
        private double lastClock, lastStep;
        private sealed class InputState
        {
            public AimVector Intent, PendingIntent;
            public ulong Sequence;
            public double ReceivedAt, PendingReceivedAt;
            public bool HasInput, HasPending;
        }
        public HostMoveGate(LanSession session, double hostTime)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            if (session.Phase != SessionPhase.Playing) throw new InvalidOperationException("Begin a round before constructing its movement gate.");
            if (!ValidTime(hostTime)) throw new ArgumentOutOfRangeException(nameof(hostTime));
            sessionId = session.SessionId; roundId = session.RoundId; lastClock = lastStep = hostTime;
            foreach (PlayerTicket member in session.SnapshotMembers()) inputs.Add(member, new InputState());
        }
        public static AimVector Spawn(int slot) => new AimVector(-9 + slot * 2, EyeHeight, -8);
        public static bool ValidIntent(AimVector input) => input.IsFinite && input.Y == 0 && input.LengthSquared <= 1.001d;
        public static bool ValidPosition(AimVector position) => position.IsFinite && Math.Abs(position.Y - EyeHeight) < .001d &&
            Math.Abs(position.X) <= ArenaLimit + .001d && Math.Abs(position.Z) <= ArenaLimit + .001d;
        private static bool ValidTime(double time) => AimVector.Finite(time) && time >= 0 && time <= HostFireGate.MaximumHostTime;
        private bool Current => session.Phase == SessionPhase.Playing && session.SessionId == sessionId && session.RoundId == roundId;
        private bool Clock(double time)
        {
            if (!ValidTime(time) || time < lastClock) return false;
            lastClock = time; return true;
        }
        public bool TrySetIntent(PlayerTicket sender, long requestSession, long requestRound, ulong sequence, AimVector input, double hostTime)
        {
            if (!Current || requestSession != sessionId || requestRound != roundId || !session.IsCurrent(sender) ||
                !inputs.TryGetValue(sender, out InputState state) || !Clock(hostTime) || sequence == 0 || sequence <= state.Sequence) return false;
            state.Sequence = sequence;
            if (!ValidIntent(input)) return false;
            double divisor = Math.Max(1d, Math.Sqrt(input.LengthSquared));
            state.PendingIntent = new AimVector(input.X / divisor, 0, input.Z / divisor);
            state.PendingReceivedAt = hostTime; state.HasPending = true;
            // An explicit stop cancels held intent immediately; it cannot add any travel.
            if (input.LengthSquared == 0) { state.Intent = default; state.HasInput = false; state.HasPending = false; }
            return true;
        }
        public bool TryStepFromHost(double hostTime, out MoveStep[] steps)
        {
            steps = Array.Empty<MoveStep>();
            if (!Current || !Clock(hostTime)) return false;
            double duration = Math.Min(MaximumStep, hostTime - lastStep); lastStep = hostTime;
            var rows = new List<MoveStep>();
            foreach (PlayerTicket player in session.SnapshotMembers())
            {
                if (!inputs.TryGetValue(player, out InputState state)) continue;
                if (state.HasInput && hostTime - state.ReceivedAt < InputLifetime)
                    rows.Add(new MoveStep(player, new AimVector(state.Intent.X * Speed * duration, 0, state.Intent.Z * Speed * duration)));
                // Intent changes take effect at the tick boundary, after the prior intent's step.
                // No mid-tick direction change can delete earlier held movement or apply backwards.
                if (state.HasPending)
                {
                    state.Intent = state.PendingIntent; state.ReceivedAt = state.PendingReceivedAt;
                    state.HasInput = hostTime - state.ReceivedAt < InputLifetime; state.HasPending = false;
                }
            }
            steps = rows.ToArray(); return true;
        }
    }
}
