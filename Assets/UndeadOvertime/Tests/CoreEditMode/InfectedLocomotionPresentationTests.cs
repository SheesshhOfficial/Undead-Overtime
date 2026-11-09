using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class InfectedLocomotionPresentationTests
    {
        private InfectedLocomotionPresentation view;
        [SetUp] public void Setup() { view = new InfectedLocomotionPresentation(); }
        private static EnemySnapshot Enemy(byte id = 0, double x = 0, double z = 6, byte health = 100) =>
            new EnemySnapshot(id, health, new AimVector(x, HostMoveGate.EyeHeight, z));
        private static RangeSnapshot Snapshot(EnemySnapshot[] enemies, long session = 1, long round = 1,
            SessionPhase phase = SessionPhase.Playing, EncounterPhase encounter = EncounterPhase.Active) =>
            new RangeSnapshot(session, round, phase,
                new[] { new RangeMember(0, 1, 0, 12, false, HostMoveGate.Spawn(0)) }, new[] { 100, 100, 100 },
                encounter, enemies, HordeSnapshot.Disabled);
        private void Observe(double now, params EnemySnapshot[] enemies) => Assert.That(view.Observe(Snapshot(enemies), now), Is.True);
        private InfectedLocomotionState Read(double now, byte id = 0)
        {
            Assert.That(view.TryRead(id, now, out InfectedLocomotionState state), Is.True);
            Assert.That(state.IsVisible, Is.True); return state;
        }
        private void Absent(double now, byte id = 0)
        {
            Assert.That(view.TryRead(id, now, out InfectedLocomotionState state), Is.False);
            Assert.That(state.IsVisible, Is.False); Assert.That(state.IsMoving, Is.False); Assert.That(state.Speed, Is.Zero);
        }
        private void Idle(double now, byte id = 0)
        { InfectedLocomotionState state = Read(now, id); Assert.That(state.IsMoving, Is.False); Assert.That(state.Speed, Is.Zero); }
        private void Walk()
        { Observe(0, Enemy()); Observe(.1, Enemy(x: .16)); Assert.That(Read(.1).IsMoving, Is.True); }
        private static void Facing(InfectedLocomotionState value, double x, double z)
        {
            Assert.That(value.Facing.X, Is.EqualTo(x).Within(1e-9)); Assert.That(value.Facing.Y, Is.Zero);
            Assert.That(value.Facing.Z, Is.EqualTo(z).Within(1e-9)); Assert.That(value.Facing.IsUnit, Is.True);
        }

        [TestCase(100)] [TestCase(40)]
        public void FirstObservationIsIdleWithDefinedHeading(int health)
        { Observe(0, Enemy(health: (byte)health)); Idle(0); Facing(Read(0), 0, 1); }

        [Test]
        public void NewObserverNeverShowsAnAlreadyDeadEnemy()
        { Observe(0, Enemy(health: 0)); Absent(0); }

        [Test]
        public void FreshDisplacementEstimatesBoundedPlanarSpeedAndHeading()
        { Walk(); Assert.That(Read(.1).Speed, Is.EqualTo(HostInfectedEncounter.Speed).Within(1e-9)); Facing(Read(.1), 1, 0); }

        [Test]
        public void DiagonalMovementProducesUnitPlanarFacing()
        {
            Observe(0, Enemy()); Observe(.1, Enemy(x: .1, z: 6.1));
            Facing(Read(.1), 1 / System.Math.Sqrt(2), 1 / System.Math.Sqrt(2));
            Assert.That(Read(.1).Speed, Is.EqualTo(System.Math.Sqrt(2)).Within(1e-9));
        }

        [Test]
        public void StandingRetainsHeadingButStopsMotion()
        { Walk(); Observe(.2, Enemy(x: .16)); Idle(.2); Facing(Read(.2), 1, 0); }

        [Test]
        public void HysteresisPreventsSmallMotionFromConstantlyChangingGait()
        {
            Observe(0, Enemy()); Observe(.1, Enemy(x: .012)); Assert.That(Read(.1).IsMoving, Is.False);
            Observe(.2, Enemy(x: .032)); Assert.That(Read(.2).IsMoving, Is.True);
            Observe(.3, Enemy(x: .044)); Assert.That(Read(.3).IsMoving, Is.True);
            Observe(.4, Enemy(x: .049)); Assert.That(Read(.4).IsMoving, Is.False);
            Observe(.5, Enemy(x: .061)); Assert.That(Read(.5).IsMoving, Is.False);
        }

        [Test]
        public void BunchedDisplacementWithinToleranceClampsToHostSpeed()
        { Observe(0, Enemy()); Observe(.1, Enemy(x: .20)); Assert.That(Read(.1).Speed, Is.EqualTo(HostInfectedEncounter.Speed)); }

        [Test]
        public void TeleportNeverBecomesWalkingAndNextPlausibleSampleCanRecover()
        {
            Walk(); Observe(.2, Enemy(x: 4)); Idle(.2); Facing(Read(.2), 1, 0);
            Observe(.3, Enemy(x: 4.16)); Assert.That(Read(.3).IsMoving, Is.True);
        }

        [Test]
        public void EqualTimeObservationRebaselinesWithoutDividingByZero()
        {
            Walk(); Observe(.1, Enemy(x: .32)); Idle(.1);
            Observe(.2, Enemy(x: .48)); Assert.That(Read(.2).IsMoving, Is.True);
        }

        [Test]
        public void DuplicateReadsNeverExtendReceiptLifetime()
        {
            Walk(); Assert.That(Read(.59).IsMoving, Is.True); Assert.That(Read(.59).IsMoving, Is.True);
            Idle(.6); Idle(10); Facing(Read(10), 1, 0);
        }

        [TestCase(.5)] [TestCase(10)]
        public void LongObservationGapStartsIdleEvenWithoutAnInterveningRead(double gap)
        { Walk(); Observe(.1 + gap, Enemy(x: .32)); Idle(.1 + gap); }

        [Test]
        public void FirstObservationAfterStalenessStartsIdleThenCanResume()
        {
            Walk(); Idle(.6); Observe(.61, Enemy(x: .32)); Idle(.61);
            Observe(.71, Enemy(x: .48)); Assert.That(Read(.71).IsMoving, Is.True);
        }

        [Test]
        public void MissingEnemyCannotLingerAndReappearanceStartsFresh()
        {
            Walk(); Observe(.2); Absent(.2);
            Observe(.3, Enemy(x: .48)); Idle(.3); Facing(Read(.3), 0, 1);
        }

        [Test]
        public void DeathClearsImmediatelyAndReusedIdNeverInheritsMotion()
        {
            Walk(); Observe(.2, Enemy(x: .16, health: 0)); Absent(.2);
            Observe(.3, Enemy(x: .32)); Idle(.3); Facing(Read(.3), 0, 1);
        }

        [Test]
        public void HealthIncreaseIsConservativeNewLifeNotOldMovement()
        {
            Observe(0, Enemy(health: 20)); Observe(.1, Enemy(x: .16, health: 20));
            Observe(.2, Enemy(x: .32, health: 100)); Idle(.2); Facing(Read(.2), 0, 1);
        }

        [Test]
        public void NonlethalDamageDoesNotInventAnAttackOrInterruptPlausibleWalking()
        { Walk(); Observe(.2, Enemy(x: .32, health: 75)); Assert.That(Read(.2).IsMoving, Is.True); }

        [TestCase(2, 1)] [TestCase(1, 2)]
        public void NewSessionOrRoundCannotInheritTheOldIdentity(long session, long round)
        {
            Walk(); Assert.That(view.Observe(Snapshot(new[] { Enemy(x: .32) }, session, round), .2), Is.True);
            Idle(.2); Facing(Read(.2), 0, 1);
        }

        [Test]
        public void EnemyIdentityFollowsIdNotArrayPositionAndInstancesRemainIndependent()
        {
            Observe(0, Enemy(4), Enemy(5, x: 1), Enemy(6, x: 2), Enemy(7, x: 3));
            Observe(.1, Enemy(7, x: 3), Enemy(6, x: 2, z: 5.84), Enemy(5, x: 1), Enemy(4, x: .16));
            Facing(Read(.1, 4), 1, 0); Facing(Read(.1, 6), 0, -1); Idle(.1, 5); Idle(.1, 7);
            Observe(.2, Enemy(8, x: .32), Enemy(6, x: 2, z: 5.68));
            Absent(.2, 4); Absent(.2, 5); Absent(.2, 7); Idle(.2, 8); Facing(Read(.2, 8), 0, 1);
            Assert.That(Read(.2, 6).IsMoving, Is.True);
        }

        [TestCase(SessionPhase.Lobby)] [TestCase(SessionPhase.Stopped)] [TestCase((SessionPhase)255)]
        public void InactiveOrInvalidSessionPhaseClearsHistory(SessionPhase phase)
        {
            Walk(); Assert.That(view.Observe(Snapshot(new[] { Enemy() }, phase: phase), .2), Is.False); Absent(.2);
            Observe(.3, Enemy()); Idle(.3);
        }

        [TestCase(EncounterPhase.Dormant)] [TestCase(EncounterPhase.Cleared)]
        [TestCase(EncounterPhase.Failed)] [TestCase((EncounterPhase)255)]
        public void InactiveOrInvalidEncounterNeverRetainsALiveVisual(EncounterPhase phase)
        { Walk(); Assert.That(view.Observe(Snapshot(new[] { Enemy() }, encounter: phase), .2), Is.False); Absent(.2); }

        [Test]
        public void NullObservationClearsAllIdentities()
        { Walk(); Assert.That(view.Observe(null, .2), Is.False); Absent(.2); }

        [Test]
        public void InvalidIdsHealthPositionsOrPoolSizeFailClosedBeforePartialApplication()
        {
            foreach (EnemySnapshot[] enemies in new[] {
                new[] { Enemy(), Enemy() }, new[] { Enemy(health: 101) },
                new[] { Enemy(), Enemy(1, x: double.NaN) }, new[] { Enemy(x: double.PositiveInfinity) },
                new[] { Enemy(x: HostMoveGate.ArenaLimit + 1) },
                new[] { new EnemySnapshot(0, 100, new AimVector(0, 0, 0)) },
                new[] { Enemy(0), Enemy(1), Enemy(2), Enemy(3), Enemy(4) } })
            {
                view.Reset(); Walk(); Assert.That(view.Observe(Snapshot(enemies), .2), Is.False); Absent(.2);
                Observe(.3, Enemy()); Idle(.3);
            }
        }

        [TestCase(0, 1)] [TestCase(1, 0)] [TestCase(-1, 1)] [TestCase(1, -1)]
        public void InvalidSessionOrRoundIdentityClearsHistory(long session, long round)
        { Walk(); Assert.That(view.Observe(Snapshot(new[] { Enemy() }, session, round), .2), Is.False); Absent(.2); }

        [Test]
        public void InvalidReadAndObservationTimesClearWithoutPoisoningTheClock()
        {
            foreach (double now in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1, HostFireGate.MaximumHostTime + 1 })
            {
                view.Reset(); Walk(); Assert.That(view.Observe(Snapshot(new[] { Enemy() }), now), Is.False); Absent(.1);
                Observe(.2, Enemy()); Idle(.2);
                view.Reset(); Walk(); Absent(now); Observe(.2, Enemy()); Idle(.2);
            }
        }

        [Test]
        public void BackwardObservationCannotLowerTheClockBoundary()
        {
            Walk(); Assert.That(view.Observe(Snapshot(new[] { Enemy() }), .05), Is.False);
            Assert.That(view.Observe(Snapshot(new[] { Enemy() }), .075), Is.False); Absent(.1);
            Observe(.2, Enemy()); Idle(.2);
        }

        [Test]
        public void BackwardReadClearsWithoutRestoringOldMotion()
        { Walk(); Absent(.05); Absent(.075); Observe(.2, Enemy()); Idle(.2); }

        [Test]
        public void ExplicitResetAllowsANewClockDomain()
        { Walk(); view.Reset(); Absent(0); Observe(0, Enemy()); Idle(0); }

        [Test]
        public void PresentationNeverChangesSnapshotBytesOrHostRules()
        {
            RangeSnapshot first = Snapshot(new[] { Enemy() }), second = Snapshot(new[] { Enemy(x: .16, health: 75) });
            byte[] firstBytes = LanRangeProtocol.Encode(first), secondBytes = LanRangeProtocol.Encode(second);
            view.Observe(first, 0); view.Observe(second, .1); Read(.2); view.Reset();
            Assert.That(LanRangeProtocol.Encode(first), Is.EqualTo(firstBytes));
            Assert.That(LanRangeProtocol.Encode(second), Is.EqualTo(secondBytes));
            Assert.That(LanRangeProtocol.Version, Is.EqualTo(5)); Assert.That(LanRangeProtocol.EnemyBytes, Is.EqualTo(14));
            Assert.That(HostInfectedEncounter.AttackInterval, Is.EqualTo(1)); Assert.That(HostInfectedEncounter.MeleeDamage, Is.EqualTo(10));
        }
    }
}
