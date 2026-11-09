namespace UndeadOvertime.Core
{
    public readonly struct LocalDamageFeedbackState
    {
        public readonly double DamagePulse;
        public readonly int LastDamage;
        public readonly bool IsDowned;
        public LocalDamageFeedbackState(double damagePulse, int lastDamage, bool isDowned)
        { DamagePulse = damagePulse; LastDamage = lastDamage; IsDowned = isDowned; }
    }

    /// <summary>
    /// Presentation-only health loss from already accepted, ordered host snapshots. This does not
    /// validate network freshness: RangeSnapshot has no tick with which to detect reordered packets.
    /// A loss may combine multiple hits; it does not identify a damage source, direction or shooter.
    /// Observe and Read share one monotonic local clock. Reset explicitly starts a new clock domain.
    /// </summary>
    public sealed class LocalDamageFeedback
    {
        public const double DamagePulseSeconds = .65d;
        private bool hasTime, hasBaseline;
        private double lastTime, damageAt;
        private long sessionId, roundId, connectionId;
        private ulong peerId;
        private byte health;
        private int lastDamage;

        /// <summary>False clears feedback/baseline. True establishes or updates the playing local
        /// member, without modifying the supplied snapshot or any authoritative game state.</summary>
        public bool Observe(RangeSnapshot snapshot, ulong localPeerId, double now)
        {
            if (!AdvanceClock(now)) return false;
            if (snapshot == null || snapshot.SessionId <= 0 || snapshot.RoundId <= 0 ||
                snapshot.Phase != SessionPhase.Playing || snapshot.MemberCount < 1 || snapshot.MemberCount > LanSession.MaximumPlayers)
            { ClearState(); return false; }

            bool found = false;
            RangeMember local = default;
            for (int i = 0; i < snapshot.MemberCount; i++)
            {
                RangeMember member = snapshot.Member(i);
                if (member.PeerId != localPeerId) continue;
                if (found || member.ConnectionId <= 0 || member.Health > 100 || member.Slot >= LanSession.MaximumPlayers)
                { ClearState(); return false; }
                local = member; found = true;
            }
            if (!found) { ClearState(); return false; }

            bool sameLife = hasBaseline && sessionId == snapshot.SessionId && roundId == snapshot.RoundId &&
                peerId == localPeerId && connectionId == local.ConnectionId;
            if (!sameLife || local.Health == 0 || local.Health > health) ClearDamage();
            else if (local.Health < health)
            { lastDamage = health - local.Health; damageAt = now; }

            hasBaseline = true; sessionId = snapshot.SessionId; roundId = snapshot.RoundId;
            peerId = localPeerId; connectionId = local.ConnectionId; health = local.Health;
            return true;
        }

        /// <summary>Reads a bounded linear fade. Duplicate reads and unchanged health never extend it.
        /// Invalid/backward time clears feedback and baseline, retaining the last valid clock bound.</summary>
        public LocalDamageFeedbackState Read(double now)
        {
            if (!AdvanceClock(now) || !hasBaseline) return default;
            double elapsed = now - damageAt;
            double pulse = lastDamage > 0 && now < damageAt + DamagePulseSeconds && elapsed < DamagePulseSeconds ?
                1d - elapsed / DamagePulseSeconds : 0d;
            return new LocalDamageFeedbackState(pulse, pulse > 0 ? lastDamage : 0, health == 0);
        }

        public void Reset()
        {
            ClearState(); hasTime = false; lastTime = 0;
        }

        private bool AdvanceClock(double now)
        {
            if (!AimVector.Finite(now) || now < 0 || now > HostFireGate.MaximumHostTime || (hasTime && now < lastTime))
            { ClearState(); return false; }
            hasTime = true; lastTime = now; return true;
        }

        private void ClearDamage() { lastDamage = 0; damageAt = 0; }
        private void ClearState()
        {
            ClearDamage(); hasBaseline = false; sessionId = roundId = connectionId = 0; peerId = 0; health = 0;
        }
    }
}
