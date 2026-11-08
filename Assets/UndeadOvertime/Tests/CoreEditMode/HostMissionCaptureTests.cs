using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class HostMissionCaptureTests
    {
        private LanSession session;
        private HostCoopMission mission;
        private HostFireGate fire;
        private PlayerTicket host, client;
        private Dictionary<PlayerTicket, byte> slots;
        private int[] targets;
        private static AimVector Power => new AimVector(-9, 1.6, -5);
        private static AimVector Exit => new AimVector(0, 1.6, 12);
        [SetUp]
        public void Setup()
        {
            session = new LanSession(CoopMissionProtocol.Version); host = session.StartHost(0);
            session.TryJoin(7, CoopMissionProtocol.Version, out client); session.TryBeginRound(host);
            mission = new HostCoopMission(session, 0, Power, Exit); fire = new HostFireGate(session, 0);
            slots = new Dictionary<PlayerTicket, byte> { { host, 0 }, { client, 7 } }; targets = new[] { 100, 75, 0 };
            Refresh(0);
        }
        private void Refresh(double time)
        { mission.SetPlayerPositionFromHost(host, Power, time); mission.SetPlayerPositionFromHost(client, HostMoveGate.Spawn(7), time); }
        private bool Capture(double time, out MissionFrame frame) => mission.TryCaptureFrame(time, fire, slots, targets, out frame, out _);
        [Test]
        public void CapturesFreshOwnedStateAndCopiesLegacyTargetsWithoutChangingTheHostClock()
        {
            Assert.That(mission.TryCaptureFrame(.1, fire, slots, targets, out MissionFrame frame, out byte[] payload), Is.True);
            Assert.That(frame.Sequence, Is.EqualTo(1)); Assert.That(CoopMissionProtocol.TryFrame(payload, out _), Is.True);
            Assert.That(frame.Range.Member(1).Slot, Is.EqualTo(7)); Assert.That(frame.Range.Member(0).Position.X, Is.EqualTo(Power.X));
            Assert.That(frame.Range.TargetHealth(1), Is.EqualTo(75)); targets[1] = 0; slots[client] = 8;
            Assert.That(frame.Range.TargetHealth(1), Is.EqualTo(75)); Assert.That(frame.Range.Member(1).Slot, Is.EqualTo(7));
            Assert.That(mission.TrySetPowerIntent(host, session.SessionId, session.RoundId, 1, true, .05), Is.True); // Capture did not advance authoritative clock to .1.
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero); Assert.That(fire.Ammo(host), Is.EqualTo(12));
        }
        [Test]
        public void DifferentSessionObjectWithIdenticalIdsCannotSupplyItsWeapons()
        {
            var other = new LanSession(CoopMissionProtocol.Version); PlayerTicket otherHost = other.StartHost(0);
            other.TryJoin(7, CoopMissionProtocol.Version, out _); other.TryBeginRound(otherHost);
            Assert.That(otherHost, Is.EqualTo(host)); // Deliberately colliding numeric identities.
            var foreign = new HostFireGate(other, 0); Assert.That(foreign.IsCurrentRound, Is.True);
            Assert.That(foreign.IsBoundToCurrentRound(session), Is.False);
            Assert.That(mission.TryCaptureFrame(0, foreign, slots, targets, out MissionFrame frame, out byte[] payload), Is.False);
            Assert.That(frame, Is.Null); Assert.That(payload, Is.Null);
            Assert.That(Capture(0, out frame), Is.True); Assert.That(frame.Sequence, Is.EqualTo(1));
        }
        [Test]
        public void OldRoundWeaponsCannotBeReusedForTheNewCoordinator()
        {
            session.TryReturnToLobby(host); session.TryBeginRound(host);
            var next = new HostCoopMission(session, 0, Power, Exit);
            next.SetPlayerPositionFromHost(host, Power, 0); next.SetPlayerPositionFromHost(client, HostMoveGate.Spawn(7), 0);
            Assert.That(next.TryCaptureFrame(0, fire, slots, targets, out _, out _), Is.False);
            Assert.That(Capture(0, out _), Is.False);
            Assert.That(next.TryCaptureFrame(0, new HostFireGate(session, 0), slots, targets, out MissionFrame frame, out _), Is.True);
            Assert.That(frame.Sequence, Is.EqualTo(1));
        }
        [TestCase(-1d)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(.25d)]
        public void InvalidTimeOrExpiredPoseDoesNotInventStateOrConsumeSequence(double time)
        {
            Assert.That(Capture(time, out MissionFrame frame), Is.False); Assert.That(frame, Is.Null);
            Assert.That(Capture(0, out frame), Is.True); Assert.That(frame.Sequence, Is.EqualTo(1));
        }
        [Test]
        public void MissingInvalidOrFuturePoseIsRejectedUntilHostRefreshesIt()
        {
            var missing = new HostCoopMission(session, 0, Power, Exit);
            Assert.That(missing.TryCaptureFrame(0, fire, slots, targets, out _, out _), Is.False);
            Assert.That(mission.SetPlayerPositionFromHost(client, new AimVector(double.NaN, 1.6, 0), .05), Is.False);
            Assert.That(Capture(.05, out _), Is.False); Refresh(.1);
            Assert.That(Capture(.05, out _), Is.False); Assert.That(Capture(.1, out MissionFrame frame), Is.True); Assert.That(frame.Sequence, Is.EqualTo(1));
        }
        [Test]
        public void StableSlotMapMustCoverCurrentTicketsWithUniqueInRangeSlots()
        {
            slots.Remove(client); Assert.That(Capture(0, out _), Is.False);
            slots[client] = 0; Assert.That(Capture(0, out _), Is.False);
            slots[client] = 10; Assert.That(Capture(0, out _), Is.False);
            slots[client] = 7; slots[new PlayerTicket(999, session.SessionId, 999)] = 0;
            Assert.That(Capture(0, out MissionFrame frame), Is.True); Assert.That(frame.Sequence, Is.EqualTo(1));
            session.Disconnect(client); Assert.That(Capture(0, out frame), Is.True); Assert.That(frame.Range.MemberCount, Is.EqualTo(1));
        }
        [Test]
        public void NullDependenciesAndInvalidTargetHealthFailWithoutConsumingSequence()
        {
            Assert.That(mission.TryCaptureFrame(0, null, slots, targets, out _, out _), Is.False);
            Assert.That(mission.TryCaptureFrame(0, fire, null, targets, out _, out _), Is.False);
            foreach (int[] health in new[] { null, new int[0], new[] { 100, 100 }, new[] { 101, 0, 0 }, new[] { -1, 0, 0 } })
                Assert.That(mission.TryCaptureFrame(0, fire, slots, health, out _, out _), Is.False);
            Assert.That(Capture(0, out MissionFrame frame), Is.True); Assert.That(frame.Sequence, Is.EqualTo(1));
        }
        [Test]
        public void FailureBeforeAnySuccessfulTickStillHasAValidTerminalCapture()
        {
            Assert.That(mission.AdvanceFromHost(0, null, _ => true), Is.False);
            Assert.That(Capture(0, out MissionFrame frame), Is.True); Assert.That(frame.Mission.Phase, Is.EqualTo(CoopMissionPhase.Failed));
            Assert.That(frame.Mission.DepotGateUnlocked, Is.False); Assert.That(frame.Range.Encounter.Phase, Is.EqualTo(EncounterPhase.Dormant));
        }
        [Test]
        public void CaptureInsidePlacementIsRejectedButPostTickCaptureSucceeds()
        {
            for (int i = 0; i <= 60; i++)
            {
                double now = i * .05; Refresh(now); mission.TrySetPowerIntent(host, session.SessionId, session.RoundId, (ulong)i + 1, true, now);
                Assert.That(mission.AdvanceFromHost(now, (_, a, b) => true, encounter =>
                { Assert.That(Capture(now, out _), Is.False); return true; }), Is.True);
            }
            Assert.That(Capture(3, out MissionFrame frame), Is.True); Assert.That(frame.Sequence, Is.EqualTo(1));
            Assert.That(frame.Mission.Phase, Is.EqualTo(CoopMissionPhase.DefendDepot));
        }
        [Test]
        public void IncoherentFinalKillFailsCaptureWithoutConsumingSequenceUntilCoordinatorAdvances()
        {
            double now = 0;
            for (int i = 0; i <= 60; i++)
            {
                now = i * .05; Refresh(now); mission.TrySetPowerIntent(host, session.SessionId, session.RoundId, (ulong)i + 1, true, now);
                mission.AdvanceFromHost(now, (_, a, b) => true, _ => true);
            }
            for (int wave = 1; wave <= 3; wave++)
            {
                foreach (var enemy in mission.Encounter.SnapshotEnemies()) mission.Encounter.DamageEnemyFromHost(host, enemy.Id, 100, now);
                if (wave < 3) { now += 5.01; Refresh(now); mission.AdvanceFromHost(now, (_, a, b) => true, _ => true); }
            }
            Assert.That(Capture(now, out MissionFrame rejected), Is.False); Assert.That(rejected, Is.Null);
            now += .05; Refresh(now); mission.AdvanceFromHost(now, (_, a, b) => true, _ => true);
            Assert.That(Capture(now, out MissionFrame accepted), Is.True); Assert.That(accepted.Sequence, Is.EqualTo(1));
            Assert.That(accepted.Mission.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
        }
        [Test]
        public void SamplingDoesNotCompleteReloadOrAdvanceMissionOnTheCallersBehalf()
        {
            var aim = new AimVector(0, 0, 1); fire.SetPoseFromHost(host, Power, aim);
            Assert.That(fire.TryAuthorize(host, new FireRequest(session.SessionId, session.RoundId, 1, Power, aim), 0, out _), Is.EqualTo(FireResult.Accepted));
            Assert.That(fire.TryBeginReloadFromHost(host, 0), Is.True); Refresh(HostFireGate.ReloadDuration);
            Assert.That(Capture(HostFireGate.ReloadDuration, out MissionFrame before), Is.True);
            Assert.That(before.Range.Member(0).Reloading, Is.True); Assert.That(before.Range.Member(0).Ammo, Is.EqualTo(11));
            Assert.That(before.Mission.HoldSeconds, Is.Zero); Assert.That(before.Mission.Phase, Is.EqualTo(CoopMissionPhase.RestorePower));
            fire.AdvanceReloadsFromHost(HostFireGate.ReloadDuration);
            Assert.That(Capture(HostFireGate.ReloadDuration, out MissionFrame after), Is.True);
            Assert.That(after.Range.Member(0).Reloading, Is.False); Assert.That(after.Range.Member(0).Ammo, Is.EqualTo(12));
            Assert.That(after.Sequence, Is.EqualTo(2));
        }
        [Test]
        public void SequenceNeverWrapsAndNoPartialOutputEscapesOnOverflow()
        {
            FieldInfo field = typeof(HostCoopMission).GetField("frameSequence", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null); field.SetValue(mission, ulong.MaxValue - 1);
            Assert.That(Capture(0, out MissionFrame frame), Is.True); Assert.That(frame.Sequence, Is.EqualTo(ulong.MaxValue));
            Assert.That(mission.TryCaptureFrame(0, fire, slots, targets, out frame, out byte[] payload), Is.False);
            Assert.That(frame, Is.Null); Assert.That(payload, Is.Null);
            Assert.That((ulong)field.GetValue(mission), Is.EqualTo(ulong.MaxValue));
        }
    }
}
