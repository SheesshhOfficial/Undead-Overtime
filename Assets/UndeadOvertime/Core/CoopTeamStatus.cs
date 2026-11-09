using System;
using System.Globalization;

namespace UndeadOvertime.Core
{
    public readonly struct CoopTeamMember
    {
        public readonly byte Slot, Health, RescuerSlot, RescueProgress;
        public readonly bool IsLocal;
        public readonly double Distance;
        public bool IsDowned => Health == 0;
        public string Label { get; }
        public string StatusText { get; }
        internal CoopTeamMember(byte slot, byte health, bool local, double distance, byte rescuer, byte progress)
        {
            Slot = slot; Health = health; IsLocal = local; Distance = distance; RescuerSlot = rescuer; RescueProgress = progress;
            string label = "Slot " + (slot + 1).ToString(CultureInfo.InvariantCulture) + (local ? " (you)" : "");
            string text = label + " | " + (health == 0 ? "DOWNED" : health.ToString(CultureInfo.InvariantCulture) + " HP");
            if (!local) text += " | " + Math.Ceiling(distance).ToString(CultureInfo.InvariantCulture) + " m";
            if (rescuer != 255) text += " | rescue " + progress.ToString(CultureInfo.InvariantCulture) + "% by slot " + (rescuer + 1).ToString(CultureInfo.InvariantCulture);
            Label = label; StatusText = text;
        }
    }

    /// <summary>Immutable, read-only team HUD projection of an accepted host snapshot. Distances
    /// are straight-line planar estimates, not navigation, visibility or permission to revive.
    /// No smoothing, invented names, damage events, cumulative rescue progress or game mutation.</summary>
    public sealed class CoopTeamStatus
    {
        private readonly CoopTeamMember[] rows;
        public int Count => rows.Length;
        public int DownedCount { get; }
        public CoopTeamMember Member(int index) => rows[index];
        public const int MinimumPanelWidth = 640, MinimumPanelHeight = 480;
        public static bool CanShowPanel(int width, int height, int count) =>
            width >= MinimumPanelWidth && height >= MinimumPanelHeight && count >= 2 && count <= LanSession.MaximumPlayers;
        public string RescueAdvice(EncounterPhase phase, bool missionTerminal)
        {
            if (DownedCount == 0) return string.Empty;
            if (missionTerminal || DownedCount == Count || (phase != EncounterPhase.Active && phase != EncounterPhase.Cleared))
                return "Rescue is unavailable. Host can return to lobby to restart.";
            return "A living teammate can hold E nearby with a clear path.";
        }

        private CoopTeamStatus(CoopTeamMember[] values, int downed) { rows = values; DownedCount = downed; }

        public static bool TryCreate(RangeSnapshot snapshot, ulong localPeer, out CoopTeamStatus status)
        {
            status = null;
            if (snapshot == null || snapshot.SessionId <= 0 || snapshot.RoundId <= 0 || snapshot.Phase != SessionPhase.Playing ||
                snapshot.MemberCount < 1 || snapshot.MemberCount > LanSession.MaximumPlayers) return false;
            int localIndex = -1;
            for (int i = 0; i < snapshot.MemberCount; i++)
            {
                RangeMember m = snapshot.Member(i);
                if (m.ConnectionId <= 0 || m.Slot >= LanSession.MaximumPlayers || m.Health > 100 ||
                    !HostMoveGate.ValidPosition(m.Position) || m.ReviveProgress > 99 ||
                    (m.ReviveTargetSlot == 255 ? m.ReviveProgress != 0 : m.ReviveTargetSlot >= LanSession.MaximumPlayers || m.ReviveTargetSlot == m.Slot || m.Health == 0)) return false;
                for (int j = 0; j < i; j++)
                {
                    RangeMember previous = snapshot.Member(j);
                    if (previous.Slot == m.Slot || previous.PeerId == m.PeerId || previous.ConnectionId == m.ConnectionId) return false;
                }
                if (m.PeerId == localPeer) localIndex = i;
            }
            if (localIndex < 0) return false;
            for (int i = 0; i < snapshot.MemberCount; i++)
            {
                RangeMember m = snapshot.Member(i);
                if (m.ReviveTargetSlot == 255) continue;
                bool found = false;
                for (int j = 0; j < snapshot.MemberCount; j++)
                    if (snapshot.Member(j).Slot == m.ReviveTargetSlot && snapshot.Member(j).Health == 0) found = true;
                if (!found) return false;
            }
            var result = new CoopTeamMember[snapshot.MemberCount];
            RangeMember local = snapshot.Member(localIndex);
            int downed = 0, at = 0;
            // Slot order is stable even when host member enumeration changes.
            for (int slot = 0; slot < LanSession.MaximumPlayers; slot++)
                for (int i = 0; i < snapshot.MemberCount; i++)
                {
                    RangeMember m = snapshot.Member(i);
                    if (m.Slot != slot) continue;
                    byte rescuer = 255, progress = 0;
                    for (int j = 0; j < snapshot.MemberCount; j++)
                    {
                        RangeMember candidate = snapshot.Member(j);
                        if (candidate.ReviveTargetSlot == m.Slot &&
                            (rescuer == 255 || candidate.ReviveProgress > progress || (candidate.ReviveProgress == progress && candidate.Slot < rescuer)))
                        { rescuer = candidate.Slot; progress = candidate.ReviveProgress; }
                    }
                    double dx = m.Position.X - local.Position.X, dz = m.Position.Z - local.Position.Z;
                    result[at++] = new CoopTeamMember(m.Slot, m.Health, m.PeerId == localPeer, Math.Sqrt(dx * dx + dz * dz), rescuer, progress);
                    if (m.Health == 0) downed++;
                }
            status = new CoopTeamStatus(result, downed);
            return true;
        }
    }
}
