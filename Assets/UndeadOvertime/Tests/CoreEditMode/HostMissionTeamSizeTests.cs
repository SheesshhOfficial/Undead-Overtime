using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    /// <summary>
    /// Deterministic host-rule/codec simulation, not ten network peers or Unity acceptance.
    /// Poses and damage are synthetic, trusted host observations. LOS/placement explicitly return true;
    /// there is no collision, raycast, movement, socket, rendering, wall-clock or performance validation.
    /// Every playing packet comes from the actual coordinator after its coherent tick, never a fabricated frame.
    /// </summary>
    public sealed class HostMissionTeamSizeTests
    {
        private static AimVector Power => new AimVector(-9, HostMoveGate.EyeHeight, -5);
        private static AimVector Exit => new AimVector(0, HostMoveGate.EyeHeight, 12);
        private static AimVector Forward => new AimVector(0, 0, 1);

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
        public void FullMissionUsesSharedPowerThreeOwnedWavesAndEveryConnectedSurvivor(int teamSize)
        {
            var simulation = new MissionSimulation(teamSize);
            simulation.RestorePower();
            simulation.ClearThreeWaves();

            // Even the host (and the only survivor in solo) must be inside. Each slot independently
            // blocks extraction for longer than its required hold while every other slot is inside.
            foreach (PlayerTicket missing in simulation.Session.SnapshotMembers())
            {
                simulation.GatherAtExitExcept(missing);
                for (int tick = 0; tick <= 40; tick++)
                {
                    simulation.Tick();
                    Assert.That(simulation.LastFrame.Mission.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
                    Assert.That(simulation.LastFrame.Mission.HoldSeconds, Is.Zero);
                }
            }
            simulation.GatherAtExit();
            simulation.CompleteExtraction();
            Assert.That(simulation.Placements, Is.EqualTo(3));
            Assert.That(simulation.MaximumPayloadBytes, Is.EqualTo(ExpectedBytes(teamSize, 4)));
            if (teamSize == LanSession.MaximumPlayers)
                Assert.That(simulation.MaximumPayloadBytes, Is.EqualTo(CoopMissionProtocol.MaximumFrameBytes));
            simulation.Report("full mission", teamSize, 1);
        }

        [TestCase(2)] [TestCase(5)] [TestCase(10)]
        public void RepeatedDisconnectRejoinAndLobbyResetRetireActualCapturedFrames(int teamSize)
        {
            var simulation = new MissionSimulation(teamSize);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                simulation.RestorePower();
                simulation.ClearThreeWaves();
                PlayerTicket[] roster = simulation.Session.SnapshotMembers();
                PlayerTicket departing = roster[roster.Length - 1];
                simulation.GatherAtExitExcept(departing);
                for (int tick = 0; tick <= 40; tick++) simulation.Tick();
                Assert.That(simulation.LastFrame.Mission.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
                Assert.That(simulation.LastFrame.Mission.HoldSeconds, Is.Zero);

                byte[] beforeDisconnect = simulation.LastPayload;
                Assert.That(simulation.Session.Disconnect(departing), Is.True);
                Assert.That(simulation.Session.TryJoin(departing.PeerId, LanRangeProtocol.Version, out _), Is.EqualTo(JoinResult.NotInLobby));
                Assert.That(simulation.Mission.TrySetPowerIntent(departing, simulation.Session.SessionId,
                    simulation.Session.RoundId, ulong.MaxValue, true, simulation.Now), Is.False);
                Assert.That(simulation.Mission.SetPlayerPositionFromHost(departing, Exit, simulation.Now), Is.False);
                Assert.That(simulation.Fire.SetPoseFromHost(departing, Exit, Forward), Is.False);
                simulation.CompleteExtraction(); // The remaining connected team still needs a fresh two-second hold.
                Assert.That(simulation.LastFrame.Range.MemberCount, Is.EqualTo(teamSize - 1));
                MissionFrame completed = simulation.Replica.Mission;
                Assert.That(simulation.Replica.TryAcceptHostPacket(beforeDisconnect), Is.False);
                Assert.That(simulation.Replica.Mission, Is.SameAs(completed));

                HostCoopMission retiredMission = simulation.Mission;
                HostFireGate retiredFire = simulation.Fire;
                byte[] retiredFrame = simulation.LastPayload;
                Assert.That(simulation.Session.TryReturnToLobby(simulation.Session.Host), Is.True);
                simulation.BindLobby();
                Assert.That(simulation.Replica.Mission, Is.Null);
                Assert.That(simulation.Replica.TryAcceptHostPacket(retiredFrame), Is.False);
                Assert.That(retiredMission.TryCaptureFrame(simulation.Now, retiredFire, simulation.Slots,
                    simulation.Targets, out MissionFrame invalidFrame, out byte[] invalidPayload), Is.False);
                Assert.That(invalidFrame, Is.Null); Assert.That(invalidPayload, Is.Null);

                byte reusedSlot = simulation.Slots[departing];
                Assert.That(simulation.Session.TryJoin(departing.PeerId, LanRangeProtocol.Version, out PlayerTicket replacement), Is.EqualTo(JoinResult.Accepted));
                Assert.That(replacement.PeerId, Is.EqualTo(departing.PeerId));
                Assert.That(replacement.ConnectionId, Is.GreaterThan(departing.ConnectionId));
                Assert.That(simulation.Session.IsCurrent(departing), Is.False);
                Assert.That(simulation.Session.Disconnect(departing), Is.False);
                simulation.ReplaceSlot(departing, replacement, reusedSlot);
                simulation.BindLobby();
                Assert.That(simulation.Replica.Range.MemberCount, Is.EqualTo(teamSize));
                Assert.That(simulation.Replica.TryAcceptHostPacket(retiredFrame), Is.False);

                if (cycle == 2) continue;
                simulation.BeginRound();
                Assert.That(retiredMission.TrySetPowerIntent(replacement, simulation.Session.SessionId,
                    simulation.Session.RoundId, 1, true, simulation.Now), Is.False);
                Assert.That(retiredMission.AdvanceFromHost(simulation.Now, (_, from, to) => true, _ => true), Is.False);
                Assert.That(retiredFire.IsCurrentRound, Is.False);
                Assert.That(simulation.Mission.TrySetPowerIntent(departing, simulation.Session.SessionId,
                    simulation.Session.RoundId, ulong.MaxValue, true, simulation.Now), Is.False);
                Assert.That(simulation.Mission.SetPlayerPositionFromHost(departing, Power, simulation.Now), Is.False);
                simulation.Tick(powerHeldByAll: true);
                Assert.That(simulation.LastFrame.Sequence, Is.EqualTo(1));
                Assert.That(simulation.LastFrame.Mission.Phase, Is.EqualTo(CoopMissionPhase.RestorePower));
                Assert.That(simulation.LastFrame.Mission.DepotGateUnlocked, Is.False);
                Assert.That(simulation.LastFrame.Mission.HoldSeconds, Is.Zero);
                Assert.That(simulation.LastFrame.Range.Encounter.Phase, Is.EqualTo(EncounterPhase.Dormant));
                Assert.That(simulation.LastFrame.Range.EnemyCount, Is.Zero);
                Assert.That(simulation.LastFrame.Range.Horde.Phase, Is.EqualTo(HordePhase.Disabled));
                MissionFrame freshRound = simulation.Replica.Mission;
                Assert.That(simulation.Replica.TryAcceptHostPacket(retiredFrame), Is.False);
                Assert.That(simulation.Replica.Mission, Is.SameAs(freshRound));
                for (int i = 0; i < freshRound.Range.MemberCount; i++)
                {
                    Assert.That(freshRound.Range.Member(i).Ammo, Is.EqualTo(HostFireGate.MagazineSize));
                    Assert.That(freshRound.Range.Member(i).Health, Is.EqualTo(100));
                    Assert.That(freshRound.Range.Member(i).Reloading, Is.False);
                }
            }
            Assert.That(simulation.Session.RoundId, Is.EqualTo(3));
            Assert.That(simulation.Session.Phase, Is.EqualTo(SessionPhase.Lobby));
            Assert.That(simulation.TotalPlacements, Is.EqualTo(9));
            Assert.That(simulation.MaximumPayloadBytes, Is.EqualTo(ExpectedBytes(teamSize, 4)));
            simulation.Report("disconnect/rejoin/reset", teamSize, 3);
        }

        private static int ExpectedBytes(int members, int enemies) => CoopMissionProtocol.FrameHeaderBytes +
            LanRangeProtocol.SnapshotHeaderBytes + members * LanRangeProtocol.MemberBytes + enemies * LanRangeProtocol.EnemyBytes;

        private static void AssertPosition(AimVector actual, AimVector expected)
        {
            // Position fields use IEEE float on the wire, while host vectors use double.
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(1e-6));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(1e-6));
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(1e-6));
        }

        private sealed class MissionSimulation
        {
            public readonly LanSession Session;
            public readonly CoopMissionSessionReplica Replica = new CoopMissionSessionReplica();
            public readonly Dictionary<PlayerTicket, byte> Slots = new Dictionary<PlayerTicket, byte>();
            public readonly int[] Targets = { 100, 50, 0 };
            private readonly Dictionary<PlayerTicket, AimVector> positions = new Dictionary<PlayerTicket, AimVector>();
            private readonly Dictionary<PlayerTicket, ulong> powerSequences = new Dictionary<PlayerTicket, ulong>();
            private int clockTick, powerTicks, framesCaptured;
            private ulong captureSequence;
            public double Now => clockTick / 20d;
            public HostCoopMission Mission { get; private set; }
            public HostFireGate Fire { get; private set; }
            public MissionFrame LastFrame { get; private set; }
            public byte[] LastPayload { get; private set; }
            public int Placements { get; private set; }
            public int TotalPlacements { get; private set; }
            public int MaximumPayloadBytes { get; private set; }

            public MissionSimulation(int teamSize)
            {
                Session = new LanSession(LanRangeProtocol.Version, teamSize);
                PlayerTicket host = Session.StartHost(0); Slots.Add(host, 0);
                for (int i = 1; i < teamSize; i++)
                {
                    Assert.That(Session.TryJoin((ulong)(100 + i), LanRangeProtocol.Version, out PlayerTicket ticket), Is.EqualTo(JoinResult.Accepted));
                    Slots.Add(ticket, (byte)i);
                }
                Assert.That(Session.PlayerCount, Is.EqualTo(teamSize)); // Capacity includes the host.
                Assert.That(Session.TryJoin(999, LanRangeProtocol.Version, out _), Is.EqualTo(JoinResult.Full));
                BindLobby();
                BeginRound();
            }

            public void BindLobby()
            {
                Assert.That(Session.Phase, Is.EqualTo(SessionPhase.Lobby));
                PlayerTicket[] roster = Session.SnapshotMembers();
                var rows = new RangeMember[roster.Length];
                for (int i = 0; i < rows.Length; i++)
                    rows[i] = new RangeMember(roster[i].PeerId, roster[i].ConnectionId, Slots[roster[i]], 0, false);
                byte[] lobby = LanRangeProtocol.Encode(new RangeSnapshot(Session.SessionId, Session.RoundId,
                    Session.Phase, rows, Targets));
                Assert.That(Replica.TryAcceptHostPacket(lobby), Is.True);
                Assert.That(Replica.Range.RoundId, Is.EqualTo(Session.RoundId));
                Assert.That(Replica.Range.Phase, Is.EqualTo(SessionPhase.Lobby));
            }

            public void BeginRound()
            {
                Assert.That(Session.TryBeginRound(Session.Host), Is.True);
                Mission = new HostCoopMission(Session, Now, Power, Exit);
                Fire = new HostFireGate(Session, Now);
                positions.Clear(); powerSequences.Clear();
                captureSequence = 0; powerTicks = 0; Placements = 0;
                foreach (PlayerTicket ticket in Session.SnapshotMembers())
                {
                    positions.Add(ticket, Offset(Power, Slots[ticket]));
                    powerSequences.Add(ticket, 0);
                }
            }

            public void ReplaceSlot(PlayerTicket oldTicket, PlayerTicket replacement, byte slot)
            {
                Slots.Remove(oldTicket); positions.Remove(oldTicket); powerSequences.Remove(oldTicket);
                Slots.Add(replacement, slot);
            }

            private static AimVector Offset(AimVector center, byte slot) =>
                new AimVector(center.X + slot * .125, center.Y, center.Z);

            public void GatherAtExitExcept(PlayerTicket absent)
            {
                foreach (PlayerTicket ticket in Session.SnapshotMembers())
                    positions[ticket] = Offset(ticket.Equals(absent) ? Power : Exit, Slots[ticket]);
            }

            public void GatherAtExit()
            {
                foreach (PlayerTicket ticket in Session.SnapshotMembers()) positions[ticket] = Offset(Exit, Slots[ticket]);
            }

            public void Tick(bool powerHeldByAll = false)
            {
                foreach (PlayerTicket ticket in Session.SnapshotMembers())
                {
                    Assert.That(Mission.SetPlayerPositionFromHost(ticket, positions[ticket], Now), Is.True);
                    Assert.That(Fire.SetPoseFromHost(ticket, positions[ticket], Forward), Is.True);
                    if (!powerHeldByAll) continue;
                    var command = new MissionPowerCommand(Session.SessionId, Session.RoundId, ++powerSequences[ticket], true);
                    byte[] bytes = CoopMissionProtocol.Encode(command);
                    Assert.That(bytes.Length, Is.EqualTo(CoopMissionProtocol.PowerCommandBytes));
                    Assert.That(CoopMissionProtocol.TryPowerCommand(bytes, out MissionPowerCommand decoded), Is.True);
                    Assert.That(Mission.TrySetPowerIntent(ticket, decoded.SessionId, decoded.RoundId,
                        decoded.Sequence, decoded.Held, Now), Is.True);
                }
                if (powerHeldByAll) powerTicks++;
                Assert.That(Mission.AdvanceFromHost(Now, (_, from, to) => true, PlaceWithSyntheticClearance), Is.True);
                foreach (PlayerTicket ticket in Session.SnapshotMembers())
                    Assert.That(Fire.SetAliveFromHost(ticket, Mission.Encounter.PlayerHealth(ticket) > 0), Is.True);
                Assert.That(Fire.AdvanceReloadsFromHost(Now), Is.True);
                CaptureAndConsume();
                clockTick++;
            }

            private bool PlaceWithSyntheticClearance(HostInfectedEncounter encounter)
            {
                Assert.That(encounter, Is.SameAs(Mission.Encounter));
                Placements++; TotalPlacements++;
                Assert.That(Placements, Is.LessThanOrEqualTo(3));
                Assert.That(encounter.HordeAt(Now).Wave, Is.EqualTo(Placements));
                Assert.That(encounter.EnemyCount, Is.EqualTo(Placements + 1));
                // This is an explicit successful host-physics stub, not a physics or collision check.
                foreach (EnemySnapshot enemy in encounter.SnapshotEnemies())
                    Assert.That(encounter.SetEnemyPositionFromHost(enemy.Id, enemy.Position), Is.True);
                return true;
            }

            public void RestorePower()
            {
                // 61 observations at 20 Hz span three seconds; all ten inputs still share that duration.
                // A restarted round may already have its first zero-credit observation.
                for (int tick = powerTicks; tick <= 60; tick++)
                {
                    Tick(powerHeldByAll: true);
                    if (tick == 60) continue;
                    Assert.That(LastFrame.Mission.Phase, Is.EqualTo(CoopMissionPhase.RestorePower));
                    Assert.That(LastFrame.Mission.DepotGateUnlocked, Is.False);
                    Assert.That(LastFrame.Mission.HoldSeconds, Is.EqualTo(tick / 20d).Within(1e-8));
                    Assert.That(Placements, Is.Zero);
                }
                Assert.That(LastFrame.Mission.Phase, Is.EqualTo(CoopMissionPhase.DefendDepot));
                Assert.That(LastFrame.Mission.DepotGateUnlocked, Is.True);
                Assert.That(Placements, Is.EqualTo(1));
            }

            public void ClearThreeWaves()
            {
                var seenIds = new HashSet<byte>();
                int killed = 0;
                for (int wave = 1; wave <= 3; wave++)
                {
                    Assert.That(Mission.Encounter.HordeAt(Now).Wave, Is.EqualTo(wave));
                    Assert.That(Mission.Encounter.HordeAt(Now).Phase, Is.EqualTo(HordePhase.Fighting));
                    EnemySnapshot[] enemies = Mission.Encounter.SnapshotEnemies();
                    Assert.That(enemies.Length, Is.EqualTo(wave + 1));
                    foreach (EnemySnapshot enemy in enemies)
                    {
                        Assert.That(seenIds.Add(enemy.Id), Is.True);
                        Assert.That(enemy.Health, Is.EqualTo(100));
                        PlayerTicket[] roster = Session.SnapshotMembers();
                        PlayerTicket shooter = roster[killed % roster.Length];
                        // Host-applied synthetic damage, not client-authorized damage or a raycast test.
                        Assert.That(Mission.Encounter.DamageEnemyFromHost(shooter, enemy.Id, 25, Now), Is.True);
                        Tick();
                        Assert.That(Mission.Encounter.DamageEnemyFromHost(shooter, enemy.Id, 75, Now), Is.True);
                        Tick(); killed++;
                    }
                    if (wave == 3) continue;
                    Assert.That(LastFrame.Range.Horde.Phase, Is.EqualTo(HordePhase.Intermission));
                    Assert.That(LastFrame.Range.Horde.IntermissionSeconds, Is.EqualTo(HostInfectedEncounter.IntermissionDuration).Within(1e-9));
                    // Observe every intermission tick. Include one tick beyond the exact boundary to avoid
                    // making this test depend on addition versus division rounding of that boundary.
                    for (int tick = 1; tick <= 101; tick++)
                    {
                        Tick();
                        if (tick >= 100) continue;
                        Assert.That(LastFrame.Range.Horde.Phase, Is.EqualTo(HordePhase.Intermission));
                        Assert.That(Placements, Is.EqualTo(wave));
                    }
                    Assert.That(Placements, Is.EqualTo(wave + 1));
                }
                Assert.That(killed, Is.EqualTo(9)); Assert.That(seenIds.Count, Is.EqualTo(9));
                Assert.That(Placements, Is.EqualTo(3));
                Assert.That(LastFrame.Mission.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
                Assert.That(LastFrame.Range.Horde.Phase, Is.EqualTo(HordePhase.Completed));
                Assert.That(LastFrame.Range.Encounter.Phase, Is.EqualTo(EncounterPhase.Cleared));
            }

            public void CompleteExtraction()
            {
                for (int tick = 0; tick <= 40; tick++)
                {
                    Tick();
                    if (tick == 40) continue;
                    Assert.That(LastFrame.Mission.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
                    Assert.That(LastFrame.Mission.HoldSeconds, Is.EqualTo(tick / 20d).Within(1e-8));
                }
                Assert.That(LastFrame.Mission.Phase, Is.EqualTo(CoopMissionPhase.Completed));
                Assert.That(LastFrame.Mission.HoldSeconds, Is.Zero);
                Assert.That(Mission.AdvanceFromHost(Now, (_, from, to) => true, PlaceWithSyntheticClearance), Is.False);
                Assert.That(Placements, Is.EqualTo(3));
            }

            private void CaptureAndConsume()
            {
                Assert.That(Mission.TryCaptureFrame(Now, Fire, Slots, Targets, out MissionFrame frame, out byte[] payload), Is.True);
                captureSequence++; framesCaptured++;
                Assert.That(frame.Sequence, Is.EqualTo(captureSequence));
                AssertMatchesHost(frame);
                Assert.That(payload.Length, Is.EqualTo(ExpectedBytes(Session.PlayerCount, Mission.Encounter.EnemyCount)));
                Assert.That(payload.Length, Is.LessThanOrEqualTo(CoopMissionProtocol.MaximumFrameBytes));
                MaximumPayloadBytes = Math.Max(MaximumPayloadBytes, payload.Length);
                Assert.That(CoopMissionProtocol.TryFrame(payload, out MissionFrame decoded), Is.True);
                Assert.That(decoded.Sequence, Is.EqualTo(frame.Sequence));
                AssertMatchesHost(decoded);
                Assert.That(CoopMissionProtocol.Encode(decoded), Is.EqualTo(payload));
                Assert.That(Replica.TryAcceptHostPacket(payload), Is.True);
                Assert.That(Replica.Range, Is.SameAs(Replica.Mission.Range));
                Assert.That(CoopMissionProtocol.Encode(Replica.Mission), Is.EqualTo(payload));
                LastFrame = frame; LastPayload = payload;
            }

            private void AssertMatchesHost(MissionFrame frame)
            {
                CoopMissionSnapshot mission = Mission.Snapshot;
                RangeSnapshot range = frame.Range;
                Assert.That(frame.Mission.SessionId, Is.EqualTo(mission.SessionId));
                Assert.That(frame.Mission.RoundId, Is.EqualTo(mission.RoundId));
                Assert.That(frame.Mission.IsCurrent, Is.True);
                Assert.That(frame.Mission.Phase, Is.EqualTo(mission.Phase));
                Assert.That(frame.Mission.DepotGateUnlocked, Is.EqualTo(mission.DepotGateUnlocked));
                Assert.That(frame.Mission.HoldSeconds, Is.EqualTo(mission.HoldSeconds).Within(1e-6));
                Assert.That(range.SessionId, Is.EqualTo(Session.SessionId));
                Assert.That(range.RoundId, Is.EqualTo(Session.RoundId));
                Assert.That(range.Phase, Is.EqualTo(SessionPhase.Playing));
                PlayerTicket[] roster = Session.SnapshotMembers();
                Assert.That(range.MemberCount, Is.EqualTo(roster.Length));
                var usedSlots = new HashSet<byte>();
                for (int i = 0; i < roster.Length; i++)
                {
                    PlayerTicket ticket = roster[i]; RangeMember member = range.Member(i);
                    Assert.That(member.PeerId, Is.EqualTo(ticket.PeerId));
                    Assert.That(member.ConnectionId, Is.EqualTo(ticket.ConnectionId));
                    Assert.That(member.Slot, Is.EqualTo(Slots[ticket]));
                    Assert.That(usedSlots.Add(member.Slot), Is.True);
                    Assert.That(member.Ammo, Is.EqualTo(Fire.Ammo(ticket)));
                    Assert.That(member.Reloading, Is.EqualTo(Fire.IsReloading(ticket)));
                    Assert.That(member.Health, Is.EqualTo(Mission.Encounter.PlayerHealth(ticket)));
                    Assert.That(member.ReviveTargetSlot, Is.EqualTo(255));
                    Assert.That(member.ReviveProgress, Is.Zero);
                    AssertPosition(member.Position, positions[ticket]);
                }
                for (int i = 0; i < Targets.Length; i++) Assert.That(range.TargetHealth(i), Is.EqualTo(Targets[i]));
                Assert.That(range.Encounter.Phase, Is.EqualTo(Mission.Encounter.Phase));
                Assert.That(range.EnemyCount, Is.EqualTo(Mission.Encounter.EnemyCount));
                for (int i = 0; i < range.EnemyCount; i++)
                {
                    EnemySnapshot expected = Mission.Encounter.Enemy(i), actual = range.Enemy(i);
                    Assert.That(actual.Id, Is.EqualTo(expected.Id)); Assert.That(actual.Health, Is.EqualTo(expected.Health));
                    AssertPosition(actual.Position, expected.Position);
                }
                HordeSnapshot horde = Mission.Encounter.HordeAt(Now);
                Assert.That(range.Horde.Phase, Is.EqualTo(horde.Phase));
                Assert.That(range.Horde.Wave, Is.EqualTo(horde.Wave)); Assert.That(range.Horde.Total, Is.EqualTo(horde.Total));
                Assert.That(range.Horde.IntermissionSeconds, Is.EqualTo(horde.IntermissionSeconds).Within(1e-6));
            }

            public void Report(string scenario, int teamSize, int completedRounds)
            {
                TestContext.Progress.WriteLine("Deterministic host-rule/codec simulation: scenario=" + scenario +
                    "; team_including_host=" + teamSize + "; frames_captured=" + framesCaptured +
                    "; max_payload_bytes=" + MaximumPayloadBytes + "; wave_placements=" + TotalPlacements +
                    "; rounds_started=" + Session.RoundId + "; rounds_completed=" + completedRounds + ".");
            }
        }
    }
}
