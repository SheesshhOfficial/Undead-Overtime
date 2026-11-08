using System;
using System.Collections.Generic;

namespace UndeadOvertime.Core
{
    /// <summary>Host-only bridge between mission rules, connection lifetimes and the real infected encounter.
    /// Not a network decoder. Transport must resolve its sender to a host-held ticket and supply host time.
    /// This instance owns its encounter; do not advance waves separately or substitute another round's state.</summary>
    public sealed class HostCoopMission
    {
        public const double PowerRange = 2, InputLifetime = .25, PoseLifetime = .25, IntentInterval = .02;
        private readonly LanSession session;
        private readonly CoopMissionProgression progression;
        private readonly Dictionary<PlayerTicket, Member> members = new Dictionary<PlayerTicket, Member>();
        private readonly AimVector powerControl, exitCenter;
        private readonly double exitHalfWidth, exitHalfDepth;
        private double lastClock, lastTick;
        private bool hasTick, advancing;
        private ulong frameSequence;
        private sealed class Member
        {
            public AimVector Position;
            public bool HasPose, Held, QualifiedLastTick, ExitInterrupted;
            public double PoseAt, IntentAt, HeldSince, NextIntentAt;
            public ulong Sequence;
            public int PreviousHealth = 100;
        }
        public HostInfectedEncounter Encounter { get; }
        public CoopMissionSnapshot Snapshot => progression.Snapshot;
        private bool Active => progression.IsCurrent && progression.Phase != CoopMissionPhase.Completed && progression.Phase != CoopMissionPhase.Failed;

        public HostCoopMission(LanSession session, double hostTime, AimVector powerControl,
            AimVector exitCenter, double exitHalfWidth = 2, double exitHalfDepth = 2)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!HostMoveGate.ValidPosition(powerControl)) throw new ArgumentOutOfRangeException(nameof(powerControl));
            if (!HostMoveGate.ValidPosition(exitCenter) || !ValidExtent(exitHalfWidth) || !ValidExtent(exitHalfDepth) ||
                Math.Abs(exitCenter.X) + exitHalfWidth > HostMoveGate.ArenaLimit || Math.Abs(exitCenter.Z) + exitHalfDepth > HostMoveGate.ArenaLimit)
                throw new ArgumentOutOfRangeException(nameof(exitCenter), "Extraction must be a finite rectangle inside the arena.");
            this.session = session; this.powerControl = powerControl; this.exitCenter = exitCenter;
            this.exitHalfWidth = exitHalfWidth; this.exitHalfDepth = exitHalfDepth;
            progression = new CoopMissionProgression(session, hostTime);
            Encounter = new HostInfectedEncounter(session, hostTime);
            lastClock = lastTick = hostTime;
            foreach (var ticket in session.SnapshotMembers()) members.Add(ticket, new Member());
        }
        private static bool ValidExtent(double extent) => AimVector.Finite(extent) && extent >= .5 && extent <= 4;
        private static bool ValidTime(double time) => AimVector.Finite(time) && time >= 0 && time <= HostFireGate.MaximumHostTime;
        private bool Clock(double time)
        { if (!ValidTime(time) || time < lastClock) return false; lastClock = time; return true; }
        private bool State(PlayerTicket ticket, out Member member)
        { member = null; return progression.IsCurrent && session.IsCurrent(ticket) && members.TryGetValue(ticket, out member); }
        private static void Cancel(Member member) { member.Held = member.QualifiedLastTick = false; }

        /// <summary>Refresh every authoritative tick after collision-resolved movement. Missing/stale poses cannot
        /// operate power or qualify for extraction. Invalid positions invalidate the prior mission pose.</summary>
        public bool SetPlayerPositionFromHost(PlayerTicket player, AimVector position, double hostTime)
        {
            if (advancing || !State(player, out Member member) || !Clock(hostTime)) return false;
            if (!HostMoveGate.ValidPosition(position)) { member.HasPose = false; member.ExitInterrupted = true; Cancel(member); return false; }
            if (!Encounter.SetPlayerPositionFromHost(player, position)) return false;
            if (member.HasPose && hostTime - member.PoseAt >= PoseLifetime)
            { member.ExitInterrupted = true; Cancel(member); }
            if (!InsideExit(position)) member.ExitInterrupted = true;
            if ((position - powerControl).LengthSquared > PowerRange * PowerRange) Cancel(member);
            member.Position = position; member.PoseAt = hostTime; member.HasPose = true; return true;
        }
        private bool InsideExit(AimVector position) => Math.Abs(position.X - exitCenter.X) <= exitHalfWidth && Math.Abs(position.Z - exitCenter.Z) <= exitHalfDepth;
        /// <summary>Only held/released intent and replay guards are accepted, never elapsed time, position or phase.
        /// Fresh rejected commands consume their sequence; release bypasses positive-input pacing.</summary>
        public bool TrySetPowerIntent(PlayerTicket sender, long requestSession, long requestRound,
            ulong sequence, bool held, double hostTime)
        {
            var snapshot = Snapshot;
            if (advancing || !State(sender, out Member member) || requestSession != snapshot.SessionId || requestRound != snapshot.RoundId ||
                !Clock(hostTime) || sequence == 0 || sequence <= member.Sequence) return false;
            member.Sequence = sequence;
            if (!held) { Cancel(member); return true; }
            if (!Active || progression.Phase != CoopMissionPhase.RestorePower || Encounter.PlayerHealth(sender) <= 0 || hostTime < member.NextIntentAt) return false;
            if (!member.Held || hostTime - member.IntentAt >= InputLifetime) member.HeldSince = hostTime;
            member.Held = true; member.IntentAt = hostTime; member.NextIntentAt = hostTime + IntentInterval;
            return true;
        }
        /// <summary>Call on authoritative fire, reload, movement, revive interaction or injury to cancel power.
        /// Tick also detects health loss; event cancellation covers actions between adjacent host ticks.</summary>
        public void CancelPowerFromHost(PlayerTicket player)
        { if (!advancing && State(player, out Member member)) Cancel(member); }

        /// <summary>Capture immediately after the coherent host tick and weapon life/reload synchronization.
        /// Uses this coordinator's fresh host poses and owned encounter, never client-reported aggregate fields.
        /// Slots are host-held, stable per connection; legacy range target health is explicit host state.
        /// No clock/objective/weapon mutation occurs. Only a validated encoded capture consumes a frame sequence.
        /// Codec validation cannot prove caller tick ordering. Not a transport or authentication boundary.</summary>
        public bool TryCaptureFrame(double hostTime, HostFireGate fireGate, IReadOnlyDictionary<PlayerTicket, byte> slots,
            int[] targetHealth, out MissionFrame frame, out byte[] payload)
        {
            frame = null; payload = null;
            if (advancing || !progression.IsCurrent || !ValidTime(hostTime) || hostTime < lastClock ||
                frameSequence == ulong.MaxValue || fireGate == null || !fireGate.IsBoundToCurrentRound(session) ||
                slots == null || targetHealth == null || targetHealth.Length != 3) return false;
            for (int i = 0; i < targetHealth.Length; i++) if (targetHealth[i] < 0 || targetHealth[i] > 100) return false;
            var rows = new List<RangeMember>(); var usedSlots = new HashSet<byte>();
            foreach (PlayerTicket ticket in session.SnapshotMembers())
            {
                if (!members.TryGetValue(ticket, out Member member) || !member.HasPose || hostTime < member.PoseAt ||
                    hostTime - member.PoseAt >= PoseLifetime || !HostMoveGate.ValidPosition(member.Position) ||
                    !slots.TryGetValue(ticket, out byte slot) || slot >= LanSession.MaximumPlayers || !usedSlots.Add(slot)) return false;
                byte targetSlot = 255, progress = 0;
                if (Encounter.ReviveStatusFromHost(ticket, hostTime, out PlayerTicket target, out progress))
                {
                    if (!session.IsCurrent(target) || !slots.TryGetValue(target, out targetSlot) || targetSlot >= LanSession.MaximumPlayers) return false;
                }
                rows.Add(new RangeMember(ticket.PeerId, ticket.ConnectionId, slot, (byte)fireGate.Ammo(ticket), fireGate.IsReloading(ticket),
                    member.Position, (byte)Encounter.PlayerHealth(ticket), targetSlot, progress));
            }
            var range = new RangeSnapshot(session.SessionId, session.RoundId, session.Phase, rows.ToArray(), targetHealth,
                Encounter.Phase, Encounter.SnapshotEnemies(), Encounter.HordeAt(hostTime));
            var candidate = new MissionFrame(frameSequence + 1, range, Snapshot);
            byte[] encoded;
            try { encoded = CoopMissionProtocol.Encode(candidate); }
            catch (ArgumentException) { return false; } // A torn/invalid aggregate must not be published or consume sequence.
            frameSequence = candidate.Sequence; frame = candidate; payload = encoded; return true;
        }

        /// <summary>Call once after authoritative movement, damage and revive processing. Fresh poses, health,
        /// membership and host LOS form the observation. Places each new wave exactly once before it can act.
        /// placeWave must collision-check and place EVERY enemy atomically, returning false if blocked.
        /// Missing callbacks/failed placement fail closed. Callback exceptions fail the mission then propagate.</summary>
        public bool AdvanceFromHost(double hostTime, Func<PlayerTicket, AimVector, AimVector, bool> clearPowerLine,
            Func<HostInfectedEncounter, bool> placeWave)
        {
            if (advancing || !Active || !Clock(hostTime) || (hasTick && hostTime == lastTick)) return false;
            advancing = true;
            try
            {
                if (clearPowerLine == null || placeWave == null) return Fail();
                // The encounter is owned by this mission. Starting a separate test encounter is invalid here.
                if (progression.Phase == CoopMissionPhase.RestorePower && Encounter.Phase != EncounterPhase.Dormant) return Fail();
                if (progression.Phase == CoopMissionPhase.DefendDepot && Encounter.AdvanceWavesFromHost(hostTime) && !Place(placeWave)) return false;
                int living = 0; bool atExit = true, powerHeld = false, powerContinuous = false;
                foreach (PlayerTicket ticket in session.SnapshotMembers())
                {
                    if (!members.TryGetValue(ticket, out Member member)) return Fail();
                    int health = Encounter.PlayerHealth(ticket);
                    if (health < member.PreviousHealth) Cancel(member);
                    member.PreviousHealth = health;
                    bool freshPose = member.HasPose && hostTime - member.PoseAt < PoseLifetime;
                    if (health > 0) living++;
                    atExit &= freshPose && health > 0 && !member.ExitInterrupted && InsideExit(member.Position);
                    member.ExitInterrupted = false;
                    bool eligible = progression.Phase == CoopMissionPhase.RestorePower && health > 0 && freshPose && member.Held &&
                        hostTime - member.IntentAt < InputLifetime && (member.Position - powerControl).LengthSquared <= PowerRange * PowerRange &&
                        clearPowerLine(ticket, member.Position, powerControl);
                    powerHeld |= eligible;
                    powerContinuous |= eligible && member.QualifiedLastTick && member.HeldSince <= lastTick;
                    member.QualifiedLastTick = eligible;
                    if (member.Held && hostTime - member.IntentAt >= InputLifetime) Cancel(member);
                }
                // A release/repress or expiry hidden between ticks must not preserve an old shared countdown.
                // Another continuously eligible teammate can preserve it; extra teammates never speed it up.
                if (hasTick && !powerContinuous) powerHeld = false;
                CoopMissionPhase previous = progression.Phase;
                bool accepted = progression.AdvanceFromHost(hostTime, new CoopMissionObservation(living, powerHeld, atExit, Encounter.HordeAt(hostTime).Phase));
                lastTick = hostTime; hasTick = true;
                if (progression.Phase == CoopMissionPhase.Failed) { Encounter.AbortFromHost(); return accepted; }
                if (previous == CoopMissionPhase.RestorePower && progression.Phase == CoopMissionPhase.DefendDepot)
                {
                    if (!Encounter.StartWavesFromHost(hostTime)) return Fail();
                    if (!Place(placeWave)) return false;
                    foreach (Member member in members.Values) Cancel(member);
                }
                return accepted;
            }
            catch { Fail(); throw; }
            finally { advancing = false; }
        }
        private bool Place(Func<HostInfectedEncounter, bool> placeWave)
        {
            if (placeWave(Encounter) && Encounter.Phase == EncounterPhase.Active && Encounter.HordeAt(lastClock).Phase == HordePhase.Fighting) return true;
            return Fail();
        }
        private bool Fail() { progression.FailFromHost(); Encounter.AbortFromHost(); return false; }
    }
}
