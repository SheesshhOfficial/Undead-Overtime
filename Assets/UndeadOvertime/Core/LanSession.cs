using System;
using System.Collections.Generic;

namespace UndeadOvertime.Core
{
    public enum SessionPhase { Stopped, Lobby, Playing }
    public enum JoinResult { Accepted, NotInLobby, ProtocolMismatch, AlreadyConnected, Full }

    /// <summary>A host-issued connection lifetime, not an authentication token or client-supplied identity.</summary>
    public readonly struct PlayerTicket : IEquatable<PlayerTicket>
    {
        public ulong PeerId { get; }
        public long SessionId { get; }
        public long ConnectionId { get; }
        public PlayerTicket(ulong peerId, long sessionId, long connectionId)
        { PeerId = peerId; SessionId = sessionId; ConnectionId = connectionId; }
        public bool Equals(PlayerTicket other) => PeerId == other.PeerId && SessionId == other.SessionId && ConnectionId == other.ConnectionId;
        public override bool Equals(object obj) => obj is PlayerTicket other && Equals(other);
        public override int GetHashCode() => PeerId.GetHashCode() ^ SessionId.GetHashCode() ^ ConnectionId.GetHashCode();
    }

    /// <summary>
    /// Single-threaded host-owned lifecycle. The transport adapter MUST derive PeerId from
    /// its connection context and retain tickets on the host. Never deserialize a ticket as authority.
    /// Contains no sockets, Unity objects, persistence, personal identifiers or host migration.
    /// </summary>
    public sealed class LanSession
    {
        public const int MaximumPlayers = 10;
        private readonly Dictionary<ulong, PlayerTicket> members = new Dictionary<ulong, PlayerTicket>();
        private long sessionId;
        private long nextConnectionId;
        public int ProtocolVersion { get; }
        public int Capacity { get; }
        public SessionPhase Phase { get; private set; }
        public PlayerTicket Host { get; private set; }
        public int PlayerCount => members.Count;
        public long SessionId => sessionId;
        public long RoundId { get; private set; }

        public LanSession(int protocolVersion, int capacity = MaximumPlayers)
        {
            if (protocolVersion <= 0) throw new ArgumentOutOfRangeException(nameof(protocolVersion));
            if (capacity < 1 || capacity > MaximumPlayers) throw new ArgumentOutOfRangeException(nameof(capacity));
            ProtocolVersion = protocolVersion;
            Capacity = capacity;
        }

        public PlayerTicket StartHost(ulong transportPeerId)
        {
            if (Phase != SessionPhase.Stopped) throw new InvalidOperationException("Stop the previous session first.");
            sessionId = checked(sessionId + 1);
            members.Clear();
            RoundId = 0;
            Host = AddMember(transportPeerId);
            Phase = SessionPhase.Lobby;
            return Host;
        }

        public JoinResult TryJoin(ulong transportPeerId, int protocolVersion, out PlayerTicket ticket)
        {
            ticket = default;
            if (Phase != SessionPhase.Lobby) return JoinResult.NotInLobby;
            if (protocolVersion != ProtocolVersion) return JoinResult.ProtocolMismatch;
            if (members.ContainsKey(transportPeerId)) return JoinResult.AlreadyConnected;
            if (members.Count >= Capacity) return JoinResult.Full;
            ticket = AddMember(transportPeerId);
            return JoinResult.Accepted;
        }

        private PlayerTicket AddMember(ulong peerId)
        {
            nextConnectionId = checked(nextConnectionId + 1);
            var ticket = new PlayerTicket(peerId, sessionId, nextConnectionId);
            members.Add(peerId, ticket);
            return ticket;
        }

        public bool IsCurrent(PlayerTicket ticket) => Phase != SessionPhase.Stopped &&
            members.TryGetValue(ticket.PeerId, out PlayerTicket current) && current.Equals(ticket);

        public PlayerTicket[] SnapshotMembers()
        {
            var snapshot = new PlayerTicket[members.Count];
            members.Values.CopyTo(snapshot, 0);
            Array.Sort(snapshot, (a, b) => a.ConnectionId.CompareTo(b.ConnectionId));
            return snapshot;
        }

        public bool TryBeginRound(PlayerTicket requester)
        {
            if (Phase != SessionPhase.Lobby || !IsHost(requester)) return false;
            RoundId = checked(RoundId + 1);
            Phase = SessionPhase.Playing;
            return true;
        }

        public bool TryReturnToLobby(PlayerTicket requester)
        {
            if (Phase != SessionPhase.Playing || !IsHost(requester)) return false;
            Phase = SessionPhase.Lobby;
            return true;
        }

        public bool TryStop(PlayerTicket requester)
        {
            if (!IsHost(requester)) return false;
            StopInternal();
            return true;
        }

        /// <summary>Called only from an authoritative transport disconnect callback.</summary>
        public bool Disconnect(PlayerTicket disconnected)
        {
            if (!IsCurrent(disconnected)) return false;
            if (disconnected.Equals(Host)) StopInternal();
            else members.Remove(disconnected.PeerId);
            return true;
        }

        private bool IsHost(PlayerTicket ticket) => IsCurrent(ticket) && Host.Equals(ticket);
        private void StopInternal()
        {
            members.Clear();
            Host = default;
            Phase = SessionPhase.Stopped;
        }
    }
}
