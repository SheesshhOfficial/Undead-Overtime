namespace UndeadOvertime.Core
{
    /// <summary>Bounded cosmetic health-loss observations from accepted host snapshots. A pulse
    /// is not a local-player hit confirmation: damage can come from any teammate or combine hits.
    /// No attacker, direction, animation or gameplay event is inferred. One monotonic local clock.</summary>
    public sealed class InfectedDamageFeedback
    {
        public const double PulseSeconds = .22, MaximumObservationGap = .5;
        private struct Entry
        {
            public byte Id, Health;
            public double ObservedAt, DamageAt;
            public bool HasDamage;
        }
        private Entry[] entries = new Entry[HostInfectedEncounter.MaximumEnemies];
        private Entry[] next = new Entry[HostInfectedEncounter.MaximumEnemies];
        private int count;
        private long sessionId, roundId;
        private bool hasTime;
        private double lastTime;

        public bool Observe(RangeSnapshot snapshot, double now)
        {
            if (!AdvanceClock(now)) return false;
            if (snapshot == null || snapshot.SessionId <= 0 || snapshot.RoundId <= 0 ||
                snapshot.Phase != SessionPhase.Playing || snapshot.Encounter.Phase != EncounterPhase.Active ||
                snapshot.EnemyCount > HostInfectedEncounter.MaximumEnemies)
            { Clear(); return false; }
            for (int i = 0; i < snapshot.EnemyCount; i++)
            {
                EnemySnapshot enemy = snapshot.Enemy(i);
                if (enemy.Health > 100 || !HostMoveGate.ValidPosition(enemy.Position)) { Clear(); return false; }
                for (int j = 0; j < i; j++) if (snapshot.Enemy(j).Id == enemy.Id) { Clear(); return false; }
            }
            if (sessionId != snapshot.SessionId || roundId != snapshot.RoundId) Clear();
            sessionId = snapshot.SessionId; roundId = snapshot.RoundId;
            int nextCount = 0;
            for (int i = 0; i < snapshot.EnemyCount; i++)
            {
                EnemySnapshot enemy = snapshot.Enemy(i);
                if (enemy.Health == 0) continue; // Never retain a dead enemy merely to show feedback.
                var value = new Entry { Id = enemy.Id, Health = enemy.Health, ObservedAt = now };
                for (int j = 0; j < count; j++)
                {
                    Entry previous = entries[j];
                    if (previous.Id != enemy.Id) continue;
                    if (enemy.Health <= previous.Health && now - previous.ObservedAt < MaximumObservationGap)
                    {
                        value.HasDamage = previous.HasDamage; value.DamageAt = previous.DamageAt;
                        if (enemy.Health < previous.Health) { value.HasDamage = true; value.DamageAt = now; }
                    }
                    break;
                }
                next[nextCount++] = value;
            }
            Entry[] swap = entries; entries = next; next = swap; count = nextCount;
            return true;
        }

        public bool TryRead(byte enemyId, double now, out double pulse)
        {
            pulse = 0;
            if (!AdvanceClock(now)) return false;
            for (int i = 0; i < count; i++)
            {
                Entry value = entries[i];
                if (value.Id != enemyId) continue;
                double elapsed = now - value.DamageAt;
                if (value.HasDamage && now < value.DamageAt + PulseSeconds && elapsed < PulseSeconds)
                    pulse = 1 - elapsed / PulseSeconds;
                return true;
            }
            return false;
        }
        private bool AdvanceClock(double now)
        {
            if (!AimVector.Finite(now) || now < 0 || now > HostFireGate.MaximumHostTime || (hasTime && now < lastTime))
            { Clear(); return false; }
            hasTime = true; lastTime = now; return true;
        }
        private void Clear() { count = 0; sessionId = roundId = 0; }
        public void Reset() { Clear(); hasTime = false; lastTime = 0; }
    }
}
