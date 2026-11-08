using System;
using System.IO;
using System.Collections.Generic;

namespace UndeadOvertime.Core
{
    public enum RangeAction : byte { Fire = 1, Reload = 2, Move = 3, Revive = 4 }
    public readonly struct RangeCommand
    {
        public readonly RangeAction Action;
        public readonly long SessionId, RoundId;
        public readonly ulong Sequence;
        public readonly AimVector Aim;
        public RangeCommand(RangeAction action, long sessionId, long roundId, ulong sequence, AimVector aim)
        { Action = action; SessionId = sessionId; RoundId = roundId; Sequence = sequence; Aim = aim; }
    }
    public readonly struct RangeMember
    {
        public readonly ulong PeerId;
        public readonly long ConnectionId;
        public readonly byte Slot, Ammo, Health, ReviveTargetSlot, ReviveProgress;
        public readonly bool Reloading;
        public readonly AimVector Position;
        public RangeMember(ulong peerId, long connectionId, byte slot, byte ammo, bool reloading)
            : this(peerId, connectionId, slot, ammo, reloading, HostMoveGate.Spawn(slot), 100) { }
        public RangeMember(ulong peerId, long connectionId, byte slot, byte ammo, bool reloading, AimVector position, byte health = 100, byte reviveTargetSlot = 255, byte reviveProgress = 0)
        { PeerId = peerId; ConnectionId = connectionId; Slot = slot; Ammo = ammo; Reloading = reloading; Position = position; Health = health; ReviveTargetSlot = reviveTargetSlot; ReviveProgress = reviveProgress; }
    }
    public sealed class RangeSnapshot
    {
        public readonly long SessionId, RoundId;
        public readonly SessionPhase Phase;
        public readonly InfectedSnapshot Encounter;
        public readonly HordeSnapshot Horde;
        private readonly RangeMember[] members;
        private readonly EnemySnapshot[] enemies;
        private readonly int[] health;
        public int MemberCount => members.Length;
        public int EnemyCount => enemies.Length;
        public RangeMember Member(int index) => members[index];
        public EnemySnapshot Enemy(int index) => enemies[index];
        public int TargetHealth(int index) => health[index];
        public RangeSnapshot(long sessionId, long roundId, SessionPhase phase, RangeMember[] members, int[] health)
            : this(sessionId, roundId, phase, members, health, InfectedSnapshot.Dormant) { }
        public RangeSnapshot(long sessionId, long roundId, SessionPhase phase, RangeMember[] members, int[] health, InfectedSnapshot encounter)
            : this(sessionId, roundId, phase, members, health, encounter.Phase,
                encounter.Phase == EncounterPhase.Dormant ? Array.Empty<EnemySnapshot>() : new[] { new EnemySnapshot(0, encounter.Health, encounter.Position) }, HordeSnapshot.Disabled) { Encounter = encounter; }
        public RangeSnapshot(long sessionId, long roundId, SessionPhase phase, RangeMember[] members, int[] health,
            EncounterPhase encounterPhase, EnemySnapshot[] enemies, HordeSnapshot horde)
        {
            if (members == null || health == null || health.Length != 3 || enemies == null) throw new ArgumentException("Target health, members and enemies are required.");
            SessionId = sessionId; RoundId = roundId; Phase = phase; Horde = horde;
            this.members = (RangeMember[])members.Clone(); this.health = (int[])health.Clone(); this.enemies = (EnemySnapshot[])enemies.Clone();
            Encounter = enemies.Length == 0 ? new InfectedSnapshot(encounterPhase, 0, HostInfectedEncounter.Spawn) :
                new InfectedSnapshot(encounterPhase, enemies[0].Health, enemies[0].Position);
        }
    }

    /// <summary>Fixed little-endian protocol, no strings/JSON/unbounded length prefixes. Not authentication.</summary>
    public static class LanRangeProtocol
    {
        public const ushort Version = 5;
        public const int CommandBytes = 44;
        public const int SnapshotHeaderBytes = 46;
        public const int MemberBytes = 34, EnemyBytes = 14;
        public const int MaximumBytes = SnapshotHeaderBytes + HostInfectedEncounter.MaximumEnemies * EnemyBytes + LanSession.MaximumPlayers * MemberBytes;
        private const uint Magic = 0x554F4C31;
        public static byte[] Handshake => new byte[] { 0x55, 0x4f, 0x52, (byte)Version };
        public static bool AcceptHandshake(byte[] data) => data != null && data.Length == 4 &&
            data[0] == 0x55 && data[1] == 0x4f && data[2] == 0x52 && data[3] == Version;

        public static byte[] Encode(RangeCommand command)
        {
            // Reject before allocating/writing if rounding would create a packet our decoder cannot accept.
            // Do not widen receive tolerances or silently normalize the caller's authoritative values.
            if (!Valid(command) || !Valid(new RangeCommand(command.Action, command.SessionId, command.RoundId,
                command.Sequence, WireVector(command.Aim)))) throw new ArgumentException("Invalid range command or float wire representation.");
            using (var stream = new MemoryStream(CommandBytes))
            using (var writer = new BinaryWriter(stream))
            {
                Header(writer, 1); writer.Write((byte)command.Action); writer.Write(command.SessionId);
                writer.Write(command.RoundId); writer.Write(command.Sequence);
                writer.Write((float)command.Aim.X); writer.Write((float)command.Aim.Y); writer.Write((float)command.Aim.Z);
                return stream.ToArray();
            }
        }
        public static bool TryCommand(byte[] bytes, out RangeCommand command)
        {
            command = default;
            if (bytes == null || bytes.Length != CommandBytes) return false;
            using (var reader = new BinaryReader(new MemoryStream(bytes, false)))
            {
                if (!Header(reader, 1)) return false;
                var parsed = new RangeCommand((RangeAction)reader.ReadByte(), reader.ReadInt64(), reader.ReadInt64(),
                    reader.ReadUInt64(), new AimVector(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                if (!Valid(parsed)) return false;
                command = parsed; return true;
            }
        }
        private static AimVector WireVector(AimVector value) => new AimVector((float)value.X, (float)value.Y, (float)value.Z);
        private static bool ValidWirePosition(AimVector value) => HostMoveGate.ValidPosition(value) && HostMoveGate.ValidPosition(WireVector(value));
        private static bool Valid(RangeCommand value) =>
            value.SessionId > 0 && value.RoundId > 0 && value.Sequence > 0 &&
            (value.Action == RangeAction.Move ? HostMoveGate.ValidIntent(value.Aim) :
                value.Action == RangeAction.Revive ? HostInfectedEncounter.ValidReviveIntent(value.Aim) :
                (value.Action == RangeAction.Fire || value.Action == RangeAction.Reload) && value.Aim.IsUnit);

        public static byte[] Encode(RangeSnapshot snapshot)
        {
            if (!Valid(snapshot)) throw new ArgumentException("Invalid range snapshot.");
            using (var stream = new MemoryStream(MaximumBytes))
            using (var writer = new BinaryWriter(stream))
            {
                Header(writer, 2); writer.Write(snapshot.SessionId); writer.Write(snapshot.RoundId);
                writer.Write((byte)snapshot.Phase); writer.Write((byte)snapshot.MemberCount);
                for (int i = 0; i < 3; i++) writer.Write(snapshot.TargetHealth(i));
                writer.Write((byte)snapshot.Encounter.Phase);
                writer.Write((byte)snapshot.Horde.Phase); writer.Write(snapshot.Horde.Wave); writer.Write(snapshot.Horde.Total);
                writer.Write((float)snapshot.Horde.IntermissionSeconds); writer.Write((byte)snapshot.EnemyCount);
                for (int i = 0; i < snapshot.EnemyCount; i++)
                {
                    EnemySnapshot enemy = snapshot.Enemy(i); writer.Write(enemy.Id); writer.Write(enemy.Health);
                    writer.Write((float)enemy.Position.X); writer.Write((float)enemy.Position.Y); writer.Write((float)enemy.Position.Z);
                }
                for (int i = 0; i < snapshot.MemberCount; i++)
                {
                    RangeMember m = snapshot.Member(i);
                    writer.Write(m.PeerId); writer.Write(m.ConnectionId); writer.Write(m.Slot); writer.Write(m.Ammo);
                    writer.Write((byte)(m.Reloading ? 1 : 0));
                    writer.Write((float)m.Position.X); writer.Write((float)m.Position.Y); writer.Write((float)m.Position.Z); writer.Write(m.Health); writer.Write(m.ReviveTargetSlot); writer.Write(m.ReviveProgress);
                }
                return stream.ToArray();
            }
        }
        public static bool TrySnapshot(byte[] bytes, out RangeSnapshot snapshot)
        {
            snapshot = null;
            if (bytes == null || bytes.Length < SnapshotHeaderBytes || bytes.Length > MaximumBytes) return false;
            using (var reader = new BinaryReader(new MemoryStream(bytes, false)))
            {
                if (!Header(reader, 2)) return false;
                long session = reader.ReadInt64(), round = reader.ReadInt64();
                var phase = (SessionPhase)reader.ReadByte(); int count = reader.ReadByte();
                if (count < 1 || count > LanSession.MaximumPlayers) return false;
                var health = new int[3]; for (int i = 0; i < 3; i++) health[i] = reader.ReadInt32();
                var encounterPhase = (EncounterPhase)reader.ReadByte();
                var horde = new HordeSnapshot((HordePhase)reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadSingle());
                int enemyCount = reader.ReadByte();
                if (enemyCount > HostInfectedEncounter.MaximumEnemies || bytes.Length != SnapshotHeaderBytes + enemyCount * EnemyBytes + count * MemberBytes) return false;
                var enemies = new EnemySnapshot[enemyCount];
                for (int i = 0; i < enemyCount; i++) enemies[i] = new EnemySnapshot(reader.ReadByte(), reader.ReadByte(),
                    new AimVector(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                var members = new RangeMember[count];
                for (int i = 0; i < count; i++)
                {
                    ulong peer = reader.ReadUInt64(); long connection = reader.ReadInt64();
                    byte slot = reader.ReadByte(), ammo = reader.ReadByte(), reload = reader.ReadByte();
                    if (reload > 1) return false;
                    members[i] = new RangeMember(peer, connection, slot, ammo, reload == 1,
                        new AimVector(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                }
                var parsed = new RangeSnapshot(session, round, phase, members, health, encounterPhase, enemies, horde);
                if (!Valid(parsed)) return false;
                snapshot = parsed; return true;
            }
        }
        private static bool Valid(RangeSnapshot snapshot)
        {
            if (snapshot == null || snapshot.SessionId <= 0 || snapshot.RoundId < 0 ||
                (snapshot.Phase != SessionPhase.Lobby && snapshot.Phase != SessionPhase.Playing) ||
                (snapshot.Phase == SessionPhase.Playing && snapshot.RoundId == 0) ||
                snapshot.MemberCount < 1 || snapshot.MemberCount > LanSession.MaximumPlayers) return false;
            InfectedSnapshot enemy = snapshot.Encounter;
            if (!ValidWirePosition(enemy.Position) || (enemy.Phase == EncounterPhase.Dormant && enemy.Health != 0) ||
                (byte)enemy.Phase > (byte)EncounterPhase.Failed || snapshot.EnemyCount > HostInfectedEncounter.MaximumEnemies ||
                (snapshot.Phase == SessionPhase.Lobby && enemy.Phase != EncounterPhase.Dormant)) return false;
            int alive = 0; var enemyIds = new HashSet<byte>();
            for (int i = 0; i < snapshot.EnemyCount; i++)
            {
                EnemySnapshot value = snapshot.Enemy(i);
                if (value.Health > 100 || !ValidWirePosition(value.Position) || !enemyIds.Add(value.Id)) return false;
                if (value.Health > 0) alive++;
            }
            HordeSnapshot horde = snapshot.Horde;
            if (!AimVector.Finite(horde.IntermissionSeconds) || horde.IntermissionSeconds < 0 || horde.IntermissionSeconds > HostInfectedEncounter.IntermissionDuration) return false;
            if (horde.Phase == HordePhase.Disabled)
            {
                if (horde.Wave != 0 || horde.Total != 0 || horde.IntermissionSeconds != 0) return false;
                if (enemy.Phase == EncounterPhase.Dormant ? snapshot.EnemyCount != 0 : snapshot.EnemyCount != 1 || snapshot.Enemy(0).Id != 0) return false;
                if ((enemy.Phase == EncounterPhase.Active || enemy.Phase == EncounterPhase.Failed) && alive != 1) return false;
                if (enemy.Phase == EncounterPhase.Cleared && alive != 0) return false;
            }
            else
            {
                if (horde.Total != HostInfectedEncounter.TotalWaves || horde.Wave < 1 || horde.Wave > horde.Total || snapshot.EnemyCount != horde.Wave + 1) return false;
                for (int i = 0; i < snapshot.EnemyCount; i++)
                    if (snapshot.Enemy(i).Id != horde.Wave * HostInfectedEncounter.MaximumEnemies + i) return false;
                switch (horde.Phase)
                {
                    case HordePhase.Fighting: if (enemy.Phase != EncounterPhase.Active || alive == 0) return false; break;
                    case HordePhase.Intermission: if (enemy.Phase != EncounterPhase.Active || alive != 0 || horde.Wave == horde.Total) return false; break;
                    case HordePhase.Completed: if (enemy.Phase != EncounterPhase.Cleared || alive != 0 || horde.Wave != horde.Total) return false; break;
                    case HordePhase.Failed: if (enemy.Phase != EncounterPhase.Failed) return false; break;
                    default: return false;
                }
                if (horde.Phase != HordePhase.Intermission && horde.IntermissionSeconds != 0) return false;
            }
            var peers = new HashSet<ulong>(); var slots = new HashSet<byte>(); var connections = new HashSet<long>();
            for (int i = 0; i < snapshot.MemberCount; i++)
            {
                RangeMember m = snapshot.Member(i);
                if (m.ConnectionId <= 0 || m.Slot >= LanSession.MaximumPlayers || m.Ammo > HostFireGate.MagazineSize || m.Health > 100 || m.ReviveProgress > 99 ||
                    (m.ReviveTargetSlot == 255 ? m.ReviveProgress != 0 : m.ReviveTargetSlot >= LanSession.MaximumPlayers || m.ReviveTargetSlot == m.Slot || m.Health == 0) ||
                    !ValidWirePosition(m.Position) || !peers.Add(m.PeerId) || !slots.Add(m.Slot) || !connections.Add(m.ConnectionId)) return false;
            }
            for (int i = 0; i < snapshot.MemberCount; i++)
            {
                RangeMember reviver = snapshot.Member(i);
                if (reviver.ReviveTargetSlot == 255) continue;
                if (enemy.Phase != EncounterPhase.Active && enemy.Phase != EncounterPhase.Cleared) return false;
                bool validTarget = false;
                for (int j = 0; j < snapshot.MemberCount; j++)
                    if (snapshot.Member(j).Slot == reviver.ReviveTargetSlot && snapshot.Member(j).Health == 0) validTarget = true;
                if (!validTarget) return false;
            }
            for (int i = 0; i < 3; i++) if (snapshot.TargetHealth(i) < 0 || snapshot.TargetHealth(i) > 100) return false;
            return true;
        }
        private static void Header(BinaryWriter writer, byte kind) { writer.Write(Magic); writer.Write(Version); writer.Write(kind); }
        private static bool Header(BinaryReader reader, byte kind) => reader.ReadUInt32() == Magic && reader.ReadUInt16() == Version && reader.ReadByte() == kind;
    }
}
