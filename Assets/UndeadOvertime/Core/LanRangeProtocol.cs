using System;
using System.IO;
using System.Collections.Generic;

namespace UndeadOvertime.Core
{
    public enum RangeAction : byte { Fire = 1, Reload = 2, Move = 3 }
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
        public readonly byte Slot, Ammo;
        public readonly bool Reloading;
        public readonly AimVector Position;
        public RangeMember(ulong peerId, long connectionId, byte slot, byte ammo, bool reloading)
            : this(peerId, connectionId, slot, ammo, reloading, HostMoveGate.Spawn(slot)) { }
        public RangeMember(ulong peerId, long connectionId, byte slot, byte ammo, bool reloading, AimVector position)
        { PeerId = peerId; ConnectionId = connectionId; Slot = slot; Ammo = ammo; Reloading = reloading; Position = position; }
    }
    public sealed class RangeSnapshot
    {
        public readonly long SessionId, RoundId;
        public readonly SessionPhase Phase;
        private readonly RangeMember[] members;
        private readonly int[] health;
        public int MemberCount => members.Length;
        public RangeMember Member(int index) => members[index];
        public int TargetHealth(int index) => health[index];
        public RangeSnapshot(long sessionId, long roundId, SessionPhase phase, RangeMember[] members, int[] health)
        {
            if (members == null || health == null || health.Length != 3) throw new ArgumentException("Three target health values and a member array are required.");
            SessionId = sessionId; RoundId = roundId; Phase = phase;
            this.members = (RangeMember[])members.Clone(); this.health = (int[])health.Clone();
        }
    }

    /// <summary>Fixed little-endian protocol, no strings/JSON/unbounded length prefixes. Not authentication.</summary>
    public static class LanRangeProtocol
    {
        public const ushort Version = 2;
        public const int CommandBytes = 44;
        public const int SnapshotHeaderBytes = 37;
        public const int MemberBytes = 31;
        public const int MaximumBytes = SnapshotHeaderBytes + LanSession.MaximumPlayers * MemberBytes;
        private const uint Magic = 0x554F4C31;
        public static byte[] Handshake => new byte[] { 0x55, 0x4f, 0x52, (byte)Version };
        public static bool AcceptHandshake(byte[] data) => data != null && data.Length == 4 &&
            data[0] == 0x55 && data[1] == 0x4f && data[2] == 0x52 && data[3] == Version;

        public static byte[] Encode(RangeCommand command)
        {
            if (!Valid(command)) throw new ArgumentException("Invalid range command.");
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
        private static bool Valid(RangeCommand value) =>
            value.SessionId > 0 && value.RoundId > 0 && value.Sequence > 0 &&
            (value.Action == RangeAction.Move ? HostMoveGate.ValidIntent(value.Aim) :
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
                for (int i = 0; i < snapshot.MemberCount; i++)
                {
                    RangeMember m = snapshot.Member(i);
                    writer.Write(m.PeerId); writer.Write(m.ConnectionId); writer.Write(m.Slot); writer.Write(m.Ammo);
                    writer.Write((byte)(m.Reloading ? 1 : 0));
                    writer.Write((float)m.Position.X); writer.Write((float)m.Position.Y); writer.Write((float)m.Position.Z);
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
                if (count < 1 || count > LanSession.MaximumPlayers || bytes.Length != SnapshotHeaderBytes + count * MemberBytes) return false;
                var health = new int[3]; for (int i = 0; i < 3; i++) health[i] = reader.ReadInt32();
                var members = new RangeMember[count];
                for (int i = 0; i < count; i++)
                {
                    ulong peer = reader.ReadUInt64(); long connection = reader.ReadInt64();
                    byte slot = reader.ReadByte(), ammo = reader.ReadByte(), reload = reader.ReadByte();
                    if (reload > 1) return false;
                    members[i] = new RangeMember(peer, connection, slot, ammo, reload == 1,
                        new AimVector(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                }
                var parsed = new RangeSnapshot(session, round, phase, members, health);
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
            var peers = new HashSet<ulong>(); var slots = new HashSet<byte>();
            for (int i = 0; i < snapshot.MemberCount; i++)
            {
                RangeMember m = snapshot.Member(i);
                if (m.ConnectionId <= 0 || m.Slot >= LanSession.MaximumPlayers || m.Ammo > HostFireGate.MagazineSize ||
                    !HostMoveGate.ValidPosition(m.Position) || !peers.Add(m.PeerId) || !slots.Add(m.Slot)) return false;
            }
            for (int i = 0; i < 3; i++) if (snapshot.TargetHealth(i) < 0 || snapshot.TargetHealth(i) > 100) return false;
            return true;
        }
        private static void Header(BinaryWriter writer, byte kind) { writer.Write(Magic); writer.Write(Version); writer.Write(kind); }
        private static bool Header(BinaryReader reader, byte kind) => reader.ReadUInt32() == Magic && reader.ReadUInt16() == Version && reader.ReadByte() == kind;
    }
}
