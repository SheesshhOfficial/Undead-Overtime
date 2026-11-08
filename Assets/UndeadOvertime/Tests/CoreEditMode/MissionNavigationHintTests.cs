using System;
using NUnit.Framework;
namespace UndeadOvertime.Core.Tests
{
    public sealed class MissionNavigationHintTests
    {
        private static readonly AimVector Eye = new AimVector(0, 1.6, 0), Forward = new AimVector(0, 0, 1);
        private static CoopMissionSnapshot Power => new CoopMissionSnapshot(5, 1, CoopMissionPhase.RestorePower, true, false, 0);
        private static AimVector Position(double x, double z) => new AimVector(x, 1.6, z);
        private static bool Hint(AimVector target, AimVector facing, out MissionNavigationHint hint) =>
            MissionNavigationHint.TryCreate(Power, Eye, facing, target, Position(0, 12), out hint);

        [TestCase(0, 5, NavigationBearing.Ahead)] [TestCase(5, 5, NavigationBearing.AheadRight)]
        [TestCase(5, 0, NavigationBearing.Right)] [TestCase(5, -5, NavigationBearing.BehindRight)]
        [TestCase(0, -5, NavigationBearing.Behind)] [TestCase(-5, -5, NavigationBearing.BehindLeft)]
        [TestCase(-5, 0, NavigationBearing.Left)] [TestCase(-5, 5, NavigationBearing.AheadLeft)]
        public void EightViewRelativeDirectionsHaveCorrectHandedness(double x, double z, NavigationBearing expected)
        {
            Assert.That(Hint(Position(x, z), Forward, out MissionNavigationHint hint), Is.True);
            Assert.That(hint.Bearing, Is.EqualTo(expected)); Assert.That(hint.Goal, Is.EqualTo(MissionNavigationGoal.PowerConsole));
            Assert.That(hint.DistanceMeters, Is.EqualTo(Math.Sqrt(x * x + z * z)).Within(1e-9));
            Assert.That(hint.GoalText, Is.EqualTo("Power console")); Assert.That(hint.BearingText, Is.Not.Empty);
        }
        [TestCase(0)] [TestCase(90)] [TestCase(180)] [TestCase(270)]
        public void RotatingViewerAndTargetTogetherPreservesBearing(double degrees)
        {
            double angle = degrees * Math.PI / 180;
            var forward = new AimVector(Math.Sin(angle), 0, Math.Cos(angle));
            var target = Position(5 * Math.Sin(angle + Math.PI / 4), 5 * Math.Cos(angle + Math.PI / 4));
            Assert.That(Hint(target, forward, out MissionNavigationHint hint), Is.True);
            Assert.That(hint.Bearing, Is.EqualTo(NavigationBearing.AheadRight));
        }
        [TestCase(22.49, NavigationBearing.Ahead)] [TestCase(22.51, NavigationBearing.AheadRight)]
        [TestCase(-22.49, NavigationBearing.Ahead)] [TestCase(-22.51, NavigationBearing.AheadLeft)]
        public void SectorBoundaryDoesNotFlipToTheWrongSide(double degrees, NavigationBearing expected)
        {
            double angle = degrees * Math.PI / 180;
            Assert.That(Hint(Position(5 * Math.Sin(angle), 5 * Math.Cos(angle)), Forward, out MissionNavigationHint hint), Is.True);
            Assert.That(hint.Bearing, Is.EqualTo(expected));
        }
        [TestCase(0)] [TestCase(.5)]
        public void NearbyThresholdIsLocationOnlyNotInteractionApproval(double distance)
        {
            Assert.That(Hint(Position(distance, 0), Forward, out MissionNavigationHint hint), Is.True);
            Assert.That(hint.Bearing, Is.EqualTo(NavigationBearing.Nearby)); Assert.That(hint.BearingText, Is.EqualTo("nearby"));
            Assert.That(hint.DistanceMeters, Is.EqualTo(distance));
        }
        [Test]
        public void PitchAndForwardScaleDoNotChangePlanarDistanceOrDirection()
        {
            Assert.That(Hint(Position(3, 4), new AimVector(0, .96, .28), out MissionNavigationHint pitched), Is.True);
            Assert.That(Hint(Position(3, 4), new AimVector(0, -50, 20), out MissionNavigationHint scaled), Is.True);
            Assert.That(pitched.Bearing, Is.EqualTo(scaled.Bearing)); Assert.That(pitched.DistanceMeters, Is.EqualTo(5));
        }
        [Test]
        public void ExtractionUsesItsOwnWaypointAfterValidatedGateTransition()
        {
            var mission = new CoopMissionSnapshot(5, 2, CoopMissionPhase.ReachExtraction, true, true, 1);
            Assert.That(MissionNavigationHint.TryCreate(mission, Eye, Forward, Position(-9, -5), Position(0, 12), out MissionNavigationHint hint), Is.True);
            Assert.That(hint.Goal, Is.EqualTo(MissionNavigationGoal.Extraction)); Assert.That(hint.GoalText, Is.EqualTo("Extraction"));
            Assert.That(hint.Bearing, Is.EqualTo(NavigationBearing.Ahead)); Assert.That(hint.DistanceMeters, Is.EqualTo(12));
        }
        [TestCase(CoopMissionPhase.DefendDepot)] [TestCase(CoopMissionPhase.Completed)]
        [TestCase(CoopMissionPhase.Failed)] [TestCase((CoopMissionPhase)255)]
        public void NonNavigationPhasesNeverInventAWaypoint(CoopMissionPhase phase)
        {
            var mission = new CoopMissionSnapshot(5, 1, phase, true, true, 0);
            Assert.That(MissionNavigationHint.TryCreate(mission, Eye, Forward, Position(-9, -5), Position(0, 12), out MissionNavigationHint hint), Is.False);
            Assert.That(hint.Goal, Is.EqualTo(MissionNavigationGoal.None));
        }
        [Test]
        public void InvalidIdentityLifecycleGateAndHoldFailClosed()
        {
            foreach (var mission in new[] {
                new CoopMissionSnapshot(0, 1, CoopMissionPhase.RestorePower, true, false, 0),
                new CoopMissionSnapshot(5, 0, CoopMissionPhase.RestorePower, true, false, 0),
                new CoopMissionSnapshot(5, 1, CoopMissionPhase.RestorePower, false, false, 0),
                new CoopMissionSnapshot(5, 1, CoopMissionPhase.RestorePower, true, true, 0),
                new CoopMissionSnapshot(5, 1, CoopMissionPhase.ReachExtraction, true, false, 0),
                new CoopMissionSnapshot(5, 1, CoopMissionPhase.RestorePower, true, false, double.NaN),
                new CoopMissionSnapshot(5, 1, CoopMissionPhase.RestorePower, true, false, -.1),
                new CoopMissionSnapshot(5, 1, CoopMissionPhase.RestorePower, true, false, 3.1),
                new CoopMissionSnapshot(5, 1, CoopMissionPhase.ReachExtraction, true, true, 2.1) })
                Assert.That(MissionNavigationHint.TryCreate(mission, Eye, Forward, Position(-9, -5), Position(0, 12), out _), Is.False);
        }
        [Test]
        public void InvalidPositionOrVerticalDegenerateFacingNeverProducesNaNText()
        {
            foreach (var look in new[] { default(AimVector), new AimVector(0, 1, 0), new AimVector(double.NaN, 0, 1),
                new AimVector(0, double.PositiveInfinity, 1), new AimVector(double.MaxValue, 0, double.MaxValue) })
                Assert.That(Hint(Position(0, 5), look, out _), Is.False);
            foreach (var bad in new[] { Position(100, 0), new AimVector(0, 0, 0), new AimVector(0, 1.6, double.NaN) })
            {
                Assert.That(MissionNavigationHint.TryCreate(Power, bad, Forward, Eye, Eye, out _), Is.False);
                Assert.That(MissionNavigationHint.TryCreate(Power, Eye, Forward, bad, Eye, out _), Is.False);
                Assert.That(MissionNavigationHint.TryCreate(Power, Eye, Forward, Eye, bad, out _), Is.False);
            }
        }
    }
}
