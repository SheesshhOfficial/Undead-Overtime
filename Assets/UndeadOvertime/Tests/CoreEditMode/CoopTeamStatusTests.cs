using System;
using System.Globalization;
using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class CoopTeamStatusTests
    {
        private static RangeMember M(byte slot, byte health = 100, ulong? peer = null, long? connection = null,
            double x = 0, double z = 0, byte target = 255, byte progress = 0) =>
            new RangeMember(peer ?? slot, connection ?? slot + 1, slot, 12, false,
                new AimVector(x, HostMoveGate.EyeHeight, z), health, target, progress);
        private static RangeSnapshot S(params RangeMember[] members) => new RangeSnapshot(1, 1, SessionPhase.Playing, members, new[] { 100, 100, 100 });
        private static CoopTeamStatus Read(RangeSnapshot snapshot, ulong peer = 0)
        { Assert.That(CoopTeamStatus.TryCreate(snapshot, peer, out CoopTeamStatus result), Is.True); return result; }
        private static void Invalid(RangeSnapshot snapshot, ulong peer = 0)
        { Assert.That(CoopTeamStatus.TryCreate(snapshot, peer, out CoopTeamStatus result), Is.False); Assert.That(result, Is.Null); }

        [Test]
        public void SoloMemberHasAnExplicitLocalLabelAndNoDistanceSuffix()
        {
            CoopTeamStatus result = Read(S(M(0)));
            Assert.That(result.Count, Is.EqualTo(1)); Assert.That(result.DownedCount, Is.Zero);
            Assert.That(result.Member(0).StatusText, Is.EqualTo("Slot 1 (you) | 100 HP"));
            Assert.That(result.Member(0).Distance, Is.Zero); Assert.That(result.Member(0).RescuerSlot, Is.EqualTo(255));
        }
        [Test]
        public void StableSlotOrderDoesNotFollowHostArrayEnumeration()
        {
            CoopTeamStatus result = Read(S(M(9), M(3, 25), M(0), M(5, 0)));
            Assert.That(result.Count, Is.EqualTo(4)); Assert.That(result.DownedCount, Is.EqualTo(1));
            Assert.That(new[] { result.Member(0).Slot, result.Member(1).Slot, result.Member(2).Slot, result.Member(3).Slot }, Is.EqualTo(new byte[] { 0, 3, 5, 9 }));
            Assert.That(result.Member(1).Health, Is.EqualTo(25)); Assert.That(result.Member(2).IsDowned, Is.True);
        }
        [Test]
        public void DistancesArePlanarAndRoundUpForTextOnly()
        {
            CoopTeamStatus result = Read(S(M(0, x: 1, z: 2), M(1, x: 4, z: 6), M(2, x: 1.01, z: 2)));
            Assert.That(result.Member(1).Distance, Is.EqualTo(5));
            Assert.That(result.Member(1).StatusText, Is.EqualTo("Slot 2 | 100 HP | 5 m"));
            Assert.That(result.Member(2).Distance, Is.EqualTo(.01).Within(1e-9));
            Assert.That(result.Member(2).StatusText, Does.EndWith("1 m"));
        }
        [Test]
        public void LocalPeerIsNotAssumedToBeHostOrSlotZero()
        {
            CoopTeamStatus result = Read(S(M(4, peer: 900), M(0, peer: 450)), 900);
            Assert.That(result.Member(0).IsLocal, Is.False); Assert.That(result.Member(1).IsLocal, Is.True);
        }
        [Test]
        public void RescueUsesHostReportedProgressWithoutAddingDifferentRescuers()
        {
            CoopTeamStatus result = Read(S(M(0, target: 3, progress: 40), M(1, target: 3, progress: 60), M(2, target: 3, progress: 60), M(3, 0)));
            CoopTeamMember target = result.Member(3);
            Assert.That(target.RescuerSlot, Is.EqualTo(1)); Assert.That(target.RescueProgress, Is.EqualTo(60));
            Assert.That(target.StatusText, Does.Contain("rescue 60% by slot 2"));
            Assert.That(result.Member(0).RescuerSlot, Is.EqualTo(255));
        }
        [Test]
        public void ZeroPercentRescueIsStillAnObservedChannel()
        {
            CoopTeamMember target = Read(S(M(0, target: 1), M(1, 0))).Member(1);
            Assert.That(target.RescuerSlot, Is.Zero); Assert.That(target.StatusText, Does.Contain("rescue 0% by slot 1"));
        }
        [Test]
        public void DownedLocalSeesWhoIsRescuingThem()
        {
            CoopTeamStatus result = Read(S(M(0, 0), M(1, target: 0, progress: 99)));
            Assert.That(result.Member(0).StatusText, Is.EqualTo("Slot 1 (you) | DOWNED | rescue 99% by slot 2"));
        }
        [Test]
        public void HealingDisconnectRejoinAndNewRoundsHaveNoRetainedRosterState()
        {
            CoopTeamStatus old = Read(S(M(0, 0), M(1, target: 0, progress: 90)));
            CoopTeamStatus healed = Read(S(M(0, 40)));
            Assert.That(healed.DownedCount, Is.Zero); Assert.That(healed.Member(0).RescuerSlot, Is.EqualTo(255));
            CoopTeamStatus rejoined = Read(S(M(0, peer: 8, connection: 20)), 8);
            Assert.That(rejoined.Member(0).Health, Is.EqualTo(100));
            CoopTeamStatus reset = Read(new RangeSnapshot(2, 2, SessionPhase.Playing, new[] { M(0) }, new[] { 100, 100, 100 }));
            Assert.That(reset.DownedCount, Is.Zero); Assert.That(old.Member(0).IsDowned, Is.True, "Previous immutable projection is not mutated.");
        }
        [Test]
        public void TenPlayersAreBoundedAndAllPresent()
        {
            var members = new RangeMember[10]; for (byte i = 0; i < members.Length; i++) members[i] = M(i, (byte)(i * 10));
            CoopTeamStatus result = Read(S(members)); Assert.That(result.Count, Is.EqualTo(10)); Assert.That(result.DownedCount, Is.EqualTo(1));
            Assert.That(result.Member(9).Label, Is.EqualTo("Slot 10"));
            Assert.Throws<IndexOutOfRangeException>(() => result.Member(10));
        }
        [Test]
        public void FormattingIsStableAcrossUserCultures()
        {
            CultureInfo before = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA"); Assert.That(Read(S(M(0), M(1, 45, x: 12))).Member(1).StatusText, Is.EqualTo("Slot 2 | 45 HP | 12 m")); }
            finally { CultureInfo.CurrentCulture = before; }
        }
        [TestCase(SessionPhase.Stopped)] [TestCase(SessionPhase.Lobby)] [TestCase((SessionPhase)255)]
        public void OutsideActiveRoundHasNoTeamHud(SessionPhase phase) =>
            Invalid(new RangeSnapshot(1, 1, phase, new[] { M(0) }, new[] { 100, 100, 100 }));
        [TestCase(0, 1)] [TestCase(1, 0)] [TestCase(-1, 1)] [TestCase(1, -1)]
        public void InvalidSessionOrRoundFailsClosed(long session, long round) =>
            Invalid(new RangeSnapshot(session, round, SessionPhase.Playing, new[] { M(0) }, new[] { 100, 100, 100 }));
        [Test]
        public void MissingLocalOrEmptyOrOversizedRosterFailsClosed()
        {
            Invalid(null); Invalid(S()); Invalid(S(M(1))); var members = new RangeMember[11];
            for (byte i = 0; i < members.Length; i++) members[i] = M(i); Invalid(S(members));
        }
        [TestCase(101, 0, 1)] [TestCase(100, 10, 1)] [TestCase(100, 0, 0)] [TestCase(100, 0, -1)]
        public void InvalidHealthSlotOrConnectionFailsClosed(byte health, byte slot, long connection) => Invalid(S(M(slot, health, connection: connection)));
        [Test]
        public void DuplicatePeerSlotOrConnectionNeverCreatesAmbiguousRescueLabels()
        {
            Invalid(S(M(0), M(0, peer: 3, connection: 4)));
            Invalid(S(M(0), M(1, peer: 0)));
            Invalid(S(M(0), M(1, connection: 1)));
        }
        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)] [TestCase(100)]
        public void NonfiniteOrOutOfArenaPositionFailsClosed(double x) => Invalid(S(M(0), M(1, x: x)));
        [Test]
        public void WrongEyeHeightIsNotTreatedAsAValidDistance()
        { Invalid(S(new RangeMember(0, 1, 0, 12, false, new AimVector(0, 0, 0)))); }
        [Test]
        public void InvalidRescueClaimsNeverBecomeDisplayedProgress()
        {
            Invalid(S(M(0, target: 0))); Invalid(S(M(0, target: 10))); Invalid(S(M(0, progress: 1)));
            Invalid(S(M(0, target: 1))); Invalid(S(M(0, target: 1), M(1, 100)));
            Invalid(S(M(0, 0, target: 1), M(1, 0))); Invalid(S(M(0, target: 1, progress: 100), M(1, 0)));
        }
        [TestCase(1280, 720, 10, true)] [TestCase(640, 480, 2, true)] [TestCase(640, 480, 10, true)]
        [TestCase(639, 480, 10, false)] [TestCase(640, 479, 10, false)] [TestCase(640, 360, 10, false)]
        [TestCase(1280, 280, 10, false)] [TestCase(1280, 720, 1, false)] [TestCase(1280, 720, 0, false)]
        [TestCase(1280, 720, 11, false)] [TestCase(0, 0, 2, false)] [TestCase(-1, -1, 2, false)]
        public void PanelHasAnExplicitNonOverlappingMinimumViewport(int width, int height, int count, bool expected)
        { Assert.That(CoopTeamStatus.CanShowPanel(width, height, count), Is.EqualTo(expected)); }
        [TestCase(EncounterPhase.Active)] [TestCase(EncounterPhase.Cleared)]
        public void RescueAdviceRequiresALivingTeammateAndEligibleEncounter(EncounterPhase phase)
        { Assert.That(Read(S(M(0, 0), M(1))).RescueAdvice(phase, false), Does.Contain("hold E")); }
        [TestCase(EncounterPhase.Failed)] [TestCase(EncounterPhase.Dormant)] [TestCase((EncounterPhase)255)]
        public void IneligibleEncounterDoesNotPromiseRescue(EncounterPhase phase)
        { Assert.That(Read(S(M(0, 0), M(1))).RescueAdvice(phase, false), Does.Contain("restart")); }
        [Test]
        public void WipeOrTerminalMissionShowsRestartAdviceNotImpossibleRescue()
        {
            Assert.That(Read(S(M(0, 0), M(1, 0))).RescueAdvice(EncounterPhase.Active, false), Does.Contain("restart"));
            Assert.That(Read(S(M(0, 0), M(1))).RescueAdvice(EncounterPhase.Active, true), Does.Contain("restart"));
            Assert.That(Read(S(M(0))).RescueAdvice(EncounterPhase.Active, false), Is.Empty);
        }
        [Test]
        public void ImmutableRowsReuseTheirFormattedStrings()
        {
            CoopTeamMember row = Read(S(M(0), M(1, 40))).Member(1);
            Assert.That(row.Label, Is.SameAs(row.Label)); Assert.That(row.StatusText, Is.SameAs(row.StatusText));
        }

        [Test]
        public void ReadsNeverMutatePacketsHealthOrReviveRules()
        {
            RangeSnapshot snapshot = S(M(0), M(1, 40, x: 3)); byte[] before = LanRangeProtocol.Encode(snapshot);
            for (int i = 0; i < 20; i++) { CoopTeamStatus value = Read(snapshot); _ = value.Member(1).StatusText; }
            Assert.That(LanRangeProtocol.Encode(snapshot), Is.EqualTo(before)); Assert.That(LanRangeProtocol.Version, Is.EqualTo(5));
            Assert.That(HostInfectedEncounter.ReviveDuration, Is.EqualTo(3)); Assert.That(HostInfectedEncounter.ReviveRange, Is.EqualTo(2));
        }
    }
}
