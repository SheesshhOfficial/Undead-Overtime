using System;
using System.Collections.Generic;

namespace UndeadOvertime.Core
{
    public enum EncounterPhase : byte { Dormant, Active, Cleared, Failed }
    public enum HordePhase : byte { Disabled, Fighting, Intermission, Completed, Failed }
    public readonly struct InfectedSnapshot
    {
        public readonly EncounterPhase Phase;
        public readonly byte Health;
        public readonly AimVector Position;
        public InfectedSnapshot(EncounterPhase phase, byte health, AimVector position)
        { Phase = phase; Health = health; Position = position; }
        public static InfectedSnapshot Dormant => new InfectedSnapshot(EncounterPhase.Dormant, 0, HostInfectedEncounter.Spawn);
    }
    public readonly struct EnemySnapshot
    {
        public readonly byte Id, Health;
        public readonly AimVector Position;
        public EnemySnapshot(byte id, byte health, AimVector position) { Id = id; Health = health; Position = position; }
    }
    public readonly struct HordeSnapshot
    {
        public readonly HordePhase Phase;
        public readonly byte Wave, Total;
        public readonly double IntermissionSeconds;
        public HordeSnapshot(HordePhase phase, byte wave, byte total, double seconds = 0)
        { Phase = phase; Wave = wave; Total = total; IntermissionSeconds = seconds; }
        public static HordeSnapshot Disabled => new HordeSnapshot(HordePhase.Disabled, 0, 0);
    }
    /// <summary>Host-owned round life state and bounded infected pool. One-enemy mode remains available.
    /// Host adapters supply physics/LOS and time; clients never supply damage, enemy IDs or spawn decisions.</summary>
    public sealed class HostInfectedEncounter
    {
        public const double Speed = 1.6, AttackRange = 1.3, AttackInterval = 1;
        public const int MeleeDamage = 10, RevivedHealth = 40, MaximumEnemies = 4, TotalWaves = 3;
        public const double ReviveRange = 2, ReviveDuration = 3, ReviveInputLifetime = .25, IntermissionDuration = 5;
        public static AimVector Spawn => new AimVector(0, HostMoveGate.EyeHeight, 6);
        public static AimVector WaveSpawn(int slot)
        {
            switch (slot)
            {
                case 0: return new AimVector(-6, HostMoveGate.EyeHeight, 6);
                case 1: return new AimVector(6, HostMoveGate.EyeHeight, 6);
                case 2: return new AimVector(0, HostMoveGate.EyeHeight, 3);
                case 3: return new AimVector(0, HostMoveGate.EyeHeight, 8);
                default: throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }
        private readonly LanSession session;
        private readonly long sessionId, roundId;
        private readonly Dictionary<PlayerTicket, Survivor> survivors = new Dictionary<PlayerTicket, Survivor>();
        private double lastClock, intermissionEndsAt;
        private HordePhase hordePhase;
        private byte wave;
        private readonly EnemyState[] enemies = new EnemyState[MaximumEnemies];
        private readonly Dictionary<PlayerTicket, ReviveState> revives = new Dictionary<PlayerTicket, ReviveState>();
        private sealed class ReviveState
        {
            public bool Held, Channeling;
            public double LastIntentAt, StartedAt;
            public PlayerTicket Target;
        }
        private sealed class EnemyState
        {
            public int Health = 100;
            public byte Id;
            public AimVector Position;
            public double LastStep, NextAttackAt;
        }
        public int EnemyCount { get; private set; }
        public EncounterPhase Phase { get; private set; }
        public InfectedSnapshot Snapshot => EnemyCount == 0 ? new InfectedSnapshot(Phase, 0, Spawn) :
            new InfectedSnapshot(Phase, (byte)enemies[0].Health, enemies[0].Position);
        public EnemySnapshot Enemy(int index)
        {
            if (index < 0 || index >= EnemyCount) throw new ArgumentOutOfRangeException(nameof(index));
            return new EnemySnapshot(enemies[index].Id, (byte)enemies[index].Health, enemies[index].Position);
        }
        public EnemySnapshot[] SnapshotEnemies()
        {
            var result = new EnemySnapshot[EnemyCount];
            for (int i = 0; i < EnemyCount; i++) result[i] = Enemy(i);
            return result;
        }
        public HordeSnapshot HordeAt(double hostTime) => hordePhase == HordePhase.Disabled ? HordeSnapshot.Disabled :
            new HordeSnapshot(hordePhase, wave, TotalWaves, hordePhase == HordePhase.Intermission && ValidTime(hostTime) ?
                Math.Min(IntermissionDuration, Math.Max(0, intermissionEndsAt - hostTime)) : 0);
        private sealed class Survivor { public int Health = 100; public AimVector Position; public bool HasPose; }
        private bool Current => session.Phase == SessionPhase.Playing && session.SessionId == sessionId && session.RoundId == roundId;
        public HostInfectedEncounter(LanSession session, double hostTime)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            if (session.Phase != SessionPhase.Playing) throw new InvalidOperationException("Begin the round first.");
            if (!ValidTime(hostTime)) throw new ArgumentOutOfRangeException(nameof(hostTime));
            sessionId = session.SessionId; roundId = session.RoundId; lastClock = hostTime;
            foreach (var ticket in session.SnapshotMembers()) survivors.Add(ticket, new Survivor());
        }
        private static bool ValidTime(double time) => AimVector.Finite(time) && time >= 0 && time <= HostFireGate.MaximumHostTime;
        private bool Clock(double time) { if (!ValidTime(time) || time < lastClock) return false; lastClock = time; return true; }
        private bool State(PlayerTicket ticket, out Survivor survivor)
        { survivor = null; return Current && session.IsCurrent(ticket) && survivors.TryGetValue(ticket, out survivor); }
        private int LiveEnemyIndex(int id)
        {
            for (int i = 0; i < EnemyCount; i++) if (enemies[i].Id == id && enemies[i].Health > 0) return i;
            return -1;
        }
        public bool AbortFromHost()
        {
            if (!Current || Phase != EncounterPhase.Active) return false;
            Fail(); return true;
        }
        public int PlayerHealth(PlayerTicket ticket) => State(ticket, out Survivor survivor) ? survivor.Health : 0;
        public bool SetPlayerPositionFromHost(PlayerTicket ticket, AimVector value)
        {
            if (!HostMoveGate.ValidPosition(value) || !State(ticket, out Survivor survivor)) return false;
            survivor.Position = value; survivor.HasPose = true; return true;
        }
        public bool StartFromHost(double hostTime)
        {
            if (!Current || Phase != EncounterPhase.Dormant || !Clock(hostTime) || !Closest(Spawn, out _)) return false;
            Phase = EncounterPhase.Active; EnemyCount = 1;
            enemies[0] = new EnemyState { Position = Spawn, LastStep = hostTime, NextAttackAt = hostTime }; return true;
        }
        public bool StartWavesFromHost(double hostTime)
        {
            if (!Current || Phase != EncounterPhase.Dormant || !Clock(hostTime) || !Closest(Spawn, out _)) return false;
            Phase = EncounterPhase.Active; SpawnWave(hostTime); return true;
        }
        private void SpawnWave(double hostTime)
        {
            wave++; hordePhase = HordePhase.Fighting; EnemyCount = wave + 1;
            for (int i = 0; i < EnemyCount; i++)
                enemies[i] = new EnemyState { Id = (byte)(wave * MaximumEnemies + i), Position = WaveSpawn(i), LastStep = hostTime, NextAttackAt = hostTime };
        }
        private void Fail()
        {
            Phase = EncounterPhase.Failed;
            if (hordePhase != HordePhase.Disabled) hordePhase = HordePhase.Failed;
            foreach (ReviveState revive in revives.Values) revive.Held = revive.Channeling = false;
        }
        public bool AdvanceWavesFromHost(double hostTime)
        {
            if (!Current || Phase != EncounterPhase.Active || !Clock(hostTime)) return false;
            if (!Closest(Spawn, out _)) { Fail(); return false; }
            if (hordePhase != HordePhase.Intermission || hostTime < intermissionEndsAt) return false;
            SpawnWave(hostTime); return true;
        }
        public bool ProposeStepFromHost(double hostTime, out AimVector displacement, out PlayerTicket target) =>
            ProposeStepFromHost(0, hostTime, out displacement, out target);
        public bool ProposeStepFromHost(int id, double hostTime, out AimVector displacement, out PlayerTicket target)
        {
            displacement = default; target = default; int index = LiveEnemyIndex(id);
            if (!Current || Phase != EncounterPhase.Active || index < 0 || !Clock(hostTime)) return false;
            EnemyState enemy = enemies[index];
            double duration = Math.Min(HostMoveGate.MaximumStep, hostTime - enemy.LastStep); enemy.LastStep = hostTime;
            if (!Closest(enemy.Position, out target)) { Fail(); return false; }
            AimVector toward = survivors[target].Position - enemy.Position;
            double distance = Math.Sqrt(toward.X * toward.X + toward.Z * toward.Z);
            if (distance > 1.1)
            {
                double travel = Math.Min(Speed * duration, distance - 1.1);
                displacement = new AimVector(toward.X / distance * travel, 0, toward.Z / distance * travel);
            }
            return true;
        }
        private bool Closest(AimVector origin, out PlayerTicket target)
        {
            target = default; double best = double.MaxValue; bool found = false;
            foreach (PlayerTicket ticket in session.SnapshotMembers())
                if (survivors.TryGetValue(ticket, out Survivor survivor) && survivor.Health > 0 && survivor.HasPose)
                {
                    double distance = (survivor.Position - origin).LengthSquared;
                    if (distance < best) { best = distance; target = ticket; found = true; }
                }
            return found;
        }
        public bool SetEnemyPositionFromHost(AimVector value) => SetEnemyPositionFromHost(0, value);
        public bool SetEnemyPositionFromHost(int id, AimVector value)
        {
            int index = LiveEnemyIndex(id);
            if (!Current || Phase != EncounterPhase.Active || index < 0 || !HostMoveGate.ValidPosition(value)) return false;
            enemies[index].Position = value; return true;
        }
        public bool DamageEnemyFromHost(PlayerTicket shooter, int damage) => DamageEnemyFromHost(shooter, 0, damage, lastClock);
        public bool DamageEnemyFromHost(PlayerTicket shooter, int id, int damage, double hostTime)
        {
            int index = LiveEnemyIndex(id);
            if (Phase != EncounterPhase.Active || index < 0 || !State(shooter, out Survivor survivor) || survivor.Health <= 0 ||
                damage <= 0 || damage > 100 || !Clock(hostTime)) return false;
            enemies[index].Health = Math.Max(0, enemies[index].Health - damage);
            bool alive = false; for (int i = 0; i < EnemyCount; i++) alive |= enemies[i].Health > 0;
            if (!alive)
            {
                if (hordePhase == HordePhase.Disabled) Phase = EncounterPhase.Cleared;
                else if (wave == TotalWaves) { Phase = EncounterPhase.Cleared; hordePhase = HordePhase.Completed; }
                else { hordePhase = HordePhase.Intermission; intermissionEndsAt = hostTime + IntermissionDuration; }
            }
            return true;
        }
        public bool MeleeFromHost(PlayerTicket victim, double hostTime, bool clearLineOfSight) => MeleeFromHost(0, victim, hostTime, clearLineOfSight);
        public bool MeleeFromHost(int id, PlayerTicket victim, double hostTime, bool clearLineOfSight)
        {
            int index = LiveEnemyIndex(id);
            if (Phase != EncounterPhase.Active || index < 0 || !State(victim, out Survivor survivor) || survivor.Health <= 0 || !survivor.HasPose ||
                !clearLineOfSight || !Clock(hostTime) || hostTime < enemies[index].NextAttackAt ||
                (survivor.Position - enemies[index].Position).LengthSquared > AttackRange * AttackRange) return false;
            survivor.Health = Math.Max(0, survivor.Health - MeleeDamage); enemies[index].NextAttackAt = hostTime + AttackInterval;
            CancelReviveFromHost(victim);
            if (!Closest(Spawn, out _)) Fail();
            return true;
        }
        public static bool ValidReviveIntent(AimVector value) => value.IsFinite && value.X == 0 && value.Y == 0 && (value.Z == 0 || value.Z == 1);
        private bool CanRevive => Current && (Phase == EncounterPhase.Active || Phase == EncounterPhase.Cleared);
        public bool HoldReviveFromHost(PlayerTicket reviver, bool held, double hostTime)
        {
            if (!CanRevive || !State(reviver, out Survivor survivor) || survivor.Health <= 0 || !Clock(hostTime)) return false;
            if (!revives.TryGetValue(reviver, out ReviveState revive)) { revive = new ReviveState(); revives.Add(reviver, revive); }
            if (!held) { revive.Held = revive.Channeling = false; return true; }
            if (hostTime - revive.LastIntentAt >= ReviveInputLifetime) revive.Channeling = false;
            revive.Held = true; revive.LastIntentAt = hostTime; return true;
        }
        public void CancelReviveFromHost(PlayerTicket reviver)
        {
            if (revives.TryGetValue(reviver, out ReviveState revive)) revive.Held = revive.Channeling = false;
        }
        public bool ProposeReviveFromHost(PlayerTicket reviver, double hostTime, out PlayerTicket target)
        {
            target = default;
            if (!Clock(hostTime) || !CanRevive || !State(reviver, out Survivor survivor) || survivor.Health <= 0 || !survivor.HasPose ||
                !revives.TryGetValue(reviver, out ReviveState revive)) return false;
            if (!revive.Held || hostTime - revive.LastIntentAt >= ReviveInputLifetime)
            { revive.Held = revive.Channeling = false; return false; }
            double best = ReviveRange * ReviveRange; bool found = false;
            foreach (PlayerTicket ticket in session.SnapshotMembers())
                if (!ticket.Equals(reviver) && survivors.TryGetValue(ticket, out Survivor other) && other.Health == 0 && other.HasPose)
                {
                    double distance = (survivor.Position - other.Position).LengthSquared;
                    if (distance <= best && (!found || distance < best)) { best = distance; target = ticket; found = true; }
                }
            if (!found) revive.Channeling = false;
            return found;
        }
        public bool AdvanceReviveFromHost(PlayerTicket reviver, PlayerTicket target, double hostTime, bool clearLineOfSight)
        {
            if (!ProposeReviveFromHost(reviver, hostTime, out PlayerTicket expected) || !expected.Equals(target)) return false;
            ReviveState revive = revives[reviver];
            if (!clearLineOfSight) { revive.Channeling = false; return false; }
            if (!revive.Channeling || !revive.Target.Equals(target))
            { revive.Channeling = true; revive.Target = target; revive.StartedAt = hostTime; }
            if (hostTime - revive.StartedAt < ReviveDuration) return false;
            survivors[target].Health = RevivedHealth;
            revive.Held = revive.Channeling = false;
            return true;
        }
        public bool ReviveStatusFromHost(PlayerTicket reviver, double hostTime, out PlayerTicket target, out byte progress)
        {
            target = default; progress = 0;
            if (!CanRevive || !ValidTime(hostTime) || !State(reviver, out Survivor survivor) || survivor.Health <= 0 ||
                !revives.TryGetValue(reviver, out ReviveState revive) || !revive.Held || !revive.Channeling ||
                hostTime - revive.LastIntentAt >= ReviveInputLifetime || !State(revive.Target, out Survivor victim) || victim.Health != 0) return false;
            target = revive.Target;
            progress = (byte)Math.Min(99, Math.Max(0, (int)((hostTime - revive.StartedAt) / ReviveDuration * 100)));
            return true;
        }
    }
}
