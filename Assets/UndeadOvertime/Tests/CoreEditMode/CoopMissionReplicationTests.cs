using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    // In-memory integration only: transport identity, LOS, hit resolution and placement are test doubles.
    // The production coordinator, life/revive/waves/fire gates, codecs and replicas are exercised together.
    public sealed class CoopMissionReplicationTests
    {
        private sealed class Simulation
        {
            public readonly LanSession Session = new LanSession(CoopMissionProtocol.Version);
            public readonly PlayerTicket[] Tickets;
            public readonly AimVector[] Positions;
            public readonly CoopMissionReplica[] Replicas;
            public HostCoopMission Mission;
            public HostFireGate Fire;
            public int Tick, Placements, ReloadStarts, AuthorizedShots, Frames, MaximumPacket;
            public bool PlaceSucceeds = true, LineClear = true;
            public byte[] LastPacket;
            private readonly ulong[] powerSequences, fireSequences;
            public double Now => Tick * .05;
            public static AimVector Power => new AimVector(-9, HostMoveGate.EyeHeight, -5);
            public static AimVector Exit => new AimVector(0, HostMoveGate.EyeHeight, 12);
            private static AimVector Aim => new AimVector(0, 0, 1);

            public Simulation(int count)
            {
                Tickets = new PlayerTicket[count]; Positions = new AimVector[count]; Replicas = new CoopMissionReplica[count];
                powerSequences = new ulong[count]; fireSequences = new ulong[count];
                Tickets[0] = Session.StartHost(0);
                for (int i = 1; i < count; i++) Assert.That(Session.TryJoin((ulong)i, CoopMissionProtocol.Version, out Tickets[i]), Is.EqualTo(JoinResult.Accepted));
                Assert.That(Session.TryBeginRound(Tickets[0]), Is.True);
                Mission = new HostCoopMission(Session, Now, Power, Exit); Fire = new HostFireGate(Session, Now);
                for (int i = 0; i < count; i++)
                { Positions[i] = HostMoveGate.Spawn(i); Replicas[i] = new CoopMissionReplica(Session.SessionId, Session.RoundId); }
                Positions[0] = Power;
            }
            public int Slot(PlayerTicket ticket) => Array.IndexOf(Tickets, ticket);
            public byte[] PowerPacket(int slot, bool held) => CoopMissionProtocol.Encode(new MissionPowerCommand(Session.SessionId, Session.RoundId, ++powerSequences[slot], held));
            public bool ReceivePower(PlayerTicket hostMappedSender, byte[] bytes)
            {
                if (!CoopMissionProtocol.TryPowerCommand(bytes, out MissionPowerCommand command)) return false;
                return Mission.TrySetPowerIntent(hostMappedSender, command.SessionId, command.RoundId, command.Sequence, command.Held, Now);
            }
            public void Refresh()
            {
                foreach (PlayerTicket ticket in Session.SnapshotMembers())
                {
                    int slot = Slot(ticket);
                    Assert.That(Mission.SetPlayerPositionFromHost(ticket, Positions[slot], Now), Is.True);
                    Assert.That(Fire.SetPoseFromHost(ticket, Positions[slot], Aim), Is.True);
                    Fire.SetAliveFromHost(ticket, Mission.Encounter.PlayerHealth(ticket) > 0);
                }
                Fire.AdvanceReloadsFromHost(Now);
            }
            public MissionFrame CurrentFrame(ulong sequence = 1)
            {
                var rows = new List<RangeMember>();
                foreach (PlayerTicket ticket in Session.SnapshotMembers())
                {
                    int slot = Slot(ticket); byte targetSlot = 255, progress = 0;
                    if (Mission.Encounter.ReviveStatusFromHost(ticket, Now, out PlayerTicket target, out progress)) targetSlot = (byte)Slot(target);
                    rows.Add(new RangeMember(ticket.PeerId, ticket.ConnectionId, (byte)slot, (byte)Fire.Ammo(ticket), Fire.IsReloading(ticket),
                        Positions[slot], (byte)Mission.Encounter.PlayerHealth(ticket), targetSlot, progress));
                }
                var range = new RangeSnapshot(Session.SessionId, Session.RoundId, Session.Phase, rows.ToArray(), new[] { 100, 100, 100 },
                    Mission.Encounter.Phase, Mission.Encounter.SnapshotEnemies(), Mission.Encounter.HordeAt(Now));
                return new MissionFrame(sequence, range, Mission.Snapshot);
            }
            public void Publish(bool flush = false)
            {
                var slots = new Dictionary<PlayerTicket, byte>();
                for (byte i = 0; i < Tickets.Length; i++) slots.Add(Tickets[i], i);
                Assert.That(Mission.TryCaptureFrame(Now, Fire, slots, new[] { 100, 100, 100 }, out MissionFrame source, out byte[] bytes), Is.True);
                AssertEquivalent(CurrentFrame(source.Sequence), source); // Independently assembled expected aggregate.

                Assert.That(bytes.Length, Is.LessThanOrEqualTo(CoopMissionProtocol.MaximumFrameBytes));
                Assert.That(CoopMissionProtocol.TryFrame(bytes, out MissionFrame decoded), Is.True);
                AssertEquivalent(source, decoded); MaximumPacket = Math.Max(MaximumPacket, bytes.Length); Frames++;
                for (int i = 0; i < Replicas.Length; i++)
                {
                    if (!Session.IsCurrent(Tickets[i]) || (!flush && (source.Sequence + (ulong)i) % 5 == 0)) continue;
                    Assert.That(Replicas[i].TryAcceptHostPacket(bytes), Is.True);
                    AssertEquivalent(source, Replicas[i].Current);
                    MissionFrame accepted = Replicas[i].Current;
                    Assert.That(Replicas[i].TryAcceptHostPacket(bytes), Is.False); // Duplicate.
                    if (LastPacket != null) Assert.That(Replicas[i].TryAcceptHostPacket(LastPacket), Is.False); // Late/reordered.
                    Assert.That(Replicas[i].Current, Is.SameAs(accepted));
                }
                LastPacket = bytes;
            }
            private void SynchronizeLife()
            {
                foreach (PlayerTicket ticket in Session.SnapshotMembers())
                    Assert.That(Fire.SetAliveFromHost(ticket, Mission.Encounter.PlayerHealth(ticket) > 0), Is.True);
            }
            public FireResult FireWithoutHit(int slot, ulong sequence, double time)
            {
                return Fire.TryAuthorize(Tickets[slot], new FireRequest(Session.SessionId, Session.RoundId, sequence,
                    Positions[slot], Aim), time, out _);
            }
            public bool Step(bool power = false, Action beforeMission = null, Action afterLife = null)
            {
                Refresh();
                if (power) Assert.That(ReceivePower(Tickets[0], PowerPacket(0, true)), Is.True);
                beforeMission?.Invoke();
                SynchronizeLife(); // Damage/revive must affect admission before this host tick publishes.
                afterLife?.Invoke();
                bool result = Mission.AdvanceFromHost(Now, (_, from, to) => LineClear, encounter =>
                { Assert.That(encounter, Is.SameAs(Mission.Encounter)); Placements++; return PlaceSucceeds; });
                Publish(); Tick++; return result;
            }
            public void PowerUp()
            {
                for (int i = 0; i <= 60; i++) Step(true);
                Assert.That(Mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.DefendDepot));
            }
            public void ClearWaves()
            {
                int deadline = Tick + 1200;
                while (Mission.Snapshot.Phase == CoopMissionPhase.DefendDepot && Tick < deadline)
                {
                    if (Mission.Encounter.HordeAt(Now).Phase == HordePhase.Intermission || Fire.IsReloading(Tickets[0])) { Step(); continue; }
                    if (Fire.Ammo(Tickets[0]) == 0)
                    {
                        Step(beforeMission: () => { Assert.That(Fire.TryBeginReloadFromHost(Tickets[0], Now), Is.True); ReloadStarts++; });
                        continue;
                    }
                    EnemySnapshot target = default;
                    foreach (EnemySnapshot enemy in Mission.Encounter.SnapshotEnemies()) if (enemy.Health > 0) { target = enemy; break; }
                    Step(beforeMission: () =>
                    {
                        var request = new FireRequest(Session.SessionId, Session.RoundId, ++fireSequences[0], Positions[0], Aim);
                        Assert.That(Fire.TryAuthorize(Tickets[0], request, Now, out AuthorizedShot shot), Is.EqualTo(FireResult.Accepted));
                        AuthorizedShots++;
                        // Fixed host hit-result fixture, not a raycast or client-claimed target.
                        Assert.That(Mission.Encounter.DamageEnemyFromHost(shot.Shooter, target.Id, shot.Damage, Now), Is.True);
                        Mission.CancelPowerFromHost(Tickets[0]);
                    });
                    for (int i = 0; i < 4; i++) Step();
                }
                Assert.That(Mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
                Assert.That(Placements, Is.EqualTo(3)); Assert.That(ReloadStarts, Is.EqualTo(2));
                Assert.That(AuthorizedShots, Is.EqualTo(36)); Assert.That(Fire.Ammo(Tickets[0]), Is.Zero);
            }
            public void AllAtExit() { for (int i = 0; i < Positions.Length; i++) Positions[i] = Exit; }
            public void Extract()
            {
                AllAtExit();
                for (int i = 0; i <= 42 && Mission.Snapshot.Phase != CoopMissionPhase.Completed; i++) Step();
                Assert.That(Mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.Completed)); Publish(true);
            }
            public void Down(int victim, bool verifySameTickFire = false)
            {
                byte attacker = Mission.Encounter.Enemy(0).Id; Positions[victim] = Mission.Encounter.Enemy(0).Position;
                for (int hit = 0; hit < 10; hit++)
                {
                    Step(beforeMission: () => Assert.That(Mission.Encounter.MeleeFromHost(attacker, Tickets[victim], Now, true), Is.True),
                        afterLife: () =>
                        {
                            if (verifySameTickFire && Mission.Encounter.PlayerHealth(Tickets[victim]) == 0)
                            { Assert.That(FireWithoutHit(victim, 1, Now), Is.EqualTo(FireResult.Downed)); Assert.That(Fire.Ammo(Tickets[victim]), Is.EqualTo(12)); }
                        });
                    for (int i = 0; i < 20; i++) Step();
                }
                Assert.That(Mission.Encounter.PlayerHealth(Tickets[victim]), Is.Zero);
            }
            public static void AssertEquivalent(MissionFrame expected, MissionFrame actual)
            {
                Assert.That(actual.Sequence, Is.EqualTo(expected.Sequence));
                Assert.That(actual.Mission.SessionId, Is.EqualTo(expected.Mission.SessionId));
                Assert.That(actual.Mission.RoundId, Is.EqualTo(expected.Mission.RoundId));
                Assert.That(actual.Mission.Phase, Is.EqualTo(expected.Mission.Phase));
                Assert.That(actual.Mission.DepotGateUnlocked, Is.EqualTo(expected.Mission.DepotGateUnlocked));
                Assert.That(actual.Mission.HoldSeconds, Is.EqualTo(expected.Mission.HoldSeconds).Within(1e-6));
                Assert.That(actual.Mission.IsCurrent, Is.EqualTo(expected.Mission.IsCurrent));
                Assert.That(actual.Range.SessionId, Is.EqualTo(expected.Range.SessionId)); Assert.That(actual.Range.RoundId, Is.EqualTo(expected.Range.RoundId));
                Assert.That(actual.Range.Phase, Is.EqualTo(expected.Range.Phase)); Assert.That(actual.Range.Encounter.Phase, Is.EqualTo(expected.Range.Encounter.Phase));
                for (int i = 0; i < 3; i++) Assert.That(actual.Range.TargetHealth(i), Is.EqualTo(expected.Range.TargetHealth(i)));
                Assert.That(actual.Range.MemberCount, Is.EqualTo(expected.Range.MemberCount));
                Assert.That(actual.Range.Horde.Phase, Is.EqualTo(expected.Range.Horde.Phase));
                Assert.That(actual.Range.Horde.Wave, Is.EqualTo(expected.Range.Horde.Wave)); Assert.That(actual.Range.Horde.Total, Is.EqualTo(expected.Range.Horde.Total));
                Assert.That(actual.Range.Horde.IntermissionSeconds, Is.EqualTo(expected.Range.Horde.IntermissionSeconds).Within(1e-6));
                Assert.That(actual.Range.EnemyCount, Is.EqualTo(expected.Range.EnemyCount));
                for (int i = 0; i < expected.Range.EnemyCount; i++)
                {
                    EnemySnapshot a = actual.Range.Enemy(i), e = expected.Range.Enemy(i);
                    Assert.That(a.Id, Is.EqualTo(e.Id)); Assert.That(a.Health, Is.EqualTo(e.Health));
                    Assert.That(a.Position.X, Is.EqualTo(e.Position.X).Within(1e-5)); Assert.That(a.Position.Y, Is.EqualTo(e.Position.Y).Within(1e-5));
                    Assert.That(a.Position.Z, Is.EqualTo(e.Position.Z).Within(1e-5));
                }
                for (int i = 0; i < expected.Range.MemberCount; i++)
                {
                    RangeMember a = actual.Range.Member(i), e = expected.Range.Member(i);
                    Assert.That(a.PeerId, Is.EqualTo(e.PeerId)); Assert.That(a.ConnectionId, Is.EqualTo(e.ConnectionId)); Assert.That(a.Slot, Is.EqualTo(e.Slot));
                    Assert.That(a.Health, Is.EqualTo(e.Health)); Assert.That(a.Ammo, Is.EqualTo(e.Ammo)); Assert.That(a.Reloading, Is.EqualTo(e.Reloading));
                    Assert.That(a.ReviveTargetSlot, Is.EqualTo(e.ReviveTargetSlot)); Assert.That(a.ReviveProgress, Is.EqualTo(e.ReviveProgress));
                    Assert.That(a.Position.X, Is.EqualTo(e.Position.X).Within(1e-5)); Assert.That(a.Position.Y, Is.EqualTo(e.Position.Y).Within(1e-5));
                    Assert.That(a.Position.Z, Is.EqualTo(e.Position.Z).Within(1e-5));
                }
            }
        }

        [TestCase(1)] [TestCase(2)] [TestCase(10)]
        public void ActualPowerCombatReloadThreeWavesAndExtractionReplicateUnderLossAndReordering(int members)
        {
            var sim = new Simulation(members); sim.PowerUp(); sim.ClearWaves(); sim.Extract();
            Assert.That(sim.Frames, Is.GreaterThan(400));
            foreach (CoopMissionReplica replica in sim.Replicas) Assert.That(replica.Current.Mission.Phase, Is.EqualTo(CoopMissionPhase.Completed));
            if (members == 10) Assert.That(sim.MaximumPacket, Is.EqualTo(465));
        }
        [Test]
        public void ActualDownedReviveStateBlocksExtractionAndSurvivesSerializationEveryTick()
        {
            var sim = new Simulation(2); sim.PowerUp(); sim.Down(1, verifySameTickFire: true);
            Assert.That(sim.Fire.Ammo(sim.Tickets[1]), Is.EqualTo(12));
            sim.ClearWaves(); sim.AllAtExit();
            for (int i = 0; i < 45; i++) sim.Step();
            Assert.That(sim.Mission.Snapshot.HoldSeconds, Is.Zero); Assert.That(sim.Mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
            bool sawProgress = false, sawRevivedFire = false;
            for (int i = 0; i <= 62; i++)
            {
                sim.Step(beforeMission: () =>
                {
                    sim.Mission.Encounter.HoldReviveFromHost(sim.Tickets[0], true, sim.Now);
                    sim.Mission.Encounter.AdvanceReviveFromHost(sim.Tickets[0], sim.Tickets[1], sim.Now, true);
                    if (sim.Mission.Encounter.ReviveStatusFromHost(sim.Tickets[0], sim.Now, out _, out byte progress) && progress > 0) sawProgress = true;
                }, afterLife: () =>
                {
                    if (!sawRevivedFire && sim.Mission.Encounter.PlayerHealth(sim.Tickets[1]) == 40)
                    {
                        Assert.That(sim.FireWithoutHit(1, 1, sim.Now), Is.EqualTo(FireResult.InvalidSequence));
                        Assert.That(sim.FireWithoutHit(1, 2, sim.Now), Is.EqualTo(FireResult.Accepted));
                        sim.Mission.CancelPowerFromHost(sim.Tickets[1]); sawRevivedFire = true;
                    }
                });
            }
            Assert.That(sawProgress, Is.True); Assert.That(sim.Mission.Encounter.PlayerHealth(sim.Tickets[1]), Is.EqualTo(40));
            Assert.That(sawRevivedFire, Is.True);
            Assert.That(sim.Fire.Ammo(sim.Tickets[1]), Is.EqualTo(11));
            sim.Mission.CancelPowerFromHost(sim.Tickets[1]); sim.Extract();
        }
        [TestCase(false)] [TestCase(true)]
        public void EarlyMissingPhysicsAndWavePlacementFailureBothProduceReplicableTerminalState(bool placementFailure)
        {
            var sim = new Simulation(2);
            if (placementFailure) { sim.PlaceSucceeds = false; for (int i = 0; i <= 60; i++) sim.Step(true); }
            else { sim.Refresh(); Assert.That(sim.Mission.AdvanceFromHost(sim.Now, null, _ => true), Is.False); }
            sim.Publish(true);
            foreach (var replica in sim.Replicas)
            { Assert.That(replica.Current.Mission.Phase, Is.EqualTo(CoopMissionPhase.Failed)); Assert.That(replica.Current.Mission.DepotGateUnlocked, Is.EqualTo(placementFailure)); }
        }
        [Test]
        public void TeamWipeReplicatesFailureWithoutInventingCompletionOrRevival()
        {
            var sim = new Simulation(2); sim.PowerUp(); sim.Down(1); sim.Down(0); sim.Publish(true);
            Assert.That(sim.Mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.Failed));
            foreach (var replica in sim.Replicas)
            { Assert.That(replica.Current.Range.Member(0).Health, Is.Zero); Assert.That(replica.Current.Range.Member(1).Health, Is.Zero); Assert.That(replica.Current.Mission.Phase, Is.EqualTo(CoopMissionPhase.Failed)); }
        }
        [Test]
        public void SerializedReleaseCancelsPowerAndReplayedHeldPacketCannotRestartIt()
        {
            var sim = new Simulation(2); byte[] held = null;
            for (int i = 0; i < 20; i++)
            { held = sim.PowerPacket(0, true); Assert.That(sim.ReceivePower(sim.Tickets[0], held), Is.True); sim.Step(); }
            Assert.That(sim.Mission.Snapshot.HoldSeconds, Is.GreaterThan(0));
            Assert.That(sim.ReceivePower(sim.Tickets[0], sim.PowerPacket(0, false)), Is.True);
            Assert.That(sim.ReceivePower(sim.Tickets[0], held), Is.False);
            for (int i = 0; i < 70; i++) sim.Step(); sim.Publish(true);
            foreach (var replica in sim.Replicas) { Assert.That(replica.Current.Mission.HoldSeconds, Is.Zero); Assert.That(replica.Current.Mission.DepotGateUnlocked, Is.False); }
            Assert.That(sim.ReceivePower(new PlayerTicket(999, sim.Session.SessionId, 999), sim.PowerPacket(0, true)), Is.False);
        }
        [Test]
        public void DisconnectedTeammateIsRemovedWithoutRenumberingSurvivorsAndCannotHoldExtraction()
        {
            var sim = new Simulation(10); sim.PowerUp(); sim.ClearWaves(); sim.AllAtExit(); sim.Positions[4] = Simulation.Power;
            for (int i = 0; i < 45; i++) sim.Step(); Assert.That(sim.Mission.Snapshot.HoldSeconds, Is.Zero);
            Assert.That(sim.Session.Disconnect(sim.Tickets[4]), Is.True);
            Assert.That(sim.Session.TryJoin(4, CoopMissionProtocol.Version, out _), Is.EqualTo(JoinResult.NotInLobby));
            Assert.That(sim.ReceivePower(sim.Tickets[4], sim.PowerPacket(4, true)), Is.False);
            sim.Extract();
            var range = sim.Replicas[0].Current.Range; Assert.That(range.MemberCount, Is.EqualTo(9));
            for (int i = 0; i < range.MemberCount; i++) Assert.That(range.Member(i).Slot, Is.Not.EqualTo(4));
            Assert.That(range.Member(4).Slot, Is.EqualTo(5));
        }
        [Test]
        public void RawCombatCompletionMustBeCoordinatedBeforePublishingOneAtomicMissionFrame()
        {
            var sim = new Simulation(1); sim.PowerUp();
            // Reach the last wave without killing it, using authoritative fixture damage.
            for (int wave = 1; wave <= 2; wave++)
            {
                foreach (var enemy in sim.Mission.Encounter.SnapshotEnemies()) sim.Mission.Encounter.DamageEnemyFromHost(sim.Tickets[0], enemy.Id, 100, sim.Now);
                for (int i = 0; i < 102; i++) sim.Step();
            }
            foreach (var enemy in sim.Mission.Encounter.SnapshotEnemies()) sim.Mission.Encounter.DamageEnemyFromHost(sim.Tickets[0], enemy.Id, 100, sim.Now);
            Assert.That(sim.Mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.DefendDepot));
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(sim.CurrentFrame()));
            sim.Step(); Assert.That(sim.Mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction)); sim.Publish(true);
        }
        [Test]
        public void RoundAndSessionRestartInvalidateOldCommandsAndFramesBeforeNewReplicaBinding()
        {
            var sim = new Simulation(2); sim.Step(true); byte[] oldFrame = sim.LastPacket, oldCommand = sim.PowerPacket(1, true);
            PlayerTicket oldClient = sim.Tickets[1]; long oldRound = sim.Session.RoundId;
            Assert.That(sim.Session.TryReturnToLobby(sim.Tickets[0]), Is.True); Assert.That(sim.Session.Disconnect(oldClient), Is.True);
            Assert.That(sim.Session.TryJoin(1, CoopMissionProtocol.Version, out PlayerTicket replacement), Is.EqualTo(JoinResult.Accepted));
            Assert.That(replacement.ConnectionId, Is.GreaterThan(oldClient.ConnectionId));
            Assert.That(sim.Session.TryBeginRound(sim.Tickets[0]), Is.True); Assert.That(sim.Session.RoundId, Is.GreaterThan(oldRound));
            var next = new HostCoopMission(sim.Session, sim.Now, Simulation.Power, Simulation.Exit);
            var replica = new CoopMissionReplica(sim.Session.SessionId, sim.Session.RoundId);
            Assert.That(replica.TryAcceptHostPacket(oldFrame), Is.False);
            Assert.That(CoopMissionProtocol.TryPowerCommand(oldCommand, out MissionPowerCommand command), Is.True);
            Assert.That(next.TrySetPowerIntent(replacement, command.SessionId, command.RoundId, command.Sequence, true, sim.Now), Is.False);
            Assert.That(next.TrySetPowerIntent(oldClient, sim.Session.SessionId, sim.Session.RoundId, 1, true, sim.Now), Is.False);
            long sessionId = sim.Session.SessionId; Assert.That(sim.Session.TryStop(sim.Tickets[0]), Is.True);
            PlayerTicket newHost = sim.Session.StartHost(0); Assert.That(sim.Session.TryBeginRound(newHost), Is.True);
            Assert.That(sim.Session.SessionId, Is.GreaterThan(sessionId));
            Assert.That(new CoopMissionReplica(sim.Session.SessionId, sim.Session.RoundId).TryAcceptHostPacket(oldFrame), Is.False);
        }
    }
}
