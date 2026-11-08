using System;

namespace UndeadOvertime.Core
{
    public enum CoopMissionPhase : byte { RestorePower, DefendDepot, ReachExtraction, Completed, Failed }

    /// <summary>Trusted aggregate computed by the host adapter, never decoded from a client packet.
    /// PowerHeld requires a living current member with fresh intent, range and line of sight.
    /// AllConnectedAtExit includes every connected member; a downed teammate must be revived first.</summary>
    public readonly struct CoopMissionObservation
    {
        public readonly int LivingPlayers;
        public readonly bool PowerHeld, AllConnectedAtExit;
        public readonly HordePhase Horde;
        public CoopMissionObservation(int livingPlayers, bool powerHeld, bool allConnectedAtExit, HordePhase horde)
        { LivingPlayers = livingPlayers; PowerHeld = powerHeld; AllConnectedAtExit = allConnectedAtExit; Horde = horde; }
    }

    public readonly struct CoopMissionSnapshot
    {
        public readonly long SessionId, RoundId;
        public readonly CoopMissionPhase Phase;
        public readonly bool IsCurrent, DepotGateUnlocked;
        public readonly double HoldSeconds;
        public CoopMissionSnapshot(long sessionId, long roundId, CoopMissionPhase phase, bool current, bool gate, double hold)
        { SessionId = sessionId; RoundId = roundId; Phase = phase; IsCurrent = current; DepotGateUnlocked = gate; HoldSeconds = hold; }
    }

    /// <summary>Round-scoped shared objective rules only. No networking, scene, save file or Unity dependency.
    /// Restore power, defend three host-owned waves, then gather every connected living survivor at extraction.
    /// A single shared clock prevents additional teammates multiplying interaction speed.</summary>
    public sealed class CoopMissionProgression
    {
        public const double PowerHoldDuration = 3, ExtractionHoldDuration = 2, MaximumStep = .1, MaximumContinuityGap = .25;
        private readonly LanSession session;
        private readonly long sessionId, roundId;
        private double lastClock, holdSeconds;
        private bool hasObservation, wasEligible, depotGateUnlocked;
        public CoopMissionPhase Phase { get; private set; }
        public bool IsCurrent => session.Phase == SessionPhase.Playing && session.SessionId == sessionId && session.RoundId == roundId;
        public CoopMissionSnapshot Snapshot => new CoopMissionSnapshot(sessionId, roundId, Phase, IsCurrent, depotGateUnlocked, holdSeconds);
        private bool Terminal => Phase == CoopMissionPhase.Completed || Phase == CoopMissionPhase.Failed;

        public CoopMissionProgression(LanSession session, double hostTime)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            if (session.Phase != SessionPhase.Playing) throw new InvalidOperationException("Begin the mission round first.");
            if (!ValidTime(hostTime)) throw new ArgumentOutOfRangeException(nameof(hostTime));
            sessionId = session.SessionId; roundId = session.RoundId; lastClock = hostTime; Phase = CoopMissionPhase.RestorePower;
        }
        private static bool ValidTime(double time) => AimVector.Finite(time) && time >= 0 && time <= HostFireGate.MaximumHostTime;
        private void ClearHold() { holdSeconds = 0; wasEligible = false; }
        private void Transition(CoopMissionPhase next) { Phase = next; ClearHold(); }

        /// <summary>Call once per authoritative host tick after membership, life state, input expiry and physics.
        /// Invalid observations cancel an in-progress hold but cannot unlock or complete an objective.
        /// Backwards/duplicate timestamps never advance progress. Long gaps require a fresh uninterrupted hold.
        /// True means the observation was accepted, not that Phase changed; adapters must compare transitions.</summary>
        public bool AdvanceFromHost(double hostTime, CoopMissionObservation observation)
        {
            if (!IsCurrent || Terminal || !ValidTime(hostTime) || hostTime < lastClock || (hasObservation && hostTime == lastClock)) return false;
            if (observation.LivingPlayers < 0 || observation.LivingPlayers > session.PlayerCount ||
                (byte)observation.Horde > (byte)HordePhase.Failed)
            { ClearHold(); return false; }
            if ((Phase == CoopMissionPhase.RestorePower && observation.Horde != HordePhase.Disabled && observation.Horde != HordePhase.Failed) ||
                (Phase == CoopMissionPhase.ReachExtraction && observation.Horde != HordePhase.Completed && observation.Horde != HordePhase.Failed))
            { ClearHold(); return false; }
            double elapsed = hostTime - lastClock;
            bool continuous = hasObservation && elapsed <= MaximumContinuityGap;
            lastClock = hostTime; hasObservation = true;
            if (!continuous) ClearHold();
            if (observation.LivingPlayers == 0 || observation.Horde == HordePhase.Failed)
            { Transition(CoopMissionPhase.Failed); return true; }
            if (Phase == CoopMissionPhase.DefendDepot)
            {
                // Wave completion alone never skips the separate extraction objective.
                if (observation.Horde == HordePhase.Completed) Transition(CoopMissionPhase.ReachExtraction);
                return true;
            }
            bool eligible = Phase == CoopMissionPhase.RestorePower ? observation.PowerHeld :
                observation.AllConnectedAtExit && observation.LivingPlayers == session.PlayerCount;
            if (!eligible) { ClearHold(); return true; }
            if (wasEligible && continuous) holdSeconds += Math.Min(MaximumStep, elapsed);
            wasEligible = true;
            double required = Phase == CoopMissionPhase.RestorePower ? PowerHoldDuration : ExtractionHoldDuration;
            if (holdSeconds + 1e-9 >= required)
            {
                if (Phase == CoopMissionPhase.RestorePower)
                { depotGateUnlocked = true; Transition(CoopMissionPhase.DefendDepot); }
                else Transition(CoopMissionPhase.Completed);
            }
            return true;
        }
        /// <summary>Explicit authoritative loss (for example spawn failure). Completed missions cannot be overwritten.</summary>
        public bool FailFromHost()
        {
            if (!IsCurrent || Terminal) return false;
            Transition(CoopMissionPhase.Failed); return true;
        }
    }
}
