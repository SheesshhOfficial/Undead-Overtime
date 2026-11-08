using System;
using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class HostCoopMissionTests
    {
        private LanSession session;
        private PlayerTicket host, client;
        private HostCoopMission mission;
        private double now;
        private ulong sequence;
        private int placements;
        private bool lineClear, spawnClear;
        private AimVector hostPosition, clientPosition;
        private static AimVector Power => new AimVector(0, HostMoveGate.EyeHeight, -4);
        private static AimVector Exit => new AimVector(0, HostMoveGate.EyeHeight, -12);
        [SetUp]
        public void Setup()
        {
            session = new LanSession(5); host = session.StartHost(0); session.TryJoin(7, 5, out client); session.TryBeginRound(host);
            mission = new HostCoopMission(session, 0, Power, Exit);
            now = 0; sequence = 0; placements = 0; lineClear = spawnClear = true;
            hostPosition = Power; clientPosition = HostMoveGate.Spawn(1); Refresh();
        }
        private void Refresh()
        {
            Assert.That(mission.SetPlayerPositionFromHost(host, hostPosition, now), Is.True);
            if (session.IsCurrent(client)) Assert.That(mission.SetPlayerPositionFromHost(client, clientPosition, now), Is.True);
        }
        private bool Hold(PlayerTicket player, bool held = true) => mission.TrySetPowerIntent(player, session.SessionId, session.RoundId, ++sequence, held, now);
        private bool Place(HostInfectedEncounter encounter)
        {
            Assert.That(encounter, Is.SameAs(mission.Encounter)); placements++;
            return spawnClear;
        }
        private bool Tick(bool hold = false, bool refresh = true)
        {
            if (refresh) Refresh();
            if (hold) Assert.That(Hold(host), Is.True);
            bool accepted = mission.AdvanceFromHost(now, (_, from, to) => lineClear, Place);
            now += .05; return accepted;
        }
        private void PowerUp()
        {
            for (int i = 0; i <= 60; i++) Assert.That(Tick(hold: true), Is.True);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.DefendDepot));
            Assert.That(placements, Is.EqualTo(1));
        }
        private void ClearWaves()
        {
            for (int wave = 1; wave <= HostInfectedEncounter.TotalWaves; wave++)
            {
                Assert.That(mission.Encounter.HordeAt(now).Wave, Is.EqualTo(wave));
                foreach (EnemySnapshot enemy in mission.Encounter.SnapshotEnemies())
                    if (enemy.Health > 0) Assert.That(mission.Encounter.DamageEnemyFromHost(host, enemy.Id, 100, now), Is.True);
                if (wave < HostInfectedEncounter.TotalWaves)
                {
                    now += HostInfectedEncounter.IntermissionDuration + .01;
                    Assert.That(Tick(), Is.True);
                }
            }
            Assert.That(Tick(), Is.True);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
        }
        [Test]
        public void ActualEncounterCompletesPowerThreeWavesAndWholeTeamExtraction()
        {
            PowerUp(); Assert.That(mission.Snapshot.DepotGateUnlocked, Is.True);
            ClearWaves(); Assert.That(placements, Is.EqualTo(3));
            hostPosition = clientPosition = Exit;
            for (int i = 0; i < 40; i++) Tick();
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
            Tick(); Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.Completed));
            Assert.That(Tick(), Is.False); Assert.That(placements, Is.EqualTo(3));
        }
        [Test]
        public void IntentNeedsCurrentSenderSessionRoundAndStrictSequence()
        {
            Assert.That(mission.TrySetPowerIntent(new PlayerTicket(99, session.SessionId, 1), session.SessionId, session.RoundId, 1, true, now), Is.False);
            Assert.That(mission.TrySetPowerIntent(host, session.SessionId + 1, session.RoundId, 1, true, now), Is.False);
            Assert.That(mission.TrySetPowerIntent(host, session.SessionId, session.RoundId + 1, 1, true, now), Is.False);
            Assert.That(mission.TrySetPowerIntent(host, session.SessionId, session.RoundId, 0, true, now), Is.False);
            Assert.That(Hold(host), Is.True);
            Assert.That(mission.TrySetPowerIntent(host, session.SessionId, session.RoundId, sequence, true, now), Is.False);
            Assert.That(mission.TrySetPowerIntent(host, session.SessionId, session.RoundId, sequence, false, now), Is.False);
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
        }
        [Test]
        public void PositiveInputPacingConsumesSequenceButReleaseAlwaysCancels()
        {
            Tick(hold: true); Tick(hold: true);
            Assert.That(mission.Snapshot.HoldSeconds, Is.GreaterThan(0));
            Assert.That(Hold(host), Is.True); // now .1
            now += .001; ulong rejected = ++sequence;
            Assert.That(mission.TrySetPowerIntent(host, session.SessionId, session.RoundId, rejected, true, now), Is.False);
            Assert.That(Hold(host, false), Is.True);
            now += .03;
            Assert.That(mission.TrySetPowerIntent(host, session.SessionId, session.RoundId, rejected, true, now), Is.False);
            Tick(); Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
        }
        [Test]
        public void ReleasedAndRepressedBetweenTicksCannotKeepOldProgress()
        {
            for (int i = 0; i < 20; i++) Tick(hold: true);
            now -= .04; Assert.That(Hold(host, false), Is.True);
            now += .02; Assert.That(Hold(host), Is.True);
            now += .02; Tick();
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            Assert.That(mission.Snapshot.DepotGateUnlocked, Is.False);
        }
        [Test]
        public void InitialLatePressDoesNotEarnTimeBeforeItsArrival()
        {
            Tick(); now = .049; Assert.That(Hold(host), Is.True); now = .05; Tick();
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            Tick(hold: true); Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            Tick(hold: true); Assert.That(mission.Snapshot.HoldSeconds, Is.EqualTo(.05).Within(1e-8));
        }
        [Test]
        public void ExpiredIntentAndRefreshedIntentAfterGapBothResetHold()
        {
            for (int i = 0; i < 10; i++) Tick(hold: true);
            for (int i = 0; i < 6; i++) Tick();
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            for (int i = 0; i < 10; i++) Tick(hold: true);
            now += .3; Tick(hold: true);
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void RangeAndLineOfSightAreHostDerived(bool blockedSight)
        {
            for (int i = 0; i < 10; i++) Tick(hold: true);
            if (blockedSight) lineClear = false; else hostPosition = new AimVector(3, HostMoveGate.EyeHeight, -4);
            Tick(hold: true);
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            for (int i = 0; i < 70; i++) Tick(hold: true);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.RestorePower)); Assert.That(placements, Is.Zero);
        }
        [Test]
        public void MissingStaleOrInvalidPoseCannotOperatePower()
        {
            Assert.That(Hold(host), Is.True);
            now = .25; Assert.That(Hold(host), Is.True); Tick(refresh: false); Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            Assert.That(mission.SetPlayerPositionFromHost(host, new AimVector(double.NaN, 1.6, 0), now), Is.False);
            Tick(hold: true, refresh: false); Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
        }
        [Test]
        public void ContinuouslyEligibleTeammatePreservesSharedHoldWithoutSpeedup()
        {
            clientPosition = Power;
            for (int i = 0; i < 20; i++)
            { Refresh(); Assert.That(Hold(client), Is.True); Tick(hold: true); }
            double held = mission.Snapshot.HoldSeconds;
            mission.CancelPowerFromHost(host);
            Refresh(); Assert.That(Hold(client), Is.True); Tick();
            Assert.That(mission.Snapshot.HoldSeconds, Is.EqualTo(held + .05).Within(1e-8));
            Assert.That(mission.Snapshot.HoldSeconds, Is.LessThan(1.1));
        }
        [Test]
        public void DisconnectedOrReplacementTicketCannotSupplyIntent()
        {
            for (int i = 0; i < 10; i++) Tick(hold: true);
            Assert.That(session.Disconnect(client), Is.True);
            Assert.That(Hold(client), Is.False);
            session.TryReturnToLobby(host); session.TryJoin(7, 5, out PlayerTicket replacement); session.TryBeginRound(host);
            Assert.That(replacement, Is.Not.EqualTo(client));
            Assert.That(mission.Snapshot.IsCurrent, Is.False);
            Assert.That(Hold(replacement), Is.False); Assert.That(mission.AdvanceFromHost(now, (_, a, b) => true, Place), Is.False);
        }
        [Test]
        public void FailedFirstWavePlacementFailsMissionAndNeverRetries()
        {
            spawnClear = false;
            for (int i = 0; i < 60; i++) Tick(hold: true);
            Assert.That(Tick(hold: true), Is.False);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.Failed));
            Assert.That(mission.Encounter.Phase, Is.EqualTo(EncounterPhase.Failed));
            Assert.That(mission.Snapshot.DepotGateUnlocked, Is.True);
            Assert.That(Tick(), Is.False); Assert.That(placements, Is.EqualTo(1));
        }
        [Test]
        public void FailedLaterWavePlacementAlsoStopsMission()
        {
            PowerUp(); foreach (var enemy in mission.Encounter.SnapshotEnemies()) mission.Encounter.DamageEnemyFromHost(host, enemy.Id, 100, now);
            spawnClear = false; now += 5.01;
            Assert.That(Tick(), Is.False); Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.Failed));
            Assert.That(placements, Is.EqualTo(2));
        }
        [Test]
        public void CallbackExceptionFailsClosedAndIsNotSwallowed()
        {
            Assert.That(Hold(host), Is.True);
            Assert.Throws<InvalidOperationException>(() => mission.AdvanceFromHost(now, (_, a, b) => throw new InvalidOperationException("physics unavailable"), Place));
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.Failed));
        }
        [Test]
        public void PlacementCannotRecursivelyAdvanceOrSupplyNewCommands()
        {
            for (int i = 0; i < 60; i++) Tick(hold: true);
            Refresh(); Hold(host);
            Assert.That(mission.AdvanceFromHost(now, (_, a, b) => true, encounter =>
            {
                Assert.That(mission.AdvanceFromHost(now, (_, a, b) => true, Place), Is.False);
                Assert.That(Hold(host, false), Is.False);
                Assert.That(mission.SetPlayerPositionFromHost(host, Exit, now), Is.False);
                return true;
            }), Is.True);
        }
        [Test]
        public void StartingSeparateEncounterInsteadOfMissionWavesFailsClosed()
        {
            Assert.That(mission.Encounter.StartFromHost(now), Is.True);
            Assert.That(Tick(hold: true), Is.False);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.Failed));
        }
        [Test]
        public void MissingPhysicsCallbacksDoNotSilentlyPass()
        {
            Assert.That(mission.AdvanceFromHost(now, null, Place), Is.False);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.Failed));
            Setup(); Assert.That(mission.AdvanceFromHost(now, (_, a, b) => true, null), Is.False);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.Failed));
        }
        [Test]
        public void DuplicateBackwardsAndInvalidTimeNeverGrantProgress()
        {
            Tick(hold: true); double hold = mission.Snapshot.HoldSeconds;
            foreach (double time in new[] { 0, -1, double.NaN, double.PositiveInfinity, HostFireGate.MaximumHostTime + 1 })
                Assert.That(mission.AdvanceFromHost(time, (_, a, b) => true, Place), Is.False);
            Assert.That(mission.Snapshot.HoldSeconds, Is.EqualTo(hold));
        }
        [Test]
        public void ExtractionRequiresEveryConnectedLivingPoseAndResetsWhenSomeoneLeaves()
        {
            PowerUp(); ClearWaves(); hostPosition = Exit;
            for (int i = 0; i < 45; i++) Tick(); Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            clientPosition = Exit; for (int i = 0; i < 20; i++) Tick();
            Assert.That(mission.Snapshot.HoldSeconds, Is.GreaterThan(0));
            clientPosition = Power; Tick(); Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            Assert.That(session.Disconnect(client), Is.True);
            for (int i = 0; i <= 40; i++) Tick(); Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.Completed));
        }
        [Test]
        public void RealDownedTeammateBlocksExtractionUntilActualReviveCompletes()
        {
            PowerUp(); byte attacker = mission.Encounter.Enemy(0).Id;
            clientPosition = mission.Encounter.Enemy(0).Position;
            for (int i = 0; i < 10; i++)
            {
                Refresh(); Assert.That(mission.Encounter.MeleeFromHost(attacker, client, now, true), Is.True); now += 1;
            }
            Assert.That(mission.Encounter.PlayerHealth(client), Is.Zero);
            Assert.That(Hold(client), Is.False);
            ClearWaves(); hostPosition = clientPosition = Exit;
            for (int i = 0; i < 45; i++) Tick();
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction)); Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            for (int i = 0; i <= 62; i++)
            {
                Refresh(); mission.Encounter.HoldReviveFromHost(host, true, now);
                mission.Encounter.AdvanceReviveFromHost(host, client, now, true); Tick();
            }
            Assert.That(mission.Encounter.PlayerHealth(client), Is.EqualTo(HostInfectedEncounter.RevivedHealth));
            for (int i = 0; i <= 40 && mission.Snapshot.Phase != CoopMissionPhase.Completed; i++) Tick();
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.Completed));
        }
        [Test]
        public void ActualTeamWipeFailsMissionWithoutExtractionOrRespawn()
        {
            PowerUp(); byte attacker = mission.Encounter.Enemy(0).Id;
            hostPosition = clientPosition = mission.Encounter.Enemy(0).Position;
            foreach (var victim in new[] { client, host }) for (int i = 0; i < 10; i++)
            { Refresh(); Assert.That(mission.Encounter.MeleeFromHost(attacker, victim, now, true), Is.True); now += 1; }
            Tick(); Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.Failed));
            Assert.That(mission.Snapshot.DepotGateUnlocked, Is.True);
        }
        [Test]
        public void StaleExtractionPoseResetsCountdownEvenWhenLastPositionWasInside()
        {
            PowerUp(); ClearWaves(); hostPosition = clientPosition = Exit;
            for (int i = 0; i < 20; i++) Tick();
            Assert.That(mission.Snapshot.HoldSeconds, Is.GreaterThan(0));
            now += .25; Tick(refresh: false);
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
        }
        [TestCase(false)] [TestCase(true)]
        public void ObservedExitInterruptionBetweenTicksResetsEvenAfterReentry(bool invalidPose)
        {
            PowerUp(); ClearWaves(); hostPosition = clientPosition = Exit;
            for (int i = 0; i < 20; i++) Tick();
            Assert.That(mission.Snapshot.HoldSeconds, Is.GreaterThan(0));
            now -= .02;
            Assert.That(mission.SetPlayerPositionFromHost(client, invalidPose ? new AimVector(double.NaN, 1.6, 0) : Power, now), Is.EqualTo(!invalidPose));
            now += .01; Assert.That(mission.SetPlayerPositionFromHost(client, Exit, now), Is.True);
            now += .01; Tick();
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
        }
        [Test]
        public void ObservedPowerRangeExitBetweenTicksRequiresFreshHold()
        {
            for (int i = 0; i < 10; i++) Tick(hold: true);
            now -= .02; Assert.That(mission.SetPlayerPositionFromHost(host, Exit, now), Is.True);
            now += .01; Assert.That(mission.SetPlayerPositionFromHost(host, Power, now), Is.True);
            now += .01; Tick();
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
        }
        [Test]
        public void ExplicitActionCancellationClearsPowerEvenBeforeNextTick()
        {
            for (int i = 0; i < 10; i++) Tick(hold: true);
            mission.CancelPowerFromHost(host); Tick();
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            for (int i = 0; i < 70; i++) Tick();
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.RestorePower));
        }
        [Test]
        public void HostDisconnectInvalidatesCoordinatorAndEncounter()
        {
            PowerUp(); Assert.That(session.Disconnect(host), Is.True);
            Assert.That(mission.Snapshot.IsCurrent, Is.False);
            Assert.That(mission.TrySetPowerIntent(client, session.SessionId, session.RoundId, ++sequence, true, now), Is.False);
            Assert.That(mission.SetPlayerPositionFromHost(client, Exit, now), Is.False);
            Assert.That(mission.AdvanceFromHost(now, (_, a, b) => true, Place), Is.False);
            Assert.That(placements, Is.EqualTo(1));
        }
        [Test]
        public void PlacementExceptionFailsActiveEncounterWithoutRetry()
        {
            for (int i = 0; i < 60; i++) Tick(hold: true);
            Refresh(); Hold(host);
            Assert.Throws<InvalidOperationException>(() => mission.AdvanceFromHost(now, (_, a, b) => true,
                encounter => throw new InvalidOperationException("placement unavailable")));
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.Failed));
            Assert.That(mission.Encounter.Phase, Is.EqualTo(EncounterPhase.Failed));
            Assert.That(mission.AdvanceFromHost(now, (_, a, b) => true, Place), Is.False);
        }
        [Test]
        public void ConstructionRejectsInvalidOrOutOfArenaLayout()
        {
            Assert.Throws<ArgumentNullException>(() => new HostCoopMission(null, 0, Power, Exit));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HostCoopMission(session, 0, new AimVector(0, 0, 0), Exit));
            foreach (double size in new[] { 0, -1, 5, double.NaN, double.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => new HostCoopMission(session, 0, Power, Exit, size));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HostCoopMission(session, 0, Power, new AimVector(15, 1.6, 0)));
        }
    }
}
