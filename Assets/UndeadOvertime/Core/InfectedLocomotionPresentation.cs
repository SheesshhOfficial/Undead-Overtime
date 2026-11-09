using System;

namespace UndeadOvertime.Core
{
    public readonly struct InfectedLocomotionState
    {
        public readonly bool IsVisible, IsMoving;
        public readonly double Speed;
        public readonly AimVector Facing;
        public InfectedLocomotionState(bool visible, bool moving, double speed, AimVector facing)
        { IsVisible = visible; IsMoving = moving; Speed = speed; Facing = facing; }
    }

    /// <summary>
    /// Cosmetic idle/walk observations only. Feed accepted, ordered host snapshots once on receipt,
    /// never once per render frame. Receipt timing estimates speed; it is not replicated velocity,
    /// aim, an attack event or an animation phase. This class cannot mutate game authority.
    /// Observe/Read share a monotonic local clock. Reset starts a new clock domain.
    /// </summary>
    public sealed class InfectedLocomotionPresentation
    {
        public const double MaximumObservationGap = .5, DisplacementTolerance = .08;
        public const double MoveEnterSpeed = .15, MoveExitSpeed = .1;
        private struct Entry
        {
            public byte Id, Health;
            public bool HasBaseline, Moving;
            public AimVector Position, Facing;
            public double At, Speed;
        }
        private Entry[] entries = new Entry[HostInfectedEncounter.MaximumEnemies];
        private Entry[] next = new Entry[HostInfectedEncounter.MaximumEnemies];
        private int count;
        private long sessionId, roundId;
        private bool hasTime;
        private double lastTime;
        private static AimVector InitialFacing => new AimVector(0, 0, 1);

        /// <summary>Invalid data or inactive encounter clears all cosmetic identities. No packet
        /// freshness validation is possible here: range v5 has no snapshot sequence/timestamp.</summary>
        public bool Observe(RangeSnapshot snapshot, double now)
        {
            if (!AdvanceClock(now)) return false;
            if (snapshot == null || snapshot.SessionId <= 0 || snapshot.RoundId <= 0 ||
                snapshot.Phase != SessionPhase.Playing || snapshot.Encounter.Phase != EncounterPhase.Active ||
                snapshot.EnemyCount > HostInfectedEncounter.MaximumEnemies)
            { Clear(); return false; }
            // Validate the entire bounded observation before retaining any part of it.
            for (int i = 0; i < snapshot.EnemyCount; i++)
            {
                EnemySnapshot enemy = snapshot.Enemy(i);
                if (enemy.Health > 100 || !HostMoveGate.ValidPosition(enemy.Position)) { Clear(); return false; }
                for (int j = 0; j < i; j++)
                    if (snapshot.Enemy(j).Id == enemy.Id) { Clear(); return false; }
            }
            if (sessionId != snapshot.SessionId || roundId != snapshot.RoundId) Clear();
            sessionId = snapshot.SessionId; roundId = snapshot.RoundId;
            int nextCount = 0;
            for (int i = 0; i < snapshot.EnemyCount; i++)
            {
                EnemySnapshot enemy = snapshot.Enemy(i);
                if (enemy.Health == 0) continue; // No corpse, lingering collider or invented death time.
                Entry previous = default;
                bool found = false;
                for (int j = 0; j < count; j++)
                    if (entries[j].Id == enemy.Id) { previous = entries[j]; found = true; break; }
                bool sameLife = found && enemy.Health <= previous.Health;
                var value = new Entry { Id = enemy.Id, Health = enemy.Health, Position = enemy.Position,
                    At = now, HasBaseline = true, Facing = sameLife ? previous.Facing : InitialFacing };
                double elapsed = now - previous.At;
                if (sameLife && previous.HasBaseline && elapsed > 0 && elapsed < MaximumObservationGap)
                {
                    double x = enemy.Position.X - previous.Position.X, z = enemy.Position.Z - previous.Position.Z;
                    double distance = Math.Sqrt(x * x + z * z);
                    if (distance <= HostInfectedEncounter.Speed * elapsed + DisplacementTolerance)
                    {
                        value.Speed = Math.Min(HostInfectedEncounter.Speed, distance / elapsed);
                        value.Moving = value.Speed > (previous.Moving ? MoveExitSpeed : MoveEnterSpeed);
                        if (value.Moving) value.Facing = new AimVector(x / distance, 0, z / distance);
                    }
                    // Teleports, same-time updates and gaps establish an idle baseline instead.
                }
                next[nextCount++] = value;
            }
            Entry[] swap = entries; entries = next; next = swap;
            count = nextCount;
            return true;
        }

        /// <summary>Stale data keeps the last standing pose visible but stops locomotion. Reads do
        /// not refresh receipt time or turn a frozen snapshot into ongoing movement.</summary>
        public bool TryRead(byte enemyId, double now, out InfectedLocomotionState state)
        {
            state = default;
            if (!AdvanceClock(now)) return false;
            for (int i = 0; i < count; i++)
            {
                if (entries[i].Id != enemyId) continue;
                if (now >= entries[i].At + MaximumObservationGap)
                {
                    entries[i].HasBaseline = false;
                    entries[i].Moving = false; entries[i].Speed = 0;
                }
                Entry value = entries[i];
                state = new InfectedLocomotionState(true, value.Moving, value.Speed, value.Facing);
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
