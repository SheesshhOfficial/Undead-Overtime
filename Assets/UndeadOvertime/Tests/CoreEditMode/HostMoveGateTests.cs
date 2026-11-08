using System;
using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class HostMoveGateTests
    {
        private LanSession session;
        private HostMoveGate gate;
        private PlayerTicket host, client;
        private static readonly AimVector Forward = new AimVector(0, 0, 1);
        [SetUp]
        public void Setup()
        {
            session = new LanSession(2); host = session.StartHost(0); session.TryJoin(5, 2, out client);
            session.TryBeginRound(host); gate = new HostMoveGate(session, 0);
        }
        private bool Input(ulong sequence, AimVector input, double time) => gate.TrySetIntent(client, 1, 1, sequence, input, time);
        private AimVector Step(double time)
        {
            Assert.That(gate.TryStepFromHost(time, out MoveStep[] steps), Is.True);
            foreach (var step in steps) if (step.Player.Equals(client)) return step.Displacement;
            return default;
        }
        [Test]
        public void PacketFloodCannotMultiplyHostTickDisplacement()
        {
            Assert.That(Input(1, Forward, 0), Is.True);
            for (ulong i = 2; i <= 1000; i++) Assert.That(Input(i, Forward, .04), Is.True);
            Assert.That(Step(.05).LengthSquared, Is.Zero); // First tick activates pending intent, never retroactively moves it.
            Assert.That(Step(.05).LengthSquared, Is.Zero);
            Assert.That(Step(.07).Z, Is.EqualTo(.08).Within(.000001));
        }
        [Test]
        public void RefreshPreservesWalkSpeedAndSilenceExpiresIntent()
        {
            Input(1, Forward, 0); Step(.04); Input(2, Forward, .05);
            Assert.That(Step(.06).Z, Is.EqualTo(.08).Within(.000001));
            Assert.That(Step(.3).LengthSquared, Is.Zero);
            Assert.That(Step(1).LengthSquared, Is.Zero);
        }
        [Test]
        public void ExplicitStopIsValidAndFreshIntentIsNotRetroactive()
        {
            Input(1, Forward, .04); Assert.That(Step(.05).LengthSquared, Is.Zero);
            Assert.That(Step(.06).Z, Is.EqualTo(.04).Within(.000001));
            Input(2, default, .06); Assert.That(Step(.07).LengthSquared, Is.Zero);
        }
        [Test]
        public void LongHostFrameIsCappedAndInvalidClockRejected()
        {
            Input(1, Forward, .9); Assert.That(Step(1).LengthSquared, Is.Zero);
            Assert.That(Step(1.1).Z, Is.EqualTo(.2).Within(.000001));
            Assert.That(gate.TryStepFromHost(.99, out _), Is.False);
            Assert.That(gate.TryStepFromHost(double.NaN, out _), Is.False);
            Assert.That(Input(2, Forward, double.PositiveInfinity), Is.False);
        }
        [Test]
        public void DirectionChangesApplyAtTickBoundariesWithoutDroppingPreviousTravel()
        {
            Input(1, Forward, 0); Step(0);
            Assert.That(Step(.04).Z, Is.EqualTo(.16).Within(.000001));
            Input(2, new AimVector(1, 0, 0), .059);
            AimVector beforeTurn = Step(.06), afterTurn = Step(.08);
            Assert.That(beforeTurn.Z, Is.EqualTo(.08).Within(.000001)); Assert.That(beforeTurn.X, Is.Zero);
            Assert.That(afterTurn.X, Is.EqualTo(.08).Within(.000001)); Assert.That(afterTurn.Z, Is.Zero);
        }
        [Test]
        public void DiagonalAndNearUnitInputsNeverExceedWalkSpeed()
        {
            Assert.That(Input(1, new AimVector(.7072, 0, .7072), 0), Is.True);
            Step(0); AimVector delta = Step(.05);
            Assert.That(delta.LengthSquared, Is.EqualTo(.04).Within(.000001));
            Assert.That(delta.X, Is.EqualTo(delta.Z).Within(.000001));
        }
        [TestCase(2, 0, 0)] [TestCase(0, 1, 0)] [TestCase(double.NaN, 0, 0)] [TestCase(double.PositiveInfinity, 0, 0)]
        public void InvalidIntentCannotBeReplayedWithTheSameSequence(double x, double y, double z)
        {
            Assert.That(Input(1, new AimVector(x, y, z), 0), Is.False);
            Assert.That(Input(1, Forward, .01), Is.False);
            Assert.That(Input(2, Forward, .02), Is.True);
        }
        [Test]
        public void OldRoundIdentityAndDisconnectedTicketsAreRejected()
        {
            Assert.That(gate.TrySetIntent(client, 8, 1, 1, Forward, 0), Is.False);
            Assert.That(gate.TrySetIntent(client, 1, 8, 1, Forward, 0), Is.False);
            Assert.That(Input(0, Forward, 0), Is.False);
            Input(1, Forward, 0); session.Disconnect(client);
            Assert.That(Input(2, Forward, .01), Is.False); Assert.That(Step(.02).LengthSquared, Is.Zero);
            session.TryReturnToLobby(host); session.TryJoin(5, 2, out PlayerTicket replacement); session.TryBeginRound(host);
            Assert.That(gate.TrySetIntent(replacement, 1, 2, 2, Forward, .03), Is.False);
            Assert.That(gate.TryStepFromHost(.04, out _), Is.False);
        }
        [Test]
        public void MoveWireAcceptsStopAndRejectsVerticalOrUnboundedVectors()
        {
            foreach (AimVector input in new[] { default(AimVector), Forward })
            {
                byte[] bytes = LanRangeProtocol.Encode(new RangeCommand(RangeAction.Move, 1, 1, 1, input));
                Assert.That(bytes.Length, Is.EqualTo(44));
                Assert.That(LanRangeProtocol.TryCommand(bytes, out RangeCommand value), Is.True);
                Assert.That(value.Action, Is.EqualTo(RangeAction.Move)); Assert.That(value.Aim.Z, Is.EqualTo(input.Z));
            }
            Assert.Throws<ArgumentException>(() => LanRangeProtocol.Encode(new RangeCommand(RangeAction.Move, 1, 1, 1, new AimVector(0, 1, 0))));
            byte[] old = LanRangeProtocol.Handshake; old[3] = 4;
            Assert.That(LanRangeProtocol.AcceptHandshake(old), Is.False);
        }
        [Test]
        public void PositionSnapshotsRoundTripAndRejectNonFiniteOrOutOfArenaCoordinates()
        {
            var hp = new[] { 100, 100, 100 }; var position = new AimVector(2, 1.6, -4);
            byte[] bytes = LanRangeProtocol.Encode(new RangeSnapshot(1, 1, SessionPhase.Playing,
                new[] { new RangeMember(0, 1, 0, 12, false, position) }, hp));
            Assert.That(bytes.Length, Is.EqualTo(80)); Assert.That(LanRangeProtocol.MaximumBytes, Is.EqualTo(442));
            Assert.That(LanRangeProtocol.TrySnapshot(bytes, out RangeSnapshot state), Is.True);
            Assert.That(state.Member(0).Position.X, Is.EqualTo(2)); Assert.That(state.Member(0).Position.Z, Is.EqualTo(-4));
            Array.Copy(BitConverter.GetBytes(float.NaN), 0, bytes, LanRangeProtocol.SnapshotHeaderBytes + 19, 4);
            Assert.That(LanRangeProtocol.TrySnapshot(bytes, out _), Is.False);
            foreach (var bad in new[] { new AimVector(100, 1.6, 0), new AimVector(0, 4, 0), new AimVector(0, 1.6, double.NaN) })
                Assert.Throws<ArgumentException>(() => LanRangeProtocol.Encode(new RangeSnapshot(1, 1, SessionPhase.Playing,
                    new[] { new RangeMember(0, 1, 0, 12, false, bad) }, hp)));
        }
    }
}
