using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class InfectedDamageFeedbackTests
    {
        private InfectedDamageFeedback feedback;
        [SetUp] public void Setup() { feedback = new InfectedDamageFeedback(); }
        private static EnemySnapshot Enemy(byte id = 0, byte health = 100) =>
            new EnemySnapshot(id, health, HostInfectedEncounter.Spawn);
        private static RangeSnapshot Snapshot(EnemySnapshot[] enemies, long session = 1, long round = 1,
            SessionPhase phase = SessionPhase.Playing, EncounterPhase encounter = EncounterPhase.Active) =>
            new RangeSnapshot(session, round, phase,
                new[] { new RangeMember(0, 1, 0, 12, false, HostMoveGate.Spawn(0)) }, new[] { 100, 100, 100 },
                encounter, enemies, HordeSnapshot.Disabled);
        private void Observe(double now, params EnemySnapshot[] enemies) =>
            Assert.That(feedback.Observe(Snapshot(enemies), now), Is.True);
        private double Read(double now, byte id = 0)
        { Assert.That(feedback.TryRead(id, now, out double pulse), Is.True); return pulse; }
        private void Absent(double now, byte id = 0)
        { Assert.That(feedback.TryRead(id, now, out double pulse), Is.False); Assert.That(pulse, Is.Zero); }
        private void Damage()
        { Observe(0, Enemy()); Observe(.1, Enemy(health: 75)); Assert.That(Read(.1), Is.EqualTo(1)); }

        [TestCase(100)] [TestCase(25)]
        public void FirstObservationNeverInventsAHit(int health)
        { Observe(0, Enemy(health: (byte)health)); Assert.That(Read(0), Is.Zero); }
        [Test]
        public void FirstDeadEnemyIsAbsent()
        { Observe(0, Enemy(health: 0)); Absent(0); }
        [Test]
        public void NonlethalHealthDropStartsOneBoundedPulse()
        { Damage(); Assert.That(Read(.21), Is.EqualTo(.5).Within(1e-9)); Assert.That(Read(.32), Is.Zero); }
        [TestCase(0)] [TestCase(1)] [TestCase(1234.5)]
        public void PulseExpiresAtItsExactBoundary(double start)
        {
            Observe(start, Enemy()); Observe(start, Enemy(health: 75));
            Assert.That(Read(start), Is.EqualTo(1));
            Assert.That(Read(start + InfectedDamageFeedback.PulseSeconds), Is.Zero);
        }
        [Test]
        public void RepeatedReadsNeverRefreshThePulse()
        { Damage(); Assert.That(Read(.21), Is.EqualTo(Read(.21))); Assert.That(Read(.32), Is.Zero); }
        [Test]
        public void DuplicateHealthDoesNotRestartThePulse()
        { Damage(); Observe(.2, Enemy(health: 75)); Assert.That(Read(.21), Is.EqualTo(.5).Within(1e-9)); Assert.That(Read(.32), Is.Zero); }
        [Test]
        public void FurtherDamageRestartsOnlyTheAffectedEnemy()
        {
            Observe(0, Enemy(4), Enemy(5)); Observe(.1, Enemy(4, 75), Enemy(5, 75));
            Observe(.2, Enemy(4, 50), Enemy(5, 75));
            Assert.That(Read(.2, 4), Is.EqualTo(1)); Assert.That(Read(.2, 5), Is.LessThan(.55));
        }
        [Test]
        public void FatalDamageNeverKeepsAnEnemyVisibleForFeedback()
        { Damage(); Observe(.2, Enemy(health: 0)); Absent(.2); }
        [Test]
        public void HealthIncreaseStartsAQuietNewLife()
        { Damage(); Observe(.2, Enemy(health: 100)); Assert.That(Read(.2), Is.Zero); }
        [Test]
        public void RemovedOrDeadIdentityCannotCarryDamageIntoReuse()
        {
            foreach (bool dead in new[] { false, true })
            {
                feedback.Reset(); Damage(); Observe(.2, dead ? new[] { Enemy(health: 0) } : new EnemySnapshot[0]);
                Absent(.2); Observe(.3, Enemy(health: 50)); Assert.That(Read(.3), Is.Zero);
            }
        }
        [Test]
        public void ArrayReorderingDoesNotExchangeEnemyFeedback()
        {
            Observe(0, Enemy(4), Enemy(5), Enemy(6), Enemy(7));
            Observe(.1, Enemy(7), Enemy(6), Enemy(5), Enemy(4, 75));
            Assert.That(Read(.1, 4), Is.EqualTo(1)); Assert.That(Read(.1, 7), Is.Zero);
            Observe(.2, Enemy(8, 25), Enemy(4, 75)); Absent(.2, 5);
            Assert.That(Read(.2, 8), Is.Zero); Assert.That(Read(.2, 4), Is.GreaterThan(0));
        }
        [TestCase(2, 1)] [TestCase(1, 2)]
        public void SessionOrRoundChangeRebaselinesWithoutAHit(long session, long round)
        { Damage(); feedback.Observe(Snapshot(new[] { Enemy(health: 50) }, session, round), .2); Assert.That(Read(.2), Is.Zero); }
        [TestCase(.5)] [TestCase(20)]
        public void LateHealthDropAfterObservationGapIsNotReplayedAsFreshDamage(double gap)
        { Damage(); Observe(.1 + gap, Enemy(health: 50)); Assert.That(Read(.1 + gap), Is.Zero); }
        [Test]
        public void SameTimeActualHealthLossStillCountsWithoutArtificialTimeAdvance()
        { Observe(0, Enemy()); Observe(0, Enemy(health: 75)); Assert.That(Read(0), Is.EqualTo(1)); }
        [TestCase(SessionPhase.Lobby)] [TestCase(SessionPhase.Stopped)] [TestCase((SessionPhase)255)]
        public void NonplayingPhaseClearsAllFeedback(SessionPhase phase)
        { Damage(); Assert.That(feedback.Observe(Snapshot(new[] { Enemy() }, phase: phase), .2), Is.False); Absent(.2); }
        [TestCase(EncounterPhase.Dormant)] [TestCase(EncounterPhase.Cleared)]
        [TestCase(EncounterPhase.Failed)] [TestCase((EncounterPhase)255)]
        public void InactiveEncounterClearsAllFeedback(EncounterPhase encounter)
        { Damage(); Assert.That(feedback.Observe(Snapshot(new[] { Enemy() }, encounter: encounter), .2), Is.False); Absent(.2); }
        [Test]
        public void MalformedObservationClearsBeforeApplyingAnyRow()
        {
            foreach (RangeSnapshot value in new[] {
                null, Snapshot(new[] { Enemy(), Enemy() }), Snapshot(new[] { Enemy(health: 101) }),
                Snapshot(new[] { Enemy(0), Enemy(1), Enemy(2), Enemy(3), Enemy(4) }),
                Snapshot(new[] { Enemy(), new EnemySnapshot(1, 100, new AimVector(double.NaN, 1.6, 0)) }),
                Snapshot(new[] { Enemy() }, session: 0), Snapshot(new[] { Enemy() }, round: 0) })
            {
                feedback.Reset(); Damage(); Assert.That(feedback.Observe(value, .2), Is.False); Absent(.2);
                Observe(.3, Enemy(health: 50)); Assert.That(Read(.3), Is.Zero);
            }
        }
        [Test]
        public void InvalidClocksClearFeedbackWithoutPoisoningClockBoundary()
        {
            foreach (double value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d, HostFireGate.MaximumHostTime + 1 })
            {
                feedback.Reset(); Damage(); Assert.That(feedback.Observe(Snapshot(new[] { Enemy() }), value), Is.False); Absent(.1);
                Observe(.2, Enemy()); Assert.That(Read(.2), Is.Zero);
                feedback.Reset(); Damage(); Absent(value); Observe(.2, Enemy()); Assert.That(Read(.2), Is.Zero);
            }
        }
        [Test]
        public void BackwardReadOrObservationCannotRestorePriorPulse()
        {
            Damage(); Absent(.05); Assert.That(feedback.Observe(Snapshot(new[] { Enemy() }), .075), Is.False);
            Observe(.2, Enemy(health: 50)); Assert.That(Read(.2), Is.Zero);
        }
        [Test]
        public void ExplicitResetAllowsNewClockAndRequiresFreshBaseline()
        { Damage(); feedback.Reset(); Absent(0); Observe(0, Enemy(health: 50)); Assert.That(Read(0), Is.Zero); }
        [Test]
        public void ObservationsAndReadsCannotChangeSnapshotBytesOrHostRules()
        {
            RangeSnapshot first = Snapshot(new[] { Enemy() }), second = Snapshot(new[] { Enemy(health: 75) });
            byte[] one = LanRangeProtocol.Encode(first), two = LanRangeProtocol.Encode(second);
            feedback.Observe(first, 0); feedback.Observe(second, .1); Read(.2); feedback.Reset();
            Assert.That(LanRangeProtocol.Encode(first), Is.EqualTo(one)); Assert.That(LanRangeProtocol.Encode(second), Is.EqualTo(two));
            Assert.That(HostInfectedEncounter.MeleeDamage, Is.EqualTo(10)); Assert.That(LanRangeProtocol.EnemyBytes, Is.EqualTo(14));
        }
    }
}
