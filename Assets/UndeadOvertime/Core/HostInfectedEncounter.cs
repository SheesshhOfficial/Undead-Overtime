using System;
using System.Collections.Generic;

namespace UndeadOvertime.Core
{
    public enum EncounterPhase : byte { Dormant, Active, Cleared, Failed }
    public readonly struct InfectedSnapshot
    {
        public readonly EncounterPhase Phase;
        public readonly byte Health;
        public readonly AimVector Position;
        public InfectedSnapshot(EncounterPhase phase, byte health, AimVector position)
        { Phase = phase; Health = health; Position = position; }
        public static InfectedSnapshot Dormant => new InfectedSnapshot(EncounterPhase.Dormant, 0, HostInfectedEncounter.Spawn);
    }
    /// <summary>One round, one host-owned infected. Physics/line of sight remain trusted adapter work.</summary>
    public sealed class HostInfectedEncounter
    {
        public const double Speed = 1.6, AttackRange = 1.3, AttackInterval = 1;
        public const int MeleeDamage = 10;
        public static AimVector Spawn => new AimVector(0, HostMoveGate.EyeHeight, 6);
        private readonly LanSession session;
        private readonly long sessionId, roundId;
        private readonly Dictionary<PlayerTicket, Survivor> survivors = new Dictionary<PlayerTicket, Survivor>();
        private double lastClock, lastStep, nextAttackAt;
        private int health;
        private AimVector position = Spawn;
        public EncounterPhase Phase { get; private set; }
        public InfectedSnapshot Snapshot => new InfectedSnapshot(Phase, (byte)health, position);
        private sealed class Survivor { public int Health = 100; public AimVector Position; public bool HasPose; }
        private bool Current => session.Phase == SessionPhase.Playing && session.SessionId == sessionId && session.RoundId == roundId;
        public HostInfectedEncounter(LanSession session, double hostTime)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            if (session.Phase != SessionPhase.Playing) throw new InvalidOperationException("Begin the round first.");
            if (!ValidTime(hostTime)) throw new ArgumentOutOfRangeException(nameof(hostTime));
            sessionId = session.SessionId; roundId = session.RoundId; lastClock = lastStep = hostTime;
            foreach (var ticket in session.SnapshotMembers()) survivors.Add(ticket, new Survivor());
        }
        private static bool ValidTime(double time) => AimVector.Finite(time) && time >= 0 && time <= HostFireGate.MaximumHostTime;
        private bool Clock(double time) { if (!ValidTime(time) || time < lastClock) return false; lastClock = time; return true; }
        private bool State(PlayerTicket ticket, out Survivor survivor)
        { survivor = null; return Current && session.IsCurrent(ticket) && survivors.TryGetValue(ticket, out survivor); }
        public int PlayerHealth(PlayerTicket ticket) => State(ticket, out Survivor survivor) ? survivor.Health : 0;
        public bool SetPlayerPositionFromHost(PlayerTicket ticket, AimVector value)
        {
            if (!HostMoveGate.ValidPosition(value) || !State(ticket, out Survivor survivor)) return false;
            survivor.Position = value; survivor.HasPose = true; return true;
        }
        public bool StartFromHost(double hostTime)
        {
            if (!Current || Phase != EncounterPhase.Dormant || !Clock(hostTime) || !Closest(out _)) return false;
            Phase = EncounterPhase.Active; health = 100; position = Spawn; lastStep = hostTime; nextAttackAt = hostTime; return true;
        }
        public bool ProposeStepFromHost(double hostTime, out AimVector displacement, out PlayerTicket target)
        {
            displacement = default; target = default;
            if (!Current || Phase != EncounterPhase.Active || !Clock(hostTime)) return false;
            double duration = Math.Min(HostMoveGate.MaximumStep, hostTime - lastStep); lastStep = hostTime;
            if (!Closest(out target)) { Phase = EncounterPhase.Failed; return false; }
            AimVector toward = survivors[target].Position - position;
            double distance = Math.Sqrt(toward.X * toward.X + toward.Z * toward.Z);
            if (distance > 1.1)
            {
                double travel = Math.Min(Speed * duration, distance - 1.1);
                displacement = new AimVector(toward.X / distance * travel, 0, toward.Z / distance * travel);
            }
            return true;
        }
        private bool Closest(out PlayerTicket target)
        {
            target = default; double best = double.MaxValue; bool found = false;
            foreach (PlayerTicket ticket in session.SnapshotMembers())
                if (survivors.TryGetValue(ticket, out Survivor survivor) && survivor.Health > 0 && survivor.HasPose)
                {
                    double distance = (survivor.Position - position).LengthSquared;
                    if (distance < best) { best = distance; target = ticket; found = true; }
                }
            return found;
        }
        public bool SetEnemyPositionFromHost(AimVector value)
        {
            if (!Current || Phase != EncounterPhase.Active || !HostMoveGate.ValidPosition(value)) return false;
            position = value; return true;
        }
        public bool DamageEnemyFromHost(PlayerTicket shooter, int damage)
        {
            if (Phase != EncounterPhase.Active || !State(shooter, out Survivor survivor) || survivor.Health <= 0 || damage <= 0 || damage > 100) return false;
            health = Math.Max(0, health - damage);
            if (health == 0) Phase = EncounterPhase.Cleared;
            return true;
        }
        public bool MeleeFromHost(PlayerTicket victim, double hostTime, bool clearLineOfSight)
        {
            if (Phase != EncounterPhase.Active || !State(victim, out Survivor survivor) || survivor.Health <= 0 || !survivor.HasPose ||
                !clearLineOfSight || !Clock(hostTime) || hostTime < nextAttackAt ||
                (survivor.Position - position).LengthSquared > AttackRange * AttackRange) return false;
            survivor.Health = Math.Max(0, survivor.Health - MeleeDamage); nextAttackAt = hostTime + AttackInterval;
            if (!Closest(out _)) Phase = EncounterPhase.Failed;
            return true;
        }
    }
}
