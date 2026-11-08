using System;
using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class LanRangeProtocolTests
    {
        private static readonly AimVector Forward = new AimVector(0, 0, 1);
        [TestCase(RangeAction.Fire)] [TestCase(RangeAction.Reload)]
        public void CommandsRoundTripExactlyWithNoAuthorityFields(RangeAction action)
        {
            byte[] bytes = LanRangeProtocol.Encode(new RangeCommand(action, 3, 2, 99, Forward));
            Assert.That(bytes.Length, Is.EqualTo(LanRangeProtocol.CommandBytes));
            Assert.That(LanRangeProtocol.TryCommand(bytes, out RangeCommand value), Is.True);
            Assert.That(value.Action, Is.EqualTo(action)); Assert.That(value.SessionId, Is.EqualTo(3));
            Assert.That(value.RoundId, Is.EqualTo(2)); Assert.That(value.Sequence, Is.EqualTo(99));
            Assert.That(value.Aim.Z, Is.EqualTo(1));
        }
        [Test]
        public void EveryTruncatedCommandAndExtraByteAreRejected()
        {
            byte[] bytes = LanRangeProtocol.Encode(new RangeCommand(RangeAction.Fire, 1, 1, 1, Forward));
            for (int n = 0; n < bytes.Length; n++)
            { var part = new byte[n]; Array.Copy(bytes, part, n); Assert.That(LanRangeProtocol.TryCommand(part, out _), Is.False); }
            Assert.That(LanRangeProtocol.TryCommand(null, out _), Is.False);
            var extra = new byte[bytes.Length + 1]; Array.Copy(bytes, extra, bytes.Length);
            Assert.That(LanRangeProtocol.TryCommand(extra, out _), Is.False);
        }
        [Test]
        public void InvalidCommandVersionKindActionAimAndSequenceAreRejected()
        {
            byte[] original = LanRangeProtocol.Encode(new RangeCommand(RangeAction.Fire, 1, 1, 1, Forward));
            foreach (int offset in new[] { 0, 4, 6, 7 })
            { var bytes = (byte[])original.Clone(); bytes[offset] = 255; Assert.That(LanRangeProtocol.TryCommand(bytes, out _), Is.False); }
            var nan = (byte[])original.Clone(); Array.Copy(BitConverter.GetBytes(float.NaN), 0, nan, 32, 4);
            Assert.That(LanRangeProtocol.TryCommand(nan, out _), Is.False);
            Assert.Throws<ArgumentException>(() => LanRangeProtocol.Encode(new RangeCommand(RangeAction.Fire, 1, 1, 0, Forward)));
            Assert.Throws<ArgumentException>(() => LanRangeProtocol.Encode(new RangeCommand(RangeAction.Fire, 1, 1, 1, new AimVector(0, 0, 2))));
        }
        [Test]
        public void MaximumSnapshotRoundTripsAndCopiesInputArrays()
        {
            var members = new RangeMember[10]; var hp = new[] { 100, 75, 0 };
            for (byte i = 0; i < 10; i++) members[i] = new RangeMember(i, i + 1, i, 11, i == 2);
            var original = new RangeSnapshot(5, 2, SessionPhase.Playing, members, hp);
            members[0] = default; hp[0] = -1;
            byte[] bytes = LanRangeProtocol.Encode(original);
            Assert.That(bytes.Length, Is.EqualTo(LanRangeProtocol.SnapshotHeaderBytes + 10 * LanRangeProtocol.MemberBytes));
            Assert.That(LanRangeProtocol.TrySnapshot(bytes, out RangeSnapshot value), Is.True);
            Assert.That(value.MemberCount, Is.EqualTo(10)); Assert.That(value.Member(2).Reloading, Is.True);
            Assert.That(value.Member(0).ConnectionId, Is.EqualTo(1)); Assert.That(value.TargetHealth(0), Is.EqualTo(100));
            Assert.That(value.TargetHealth(2), Is.Zero);
            for (int n = 0; n < bytes.Length; n++)
            { var part = new byte[n]; Array.Copy(bytes, part, n); Assert.That(LanRangeProtocol.TrySnapshot(part, out _), Is.False); }
        }
        [Test]
        public void SnapshotRejectsDuplicateSlotsAndUnboundedData()
        {
            var members = new[] { new RangeMember(0, 1, 0, 12, false), new RangeMember(1, 2, 0, 12, false) };
            Assert.Throws<ArgumentException>(() => LanRangeProtocol.Encode(new RangeSnapshot(1, 1, SessionPhase.Playing, members, new[] { 100, 100, 100 })));
            Assert.That(LanRangeProtocol.TrySnapshot(new byte[LanRangeProtocol.MaximumBytes + 1], out _), Is.False);
            Assert.That(LanRangeProtocol.TrySnapshot(null, out _), Is.False);
            Assert.Throws<ArgumentException>(() => new RangeSnapshot(1, 1, SessionPhase.Playing, members, new[] { 100 }));
            byte[] bytes = LanRangeProtocol.Encode(new RangeSnapshot(1, 0, SessionPhase.Lobby,
                new[] { new RangeMember(0, 1, 0, 0, false) }, new[] { 100, 100, 100 }));
            bytes[24] = 255; Assert.That(LanRangeProtocol.TrySnapshot(bytes, out _), Is.False);
        }
        [Test]
        public void HandshakeIsExactAndNotMutableSharedState()
        {
            byte[] handshake = LanRangeProtocol.Handshake; handshake[0] = 0;
            Assert.That(LanRangeProtocol.AcceptHandshake(handshake), Is.False);
            Assert.That(LanRangeProtocol.AcceptHandshake(null), Is.False);
            Assert.That(LanRangeProtocol.AcceptHandshake(new byte[100]), Is.False);
            Assert.That(LanRangeProtocol.AcceptHandshake(LanRangeProtocol.Handshake), Is.True);
        }
        [TestCase("127.0.0.1", "7777")][TestCase("10.0.0.2", "1")]
        [TestCase("172.16.0.2", "65535")][TestCase("192.168.1.7", "7777")][TestCase("169.254.1.2", "7777")]
        public void ExplicitLocalEndpointsAccepted(string ip, string port)
        { Assert.That(LanEndpoint.TryParse(ip, port, out _), Is.True); }
        [TestCase("0.0.0.0", "7777")][TestCase("8.8.8.8", "7777")][TestCase("localhost", "7777")]
        [TestCase("127.1", "7777")][TestCase("127.00.0.1", "7777")][TestCase("192.168.1.255", "7777")]
        [TestCase("127.0.0.1", "0")][TestCase("127.0.0.1", "65536")][TestCase("127.0.0.1", "+7777")]
        public void InternetWildcardAndAmbiguousEndpointsRejected(string ip, string port)
        { Assert.That(LanEndpoint.TryParse(ip, port, out _), Is.False); }
        [Test]
        public void TrustedTickCompletesReloadWithoutAFireCommand()
        {
            var session = new LanSession(1); PlayerTicket host = session.StartHost(0); session.TryBeginRound(host);
            var gate = new HostFireGate(session, 0); gate.SetPoseFromHost(host, new AimVector(0, 0, 0), Forward);
            gate.TryAuthorize(host, new FireRequest(1, 1, 1, new AimVector(0, 0, 0), Forward), 0, out _);
            Assert.That(gate.TryBeginReloadFromHost(host, .2), Is.True);
            Assert.That(gate.IsReloading(host), Is.True);
            Assert.That(gate.AdvanceReloadsFromHost(1.69), Is.True); Assert.That(gate.Ammo(host), Is.EqualTo(11));
            Assert.That(gate.AdvanceReloadsFromHost(1.7), Is.True); Assert.That(gate.Ammo(host), Is.EqualTo(12));
            Assert.That(gate.IsReloading(host), Is.False);
            Assert.That(gate.AdvanceReloadsFromHost(double.NaN), Is.False);
            Assert.That(gate.AdvanceReloadsFromHost(1), Is.False);
            session.TryReturnToLobby(host); Assert.That(gate.AdvanceReloadsFromHost(2), Is.False);
        }
    }
}
