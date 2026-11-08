using System;
using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class CoopMissionProtocolTests
    {
        private static RangeSnapshot Range(HordePhase horde = HordePhase.Disabled, int count = 2, long session = 5, long round = 2)
        {
            var members = new RangeMember[count];
            for (byte i = 0; i < count; i++) members[i] = new RangeMember(i, i + 1, i, 12, false);
            int wave = horde == HordePhase.Completed ? 3 : 1;
            EncounterPhase encounter = horde == HordePhase.Disabled ? EncounterPhase.Dormant :
                horde == HordePhase.Completed ? EncounterPhase.Cleared : horde == HordePhase.Failed ? EncounterPhase.Failed : EncounterPhase.Active;
            var enemies = horde == HordePhase.Disabled ? Array.Empty<EnemySnapshot>() : new EnemySnapshot[wave + 1];
            for (int i = 0; i < enemies.Length; i++) enemies[i] = new EnemySnapshot((byte)(wave * 4 + i),
                (byte)(horde == HordePhase.Intermission || horde == HordePhase.Completed ? 0 : 100), HostInfectedEncounter.WaveSpawn(i));
            return new RangeSnapshot(session, round, SessionPhase.Playing, members, new[] { 100, 75, 0 }, encounter, enemies,
                horde == HordePhase.Disabled ? HordeSnapshot.Disabled : new HordeSnapshot(horde, (byte)wave, 3, horde == HordePhase.Intermission ? 4 : 0));
        }
        private static MissionFrame Frame(CoopMissionPhase phase = CoopMissionPhase.RestorePower, ulong sequence = 1,
            double hold = 0, int members = 2, long session = 5, long round = 2)
        {
            HordePhase horde = phase == CoopMissionPhase.RestorePower ? HordePhase.Disabled :
                phase == CoopMissionPhase.DefendDepot ? HordePhase.Fighting : phase == CoopMissionPhase.Failed ? HordePhase.Failed : HordePhase.Completed;
            return new MissionFrame(sequence, Range(horde, members, session, round),
                new CoopMissionSnapshot(session, round, phase, true, phase != CoopMissionPhase.RestorePower, hold));
        }
        private static MissionFrame WithMission(MissionFrame frame, CoopMissionPhase phase, bool gate, double hold = 0, bool current = true,
            long session = 5, long round = 2) => new MissionFrame(frame.Sequence, frame.Range, new CoopMissionSnapshot(session, round, phase, current, gate, hold));
        private static MissionFrame WithHealth(MissionFrame frame, byte health, bool all = false)
        {
            RangeSnapshot source = frame.Range;
            var members = new RangeMember[source.MemberCount];
            for (int i = 0; i < members.Length; i++)
            {
                RangeMember m = source.Member(i);
                members[i] = new RangeMember(m.PeerId, m.ConnectionId, m.Slot, m.Ammo, m.Reloading, m.Position, i == 0 || all ? health : m.Health);
            }
            var enemies = new EnemySnapshot[source.EnemyCount]; for (int i = 0; i < enemies.Length; i++) enemies[i] = source.Enemy(i);
            return new MissionFrame(frame.Sequence, new RangeSnapshot(source.SessionId, source.RoundId, source.Phase, members,
                new[] { source.TargetHealth(0), source.TargetHealth(1), source.TargetHealth(2) }, source.Encounter.Phase, enemies, source.Horde), frame.Mission);
        }
        private static byte[] Mutate(byte[] source, int offset, byte value)
        { var bytes = (byte[])source.Clone(); bytes[offset] = value; return bytes; }
        private static byte[] Extra(byte[] source)
        { var bytes = new byte[source.Length + 1]; Array.Copy(source, bytes, source.Length); return bytes; }

        [TestCase(false)] [TestCase(true)]
        public void PowerCommandHasOnlyIntentAndReplayFields(bool held)
        {
            byte[] bytes = CoopMissionProtocol.Encode(new MissionPowerCommand(5, 2, 42, held));
            Assert.That(bytes.Length, Is.EqualTo(32));
            Assert.That(CoopMissionProtocol.TryPowerCommand(bytes, out MissionPowerCommand value), Is.True);
            Assert.That(value.SessionId, Is.EqualTo(5)); Assert.That(value.RoundId, Is.EqualTo(2));
            Assert.That(value.Sequence, Is.EqualTo(42)); Assert.That(value.Held, Is.EqualTo(held));
        }
        [Test]
        public void PowerRejectsEveryTruncationExtraByteAndNoncanonicalBoolean()
        {
            byte[] bytes = CoopMissionProtocol.Encode(new MissionPowerCommand(1, 1, 1, true));
            for (int n = 0; n < bytes.Length; n++)
            { var truncated = new byte[n]; Array.Copy(bytes, truncated, n); Assert.That(CoopMissionProtocol.TryPowerCommand(truncated, out _), Is.False); }
            Assert.That(CoopMissionProtocol.TryPowerCommand(null, out _), Is.False);
            Assert.That(CoopMissionProtocol.TryPowerCommand(Extra(bytes), out _), Is.False);
            Assert.That(CoopMissionProtocol.TryPowerCommand(Mutate(bytes, 31, 2), out _), Is.False);
            foreach (int offset in new[] { 0, 4, 6 }) Assert.That(CoopMissionProtocol.TryPowerCommand(Mutate(bytes, offset, 255), out _), Is.False);
        }
        [Test]
        public void PowerRejectsZeroIdsAndSequenceAtEncodeAndDecode()
        {
            foreach (var command in new[] { new MissionPowerCommand(0, 1, 1, true), new MissionPowerCommand(1, 0, 1, true),
                new MissionPowerCommand(1, 1, 0, true), new MissionPowerCommand(-1, 1, 1, true) })
                Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(command));
            byte[] valid = CoopMissionProtocol.Encode(new MissionPowerCommand(1, 1, 1, true));
            foreach (int offset in new[] { 7, 15, 23 })
            { var bytes = (byte[])valid.Clone(); Array.Clear(bytes, offset, 8); Assert.That(CoopMissionProtocol.TryPowerCommand(bytes, out _), Is.False); }
        }
        [TestCase(CoopMissionPhase.RestorePower)] [TestCase(CoopMissionPhase.DefendDepot)]
        [TestCase(CoopMissionPhase.ReachExtraction)] [TestCase(CoopMissionPhase.Completed)] [TestCase(CoopMissionPhase.Failed)]
        public void CoherentMissionAndCombatRoundTripAtomically(CoopMissionPhase phase)
        {
            MissionFrame original = Frame(phase, 42);
            byte[] bytes = CoopMissionProtocol.Encode(original);
            Assert.That(CoopMissionProtocol.TryFrame(bytes, out MissionFrame decoded), Is.True);
            Assert.That(decoded.Sequence, Is.EqualTo(42)); Assert.That(decoded.Mission.Phase, Is.EqualTo(phase));
            Assert.That(decoded.Range.SessionId, Is.EqualTo(decoded.Mission.SessionId));
            Assert.That(decoded.Range.RoundId, Is.EqualTo(decoded.Mission.RoundId)); Assert.That(decoded.Mission.IsCurrent, Is.True);
            Assert.That(decoded.Range.MemberCount, Is.EqualTo(2)); Assert.That(decoded.Range.TargetHealth(1), Is.EqualTo(75));
            Assert.That(decoded.Range.Horde.Phase, Is.EqualTo(original.Range.Horde.Phase));
        }
        [Test]
        public void MaximumTenMembersFourEnemiesFitsFixed465ByteBound()
        {
            byte[] bytes = CoopMissionProtocol.Encode(Frame(CoopMissionPhase.ReachExtraction, members: 10, hold: 1.25));
            Assert.That(bytes.Length, Is.EqualTo(465)); Assert.That(bytes.Length, Is.EqualTo(CoopMissionProtocol.MaximumFrameBytes));
            Assert.That(CoopMissionProtocol.TryFrame(bytes, out MissionFrame frame), Is.True);
            Assert.That(frame.Range.MemberCount, Is.EqualTo(10)); Assert.That(frame.Range.EnemyCount, Is.EqualTo(4));
            Assert.That(frame.Mission.HoldSeconds, Is.EqualTo(1.25));
        }
        [Test]
        public void EveryFrameTruncationAndSurplusByteIsRejected()
        {
            byte[] valid = CoopMissionProtocol.Encode(Frame(CoopMissionPhase.ReachExtraction, members: 10));
            for (int n = 0; n < valid.Length; n++)
            { var bytes = new byte[n]; Array.Copy(valid, bytes, n); Assert.That(CoopMissionProtocol.TryFrame(bytes, out MissionFrame result), Is.False); Assert.That(result, Is.Null); }
            Assert.That(CoopMissionProtocol.TryFrame(null, out _), Is.False);
            Assert.That(CoopMissionProtocol.TryFrame(Extra(valid), out _), Is.False);
            Assert.That(CoopMissionProtocol.TryFrame(new byte[65535], out _), Is.False);
        }
        [Test]
        public void BothHeaderVersionsKindsAndNestedLengthAreStrict()
        {
            byte[] valid = CoopMissionProtocol.Encode(Frame());
            foreach (int offset in new[] { 0, 4, 6, 15, 16, 21, 22, 23, 27, 29 })
                Assert.That(CoopMissionProtocol.TryFrame(Mutate(valid, offset, 255), out _), Is.False, "offset=" + offset);
            var zeroSequence = (byte[])valid.Clone(); Array.Clear(zeroSequence, 7, 8);
            Assert.That(CoopMissionProtocol.TryFrame(zeroSequence, out _), Is.False);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)] [TestCase(3.1f)]
        public void NonfiniteNegativeOrExcessHoldIsRejected(float hold)
        {
            byte[] bytes = CoopMissionProtocol.Encode(Frame());
            Array.Copy(BitConverter.GetBytes(hold), 0, bytes, 17, 4);
            Assert.That(CoopMissionProtocol.TryFrame(bytes, out _), Is.False);
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(Frame(hold: hold)));
        }
        [TestCase(CoopMissionPhase.ReachExtraction)] [TestCase(CoopMissionPhase.Completed)]
        public void PositiveExtractionOrCompletionCannotContainDownedTeammate(CoopMissionPhase phase)
        {
            var frame = Frame(phase, hold: phase == CoopMissionPhase.ReachExtraction ? 1 : 0);
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(WithHealth(frame, 0)));
            byte[] bytes = CoopMissionProtocol.Encode(frame);
            int firstMember = CoopMissionProtocol.FrameHeaderBytes + LanRangeProtocol.SnapshotHeaderBytes + frame.Range.EnemyCount * LanRangeProtocol.EnemyBytes;
            bytes[firstMember + 31] = 0; // Health, with valid combat packet layout and no revive relationship.
            Assert.That(CoopMissionProtocol.TryFrame(bytes, out _), Is.False);
        }
        [Test]
        public void DownedTeammateMayWaitAtExtractionOnlyWithZeroHold()
        {
            var waiting = WithHealth(Frame(CoopMissionPhase.ReachExtraction), 0);
            Assert.That(CoopMissionProtocol.TryFrame(CoopMissionProtocol.Encode(waiting), out _), Is.True);
        }
        [TestCase(CoopMissionPhase.RestorePower)] [TestCase(CoopMissionPhase.DefendDepot)] [TestCase(CoopMissionPhase.ReachExtraction)]
        public void NonfailedMissionMustHaveAtLeastOneLivingMember(CoopMissionPhase phase)
        {
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(WithHealth(Frame(phase), 0, true)));
        }
        [Test]
        public void GateAndPhaseMustAgreeWithActualNestedEncounter()
        {
            var power = Frame();
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(WithMission(power, CoopMissionPhase.RestorePower, true)));
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(WithMission(power, CoopMissionPhase.DefendDepot, true)));
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(WithMission(power, CoopMissionPhase.Completed, true)));
            var defense = Frame(CoopMissionPhase.DefendDepot);
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(WithMission(defense, CoopMissionPhase.DefendDepot, false)));
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(WithMission(defense, CoopMissionPhase.DefendDepot, true, .5)));
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(WithMission(defense, CoopMissionPhase.ReachExtraction, true)));
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(Frame(CoopMissionPhase.Completed, hold: .1)));
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(Frame(CoopMissionPhase.ReachExtraction, hold: 2.1)));
        }
        [Test]
        public void MissionIdentityMustMatchRangeAndBeCurrentPlayingRound()
        {
            var frame = Frame();
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(WithMission(frame, CoopMissionPhase.RestorePower, false, current: false)));
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(WithMission(frame, CoopMissionPhase.RestorePower, false, session: 6)));
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(WithMission(frame, CoopMissionPhase.RestorePower, false, round: 3)));
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(Frame(sequence: 0)));
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode((MissionFrame)null));
            var lobby = new RangeSnapshot(5, 2, SessionPhase.Lobby, new[] { new RangeMember(0, 1, 0, 12, false) }, new[] { 100, 100, 100 });
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(new MissionFrame(1, lobby, frame.Mission)));
        }
        [Test]
        public void InvalidNestedMembersCannotHideInsideValidMissionEnvelope()
        {
            byte[] bytes = CoopMissionProtocol.Encode(Frame());
            int memberStart = CoopMissionProtocol.FrameHeaderBytes + LanRangeProtocol.SnapshotHeaderBytes;
            bytes[memberStart + LanRangeProtocol.MemberBytes + 16] = 0; // Duplicate slot.
            Assert.That(CoopMissionProtocol.TryFrame(bytes, out _), Is.False);
            var invalid = new RangeSnapshot(5, 2, SessionPhase.Playing,
                new[] { new RangeMember(1, 1, 0, 12, false), new RangeMember(2, 2, 0, 12, false) }, new[] { 100, 100, 100 });
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(new MissionFrame(1, invalid, Frame().Mission)));
        }
        [Test]
        public void IntermissionAndFailureBeforeOrAfterPowerHaveExplicitRepresentations()
        {
            var intermission = new MissionFrame(1, Range(HordePhase.Intermission), Frame(CoopMissionPhase.DefendDepot).Mission);
            Assert.That(CoopMissionProtocol.TryFrame(CoopMissionProtocol.Encode(intermission), out _), Is.True);
            var beforePower = WithMission(Frame(), CoopMissionPhase.Failed, false);
            Assert.That(CoopMissionProtocol.TryFrame(CoopMissionProtocol.Encode(beforePower), out _), Is.True);
            var afterClear = WithMission(Frame(CoopMissionPhase.ReachExtraction), CoopMissionPhase.Failed, true);
            Assert.That(CoopMissionProtocol.TryFrame(CoopMissionProtocol.Encode(afterClear), out _), Is.True);
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(WithMission(Frame(CoopMissionPhase.Failed), CoopMissionPhase.Failed, false)));
            Assert.Throws<ArgumentException>(() => CoopMissionProtocol.Encode(Frame(CoopMissionPhase.Failed, hold: 1)));
        }
        [Test]
        public void DistinctHandshakeAndMessageTypesCannotBeInterpretedAsRangeV5()
        {
            Assert.That(CoopMissionProtocol.AcceptHandshake(CoopMissionProtocol.Handshake), Is.True);
            Assert.That(LanRangeProtocol.Version, Is.EqualTo(5));
            Assert.That(CoopMissionProtocol.AcceptHandshake(LanRangeProtocol.Handshake), Is.False);
            Assert.That(LanRangeProtocol.AcceptHandshake(CoopMissionProtocol.Handshake), Is.False);
            var handshake = CoopMissionProtocol.Handshake; handshake[0] = 0;
            Assert.That(CoopMissionProtocol.AcceptHandshake(handshake), Is.False);
            Assert.That(CoopMissionProtocol.AcceptHandshake(null), Is.False);
            Assert.That(CoopMissionProtocol.AcceptHandshake(Extra(CoopMissionProtocol.Handshake)), Is.False);
            Assert.That(LanRangeProtocol.TrySnapshot(CoopMissionProtocol.Encode(Frame()), out _), Is.False);
            Assert.That(CoopMissionProtocol.TryFrame(LanRangeProtocol.Encode(Range()), out _), Is.False);
            Assert.That(CoopMissionProtocol.TryPowerCommand(LanRangeProtocol.Encode(new RangeCommand(RangeAction.Fire, 5, 2, 1, new AimVector(0, 0, 1))), out _), Is.False);
            Assert.That(CoopMissionProtocol.TryFrame(CoopMissionProtocol.Encode(new MissionPowerCommand(5, 2, 1, true)), out _), Is.False);
        }
        [Test]
        public void ReplicaRejectsWrongRoundReplayAndOutOfOrderWithoutChangingAcceptedState()
        {
            var replica = new CoopMissionReplica(5, 2);
            Assert.That(replica.Current, Is.Null);
            byte[] first = CoopMissionProtocol.Encode(Frame(sequence: 10, hold: .5));
            Assert.That(replica.TryAcceptHostPacket(first), Is.True); MissionFrame accepted = replica.Current;
            Assert.That(replica.TryAcceptHostPacket(first), Is.False);
            Assert.That(replica.TryAcceptHostPacket(CoopMissionProtocol.Encode(Frame(sequence: 9))), Is.False);
            Assert.That(replica.TryAcceptHostPacket(CoopMissionProtocol.Encode(Frame(sequence: 11, session: 6))), Is.False);
            Assert.That(replica.TryAcceptHostPacket(CoopMissionProtocol.Encode(Frame(sequence: 11, round: 3))), Is.False);
            Assert.That(replica.TryAcceptHostPacket(new byte[1]), Is.False);
            Assert.That(replica.Current, Is.SameAs(accepted));
        }
        [Test]
        public void ReplicaAllowsDroppedTransitionsAndCountdownResetsButNotPhaseOrGateRegression()
        {
            var replica = new CoopMissionReplica(5, 2);
            Assert.That(replica.TryAcceptHostPacket(CoopMissionProtocol.Encode(Frame(sequence: 1, hold: 1))), Is.True);
            Assert.That(replica.TryAcceptHostPacket(CoopMissionProtocol.Encode(Frame(sequence: 2, hold: 0))), Is.True);
            Assert.That(replica.TryAcceptHostPacket(CoopMissionProtocol.Encode(Frame(CoopMissionPhase.ReachExtraction, 5, hold: 1.5))), Is.True);
            Assert.That(replica.TryAcceptHostPacket(CoopMissionProtocol.Encode(Frame(CoopMissionPhase.ReachExtraction, 6, hold: 0))), Is.True);
            Assert.That(replica.TryAcceptHostPacket(CoopMissionProtocol.Encode(Frame(CoopMissionPhase.DefendDepot, 7))), Is.False);
            Assert.That(replica.TryAcceptHostPacket(CoopMissionProtocol.Encode(Frame(sequence: 8))), Is.False);
            Assert.That(replica.Current.Sequence, Is.EqualTo(6));
            Assert.That(replica.TryAcceptHostPacket(CoopMissionProtocol.Encode(Frame(CoopMissionPhase.Completed, 9))), Is.True);
        }
        [TestCase(CoopMissionPhase.Completed)] [TestCase(CoopMissionPhase.Failed)]
        public void ReplicaTerminalOutcomeCannotChangeButSameOutcomeCanRefresh(CoopMissionPhase phase)
        {
            var replica = new CoopMissionReplica(5, 2);
            Assert.That(replica.TryAcceptHostPacket(CoopMissionProtocol.Encode(Frame(phase))), Is.True);
            Assert.That(replica.TryAcceptHostPacket(CoopMissionProtocol.Encode(Frame(phase, 2))), Is.True);
            Assert.That(replica.TryAcceptHostPacket(CoopMissionProtocol.Encode(Frame(phase == CoopMissionPhase.Completed ? CoopMissionPhase.Failed : CoopMissionPhase.Completed, 3))), Is.False);
            Assert.That(replica.TryAcceptHostPacket(CoopMissionProtocol.Encode(Frame(CoopMissionPhase.ReachExtraction, 4))), Is.False);
            Assert.That(replica.Current.Sequence, Is.EqualTo(2));
        }
        [Test]
        public void FailureCannotUnlockTheGateAfterTerminalOutcome()
        {
            var replica = new CoopMissionReplica(5, 2);
            Assert.That(replica.TryAcceptHostPacket(CoopMissionProtocol.Encode(WithMission(Frame(), CoopMissionPhase.Failed, false))), Is.True);
            Assert.That(replica.TryAcceptHostPacket(CoopMissionProtocol.Encode(Frame(CoopMissionPhase.Failed, 2))), Is.False);
            Assert.That(replica.Current.Mission.DepotGateUnlocked, Is.False);
        }
        [Test]
        public void DecodedStateDoesNotRetainMutablePacketBytes()
        {
            byte[] bytes = CoopMissionProtocol.Encode(Frame()); var replica = new CoopMissionReplica(5, 2);
            Assert.That(replica.TryAcceptHostPacket(bytes), Is.True); Array.Clear(bytes, 0, bytes.Length);
            Assert.That(replica.Current.Sequence, Is.EqualTo(1)); Assert.That(replica.Current.Range.MemberCount, Is.EqualTo(2));
            Assert.That(replica.Current.Range.TargetHealth(0), Is.EqualTo(100));
        }
        [Test]
        public void ReplicaCannotBindToUnissuedSessionOrRound()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoopMissionReplica(0, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoopMissionReplica(5, 0));
        }
        [Test]
        public void MutationSweepThroughValidFrameExercisesDeepDecoderWithoutThrowing()
        {
            byte[] valid = CoopMissionProtocol.Encode(Frame(CoopMissionPhase.ReachExtraction, members: 10, hold: 1));
            for (int offset = 0; offset < valid.Length; offset++)
                foreach (byte replacement in new byte[] { 0, 1, 127, 255 })
                {
                    byte[] bytes = Mutate(valid, offset, replacement);
                    // Some mutations are legitimate new values; the guarantee here is bounded, nonthrowing parsing.
                    Assert.DoesNotThrow(() => CoopMissionProtocol.TryFrame(bytes, out _), "offset=" + offset);
                }
        }
        [Test]
        public void DeterministicMalformedPacketSweepDoesNotThrow()
        {
            var random = new Random(44107);
            for (int i = 0; i < 2000; i++)
            {
                var bytes = new byte[random.Next(0, CoopMissionProtocol.MaximumFrameBytes + 2)]; random.NextBytes(bytes);
                Assert.DoesNotThrow(() => CoopMissionProtocol.TryFrame(bytes, out _));
                Assert.DoesNotThrow(() => CoopMissionProtocol.TryPowerCommand(bytes, out _));
            }
        }
    }
}
