using System;
using System.Collections.Generic;
using UndeadOvertime.Core;
using Unity.Netcode;
using UnityEngine;

namespace UndeadOvertime.LanPrototype
{
    public sealed partial class LanRange
    {
        private const string MissionPowerMessage = "uo.mission.power.v1", MissionSnapshotMessage = "uo.mission.state.v1";
        private const ushort MissionTransportVersion = 101;
        private HostCoopMission hostMission;
        private CoopMissionSessionReplica missionReplica;
        private DepotMissionPresentation missionPresentation;
        private DepotPerimeterPresentation missionPerimeter;
        private MissionFrame missionFrame;
        private Material missionPartitionMaterial, missionControlMaterial, missionExtractionMaterial;
        private ulong powerSequence;
        public bool MissionMode { get; private set; }
        public MissionFrame CurrentMission => missionFrame;
        public bool MissionGateOpen => missionPresentation != null && missionPresentation.GateUnlocked;
        public string MissionNavigationText
        {
            get
            {
                if (!MissionMode || !IsConnected || Phase != SessionPhase.Playing || missionFrame == null || worldCamera == null ||
                    !TryGetLocalMember(out RangeMember player) || player.Health == 0 ||
                    !MissionNavigationHint.TryCreate(missionFrame.Mission, player.Position, ToCore(worldCamera.transform.forward),
                        DepotMissionPresentation.PowerControl, DepotMissionPresentation.ExtractionCenter, out MissionNavigationHint hint)) return "";
                return "Waypoint: " + hint.GoalText + " | " + Math.Ceiling(hint.DistanceMeters).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    " m | " + hint.BearingText;
            }
        }
        private bool MissionTerminal => MissionMode && (hostMission != null ? Terminal(hostMission.Snapshot.Phase) : missionFrame != null && Terminal(missionFrame.Mission.Phase));
        private static bool Terminal(CoopMissionPhase phase) => phase == CoopMissionPhase.Completed || phase == CoopMissionPhase.Failed;
        public bool SetMissionMode(bool enabled)
        {
            if (IsBusy || IsConnected || snapshot != null) return false;
            MissionMode = enabled; ApplyVisuals(); return true;
        }
        private void ConfigureSessionProtocol()
        {
            // A fresh configuration also avoids a cached old compatibility hash on a reused idle manager.
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport, ConnectionApproval = true, EnableSceneManagement = false,
                ForceSamePrefabs = false, ProtocolVersion = MissionMode ? MissionTransportVersion : LanRangeProtocol.Version,
                ConnectionData = MissionMode ? CoopMissionProtocol.Handshake : LanRangeProtocol.Handshake,
                ClientConnectionBufferTimeout = 8, PlayerPrefab = null, TickRate = 30
            };
        }
        private bool AcceptSessionHandshake(byte[] bytes) => MissionMode ? CoopMissionProtocol.AcceptHandshake(bytes) : LanRangeProtocol.AcceptHandshake(bytes);
        private void RegisterMissionHandlers()
        {
            manager.CustomMessagingManager.RegisterNamedMessageHandler(MissionPowerMessage, ReceiveMissionPower);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(MissionSnapshotMessage, ReceiveMissionState);
        }
        private void UnregisterMissionHandlers()
        {
            if (manager?.CustomMessagingManager == null) return;
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(MissionPowerMessage);
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(MissionSnapshotMessage);
        }
        private void ClearMissionRound()
        {
            missionPerimeter?.Dispose(); missionPerimeter = null;
            missionPresentation?.Dispose(); missionPresentation = null; hostMission = null; missionFrame = null; powerSequence = 0;
        }
        private void ResetMissionSession() { ClearMissionRound(); missionReplica = new CoopMissionSessionReplica(); }
        private void CreateMissionPresentation(long sessionId, long roundId)
        {
            Material concrete = PresentationEnabled ? DepotFloorPresentation.LoadConcreteMaterial() : null;
            missionPresentation = new DepotMissionPresentation(generated, sessionId, roundId, PresentationEnabled,
                missionPartitionMaterial, missionControlMaterial, missionExtractionMaterial, concrete);
            if (PresentationEnabled) missionPerimeter = new DepotPerimeterPresentation(generated, concrete);
        }
        private void BeginMissionRound(double now)
        {
            ClearMissionRound(); CreateMissionPresentation(session.SessionId, session.RoundId);
            hostMission = new HostCoopMission(session, now, DepotMissionPresentation.PowerControl, DepotMissionPresentation.ExtractionCenter);
            encounter = hostMission.Encounter;
            foreach (MemberState member in members.Values)
                if (member.Connected) hostMission.SetPlayerPositionFromHost(member.Ticket, ToCore(member.Position), now);
            missionPresentation.TryApply(hostMission.Snapshot);
        }
        public bool SendPowerIntent(bool held)
        {
            if (!MissionMode || !IsConnected || snapshot == null || Phase != SessionPhase.Playing || MissionTerminal || powerSequence == ulong.MaxValue) return false;
            var command = new MissionPowerCommand(snapshot.SessionId, snapshot.RoundId, ++powerSequence, held);
            if (IsHosting) HandleMissionPower(manager.LocalClientId, command);
            else Send(MissionPowerMessage, NetworkManager.ServerClientId, CoopMissionProtocol.Encode(command), NetworkDelivery.ReliableSequenced);
            return true;
        }
        private void ReceiveMissionPower(ulong sender, FastBufferReader reader)
        {
            if (!MissionMode || !IsHosting || hostMission == null ||
                !TryReadPayload(reader, CoopMissionProtocol.PowerCommandBytes, CoopMissionProtocol.PowerCommandBytes, out byte[] bytes)) return;
            if (CoopMissionProtocol.TryPowerCommand(bytes, out MissionPowerCommand command)) HandleMissionPower(sender, command);
        }
        private void HandleMissionPower(ulong sender, MissionPowerCommand command)
        {
            if (!MissionMode || !IsHosting || hostMission == null || !members.TryGetValue(sender, out MemberState member) || !member.Connected) return;
            // Power and combat deliberately have independent monotonic sequence domains and pacing.
            hostMission.TrySetPowerIntent(member.Ticket, command.SessionId, command.RoundId, command.Sequence, command.Held, Time.realtimeSinceStartupAsDouble);
        }
        private void AdvanceMission(double now)
        {
            if (hostMission == null) return;
            hostMission.AdvanceFromHost(now, (ticket, eye, control) => missionPresentation.HasPowerLine(eye), owned =>
            {
                // The power transition opens physical passage before searching for collision-safe new enemies.
                if (!ReferenceEquals(owned, encounter) || !missionPresentation.TryApply(hostMission.Snapshot)) return false;
                return PlaceWaveFromHost();
            });
            if (!missionPresentation.TryApply(hostMission.Snapshot)) throw new InvalidOperationException("Host mission geometry rejected its current state.");
            if (MissionTerminal) ReleaseControls();
        }
        private void PublishMissionFrame()
        {
            if (hostMission == null) return;
            var slots = new Dictionary<PlayerTicket, byte>();
            foreach (MemberState member in members.Values) if (member.Connected) slots.Add(member.Ticket, member.Slot);
            if (!hostMission.TryCaptureFrame(Time.realtimeSinceStartupAsDouble, fireGate, slots, health, out MissionFrame frame, out byte[] bytes)) return;
            missionFrame = frame; ApplySnapshot(frame.Range);
            for (int i = 0; i < frame.Range.MemberCount; i++)
                if (frame.Range.Member(i).PeerId != manager.LocalClientId)
                    Send(MissionSnapshotMessage, frame.Range.Member(i).PeerId, bytes, NetworkDelivery.ReliableSequenced);
        }
        private void ReceiveMissionState(ulong sender, FastBufferReader reader)
        {
            if (!MissionMode || manager == null || manager.IsServer || stopping || sender != NetworkManager.ServerClientId ||
                !TryReadPayload(reader, LanRangeProtocol.SnapshotHeaderBytes, CoopMissionProtocol.MaximumFrameBytes, out byte[] bytes)) return;
            // The same reliable-sequenced named channel carries both lobby binding and coherent playing frames.
            RangeSnapshot range;
            if (CoopMissionProtocol.TryFrame(bytes, out MissionFrame decoded)) range = decoded.Range;
            else if (!LanRangeProtocol.TrySnapshot(bytes, out range)) return;
            bool containsLocal = false;
            for (int i = 0; i < range.MemberCount; i++) if (range.Member(i).PeerId == manager.LocalClientId) containsLocal = true;
            if (!containsLocal || !missionReplica.TryAcceptHostPacket(bytes)) return;
            try
            {
                MissionFrame accepted = missionReplica.Mission;
                if (accepted == null) ClearMissionRound();
                else
                {
                    if (missionFrame == null || missionFrame.Range.RoundId != accepted.Range.RoundId)
                    { ClearMissionRound(); CreateMissionPresentation(accepted.Range.SessionId, accepted.Range.RoundId); }
                    if (!missionPresentation.TryApply(accepted.Mission)) throw new InvalidOperationException("Client mission geometry rejected validated host state.");
                    missionFrame = accepted;
                }
                ApplySnapshot(missionReplica.Range);
            }
            catch { StopSession("Mission presentation failed; inspect the reported exception."); throw; }
        }
        // A small explicit gate waypoint, not NavMesh/pathfinding for a city. Direct pursuit elsewhere still uses physics sweeps.
        public static Vector3 MissionChaseStep(Vector3 origin, Vector3 target, Vector3 originalStep)
        {
            if (!HostMoveGate.ValidPosition(ToCore(origin)) || !HostMoveGate.ValidPosition(ToCore(target)) || !ToCore(originalStep).IsFinite || originalStep.y != 0) return Vector3.zero;
            float gate = DepotMissionPresentation.PartitionZ;
            bool north = origin.z >= gate;
            if ((north && target.z >= gate) || (!north && target.z < gate)) return originalStep;
            // Reach the center corridor on the current side, then cross to the other side.
            Vector3 waypoint = new Vector3(0, origin.y, gate + (north ? 1.2f : -1.2f));
            if (Mathf.Abs(origin.x) < 1.1f) waypoint.z = gate + (north ? -1.2f : 1.2f);
            Vector3 toward = waypoint - origin; toward.y = 0;
            return Vector3.ClampMagnitude(toward, originalStep.magnitude);
        }
        private string MissionObjectiveText
        {
            get
            {
                if (missionFrame == null) return "Restore power, survive three waves, then bring everyone to extraction.";
                CoopMissionSnapshot state = missionFrame.Mission;
                switch (state.Phase)
                {
                    case CoopMissionPhase.RestorePower: return "POWER: approach the orange console and hold F while stationary. " + Math.Floor(state.HoldSeconds / 3 * 100) + "%";
                    case CoopMissionPhase.DefendDepot: return "DEFEND: clear all three waves. The central gate is open for backtracking.";
                    case CoopMissionPhase.ReachExtraction: return "EXTRACT: gather every living survivor on the blue north marker. " + Math.Floor(state.HoldSeconds / 2 * 100) + "%";
                    case CoopMissionPhase.Completed: return "MISSION COMPLETE. Everyone escaped the depot. Host can return to lobby.";
                    default: return "MISSION FAILED. Host can return to lobby and restart.";
                }
            }
        }
    }
}
