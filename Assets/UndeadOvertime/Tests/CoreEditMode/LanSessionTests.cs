using System;
using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class LanSessionTests
    {
        [TestCase(0)] [TestCase(11)]
        public void CapacityMustBeOneToTen(int capacity) => Assert.Throws<ArgumentOutOfRangeException>(() => new LanSession(1, capacity));

        [TestCase(0)] [TestCase(-1)]
        public void ProtocolMustBePositive(int protocol) => Assert.Throws<ArgumentOutOfRangeException>(() => new LanSession(protocol));

        [Test]
        public void HostCountsTowardTenAndEleventhIsRejected()
        {
            var session = new LanSession(7);
            var host = session.StartHost(0);
            Assert.That(session.IsCurrent(host), Is.True);
            for (ulong peer = 1; peer < 10; peer++)
                Assert.That(session.TryJoin(peer, 7, out _), Is.EqualTo(JoinResult.Accepted));
            Assert.That(session.PlayerCount, Is.EqualTo(10));
            Assert.That(session.TryJoin(10, 7, out var rejected), Is.EqualTo(JoinResult.Full));
            Assert.That(session.IsCurrent(rejected), Is.False);
        }

        [Test]
        public void ProtocolAndDuplicateRejectionDoNotConsumeCapacity()
        {
            var session = new LanSession(7, 2);
            session.StartHost(0);
            Assert.That(session.TryJoin(1, 6, out _), Is.EqualTo(JoinResult.ProtocolMismatch));
            Assert.That(session.TryJoin(0, 7, out _), Is.EqualTo(JoinResult.AlreadyConnected));
            Assert.That(session.PlayerCount, Is.EqualTo(1));
            Assert.That(session.TryJoin(1, 7, out _), Is.EqualTo(JoinResult.Accepted));
        }

        [Test]
        public void LateJoinIsRejectedButLobbyRejoinGetsNewTicket()
        {
            var session = new LanSession(1);
            var host = session.StartHost(0);
            session.TryJoin(1, 1, out var old);
            Assert.That(session.TryBeginRound(host), Is.True);
            Assert.That(session.Disconnect(old), Is.True);
            Assert.That(session.TryJoin(1, 1, out _), Is.EqualTo(JoinResult.NotInLobby));
            Assert.That(session.TryReturnToLobby(host), Is.True);
            Assert.That(session.TryJoin(1, 1, out var current), Is.EqualTo(JoinResult.Accepted));
            Assert.That(current.Equals(old), Is.False);
            Assert.That(session.IsCurrent(old), Is.False);
            Assert.That(session.Disconnect(old), Is.False);
            Assert.That(session.IsCurrent(current), Is.True);
        }

        [Test]
        public void RemoteMemberCannotControlMatchLifecycle()
        {
            var session = new LanSession(1);
            var host = session.StartHost(0);
            session.TryJoin(1, 1, out var remote);
            Assert.That(session.TryBeginRound(remote), Is.False);
            Assert.That(session.TryStop(remote), Is.False);
            Assert.That(session.TryBeginRound(host), Is.True);
            Assert.That(session.TryReturnToLobby(remote), Is.False);
            Assert.That(session.TryBeginRound(host), Is.False);
            Assert.That(session.RoundId, Is.EqualTo(1));
        }

        [Test]
        public void HostDisconnectClearsEveryMemberAndOldSessionTickets()
        {
            var session = new LanSession(1);
            var host = session.StartHost(42);
            session.TryJoin(1, 1, out var remote);
            session.TryBeginRound(host);
            Assert.That(session.Disconnect(host), Is.True);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Stopped));
            Assert.That(session.PlayerCount, Is.Zero);
            Assert.That(session.IsCurrent(remote), Is.False);
            var restarted = session.StartHost(42);
            Assert.That(restarted.Equals(host), Is.False);
            Assert.That(session.TryStop(host), Is.False);
            Assert.That(session.IsCurrent(restarted), Is.True);
        }

        [Test]
        public void RepeatedCyclesStayBoundedAndSnapshotsCannotMutateMembership()
        {
            var session = new LanSession(1);
            for (int cycle = 0; cycle < 100; cycle++)
            {
                var host = session.StartHost(0);
                var snapshot = session.SnapshotMembers();
                snapshot[0] = default;
                Assert.That(session.IsCurrent(host), Is.True);
                Assert.Throws<InvalidOperationException>(() => session.StartHost(1));
                Assert.That(session.TryStop(host), Is.True);
                Assert.That(session.TryStop(host), Is.False);
                Assert.That(session.PlayerCount, Is.Zero);
            }
        }

        [Test]
        public void SoloRoundUsesSameLifecycle()
        {
            var session = new LanSession(1, 1);
            var host = session.StartHost(0);
            Assert.That(session.TryBeginRound(host), Is.True);
            Assert.That(session.PlayerCount, Is.EqualTo(1));
            Assert.That(session.TryReturnToLobby(host), Is.True);
            Assert.That(session.TryBeginRound(host), Is.True);
            Assert.That(session.RoundId, Is.EqualTo(2));
        }
    }
}
