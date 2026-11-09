using UndeadOvertime.Core;
using UnityEngine;

namespace UndeadOvertime.LanPrototype
{
    public sealed partial class LanRange
    {
        private RangeSnapshot teamStatusSnapshot;
        private CoopTeamStatus teamStatus;
        private Vector2 teamScroll, menuScroll;
        public bool TeamStatusEnabled { get; private set; } = true;
        public void SetTeamStatusEnabled(bool enabled) { TeamStatusEnabled = enabled; }
        public CoopTeamStatus CurrentTeamStatus
        {
            get
            {
                if (!IsConnected || snapshot == null || snapshot.Phase != SessionPhase.Playing)
                { teamStatusSnapshot = null; teamStatus = null; return null; }
                if (!ReferenceEquals(teamStatusSnapshot, snapshot))
                {
                    CoopTeamStatus.TryCreate(snapshot, manager.LocalClientId, out teamStatus);
                    teamStatusSnapshot = snapshot;
                }
                return teamStatus;
            }
        }
        private float TeamHudHeight
        {
            get
            {
                CoopTeamStatus team = CurrentTeamStatus;
                return TeamStatusEnabled && team != null && CoopTeamStatus.CanShowPanel(Screen.width, Screen.height, team.Count) ?
                    Mathf.Min(55 + team.Count * 22, Mathf.Max(100, Screen.height * .32f)) : 0;
            }
        }
        public string TeamRescueAdvice => CurrentTeamStatus?.RescueAdvice(snapshot.Encounter.Phase, MissionTerminal) ?? string.Empty;
        private string CompactTeamStatus
        {
            get
            {
                CoopTeamStatus team = CurrentTeamStatus;
                return TeamStatusEnabled && team != null && team.Count > 1 && TeamHudHeight == 0 ?
                    "TEAM | " + team.DownedCount + " downed | Roster needs 640 x 480." : string.Empty;
            }
        }
        private void DrawTeamStatus()
        {
            float height = TeamHudHeight;
            if (height <= 0) return;
            CoopTeamStatus team = CurrentTeamStatus;
            GUILayout.BeginArea(new Rect(14, Screen.height - height - 14, Mathf.Min(590, Screen.width - 28), height), GUI.skin.box);
            GUILayout.Label("TEAM | " + team.DownedCount + " downed | distances are straight-line");
            teamScroll = GUILayout.BeginScrollView(teamScroll);
            for (int i = 0; i < team.Count; i++) GUILayout.Label(team.Member(i).StatusText);
            GUILayout.EndScrollView();
            if (team.DownedCount > 0) GUILayout.Label(TeamRescueAdvice);
            GUILayout.EndArea();
        }
    }
}
