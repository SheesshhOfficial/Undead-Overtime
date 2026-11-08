using System.Collections;
using UndeadOvertime.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
namespace UndeadOvertime.LanPrototype
{
    public sealed partial class LanLoopbackSmoke
    {
        private const string MissionReceipt = "uo.smoke.mission.receipt.v1";
        private bool missionReceipt;
        private void ReceiveMissionReceipt(ulong sender, FastBufferReader reader)
        {
            if (!options.Mission || !options.Host || !range.IsHosting || sender != Remote().PeerId || range.CurrentMission == null ||
                range.CurrentMission.Mission.Phase != CoopMissionPhase.Completed ||
                !LanRange.TryReadPayload(reader, CoopMissionProtocol.PowerCommandBytes, CoopMissionProtocol.PowerCommandBytes, out byte[] bytes)) return;
            if (CoopMissionProtocol.TryPowerCommand(bytes, out MissionPowerCommand receipt) && !receipt.Held &&
                receipt.SessionId == range.CurrentMission.Range.SessionId && receipt.RoundId == range.CurrentMission.Range.RoundId &&
                receipt.Sequence <= range.CurrentMission.Sequence) missionReceipt = true;
        }
        private void SendMissionReceipt()
        {
            MissionFrame frame = range.CurrentMission;
            byte[] bytes = CoopMissionProtocol.Encode(new MissionPowerCommand(frame.Range.SessionId, frame.Range.RoundId, frame.Sequence, false));
            using (var writer = new FastBufferWriter(bytes.Length, Allocator.Temp))
            {
                writer.WriteBytesSafe(bytes);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MissionReceipt, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }
        private bool DriveMissionToward(Vector3 destination)
        {
            RangeMember player = Local(); Require(player.Health > 0, "Mission walker was downed.");
            Vector3 at = new Vector3((float)player.Position.X, (float)player.Position.Y, (float)player.Position.Z);
            Vector3 delta = destination - at; delta.y = 0;
            if (delta.magnitude < .22f) { range.SendAction(RangeAction.Move, Vector3.zero); return true; }
            Require(range.SendAction(RangeAction.Move, delta.normalized), "Mission movement input rejected."); return false;
        }
        private void FightMission()
        {
            Require(range.CurrentMission != null && range.CurrentMission.Mission.Phase != CoopMissionPhase.Failed, "Mission encounter failed.");
            RangeMember player = Local(); Require(player.Health > 0, "Mission shooter was downed.");
            if (player.Reloading || range.CurrentSnapshot.Horde.Phase != HordePhase.Fighting) return;
            Require(player.Ammo == 0 ? range.SendAction(RangeAction.Reload, Vector3.up) : range.SendAction(RangeAction.Fire, AimAtNearestEnemy()),
                "Mission combat input rejected.");
        }
        private void RequireFreshMissionRound(long sessionId, long roundId)
        {
            MissionFrame frame = range.CurrentMission;
            Require(frame != null && frame.Range.SessionId == sessionId && frame.Range.RoundId == roundId &&
                frame.Range.Phase == SessionPhase.Playing && frame.Range.MemberCount == 2 &&
                frame.Mission.Phase == CoopMissionPhase.RestorePower && frame.Mission.HoldSeconds == 0 && !range.MissionGateOpen &&
                frame.Range.EnemyCount == 0 && frame.Range.Horde.Phase == HordePhase.Disabled,
                "Mission restart did not publish a fresh, locked two-member round.");
            Require(LanRange.ResolvePlanarMovement(new Vector3(0, 1.6f, -2.6f), Vector3.forward * .2f).z < -2.5f,
                "Fresh mission gate does not physically block the crossing.");
            for (int i = 0; i < frame.Range.MemberCount; i++)
            {
                RangeMember member = frame.Range.Member(i);
                Require(member.Health == 100 && member.Ammo == 12 && !member.Reloading && member.ReviveProgress == 0 &&
                    (member.Position - HostMoveGate.Spawn(member.Slot)).LengthSquared < .0001,
                    "Mission restart retained life, weapon, revive or movement state.");
            }
        }
        private void RequireMissionLobbyCleared(long sessionId)
        {
            Require(range.IsConnected && range.Phase == SessionPhase.Lobby && range.CurrentMission == null && !range.MissionGateOpen &&
                range.CurrentSnapshot.SessionId == sessionId && range.CurrentSnapshot.RoundId == 1,
                "Lobby retained the completed mission or lost session identity.");
            // Probe a formerly solid partition section, not the gate that was already open at completion.
            Require(LanRange.ResolvePlanarMovement(new Vector3(5, 1.6f, -2.6f), Vector3.forward * .2f).z > -2.5f,
                "Old mission partition remains in lobby physics.");
        }
        private IEnumerator HostMissionRun()
        {
            Require(range.SetMissionMode(true), "Could not select mission mode.");
            Require(range.StartHosting("127.0.0.1", options.Port), "Mission host failed."); Record("host-started");
            while (range.MemberCount < 2) yield return null;
            Require(range.MemberCount == 2, "Unexpected mission peer count.");
            report.firstRemoteConnection = Remote().ConnectionId; Record("two-process-membership");
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(MissionReceipt, ReceiveMissionReceipt);
            Require(range.BeginRound(), "Mission round did not start.");
            Require(!range.MissionGateOpen && range.CurrentMission.Mission.Phase == CoopMissionPhase.RestorePower, "Mission did not begin locked.");
            Record("mission-round-started");
            while (range.CurrentMission.Mission.Phase == CoopMissionPhase.RestorePower) yield return null;
            Require(range.MissionGateOpen && range.CurrentMission.Mission.Phase == CoopMissionPhase.DefendDepot, "Remote power did not open the gate/start waves.");
            Record("remote-power-opened-gate");
            Require(range.SendAction(RangeAction.Fire, Vector3.up), "Mission weapon probe rejected.");
            while (Local().Ammo != 11) yield return null;
            Record("host-mission-shot-replicated");
            Require(range.SendAction(RangeAction.Reload, Vector3.up), "Mission reload probe rejected.");
            while (!Local().Reloading) yield return null;
            Record("host-mission-reload-started");
            while (Local().Reloading || Local().Ammo != 12) yield return null;
            Record("host-mission-reload-completed");
            while (!DriveMissionToward(new Vector3(-.6f, 1.6f, -4))) yield return new WaitForSecondsRealtime(.05f);
            int waveMask = 0;
            while (range.CurrentMission.Mission.Phase == CoopMissionPhase.DefendDepot)
            {
                waveMask |= 1 << range.CurrentSnapshot.Horde.Wave; FightMission(); yield return new WaitForSecondsRealtime(.22f);
            }
            Require(waveMask == 14 && range.CurrentMission.Mission.Phase == CoopMissionPhase.ReachExtraction, "Mission did not clear all three waves.");
            Record("mission-three-waves-cleared");
            while (!DriveMissionToward(new Vector3(-.6f, 1.6f, 12))) yield return new WaitForSecondsRealtime(.05f);
            while (range.CurrentMission.Mission.Phase == CoopMissionPhase.ReachExtraction) yield return null;
            Require(range.CurrentMission.Mission.Phase == CoopMissionPhase.Completed && Local().Health > 0 && Remote().Health > 0, "Whole-team extraction failed.");
            Require(!range.SendAction(RangeAction.Fire, Vector3.up), "Terminal mission still accepts combat."); Record("mission-extracted");
            while (!missionReceipt) yield return null;
            Record("client-completion-acknowledged");
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(MissionReceipt);
            long sessionId = range.CurrentSnapshot.SessionId;
            Require(range.ReturnToLobby(), "Completed mission did not return to lobby.");
            RequireMissionLobbyCleared(sessionId);
            Record("mission-lobby-reset");
            while (range.MemberCount != 1) yield return null;
            Record("mission-client-disconnected");
            while (range.MemberCount < 2) yield return null;
            Require(range.MemberCount == 2, "Unexpected mission rejoin count.");
            report.secondRemoteConnection = Remote().ConnectionId;
            Require(report.secondRemoteConnection != report.firstRemoteConnection, "Mission rejoin reused the retired connection identity.");
            Record("mission-fresh-client-rejoin");
            Require(range.BeginRound(), "Second mission round did not start.");
            RequireFreshMissionRound(sessionId, 2); Record("mission-second-round-reset");
            while (range.CurrentMission.Mission.Phase == CoopMissionPhase.RestorePower) yield return null;
            Require(range.MissionGateOpen && range.CurrentMission.Mission.Phase == CoopMissionPhase.DefendDepot,
                "Fresh remote power did not operate the second round.");
            Record("mission-second-round-power");
            // The client sends these only after verifying the new round's replicated gate/state.
            while (Remote().Ammo != 11) yield return null;
            while (!Remote().Reloading) yield return null;
            while (Remote().Reloading || Remote().Ammo != 12) yield return null;
            while (Remote().Ammo != 11) yield return null;
            Require(range.CurrentMission.Range.RoundId == 2 && range.MissionGateOpen && Remote().Health > 0,
                "Second-round acknowledgement lost its live mission binding.");
            Record("mission-second-round-client-acknowledged");
            range.StopSession("Mission lifecycle smoke complete."); while (range.IsBusy) yield return null;
            Require(range.CurrentMission == null && range.Phase == SessionPhase.Stopped, "Mission shutdown retained state."); Record("host-shutdown-completed");
        }
        private IEnumerator ClientMissionRun()
        {
            Require(range.SetMissionMode(true), "Could not select client mission mode.");
            Require(range.Join("127.0.0.1", options.Port), "Mission client failed."); Record("client-started");
            while (range.CurrentMission == null) yield return null;
            Require(range.CurrentMission.Mission.Phase == CoopMissionPhase.RestorePower && !range.MissionGateOpen, "Client missed initial locked mission.");
            Record("mission-round-received");
            while (!DriveMissionToward(new Vector3(-7.6f, 1.6f, -6))) yield return new WaitForSecondsRealtime(.05f);
            Record("client-power-held");
            while (range.CurrentMission.Mission.Phase == CoopMissionPhase.RestorePower)
            { Require(range.SendPowerIntent(true), "Client power intent rejected."); yield return new WaitForSecondsRealtime(.05f); }
            Require(range.MissionGateOpen && range.CurrentMission.Mission.Phase == CoopMissionPhase.DefendDepot, "Client gate transition missing.");
            range.SendPowerIntent(false); Record("replicated-mission-gate-open");
            Require(range.SendAction(RangeAction.Fire, Vector3.up), "Mission weapon probe rejected.");
            while (Local().Ammo != 11) yield return null;
            Record("client-mission-shot-replicated");
            Require(range.SendAction(RangeAction.Reload, Vector3.up), "Mission reload probe rejected.");
            while (!Local().Reloading) yield return null;
            Record("client-mission-reload-started");
            while (Local().Reloading || Local().Ammo != 12) yield return null;
            Record("client-mission-reload-completed");
            while (!DriveMissionToward(new Vector3(.6f, 1.6f, -4))) yield return new WaitForSecondsRealtime(.05f);
            int waveMask = 0;
            while (range.CurrentMission.Mission.Phase == CoopMissionPhase.DefendDepot)
            { waveMask |= 1 << range.CurrentSnapshot.Horde.Wave; FightMission(); yield return new WaitForSecondsRealtime(.22f); }
            Require(waveMask == 14 && range.CurrentMission.Mission.Phase == CoopMissionPhase.ReachExtraction, "Client did not observe all waves."); Record("replicated-mission-three-waves");
            while (!DriveMissionToward(new Vector3(.6f, 1.6f, 12))) yield return new WaitForSecondsRealtime(.05f);
            while (range.CurrentMission.Mission.Phase == CoopMissionPhase.ReachExtraction) yield return null;
            Require(range.CurrentMission.Mission.Phase == CoopMissionPhase.Completed && Local().Health > 0, "Client did not observe mission completion.");
            Require(!range.SendPowerIntent(true) && !range.SendAction(RangeAction.Move, Vector3.right), "Client terminal controls are not blocked.");
            Record("replicated-mission-completed");
            long sessionId = range.CurrentSnapshot.SessionId, firstConnection = Local().ConnectionId;
            SendMissionReceipt();
            while (range.Phase != SessionPhase.Lobby) { Require(range.IsConnected, "Host disconnected instead of returning to lobby."); yield return null; }
            RequireMissionLobbyCleared(sessionId);
            Record("replicated-mission-lobby-reset");
            range.StopSession("Mission reconnect verification.");
            while (range.IsBusy) yield return null;
            Require(range.CurrentMission == null && range.Phase == SessionPhase.Stopped, "Mission disconnect retained round state.");
            Record("mission-client-disconnected"); yield return new WaitForSecondsRealtime(.5f);
            Require(range.Join("127.0.0.1", options.Port), "Mission rejoin failed to start.");
            while (range.CurrentMission == null) { Require(range.IsBusy, "Mission rejoin connection failed."); yield return null; }
            RequireFreshMissionRound(sessionId, 2);
            Require(Local().ConnectionId != firstConnection, "Client reused its old mission connection lifetime.");
            Record("replicated-mission-second-round-reset");
            while (!DriveMissionToward(new Vector3(-7.6f, 1.6f, -6))) yield return new WaitForSecondsRealtime(.05f);
            while (range.CurrentMission.Mission.Phase == CoopMissionPhase.RestorePower)
            { Require(range.SendPowerIntent(true), "Fresh second-round power intent rejected."); yield return new WaitForSecondsRealtime(.05f); }
            Require(range.MissionGateOpen && range.CurrentMission.Mission.Phase == CoopMissionPhase.DefendDepot,
                "Client did not receive the second-round gate transition.");
            range.SendPowerIntent(false); Record("replicated-mission-second-round-power");
            Require(range.SendAction(RangeAction.Fire, Vector3.up), "Second-round shot rejected.");
            while (Local().Ammo != 11) yield return null;
            Require(range.SendAction(RangeAction.Reload, Vector3.up), "Second-round reload rejected.");
            while (!Local().Reloading) yield return null;
            while (Local().Reloading || Local().Ammo != 12) yield return null;
            Record("mission-second-round-reload-completed");
            Require(range.SendAction(RangeAction.Fire, Vector3.up), "Second-round completion receipt rejected.");
            while (range.IsBusy || range.IsConnected) yield return null;
            Require(range.CurrentMission == null && range.Phase == SessionPhase.Stopped, "Host shutdown left mission geometry/state."); Record("host-shutdown-observed");
        }
    }
}
