using NUnit.Framework;
namespace UndeadOvertime.Core.Tests
{
    public sealed class CoopMissionSessionReplicaTests
    {
        private static byte[] Lobby(long session = 5, long round = 0) => LanRangeProtocol.Encode(new RangeSnapshot(session, round,
            SessionPhase.Lobby, new[] { new RangeMember(0, 1, 0, 0, false) }, new[] { 100, 100, 100 }));
        private static byte[] Frame(long session = 5, long round = 1, ulong sequence = 1) => CoopMissionProtocol.Encode(new MissionFrame(sequence,
            new RangeSnapshot(session, round, SessionPhase.Playing, new[] { new RangeMember(0, 1, 0, 12, false) }, new[] { 100, 100, 100 }),
            new CoopMissionSnapshot(session, round, CoopMissionPhase.RestorePower, true, false, 0)));
        [Test]
        public void FirstFrameCannotInventSessionWithoutLobbyBinding()
        { var guard = new CoopMissionSessionReplica(); Assert.That(guard.TryAcceptHostPacket(Frame()), Is.False); Assert.That(guard.Range, Is.Null); }
        [Test]
        public void LobbyBindsNextRoundThenClosingLobbyRetiresIt()
        {
            var guard = new CoopMissionSessionReplica(); Assert.That(guard.TryAcceptHostPacket(Lobby()), Is.True);
            Assert.That(guard.TryAcceptHostPacket(Frame()), Is.True); Assert.That(guard.Mission.Sequence, Is.EqualTo(1));
            Assert.That(guard.TryAcceptHostPacket(Lobby(round: 1)), Is.True); Assert.That(guard.Mission, Is.Null);
            Assert.That(guard.TryAcceptHostPacket(Frame(sequence: 2)), Is.False);
            Assert.That(guard.TryAcceptHostPacket(Frame(round: 2)), Is.True); Assert.That(guard.Range.RoundId, Is.EqualTo(2));
        }
        [Test]
        public void DelayedInitialLobbyCannotCloseActiveMission()
        {
            var guard = new CoopMissionSessionReplica(); guard.TryAcceptHostPacket(Lobby()); guard.TryAcceptHostPacket(Frame());
            var before = guard.Mission; Assert.That(guard.TryAcceptHostPacket(Lobby()), Is.False); Assert.That(guard.Mission, Is.SameAs(before));
        }
        [TestCase(4, 1)] [TestCase(6, 1)] [TestCase(5, 2)] [TestCase(5, 9)]
        public void FrameCannotSwitchSessionOrSkipRound(long session, long round)
        { var guard = new CoopMissionSessionReplica(); guard.TryAcceptHostPacket(Lobby()); Assert.That(guard.TryAcceptHostPacket(Frame(session, round)), Is.False); }
        [Test]
        public void ChangedIdentityOrUnexpectedLobbyRoundCannotReplaceBinding()
        {
            var guard = new CoopMissionSessionReplica(); guard.TryAcceptHostPacket(Lobby());
            Assert.That(guard.TryAcceptHostPacket(Lobby(session: 6)), Is.False); Assert.That(guard.TryAcceptHostPacket(Lobby(round: 1)), Is.False);
            Assert.That(guard.TryAcceptHostPacket(Frame()), Is.True); Assert.That(guard.TryAcceptHostPacket(Lobby(round: 2)), Is.False);
        }
        [Test]
        public void JoiningExistingLobbyAllowsExactlyItsNextRound()
        {
            var guard = new CoopMissionSessionReplica(); Assert.That(guard.TryAcceptHostPacket(Lobby(round: 7)), Is.True);
            Assert.That(guard.TryAcceptHostPacket(Frame(round: 7)), Is.False); Assert.That(guard.TryAcceptHostPacket(Frame(round: 8)), Is.True);
        }
        [Test]
        public void DuplicateOutOfOrderOrMalformedFramesDoNotMutateCurrent()
        {
            var guard = new CoopMissionSessionReplica(); guard.TryAcceptHostPacket(Lobby()); guard.TryAcceptHostPacket(Frame(sequence: 5)); var before = guard.Mission;
            foreach (var bytes in new[] { Frame(sequence: 5), Frame(sequence: 4), null, new byte[500], Lobby(6, 1) })
            { Assert.That(guard.TryAcceptHostPacket(bytes), Is.False); Assert.That(guard.Mission, Is.SameAs(before)); }
            Assert.That(guard.TryAcceptHostPacket(Frame(sequence: 6)), Is.True);
        }
        [Test]
        public void LegacyPlayingSnapshotIsNotACompleteMissionState()
        {
            var guard = new CoopMissionSessionReplica(); guard.TryAcceptHostPacket(Lobby());
            CoopMissionProtocol.TryFrame(Frame(), out MissionFrame frame);
            Assert.That(guard.TryAcceptHostPacket(LanRangeProtocol.Encode(frame.Range)), Is.False); Assert.That(guard.Mission, Is.Null);
        }
        private static byte[] TerminalFrame(CoopMissionPhase phase, long round = 1, ulong sequence = 100)
        {
            bool completed = phase == CoopMissionPhase.Completed;
            var enemies = completed ? new EnemySnapshot[4] : System.Array.Empty<EnemySnapshot>();
            for (int i = 0; i < enemies.Length; i++) enemies[i] = new EnemySnapshot((byte)(12 + i), 0, HostInfectedEncounter.WaveSpawn(i));
            var range = new RangeSnapshot(5, round, SessionPhase.Playing, new[] { new RangeMember(0, 1, 0, 7, false) },
                new[] { 100, 100, 100 }, completed ? EncounterPhase.Cleared : EncounterPhase.Dormant, enemies,
                completed ? new HordeSnapshot(HordePhase.Completed, 3, 3, 0) : HordeSnapshot.Disabled);
            return CoopMissionProtocol.Encode(new MissionFrame(sequence, range, new CoopMissionSnapshot(5, round, phase, true, completed, 0)));
        }
        [TestCase(CoopMissionPhase.Completed)] [TestCase(CoopMissionPhase.Failed)]
        public void TerminalRoundRequiresClosingLobbyBeforeFreshRoundCanReset(CoopMissionPhase phase)
        {
            var guard = new CoopMissionSessionReplica(); Assert.That(guard.TryAcceptHostPacket(Lobby()), Is.True);
            Assert.That(guard.TryAcceptHostPacket(TerminalFrame(phase)), Is.True); MissionFrame terminal = guard.Mission;
            Assert.That(guard.TryAcceptHostPacket(Frame(sequence: 101)), Is.False);
            Assert.That(guard.TryAcceptHostPacket(Frame(round: 2)), Is.False);
            Assert.That(guard.Mission, Is.SameAs(terminal));
            Assert.That(guard.TryAcceptHostPacket(Lobby(round: 1)), Is.True); Assert.That(guard.Mission, Is.Null);
            Assert.That(guard.TryAcceptHostPacket(TerminalFrame(phase, sequence: 102)), Is.False);
            Assert.That(guard.TryAcceptHostPacket(Frame(round: 2)), Is.True);
            Assert.That(guard.Mission.Mission.Phase, Is.EqualTo(CoopMissionPhase.RestorePower));
            Assert.That(guard.Mission.Mission.DepotGateUnlocked, Is.False); Assert.That(guard.Mission.Sequence, Is.EqualTo(1));
            Assert.That(guard.Mission.Range.Member(0).Ammo, Is.EqualTo(12));
        }
        [Test]
        public void RepeatedCompletedRoundsNeverReopenRetiredTerminalOrLobbyPackets()
        {
            var guard = new CoopMissionSessionReplica(); guard.TryAcceptHostPacket(Lobby());
            for (long round = 1; round <= 100; round++)
            {
                Assert.That(guard.TryAcceptHostPacket(Frame(round: round)), Is.True);
                Assert.That(guard.TryAcceptHostPacket(TerminalFrame(CoopMissionPhase.Completed, round)), Is.True);
                Assert.That(guard.TryAcceptHostPacket(Lobby(round: round - 1)), Is.False);
                Assert.That(guard.TryAcceptHostPacket(Lobby(round: round)), Is.True);
                Assert.That(guard.TryAcceptHostPacket(TerminalFrame(CoopMissionPhase.Completed, round, ulong.MaxValue)), Is.False);
                Assert.That(guard.Mission, Is.Null); Assert.That(guard.Range.RoundId, Is.EqualTo(round));
            }
        }
        [Test]
        public void ClosedRoundOverflowCannotWrapIntoAValidMission()
        {
            var guard = new CoopMissionSessionReplica(); Assert.That(guard.TryAcceptHostPacket(Lobby(round: long.MaxValue)), Is.True);
            Assert.That(guard.TryAcceptHostPacket(Frame()), Is.False);
        }
    }
}
