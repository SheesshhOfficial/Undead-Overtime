using System;
using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class HostReviveContinuityTests
    {
        private LanSession session;
        private HostInfectedEncounter encounter;
        private PlayerTicket host, client, helper;
        private static AimVector NearHost => new AimVector(.5, 1.6, 5);
        private static AimVector NearClient => new AimVector(0, 1.6, 5);
        [SetUp]
        public void Setup()
        {
            session = new LanSession(5); host = session.StartHost(0);
            session.TryJoin(7, 5, out client); session.TryJoin(8, 5, out helper); session.TryBeginRound(host);
            encounter = new HostInfectedEncounter(session, 0);
            encounter.SetPlayerPositionFromHost(host, NearHost);
            encounter.SetPlayerPositionFromHost(client, NearClient);
            encounter.SetPlayerPositionFromHost(helper, new AimVector(-.5, 1.6, 5));
            Assert.That(encounter.StartFromHost(0), Is.True);
            for (int i = 0; i < 10; i++) Assert.That(encounter.MeleeFromHost(client, i, true), Is.True);
            Assert.That(encounter.PlayerHealth(client), Is.Zero);
        }
        private bool Tick(PlayerTicket rescuer, double time)
        {
            Assert.That(encounter.HoldReviveFromHost(rescuer, true, time), Is.True);
            return encounter.AdvanceReviveFromHost(rescuer, client, time, true);
        }
        private void HoldThrough(double start, double end)
        {
            int ticks = (int)Math.Round((end - start) * 10);
            for (int i = 0; i <= ticks; i++) Assert.That(Tick(host, start + i / 10d), Is.False);
        }
        [Test]
        public void FreshInputWithoutHostValidationCannotCompleteOldWallClockChannel()
        {
            Assert.That(Tick(host, 10), Is.False);
            for (int i = 1; i <= 31; i++) Assert.That(encounter.HoldReviveFromHost(host, true, 10 + i / 10d), Is.True);
            Assert.That(encounter.AdvanceReviveFromHost(host, client, 13.1, true), Is.False);
            Assert.That(encounter.PlayerHealth(client), Is.Zero);
            Assert.That(encounter.ReviveStatusFromHost(host, 13.1, out _, out byte progress), Is.True);
            Assert.That(progress, Is.Zero);
            HoldThrough(13.2, 16); Assert.That(Tick(host, 16.1), Is.True);
        }
        [Test]
        public void StatusUsesOnlyVerifiedProgressAndExpiresWithHostValidation()
        {
            HoldThrough(10, 11);
            Assert.That(encounter.ReviveStatusFromHost(host, 11, out _, out byte verified), Is.True);
            Assert.That(encounter.ReviveStatusFromHost(host, 11.2, out _, out byte later), Is.True);
            Assert.That(later, Is.EqualTo(verified));
            Assert.That(encounter.HoldReviveFromHost(host, true, 11.2), Is.True);
            Assert.That(encounter.ReviveStatusFromHost(host, 11.25, out _, out _), Is.False);
            Assert.That(encounter.PlayerHealth(client), Is.Zero);
        }
        [Test]
        public void StatusRejectsObservationsBeforeLastVerifiedStepOrFreshIntent()
        {
            HoldThrough(10, 11);
            Assert.That(encounter.ReviveStatusFromHost(host, 10.9, out _, out _), Is.False);
            Assert.That(encounter.HoldReviveFromHost(host, true, 11.1), Is.True);
            Assert.That(encounter.ReviveStatusFromHost(host, 11.05, out _, out _), Is.False);
        }
        [TestCase(false)] [TestCase(true)]
        public void OutAndBackPoseChangeCannotHideAnInterruptedRangeCheck(bool moveTarget)
        {
            HoldThrough(10, 12);
            PlayerTicket moving = moveTarget ? client : host;
            Assert.That(encounter.SetPlayerPositionFromHost(moving, new AimVector(5, 1.6, 5)), Is.True);
            Assert.That(encounter.SetPlayerPositionFromHost(moving, moveTarget ? NearClient : NearHost), Is.True);
            Assert.That(encounter.ReviveStatusFromHost(host, 12, out _, out _), Is.False);
            HoldThrough(12.1, 15); Assert.That(Tick(host, 15.1), Is.True);
        }
        [TestCase(false)] [TestCase(true)]
        public void InvalidPoseInvalidatesOldChannelEvenWhenImmediatelyCorrected(bool moveTarget)
        {
            HoldThrough(10, 12); PlayerTicket moving = moveTarget ? client : host;
            Assert.That(encounter.SetPlayerPositionFromHost(moving, new AimVector(double.NaN, 1.6, 5)), Is.False);
            Assert.That(encounter.ReviveStatusFromHost(host, 12, out _, out _), Is.False);
            Assert.That(encounter.SetPlayerPositionFromHost(moving, moveTarget ? NearClient : NearHost), Is.True);
            Assert.That(Tick(host, 12), Is.False);
            Assert.That(encounter.ReviveStatusFromHost(host, 12, out _, out byte progress), Is.True); Assert.That(progress, Is.Zero);
        }
        [Test]
        public void WrongValidatedTargetCancelsOldProgressInsteadOfResumingItLater()
        {
            HoldThrough(10, 12);
            Assert.That(encounter.AdvanceReviveFromHost(host, helper, 12, true), Is.False);
            Assert.That(encounter.ReviveStatusFromHost(host, 12, out _, out _), Is.False);
            Assert.That(Tick(host, 12.1), Is.False);
            Assert.That(encounter.ReviveStatusFromHost(host, 12.1, out _, out byte progress), Is.True); Assert.That(progress, Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void MissionOwnerForwardsInvalidPoseToItsReviveEncounter(bool moveTarget)
        {
            var owner = new HostCoopMission(session, 0, NearHost, new AimVector(0, 1.6, -12));
            encounter = owner.Encounter;
            for (int i = 0; i <= 60; i++)
            {
                double time = i / 20d;
                Assert.That(owner.SetPlayerPositionFromHost(host, NearHost, time), Is.True);
                Assert.That(owner.SetPlayerPositionFromHost(client, NearClient, time), Is.True);
                Assert.That(owner.SetPlayerPositionFromHost(helper, new AimVector(-.5, 1.6, 5), time), Is.True);
                Assert.That(owner.TrySetPowerIntent(host, session.SessionId, session.RoundId, (ulong)i + 1, true, time), Is.True);
                Assert.That(owner.AdvanceFromHost(time, (_, from, to) => true, active => true), Is.True);
            }
            Assert.That(owner.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.DefendDepot));
            int enemyId = encounter.Enemy(0).Id;
            Assert.That(encounter.SetEnemyPositionFromHost(enemyId, NearClient), Is.True);
            for (int i = 0; i < 10; i++) Assert.That(encounter.MeleeFromHost(enemyId, client, 4 + i, true), Is.True);
            for (int i = 0; i <= 20; i++)
            {
                double time = 14 + i / 10d;
                Assert.That(owner.SetPlayerPositionFromHost(host, NearHost, time), Is.True);
                Assert.That(owner.SetPlayerPositionFromHost(client, NearClient, time), Is.True);
                Assert.That(Tick(host, time), Is.False);
            }
            PlayerTicket moving = moveTarget ? client : host;
            Assert.That(owner.SetPlayerPositionFromHost(moving, new AimVector(double.NaN, 1.6, 5), 16), Is.False);
            Assert.That(encounter.ReviveStatusFromHost(host, 16, out _, out _), Is.False);
            Assert.That(owner.SetPlayerPositionFromHost(moving, moveTarget ? NearClient : NearHost, 16), Is.True);
            Assert.That(Tick(host, 16), Is.False);
            Assert.That(encounter.ReviveStatusFromHost(host, 16, out _, out byte progress), Is.True); Assert.That(progress, Is.Zero);
        }
        [TestCase(.25)] [TestCase(.251)] [TestCase(1)]
        public void GapAtOrBeyondValidationLifetimeRestartsProgress(double gap)
        {
            HoldThrough(10, 12);
            for (double time = 12.1; time < 12 + gap; time += .1) encounter.HoldReviveFromHost(host, true, time);
            double now = 12 + gap;
            Assert.That(encounter.HoldReviveFromHost(host, true, now), Is.True);
            Assert.That(encounter.AdvanceReviveFromHost(host, client, now, true), Is.False);
            Assert.That(encounter.ReviveStatusFromHost(host, now, out _, out byte progress), Is.True);
            Assert.That(progress, Is.Zero); Assert.That(encounter.PlayerHealth(client), Is.Zero);
        }
        [Test]
        public void SparseVerifiedTicksCannotCreditMoreThanOneTenthSecondEach()
        {
            Assert.That(Tick(host, 10), Is.False);
            for (int i = 1; i <= 15; i++) Assert.That(Tick(host, 10 + i * .2), Is.False);
            Assert.That(encounter.PlayerHealth(client), Is.Zero);
            Assert.That(encounter.ReviveStatusFromHost(host, 13, out _, out byte progress), Is.True);
            Assert.That(progress, Is.InRange(49, 50));
        }
        [Test]
        public void DuplicateChecksDoNotAdvanceOrRegressConfirmedProgress()
        {
            HoldThrough(10, 11);
            Assert.That(encounter.ReviveStatusFromHost(host, 11, out _, out byte before), Is.True);
            for (int i = 0; i < 100; i++) Assert.That(encounter.AdvanceReviveFromHost(host, client, 11, true), Is.False);
            Assert.That(encounter.ReviveStatusFromHost(host, 11, out _, out byte after), Is.True);
            Assert.That(after, Is.EqualTo(before)); Assert.That(encounter.PlayerHealth(client), Is.Zero);
        }
        [Test]
        public void RevivingAndRedowningTargetCannotReuseAnotherRescuersOldChannel()
        {
            // Use four real enemies in wave three to down a freshly revived 40-HP teammate in the same host tick.
            encounter = new HostInfectedEncounter(session, 0);
            encounter.SetPlayerPositionFromHost(host, NearHost); encounter.SetPlayerPositionFromHost(client, NearClient);
            encounter.SetPlayerPositionFromHost(helper, new AimVector(-.5, 1.6, 5));
            Assert.That(encounter.StartWavesFromHost(0), Is.True);
            for (int wave = 1; wave <= 2; wave++)
            {
                double now = (wave - 1) * 5;
                for (int i = 0; i < encounter.EnemyCount; i++) Assert.That(encounter.DamageEnemyFromHost(host, encounter.Enemy(i).Id, 100, now), Is.True);
                Assert.That(encounter.AdvanceWavesFromHost(wave * 5), Is.True);
            }
            for (int i = 0; i < encounter.EnemyCount; i++) Assert.That(encounter.SetEnemyPositionFromHost(encounter.Enemy(i).Id, NearClient), Is.True);
            for (int hit = 0; hit < 10; hit++) Assert.That(encounter.MeleeFromHost(encounter.Enemy(hit % 4).Id, client, 10 + hit / 4, true), Is.True);
            for (int i = 0; i < 30; i++)
            {
                double time = 13 + i / 10d;
                Assert.That(Tick(host, time), Is.False); Assert.That(Tick(helper, time), Is.False);
            }
            Assert.That(Tick(host, 16), Is.True); Assert.That(encounter.PlayerHealth(client), Is.EqualTo(40));
            for (int i = 0; i < 4; i++) Assert.That(encounter.MeleeFromHost(encounter.Enemy(i).Id, client, 16, true), Is.True);
            Assert.That(encounter.PlayerHealth(client), Is.Zero);
            Assert.That(Tick(helper, 16), Is.False);
            Assert.That(encounter.PlayerHealth(client), Is.Zero);
            Assert.That(encounter.ReviveStatusFromHost(helper, 16, out _, out byte progress), Is.True);
            Assert.That(progress, Is.Zero);
        }
    }
}
