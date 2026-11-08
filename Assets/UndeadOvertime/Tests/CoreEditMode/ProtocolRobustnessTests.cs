using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class ProtocolRobustnessTests
    {
        private static AimVector Quantize(AimVector value) => new AimVector((float)value.X, (float)value.Y, (float)value.Z);
        private static AimVector EdgeEye => new AimVector(0, 1.5990000001, 0);

        [TestCase(RangeAction.Fire)] [TestCase(RangeAction.Reload)] [TestCase(RangeAction.Move)]
        public void EncoderRejectsIntentThatOnlyBecomesInvalidAfterFloatConversion(RangeAction action)
        {
            AimVector direction = action == RangeAction.Move
                ? new AimVector(1.0004753629146135, 0, .0070034419303274965)
                : new AimVector(Math.Sqrt(.999) + 1e-12, 0, 0);
            Assert.That(action == RangeAction.Move ? HostMoveGate.ValidIntent(direction) : direction.IsUnit, Is.True);
            Assert.That(action == RangeAction.Move ? HostMoveGate.ValidIntent(Quantize(direction)) : Quantize(direction).IsUnit, Is.False);
            Assert.Throws<ArgumentException>(() => LanRangeProtocol.Encode(new RangeCommand(action, 1, 1, 1, direction)));
        }
        [TestCase(false)] [TestCase(true)]
        public void EncoderRejectsMemberOrEnemyPoseThatCannotSurviveWireConversion(bool enemy)
        {
            Assert.That(HostMoveGate.ValidPosition(EdgeEye), Is.True);
            Assert.That(HostMoveGate.ValidPosition(Quantize(EdgeEye)), Is.False);
            RangeMember member = new RangeMember(0, 1, 0, 12, false, enemy ? HostMoveGate.Spawn(0) : EdgeEye);
            var range = new RangeSnapshot(1, 1, SessionPhase.Playing, new[] { member }, new[] { 100, 100, 100 },
                enemy ? new InfectedSnapshot(EncounterPhase.Active, 100, EdgeEye) : InfectedSnapshot.Dormant);
            Assert.Throws<ArgumentException>(() => LanRangeProtocol.Encode(range));
        }
        [Test]
        public void CaptureDoesNotClaimSuccessOrConsumeSequenceForUnreadableQuantizedPose()
        {
            var session = new LanSession(CoopMissionProtocol.Version); PlayerTicket host = session.StartHost(0); session.TryBeginRound(host);
            var mission = new HostCoopMission(session, 0, HostMoveGate.Spawn(0), HostMoveGate.Spawn(1));
            var fire = new HostFireGate(session, 0); var slots = new Dictionary<PlayerTicket, byte> { { host, 0 } };
            Assert.That(mission.SetPlayerPositionFromHost(host, EdgeEye, 0), Is.True);
            Assert.That(mission.TryCaptureFrame(0, fire, slots, new[] { 100, 100, 100 }, out MissionFrame frame, out byte[] bytes), Is.False);
            Assert.That(frame, Is.Null); Assert.That(bytes, Is.Null);
            Assert.That(mission.SetPlayerPositionFromHost(host, HostMoveGate.Spawn(0), 0), Is.True);
            Assert.That(mission.TryCaptureFrame(0, fire, slots, new[] { 100, 100, 100 }, out frame, out bytes), Is.True);
            Assert.That(frame.Sequence, Is.EqualTo(1)); Assert.That(CoopMissionProtocol.TryFrame(bytes, out _), Is.True);
        }
        [Test]
        public void EncoderRejectsTwoPeersSharingOneHostIssuedConnectionLifetime()
        {
            var members = new[] { new RangeMember(0, 1, 0, 12, false), new RangeMember(8, 1, 1, 12, false) };
            var range = new RangeSnapshot(9, 4, SessionPhase.Playing, members, new[] { 100, 100, 100 });
            Assert.Throws<ArgumentException>(() => LanRangeProtocol.Encode(range));
        }
        [TestCase(false)] [TestCase(true)]
        public void DecodersRejectDuplicateConnectionLifetimesEvenWithDifferentPeersAndSlots(bool mission)
        {
            MissionFrame source = MakeFrame(new Random(451), 0, 2, 1);
            byte[] bytes = mission ? CoopMissionProtocol.Encode(source) : LanRangeProtocol.Encode(source.Range);
            int start = (mission ? CoopMissionProtocol.FrameHeaderBytes : 0) + LanRangeProtocol.SnapshotHeaderBytes;
            Array.Copy(bytes, start + 8, bytes, start + LanRangeProtocol.MemberBytes + 8, 8);
            if (mission) { Assert.That(CoopMissionProtocol.TryFrame(bytes, out MissionFrame frame), Is.False); Assert.That(frame, Is.Null); }
            else { Assert.That(LanRangeProtocol.TrySnapshot(bytes, out RangeSnapshot range), Is.False); Assert.That(range, Is.Null); }
        }
        [Test]
        public void FiveThousandGeneratedOrdinaryCommandsHaveStableWireRoundTrips()
        {
            var random = new Random(90187);
            for (int i = 0; i < 5000; i++)
            {
                double angle = random.NextDouble() * Math.PI * 2;
                RangeAction action = (RangeAction)(i % 4 + 1);
                double magnitude = action == RangeAction.Move ? random.NextDouble() : 1;
                AimVector aim = action == RangeAction.Revive ? new AimVector(0, 0, i / 4 % 2) :
                    new AimVector(Math.Cos(angle) * magnitude, 0, Math.Sin(angle) * magnitude);
                byte[] command = LanRangeProtocol.Encode(new RangeCommand(action, long.MaxValue - i, i + 1, ulong.MaxValue - (ulong)i, aim));
                Assert.That(LanRangeProtocol.TryCommand(command, out RangeCommand decoded), Is.True, "command " + i);
                Assert.That(LanRangeProtocol.Encode(decoded), Is.EqualTo(command));
                byte[] power = CoopMissionProtocol.Encode(new MissionPowerCommand(long.MaxValue - i, i + 1, ulong.MaxValue - (ulong)i, i % 2 == 0));
                Assert.That(CoopMissionProtocol.TryPowerCommand(power, out MissionPowerCommand decodedPower), Is.True);
                Assert.That(CoopMissionProtocol.Encode(decodedPower), Is.EqualTo(power));
            }
        }
        [Test]
        public void TwelveHundredGeneratedFramesRoundTripAcrossAllPhasesAndTeamSizes()
        {
            var random = new Random(373591);
            for (int i = 0; i < 1200; i++)
            {
                MissionFrame original = MakeFrame(random, i % 6, (i / 6) % 10 + 1, (ulong)i + 1);
                byte[] bytes = CoopMissionProtocol.Encode(original);
                Assert.That(CoopMissionProtocol.TryFrame(bytes, out MissionFrame decoded), Is.True, "frame " + i);
                Assert.That(CoopMissionProtocol.Encode(decoded), Is.EqualTo(bytes));
                Assert.That(decoded.Range.MemberCount, Is.EqualTo(original.Range.MemberCount));
                Assert.That(decoded.Mission.Phase, Is.EqualTo(original.Mission.Phase));
                CheckAllDecoders(bytes);
                CheckAllDecoders(LanRangeProtocol.Encode(original.Range));
            }
        }
        [Test]
        public void EverySingleBitMutationOfFourteenRepresentativeFramesIsBoundedAndCanonical()
        {
            var random = new Random(53012); int checkedPackets = 0;
            var corpus = new List<MissionFrame>();
            for (int phase = 0; phase < 6; phase++)
                foreach (int members in new[] { 1, 10 }) corpus.Add(MakeFrame(random, phase, members, 1));
            foreach (int members in new[] { 2, 10 }) corpus.Add(WithRevive(MakeFrame(random, 1, members, 1)));
            foreach (MissionFrame seed in corpus)
            {
                byte[] original = CoopMissionProtocol.Encode(seed);
                Assert.That(CoopMissionProtocol.TryFrame(original, out _), Is.True);
                for (int offset = 0; offset < original.Length; offset++)
                    for (int bit = 0; bit < 8; bit++)
                    {
                        var bytes = (byte[])original.Clone(); bytes[offset] ^= (byte)(1 << bit);
                        CheckAllDecoders(bytes); checkedPackets++;
                    }
            }
            TestContext.Progress.WriteLine("Single-bit packets checked: " + checkedPackets);
        }
        [Test]
        public void TwentyThousandRawAndHeaderBiasedPacketsNeverThrowOrLeakPartialResults()
        {
            var random = new Random(721033);
            byte[][] seeds = {
                LanRangeProtocol.Encode(new RangeCommand(RangeAction.Fire, 1, 1, 1, new AimVector(0, 0, 1))),
                CoopMissionProtocol.Encode(new MissionPowerCommand(1, 1, 1, true)),
                LanRangeProtocol.Encode(MakeFrame(random, 0, 10, 1).Range),
                CoopMissionProtocol.Encode(MakeFrame(random, 3, 10, 1))
            };
            for (int i = 0; i < 20000; i++)
            {
                var bytes = new byte[random.Next(0, CoopMissionProtocol.MaximumFrameBytes + 65)]; random.NextBytes(bytes);
                if (i % 2 == 0)
                {
                    byte[] seed = seeds[i / 2 % seeds.Length];
                    if (bytes.Length >= 7) Array.Copy(seed, bytes, 7);
                    // Exercise valid headers with random lengths, ids, counts and body data, not just bad magic.
                }
                CheckAllDecoders(bytes);
            }
            CheckAllDecoders(null); CheckAllDecoders(new byte[65536]);
        }
        [Test]
        public void DuplicateReorderedWrongRoundAndMalformedFloodCannotReplaceTerminalReplica()
        {
            var random = new Random(61091); var replica = new CoopMissionReplica(9, 4);
            byte[] initial = CoopMissionProtocol.Encode(MakeFrame(random, 4, 10, 100));
            Assert.That(replica.TryAcceptHostPacket(initial), Is.True); MissionFrame terminal = replica.Current;
            for (int i = 0; i < 5000; i++)
            {
                MissionFrame candidate = MakeFrame(random, i % 6, i % 10 + 1, (ulong)(i % 101));
                byte[] bytes = candidate.Sequence == 0 ? new byte[i % 530] : CoopMissionProtocol.Encode(candidate);
                Assert.That(replica.TryAcceptHostPacket(bytes), Is.False); Assert.That(replica.Current, Is.SameAs(terminal));
            }
            byte[] wrongRound = CoopMissionProtocol.Encode(MakeFrame(random, 4, 10, 101));
            Array.Copy(BitConverter.GetBytes(5L), 0, wrongRound, CoopMissionProtocol.FrameHeaderBytes + 15, 8);
            Assert.That(CoopMissionProtocol.TryFrame(wrongRound, out _), Is.True);
            Assert.That(replica.TryAcceptHostPacket(wrongRound), Is.False); Assert.That(replica.Current, Is.SameAs(terminal));
            byte[] wrongSession = CoopMissionProtocol.Encode(MakeFrame(random, 4, 10, 101));
            Array.Copy(BitConverter.GetBytes(10L), 0, wrongSession, CoopMissionProtocol.FrameHeaderBytes + 7, 8);
            Assert.That(CoopMissionProtocol.TryFrame(wrongSession, out _), Is.True);
            Assert.That(replica.TryAcceptHostPacket(wrongSession), Is.False); Assert.That(replica.Current, Is.SameAs(terminal));
            byte[] next = CoopMissionProtocol.Encode(MakeFrame(random, 4, 10, 101));
            Assert.That(replica.TryAcceptHostPacket(next), Is.True);
            Assert.That(replica.Current.Sequence, Is.EqualTo(101));
        }
        private static MissionFrame MakeFrame(Random random, int state, int count, ulong sequence)
        {
            var slots = new byte[10]; for (byte i = 0; i < slots.Length; i++) slots[i] = i;
            for (int i = slots.Length - 1; i > 0; i--) { int j = random.Next(i + 1); byte value = slots[i]; slots[i] = slots[j]; slots[j] = value; }
            var members = new RangeMember[count];
            for (int i = 0; i < count; i++) members[i] = new RangeMember((ulong)i, i + 1, slots[i], (byte)random.Next(13), random.Next(2) == 1,
                new AimVector(random.NextDouble() * 30 - 15, HostMoveGate.EyeHeight, random.NextDouble() * 30 - 15), (byte)random.Next(1, 101));
            HordePhase horde = state == 0 ? HordePhase.Disabled : state == 1 ? HordePhase.Fighting : state == 2 ? HordePhase.Intermission :
                state == 5 ? HordePhase.Failed : HordePhase.Completed;
            int wave = state == 0 ? 0 : state == 2 ? 1 + random.Next(2) : state == 1 || state == 5 ? 1 + random.Next(3) : 3;
            var enemies = new EnemySnapshot[wave == 0 ? 0 : wave + 1];
            for (int i = 0; i < enemies.Length; i++) enemies[i] = new EnemySnapshot((byte)(wave * 4 + i),
                (byte)(state == 1 || state == 5 ? random.Next(1, 101) : 0), HostInfectedEncounter.WaveSpawn(i));
            EncounterPhase encounter = state == 0 ? EncounterPhase.Dormant : state == 5 ? EncounterPhase.Failed : state == 3 || state == 4 ? EncounterPhase.Cleared : EncounterPhase.Active;
            CoopMissionPhase phase = state == 0 ? CoopMissionPhase.RestorePower : state <= 2 ? CoopMissionPhase.DefendDepot : state == 3 ? CoopMissionPhase.ReachExtraction : state == 4 ? CoopMissionPhase.Completed : CoopMissionPhase.Failed;
            double hold = state == 0 ? random.NextDouble() * 3 : state == 3 ? random.NextDouble() * 2 : 0;
            var range = new RangeSnapshot(9, 4, SessionPhase.Playing, members, new[] { random.Next(101), random.Next(101), random.Next(101) }, encounter, enemies,
                horde == HordePhase.Disabled ? HordeSnapshot.Disabled : new HordeSnapshot(horde, (byte)wave, 3, state == 2 ? random.NextDouble() * 5 : 0));
            return new MissionFrame(sequence, range, new CoopMissionSnapshot(9, 4, phase, true, state != 0, hold));
        }
        private static MissionFrame WithRevive(MissionFrame frame)
        {
            RangeSnapshot range = frame.Range; var members = new RangeMember[range.MemberCount];
            for (int i = 0; i < members.Length; i++)
            {
                RangeMember member = range.Member(i);
                members[i] = new RangeMember(member.PeerId, member.ConnectionId, member.Slot, member.Ammo, false,
                    i < 2 ? new AimVector(i, HostMoveGate.EyeHeight, 0) : member.Position,
                    (byte)(i == 1 ? 0 : member.Health), i == 0 ? range.Member(1).Slot : (byte)255, (byte)(i == 0 ? 42 : 0));
            }
            var enemies = new EnemySnapshot[range.EnemyCount]; for (int i = 0; i < enemies.Length; i++) enemies[i] = range.Enemy(i);
            var next = new RangeSnapshot(range.SessionId, range.RoundId, range.Phase, members,
                new[] { range.TargetHealth(0), range.TargetHealth(1), range.TargetHealth(2) }, range.Encounter.Phase, enemies, range.Horde);
            return new MissionFrame(frame.Sequence, next, frame.Mission);
        }
        private static void CheckAllDecoders(byte[] bytes)
        {
            if (LanRangeProtocol.TryCommand(bytes, out RangeCommand command)) Assert.That(LanRangeProtocol.Encode(command), Is.EqualTo(bytes));
            else Assert.That(command.Sequence, Is.Zero);
            if (CoopMissionProtocol.TryPowerCommand(bytes, out MissionPowerCommand power)) Assert.That(CoopMissionProtocol.Encode(power), Is.EqualTo(bytes));
            else Assert.That(power.Sequence, Is.Zero);
            if (LanRangeProtocol.TrySnapshot(bytes, out RangeSnapshot range)) Assert.That(LanRangeProtocol.Encode(range), Is.EqualTo(bytes));
            else Assert.That(range, Is.Null);
            if (CoopMissionProtocol.TryFrame(bytes, out MissionFrame frame)) Assert.That(CoopMissionProtocol.Encode(frame), Is.EqualTo(bytes));
            else Assert.That(frame, Is.Null);
        }
    }
}
