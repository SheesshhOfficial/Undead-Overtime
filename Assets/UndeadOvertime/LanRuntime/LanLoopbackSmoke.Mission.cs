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
        private IEnumerator HostMissionRun()
        {
            Require(range.SetMissionMode(true), "Could not select mission mode.");
            Require(range.StartHosting("127.0.0.1", options.Port), "Mission host failed."); Record("host-started");
            while (range.MemberCount < 2) yield return null;
            Require(range.MemberCount == 2, "Unexpected mission peer count."); Record("two-process-membership");
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
            range.StopSession("Mission smoke complete."); while (range.IsBusy) yield return null;
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
            Record("replicated-mission-completed"); SendMissionReceipt();
            while (range.IsBusy || range.IsConnected) yield return null;
            Require(range.CurrentMission == null && range.Phase == SessionPhase.Stopped, "Host shutdown left mission geometry/state."); Record("host-shutdown-observed");
        }
    }
}
