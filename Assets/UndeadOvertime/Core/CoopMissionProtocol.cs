using System;
using System.IO;

namespace UndeadOvertime.Core
{
    public readonly struct MissionPowerCommand
    {
        public readonly long SessionId, RoundId;
        public readonly ulong Sequence;
        public readonly bool Held;
        public MissionPowerCommand(long sessionId, long roundId, ulong sequence, bool held)
        { SessionId = sessionId; RoundId = roundId; Sequence = sequence; Held = held; }
    }
    public sealed class MissionFrame
    {
        public readonly ulong Sequence;
        public readonly RangeSnapshot Range;
        public readonly CoopMissionSnapshot Mission;
        public MissionFrame(ulong sequence, RangeSnapshot range, CoopMissionSnapshot mission)
        { Sequence = sequence; Range = range; Mission = mission; }
    }

    /// <summary>Mission-channel codec, distinct from the optional range v5 session protocol.
    /// Fixed little-endian fields; at most 465 bytes, no strings or unbounded array prefixes.
    /// Syntax validation is not authentication: only transport-authenticated host packets may feed the replica.</summary>
    public static class CoopMissionProtocol
    {
        public const ushort Version = 1;
        public const int PowerCommandBytes = 32, FrameHeaderBytes = 23;
        public const int MaximumFrameBytes = FrameHeaderBytes + LanRangeProtocol.MaximumBytes;
        private const uint Magic = 0x554F4D31;
        public static byte[] Handshake => new byte[] { 0x55, 0x4f, 0x4d, (byte)Version };
        public static bool AcceptHandshake(byte[] bytes) => bytes != null && bytes.Length == 4 &&
            bytes[0] == 0x55 && bytes[1] == 0x4f && bytes[2] == 0x4d && bytes[3] == Version;
        private static void Header(BinaryWriter writer, byte kind) { writer.Write(Magic); writer.Write(Version); writer.Write(kind); }
        private static bool Header(BinaryReader reader, byte kind) => reader.ReadUInt32() == Magic && reader.ReadUInt16() == Version && reader.ReadByte() == kind;
        private static bool Valid(MissionPowerCommand value) => value.SessionId > 0 && value.RoundId > 0 && value.Sequence > 0;
        public static byte[] Encode(MissionPowerCommand command)
        {
            if (!Valid(command)) throw new ArgumentException("Invalid mission power command.");
            using (var stream = new MemoryStream(PowerCommandBytes)) using (var writer = new BinaryWriter(stream))
            {
                Header(writer, 1); writer.Write(command.SessionId); writer.Write(command.RoundId);
                writer.Write(command.Sequence); writer.Write((byte)(command.Held ? 1 : 0));
                return stream.ToArray();
            }
        }
        public static bool TryPowerCommand(byte[] bytes, out MissionPowerCommand command)
        {
            command = default;
            if (bytes == null || bytes.Length != PowerCommandBytes) return false;
            using (var reader = new BinaryReader(new MemoryStream(bytes, false)))
            {
                if (!Header(reader, 1)) return false;
                long session = reader.ReadInt64(), round = reader.ReadInt64(); ulong sequence = reader.ReadUInt64();
                byte held = reader.ReadByte(); if (held > 1) return false;
                var parsed = new MissionPowerCommand(session, round, sequence, held == 1);
                if (!Valid(parsed)) return false;
                command = parsed; return true;
            }
        }
        public static byte[] Encode(MissionFrame frame)
        {
            if (!ValidMission(frame)) throw new ArgumentException("Incoherent mission frame.");
            byte[] range = LanRangeProtocol.Encode(frame.Range); // Also validates every nested combat/member field.
            using (var stream = new MemoryStream(FrameHeaderBytes + range.Length)) using (var writer = new BinaryWriter(stream))
            {
                Header(writer, 2); writer.Write(frame.Sequence); writer.Write((byte)frame.Mission.Phase);
                writer.Write((byte)(frame.Mission.DepotGateUnlocked ? 1 : 0)); writer.Write((float)frame.Mission.HoldSeconds);
                writer.Write((ushort)range.Length); writer.Write(range); return stream.ToArray();
            }
        }
        public static bool TryFrame(byte[] bytes, out MissionFrame frame)
        {
            frame = null;
            if (bytes == null || bytes.Length < FrameHeaderBytes + LanRangeProtocol.SnapshotHeaderBytes || bytes.Length > MaximumFrameBytes) return false;
            using (var reader = new BinaryReader(new MemoryStream(bytes, false)))
            {
                if (!Header(reader, 2)) return false;
                ulong sequence = reader.ReadUInt64(); var phase = (CoopMissionPhase)reader.ReadByte();
                byte gate = reader.ReadByte(); float hold = reader.ReadSingle(); int length = reader.ReadUInt16();
                if (gate > 1 || length < LanRangeProtocol.SnapshotHeaderBytes || length > LanRangeProtocol.MaximumBytes || bytes.Length != FrameHeaderBytes + length) return false;
                // Allocation is bounded by the checked nested length before any copy or decoding.
                if (!LanRangeProtocol.TrySnapshot(reader.ReadBytes(length), out RangeSnapshot range)) return false;
                var mission = new CoopMissionSnapshot(range.SessionId, range.RoundId, phase, true, gate == 1, hold);
                var parsed = new MissionFrame(sequence, range, mission);
                if (!ValidMission(parsed)) return false;
                frame = parsed; return true;
            }
        }
        private static bool ValidMission(MissionFrame frame)
        {
            if (frame == null || frame.Sequence == 0 || frame.Range == null) return false;
            RangeSnapshot range = frame.Range; CoopMissionSnapshot mission = frame.Mission;
            if (range.Phase != SessionPhase.Playing || range.SessionId <= 0 || range.RoundId <= 0 || !mission.IsCurrent ||
                range.SessionId != mission.SessionId || range.RoundId != mission.RoundId || range.MemberCount < 1 || range.MemberCount > LanSession.MaximumPlayers || !AimVector.Finite(mission.HoldSeconds) || mission.HoldSeconds < 0) return false;
            int living = 0;
            for (int i = 0; i < range.MemberCount; i++) if (range.Member(i).Health > 0) living++;
            if (mission.Phase != CoopMissionPhase.Failed && living == 0) return false;
            if ((mission.Phase == CoopMissionPhase.Completed || (mission.Phase == CoopMissionPhase.ReachExtraction && mission.HoldSeconds > 0)) && living != range.MemberCount) return false;
            bool gate = mission.DepotGateUnlocked; HordePhase horde = range.Horde.Phase; EncounterPhase encounter = range.Encounter.Phase;
            switch (mission.Phase)
            {
                case CoopMissionPhase.RestorePower:
                    return !gate && horde == HordePhase.Disabled && encounter == EncounterPhase.Dormant && mission.HoldSeconds <= CoopMissionProgression.PowerHoldDuration;
                case CoopMissionPhase.DefendDepot:
                    return gate && mission.HoldSeconds == 0 && encounter == EncounterPhase.Active && (horde == HordePhase.Fighting || horde == HordePhase.Intermission);
                case CoopMissionPhase.ReachExtraction:
                    return gate && horde == HordePhase.Completed && encounter == EncounterPhase.Cleared && mission.HoldSeconds <= CoopMissionProgression.ExtractionHoldDuration;
                case CoopMissionPhase.Completed:
                    return gate && horde == HordePhase.Completed && encounter == EncounterPhase.Cleared && mission.HoldSeconds == 0;
                case CoopMissionPhase.Failed:
                    return mission.HoldSeconds == 0 &&
                        ((horde == HordePhase.Disabled && (encounter == EncounterPhase.Dormant || encounter == EncounterPhase.Failed)) ||
                        (gate && (horde == HordePhase.Failed || horde == HordePhase.Completed)));
                default: return false;
            }
        }
    }

    /// <summary>View-only packet guard for one host-issued session/round. Create a new replica on an authorized
    /// round change. Caller must reject non-host transport senders BEFORE calling TryAcceptHostPacket.
    /// No received value changes host collision, life state, rewards or mission progression.</summary>
    public sealed class CoopMissionReplica
    {
        private readonly long sessionId, roundId;
        public MissionFrame Current { get; private set; }
        public CoopMissionReplica(long sessionId, long roundId)
        {
            if (sessionId <= 0) throw new ArgumentOutOfRangeException(nameof(sessionId));
            if (roundId <= 0) throw new ArgumentOutOfRangeException(nameof(roundId));
            this.sessionId = sessionId; this.roundId = roundId;
        }
        public bool TryAcceptHostPacket(byte[] bytes)
        {
            if (!CoopMissionProtocol.TryFrame(bytes, out MissionFrame next) || next.Range.SessionId != sessionId || next.Range.RoundId != roundId) return false;
            if (Current != null)
            {
                if (next.Sequence <= Current.Sequence || (Current.Mission.DepotGateUnlocked && !next.Mission.DepotGateUnlocked)) return false;
                CoopMissionPhase before = Current.Mission.Phase, after = next.Mission.Phase;
                if ((before == CoopMissionPhase.Completed || before == CoopMissionPhase.Failed) &&
                    (after != before || next.Mission.DepotGateUnlocked != Current.Mission.DepotGateUnlocked)) return false;
                if (after != CoopMissionPhase.Failed && (byte)after < (byte)before) return false;
            }
            Current = next; return true;
        }
    }
}
