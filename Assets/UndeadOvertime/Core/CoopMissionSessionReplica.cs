namespace UndeadOvertime.Core
{
    /// <summary>One connected mission session's ordered host state stream. A lobby must bind identity before
    /// round N+1 can open; closing round N retires every later packet from N. Recreate only on disconnect.
    /// Caller authenticates the transport sender and verifies local membership before calling this guard.</summary>
    public sealed class CoopMissionSessionReplica
    {
        private long sessionId, closedRound;
        private CoopMissionReplica replica;
        public RangeSnapshot Range { get; private set; }
        public MissionFrame Mission => replica?.Current;
        public bool TryAcceptHostPacket(byte[] bytes)
        {
            if (CoopMissionProtocol.TryFrame(bytes, out MissionFrame next))
            {
                if (sessionId == 0 || next.Range.SessionId != sessionId || closedRound == long.MaxValue || next.Range.RoundId != closedRound + 1) return false;
                var candidate = replica ?? new CoopMissionReplica(sessionId, next.Range.RoundId);
                if (!candidate.TryAcceptHostPacket(bytes)) return false;
                replica = candidate; Range = candidate.Current.Range; return true;
            }
            if (!LanRangeProtocol.TrySnapshot(bytes, out RangeSnapshot lobby) || lobby.Phase != SessionPhase.Lobby) return false;
            if (sessionId == 0) { sessionId = lobby.SessionId; closedRound = lobby.RoundId; }
            else
            {
                if (lobby.SessionId != sessionId) return false;
                if (replica != null)
                {
                    if (lobby.RoundId != replica.Current.Range.RoundId) return false;
                    closedRound = lobby.RoundId; replica = null;
                }
                else if (lobby.RoundId != closedRound) return false;
            }
            Range = lobby; return true;
        }
    }
}
