using System;
using System.Collections;
using NUnit.Framework;
using UndeadOvertime.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class DepotMissionPresentationTests
    {
        private GameObject owner;
        private DepotMissionPresentation layout;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            owner = new GameObject("Mission layout test owner");
            layout = new DepotMissionPresentation(owner.transform, 5, 2, false);
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            layout?.Dispose(); UnityEngine.Object.Destroy(owner);
            yield return null; yield return null;
        }
        private static CoopMissionSnapshot State(CoopMissionPhase phase = CoopMissionPhase.DefendDepot, bool gate = true,
            long session = 5, long round = 2, bool current = true, double hold = 0) => new CoopMissionSnapshot(session, round, phase, current, gate, hold);
        private static Vector3 Walk(Vector3 start, Vector3 step, int ticks)
        {
            for (int i = 0; i < ticks; i++) start = LanRange.ResolvePlanarMovement(start, step);
            return start;
        }
        [Test]
        public void ColliderOnlyLayoutLoadsNoAssetsMeshesMaterialsOrNetworkObjects()
        {
            Assert.That(layout.RenderingEnabled, Is.False);
            Assert.That(layout.Root.childCount, Is.EqualTo(5));
            Assert.That(layout.Root.GetComponentsInChildren<Renderer>(true), Is.Empty);
            Assert.That(layout.Root.GetComponentsInChildren<MeshFilter>(true), Is.Empty);
            Assert.That(layout.Root.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(4));
            Assert.That(layout.GateUnlocked, Is.False);
            Assert.That(layout.Phase, Is.EqualTo(CoopMissionPhase.RestorePower));
        }
        [Test]
        public void ClosedGateBlocksExistingCapsuleMovementAndOpenGateAllowsBothDirections()
        {
            Vector3 start = new Vector3(0, 1.6f, -3);
            Vector3 blocked = Walk(start, Vector3.forward * .2f, 10);
            Assert.That(blocked.z, Is.LessThan(-2.5f));
            Assert.That(layout.TryApply(State()), Is.True);
            Vector3 north = Walk(blocked, Vector3.forward * .2f, 15);
            Assert.That(north.z, Is.GreaterThan(0));
            Vector3 back = Walk(north, Vector3.back * .2f, 25);
            Assert.That(back.z, Is.LessThan(-3));
        }
        [TestCase(-5f)] [TestCase(5f)]
        public void OpeningGateDoesNotRemoveAdjacentPartitionCollision(float x)
        {
            layout.TryApply(State());
            Vector3 blocked = Walk(new Vector3(x, 1.6f, -3), Vector3.forward * .2f, 10);
            Assert.That(blocked.z, Is.LessThan(-2.5f));
        }
        [Test]
        public void PowerControlIsReachableAboveItsPhysicalConsole()
        {
            Assert.That(layout.HasPowerLine(new AimVector(-9, 1.6, -6.8)), Is.True);
            Assert.That(layout.HasPowerLine(new AimVector(-9, 1.6, -8)), Is.False);
            Assert.That(layout.HasPowerLine(new AimVector(double.NaN, 1.6, 0)), Is.False);
            Assert.That(layout.HasPowerLine(new AimVector(-9, 0, -5)), Is.False);
            Collider console = layout.Root.Find("Mission power console").GetComponent<Collider>();
            Assert.That(console.enabled, Is.True); Assert.That(console.bounds.max.y, Is.LessThan(1.6f));
        }
        [Test]
        public void PowerLineRejectsHostOcclusionButIgnoresTriggers()
        {
            var blocker = new GameObject("Power LOS test obstruction"); blocker.transform.SetParent(owner.transform);
            blocker.transform.position = new Vector3(-9, 1.6f, -6);
            var collider = blocker.AddComponent<BoxCollider>(); collider.size = new Vector3(.8f, .8f, .2f);
            Assert.That(layout.HasPowerLine(new AimVector(-9, 1.6, -6.8)), Is.False);
            collider.isTrigger = true;
            Assert.That(layout.HasPowerLine(new AimVector(-9, 1.6, -6.8)), Is.True);
        }
        [Test]
        public void ExtractionMatchesCoordinatorRectangleAndIsNotAnotherColliderFloor()
        {
            Assert.That(layout.ContainsExtraction(DepotMissionPresentation.ExtractionCenter), Is.True);
            Assert.That(layout.ContainsExtraction(new AimVector(2, 1.6, 14)), Is.True);
            Assert.That(layout.ContainsExtraction(new AimVector(2.01, 1.6, 14)), Is.False);
            Assert.That(layout.ContainsExtraction(new AimVector(0, 0, 12)), Is.False);
            Assert.That(layout.ContainsExtraction(new AimVector(0, 1.6, double.PositiveInfinity)), Is.False);
            Assert.That(layout.Root.Find("Mission extraction marker").GetComponent<Collider>(), Is.Null);
            Assert.That(DepotMissionPresentation.ExtractionHalfWidth, Is.EqualTo(2));
            Assert.That(DepotMissionPresentation.ExtractionHalfDepth, Is.EqualTo(2));
        }
        [Test]
        public void StaleWrongRoundAndInvalidSnapshotsCannotUnlock()
        {
            foreach (var state in new[] { State(session: 6), State(round: 3), State(current: false),
                State(CoopMissionPhase.RestorePower, true), State((CoopMissionPhase)255), State(hold: double.NaN), State(hold: 1) })
                Assert.That(layout.TryApply(state), Is.False);
            Assert.That(layout.GateUnlocked, Is.False);
            Assert.That(layout.Root.Find("Mission route gate").gameObject.activeSelf, Is.True);
        }
        [Test]
        public void ProgressionNeverRelocksGateOrRegressesAndFailurePreservesBacktracking()
        {
            Assert.That(layout.TryApply(State()), Is.True);
            Assert.That(layout.TryApply(State(CoopMissionPhase.RestorePower, false)), Is.False);
            Assert.That(layout.TryApply(State(CoopMissionPhase.ReachExtraction, hold: 1)), Is.True);
            Assert.That(layout.TryApply(State()), Is.False);
            Assert.That(layout.TryApply(State(CoopMissionPhase.Failed)), Is.True);
            Assert.That(layout.TryApply(State(CoopMissionPhase.Failed, false)), Is.False);
            Assert.That(layout.TryApply(State(CoopMissionPhase.Completed)), Is.False);
            Assert.That(layout.GateUnlocked, Is.True);
            Assert.That(layout.Root.Find("Mission route gate").gameObject.activeSelf, Is.False);
        }
        [Test]
        public void TerminalSuccessCannotBeReplacedByLateFailure()
        {
            Assert.That(layout.TryApply(State(CoopMissionPhase.Completed)), Is.True);
            Assert.That(layout.TryApply(State(CoopMissionPhase.Failed)), Is.False);
            Assert.That(layout.TryApply(State(CoopMissionPhase.Completed)), Is.True);
            Assert.That(layout.Phase, Is.EqualTo(CoopMissionPhase.Completed));
        }
        [Test]
        public void FailureBeforeUnlockCannotOpenGateLater()
        {
            Assert.That(layout.TryApply(State(CoopMissionPhase.Failed, false)), Is.True);
            Assert.That(layout.TryApply(State(CoopMissionPhase.Failed, true)), Is.False);
            Assert.That(layout.GateUnlocked, Is.False);
        }
        [UnityTest]
        public IEnumerator DisposalDisablesCollisionImmediatelyThenDestroysOwnedGeometry()
        {
            Transform root = layout.Root;
            layout.Dispose(); layout.Dispose();
            Assert.That(root.gameObject.activeSelf, Is.False);
            Assert.That(layout.Root, Is.Null);
            Assert.That(layout.TryApply(State()), Is.False);
            Assert.That(layout.HasPowerLine(DepotMissionPresentation.PowerControl), Is.False);
            Assert.That(layout.ContainsExtraction(DepotMissionPresentation.ExtractionCenter), Is.False);
            Vector3 moved = Walk(new Vector3(0, 1.6f, -3), Vector3.forward * .2f, 10);
            Assert.That(moved.z, Is.GreaterThan(-1.1f));
            yield return null; Assert.That(root == null, Is.True);
        }
        [Test]
        public void ConstructionRejectsInvalidIdentityAndTransformedWorldParent()
        {
            Assert.Throws<ArgumentNullException>(() => new DepotMissionPresentation(null, 5, 2, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DepotMissionPresentation(owner.transform, 0, 2, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DepotMissionPresentation(owner.transform, 5, 0, false));
            var translated = new GameObject("Translated mission parent"); translated.transform.SetParent(owner.transform);
            translated.transform.position = Vector3.right;
            Assert.Throws<ArgumentException>(() => new DepotMissionPresentation(translated.transform, 5, 2, false));
            translated.transform.position = Vector3.zero; translated.transform.localScale = Vector3.one * 2;
            Assert.Throws<ArgumentException>(() => new DepotMissionPresentation(translated.transform, 5, 2, false));
            translated.transform.localScale = Vector3.one; translated.transform.rotation = Quaternion.Euler(0, 10, 0);
            Assert.Throws<ArgumentException>(() => new DepotMissionPresentation(translated.transform, 5, 2, false));
        }
        [UnityTest]
        public IEnumerator CoordinatorPowerTransitionOpensActualGateWithHostLineChecks()
        {
            var session = new LanSession(5); PlayerTicket host = session.StartHost(0); session.TryBeginRound(host);
            // The presentation binds a separate explicit test round for this coordinator fixture.
            layout.Dispose(); layout = new DepotMissionPresentation(owner.transform, session.SessionId, session.RoundId, false);
            yield return null;
            var mission = new HostCoopMission(session, 0, DepotMissionPresentation.PowerControl, DepotMissionPresentation.ExtractionCenter,
                DepotMissionPresentation.ExtractionHalfWidth, DepotMissionPresentation.ExtractionHalfDepth);
            for (int i = 0; i <= 60; i++)
            {
                double now = i * .05;
                mission.SetPlayerPositionFromHost(host, new AimVector(-9, 1.6, -6.8), now);
                mission.TrySetPowerIntent(host, session.SessionId, session.RoundId, (ulong)i + 1, true, now);
                Assert.That(mission.AdvanceFromHost(now, (_, eye, control) => layout.HasPowerLine(eye), encounter => true), Is.True);
                Assert.That(layout.TryApply(mission.Snapshot), Is.True);
            }
            Assert.That(mission.Snapshot.Phase, Is.EqualTo(CoopMissionPhase.DefendDepot));
            Assert.That(layout.GateUnlocked, Is.True);
            Assert.That(mission.Encounter.EnemyCount, Is.EqualTo(2));
            Assert.That(Walk(new Vector3(0, 1.6f, -3), Vector3.forward * .2f, 10).z, Is.GreaterThan(-1.1f));
        }
    }
}
