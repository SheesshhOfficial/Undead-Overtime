using System;
using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class HostFireGateTests
    {
        private LanSession session;
        private PlayerTicket host, remote;
        private HostFireGate gate;
        private static readonly AimVector Origin = new AimVector(0, 1.4, 0);
        private static readonly AimVector Forward = new AimVector(0, 0, 1);

        [SetUp]
        public void Setup()
        {
            session = new LanSession(1);
            host = session.StartHost(0);
            session.TryJoin(1, 1, out remote);
            session.TryBeginRound(host);
            gate = new HostFireGate(session, 0);
            gate.SetPoseFromHost(host, Origin, Forward);
            gate.SetPoseFromHost(remote, Origin, Forward);
        }

        private FireRequest Request(ulong sequence, AimVector? origin = null, AimVector? direction = null) =>
            new FireRequest(session.SessionId, session.RoundId, sequence, origin ?? Origin, direction ?? Forward);

        [Test]
        public void ShotUsesServerOriginAndRulesNotClientOutcome()
        {
            var closeOrigin = new AimVector(0.1, 1.4, 0);
            Assert.That(gate.TryAuthorize(remote, Request(1, closeOrigin), 0, out var shot), Is.EqualTo(FireResult.Accepted));
            Assert.That(shot.Shooter, Is.EqualTo(remote));
            Assert.That(shot.Origin.X, Is.Zero);
            Assert.That(shot.Origin.Y, Is.EqualTo(1.4));
            Assert.That(shot.Damage, Is.EqualTo(25));
            Assert.That(shot.Range, Is.EqualTo(100));
            Assert.That(gate.Ammo(remote), Is.EqualTo(11));
            Assert.That(gate.Ammo(host), Is.EqualTo(12));
        }

        [Test]
        public void UnknownAndStaleConnectionCannotFire()
        {
            var forged = new PlayerTicket(remote.PeerId, remote.SessionId, remote.ConnectionId + 5);
            Assert.That(gate.TryAuthorize(forged, Request(1), 0, out _), Is.EqualTo(FireResult.UnknownPlayer));
            session.Disconnect(remote);
            Assert.That(gate.TryAuthorize(remote, Request(2), 0, out _), Is.EqualTo(FireResult.UnknownPlayer));
            Assert.That(gate.SetPoseFromHost(remote, Origin, Forward), Is.False);
        }

        [Test]
        public void RoundTransitionsInvalidateEntireGateAndOldRequests()
        {
            long oldRound = session.RoundId;
            session.TryReturnToLobby(host);
            Assert.That(gate.TryAuthorize(host, Request(1), 0, out _), Is.EqualTo(FireResult.StaleRound));
            session.TryBeginRound(host);
            Assert.That(gate.TryAuthorize(host, Request(2), 0, out _), Is.EqualTo(FireResult.StaleRound));
            gate = new HostFireGate(session, 0);
            gate.SetPoseFromHost(host, Origin, Forward);
            Assert.That(gate.TryAuthorize(host, new FireRequest(session.SessionId, oldRound, 1, Origin, Forward), 0, out _), Is.EqualTo(FireResult.StaleRound));
            Assert.That(gate.TryAuthorize(host, Request(1), 0, out _), Is.EqualTo(FireResult.Accepted));
        }

        [Test]
        public void RestartCannotReviveAnOldGateEvenWhenRoundNumberMatches()
        {
            FireRequest oldRequest = Request(1);
            session.TryStop(host);
            host = session.StartHost(0);
            session.TryBeginRound(host);
            Assert.That(session.RoundId, Is.EqualTo(1));
            Assert.That(gate.IsCurrentRound, Is.False);
            Assert.That(gate.TryAuthorize(host, Request(1), 0, out _), Is.EqualTo(FireResult.StaleRound));
            gate = new HostFireGate(session, 0);
            gate.SetPoseFromHost(host, Origin, Forward);
            Assert.That(gate.TryAuthorize(host, oldRequest, 0, out _), Is.EqualTo(FireResult.StaleRound));
        }

        [Test]
        public void NoPoseRejectsWithoutSpendingAmmoAndConsumesSequence()
        {
            gate = new HostFireGate(session, 0);
            Assert.That(gate.TryAuthorize(host, Request(1), 0, out _), Is.EqualTo(FireResult.NoHostPose));
            gate.SetPoseFromHost(host, Origin, Forward);
            Assert.That(gate.TryAuthorize(host, Request(1), 0, out _), Is.EqualTo(FireResult.InvalidSequence));
            Assert.That(gate.Ammo(host), Is.EqualTo(12));
        }

        [Test]
        public void CooldownIsPerPlayerAndRejectedRequestCannotReplayLater()
        {
            Assert.That(gate.TryAuthorize(host, Request(1), 0, out _), Is.EqualTo(FireResult.Accepted));
            Assert.That(gate.TryAuthorize(remote, Request(1), 0, out _), Is.EqualTo(FireResult.Accepted));
            Assert.That(gate.TryAuthorize(host, Request(2), 0.1, out _), Is.EqualTo(FireResult.RateLimited));
            Assert.That(gate.TryAuthorize(host, Request(2), 0.2, out _), Is.EqualTo(FireResult.InvalidSequence));
            Assert.That(gate.TryAuthorize(host, Request(3), 0.2, out _), Is.EqualTo(FireResult.Accepted));
            Assert.That(gate.Ammo(host), Is.EqualTo(10));
        }

        [Test]
        public void SequenceZeroDuplicateAndReorderingAreRejected()
        {
            Assert.That(gate.TryAuthorize(host, Request(0), 0, out _), Is.EqualTo(FireResult.InvalidSequence));
            Assert.That(gate.TryAuthorize(host, Request(10), 0, out _), Is.EqualTo(FireResult.Accepted));
            Assert.That(gate.TryAuthorize(host, Request(9), 1, out _), Is.EqualTo(FireResult.InvalidSequence));
            Assert.That(gate.TryAuthorize(host, Request(10), 1, out _), Is.EqualTo(FireResult.InvalidSequence));
        }

        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)] [TestCase(-1d)] [TestCase(double.MaxValue)]
        public void InvalidServerClockRejectedWithoutSequenceConsumption(double time)
        {
            Assert.That(gate.TryAuthorize(host, Request(1), time, out _), Is.EqualTo(FireResult.InvalidTime));
            Assert.That(gate.TryAuthorize(host, Request(1), 0, out _), Is.EqualTo(FireResult.Accepted));
        }

        [Test]
        public void ClockCannotMoveBackwardsAcrossPlayers()
        {
            Assert.That(gate.TryAuthorize(host, Request(1), 2, out _), Is.EqualTo(FireResult.Accepted));
            Assert.That(gate.TryAuthorize(remote, Request(1), 1, out _), Is.EqualTo(FireResult.InvalidTime));
            Assert.That(gate.TryAuthorize(remote, Request(1), 2, out _), Is.EqualTo(FireResult.Accepted));
        }

        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)]
        public void NonFiniteClientDataRejected(double value)
        {
            Assert.That(gate.TryAuthorize(host, Request(1, new AimVector(value, 0, 0)), 0, out _), Is.EqualTo(FireResult.InvalidAim));
            Assert.That(gate.TryAuthorize(host, Request(2, direction: new AimVector(value, 0, 1)), 0, out _), Is.EqualTo(FireResult.InvalidAim));
            Assert.That(gate.Ammo(host), Is.EqualTo(12));
        }

        [TestCase(0d)] [TestCase(2d)] [TestCase(1e300)]
        public void NonUnitDirectionRejected(double z)
        {
            Assert.That(gate.TryAuthorize(host, Request(1, direction: new AimVector(0, 0, z)), 0, out _), Is.EqualTo(FireResult.InvalidAim));
        }

        [Test]
        public void SpoofedOriginOrAimCannotAuthorizeDamage()
        {
            Assert.That(gate.TryAuthorize(host, Request(1, new AimVector(2, 1.4, 0)), 0, out _), Is.EqualTo(FireResult.OriginMismatch));
            Assert.That(gate.TryAuthorize(host, Request(2, direction: new AimVector(0, 0, -1)), 0, out _), Is.EqualTo(FireResult.AimMismatch));
            Assert.That(gate.Ammo(host), Is.EqualTo(12));
        }

        [Test]
        public void AcceptedNearUnitDirectionIsNormalized()
        {
            Assert.That(gate.TryAuthorize(host, Request(1, direction: new AimVector(0, 0, 1.0001)), 0, out var shot), Is.EqualTo(FireResult.Accepted));
            Assert.That(shot.Direction.LengthSquared, Is.EqualTo(1).Within(1e-12));
        }

        [Test]
        public void EmptyMagazineReloadAndRepeatedReloadAreBounded()
        {
            for (ulong sequence = 1; sequence <= 12; sequence++)
                Assert.That(gate.TryAuthorize(host, Request(sequence), sequence, out _), Is.EqualTo(FireResult.Accepted));
            Assert.That(gate.TryAuthorize(host, Request(13), 13, out _), Is.EqualTo(FireResult.Empty));
            Assert.That(gate.TryBeginReloadFromHost(host, 13), Is.True);
            Assert.That(gate.TryBeginReloadFromHost(host, 13.1), Is.False);
            Assert.That(gate.TryAuthorize(host, Request(14), 14, out _), Is.EqualTo(FireResult.Reloading));
            Assert.That(gate.TryAuthorize(host, Request(15), 14.5, out _), Is.EqualTo(FireResult.Accepted));
            Assert.That(gate.Ammo(host), Is.EqualTo(11));
        }

        [Test]
        public void DowningCancelsReloadAndReviveCannotReplayRejectedShot()
        {
            gate.TryAuthorize(host, Request(1), 0, out _);
            Assert.That(gate.TryBeginReloadFromHost(host, 0.1), Is.True);
            gate.SetAliveFromHost(host, false);
            Assert.That(gate.TryBeginReloadFromHost(host, 1), Is.False);
            Assert.That(gate.TryAuthorize(host, Request(2), 2, out _), Is.EqualTo(FireResult.Downed));
            gate.SetAliveFromHost(host, true);
            Assert.That(gate.TryAuthorize(host, Request(2), 3, out _), Is.EqualTo(FireResult.InvalidSequence));
            Assert.That(gate.TryAuthorize(host, Request(3), 3, out _), Is.EqualTo(FireResult.Accepted));
            Assert.That(gate.Ammo(host), Is.EqualTo(10));
        }

        [Test]
        public void FullMagazineDoesNotReloadAndInvalidPoseDoesNotReplaceValidPose()
        {
            Assert.That(gate.TryBeginReloadFromHost(host, 0), Is.False);
            Assert.That(gate.SetPoseFromHost(host, new AimVector(double.NaN, 0, 0), Forward), Is.False);
            Assert.That(gate.SetPoseFromHost(host, Origin, default), Is.False);
            Assert.That(gate.TryAuthorize(host, Request(1), 0, out _), Is.EqualTo(FireResult.Accepted));
        }

        [Test]
        public void GateCannotBeConstructedBeforeRound()
        {
            Assert.Throws<ArgumentNullException>(() => new HostFireGate(null, 0));
            Assert.Throws<InvalidOperationException>(() => new HostFireGate(new LanSession(1), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HostFireGate(session, double.NaN));
        }
    }
}
