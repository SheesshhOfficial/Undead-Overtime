using System.Collections;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UndeadOvertime.Core;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class LanInfectedPresentationTests
    {
        private GameObject root;
        private LanRange range;
        [UnitySetUp] public IEnumerator Setup() { root = new GameObject("Infected presentation test"); yield return null; }
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (range != null) range.StopSession();
            for (int i = 0; i < 120 && range != null && range.IsBusy; i++) yield return null;
            Object.Destroy(root); yield return null; yield return null;
        }
        private IEnumerator StartEncounter(bool presentation)
        {
            var camera = new GameObject("Infected presentation camera"); camera.transform.SetParent(root.transform);
            range = root.AddComponent<LanRange>(); range.Build(camera.AddComponent<Camera>(), presentation);
            ushort port;
            using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
                port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            Assert.That(range.StartHosting("127.0.0.1", port), Is.True);
            for (int i = 0; i < 60 && !range.IsConnected; i++) yield return null;
            Assert.That(range.BeginRound(), Is.True); Assert.That(range.StartEncounter(), Is.True);
        }
        [UnityTest]
        public IEnumerator HeadlessNeverTracksLocomotionOrCreatesAnimationContent()
        {
            yield return StartEncounter(false);
            Assert.That(range.PresentationEnabled, Is.False);
            Assert.That(range.CurrentSnapshot.EnemyCount, Is.EqualTo(1));
            Assert.That(range.TryGetInfectedLocomotion(0, out InfectedLocomotionState state), Is.False);
            Assert.That(state.IsVisible, Is.False);
            Assert.That(root.GetComponentsInChildren<Renderer>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<MeshFilter>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Animator>(true), Is.Empty);
        }
        [UnityTest]
        public IEnumerator RepeatedPresentationReadsNeverReplayOldSnapshotIntoHostPhysics()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Requires graphics-enabled Unity to exercise populated presentation, not its disabled branch.");
            yield return StartEncounter(true);
            Assert.That(range.PresentationEnabled, Is.True);
            Transform enemy = null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == "Host-owned infected 0") enemy = child;
            Assert.That(enemy, Is.Not.Null);
            bool observedNewerPhysics = false;
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForFixedUpdate();
                AimVector old = range.CurrentSnapshot.Enemy(0).Position;
                Vector3 publishedBody = new Vector3((float)old.X, (float)old.Y - .7f, (float)old.Z);
                if ((enemy.position - publishedBody).sqrMagnitude > .0000001f) { observedNewerPhysics = true; break; }
            }
            Assert.That(observedNewerPhysics, Is.True);
            Vector3 position = enemy.position, scale = enemy.localScale;
            Quaternion rotation = enemy.rotation;
            Collider collider = enemy.GetComponent<Collider>();
            bool active = enemy.gameObject.activeSelf, collision = collider.enabled;
            RangeSnapshot before = range.CurrentSnapshot; byte[] bytes = LanRangeProtocol.Encode(before);
            for (int i = 0; i < 20; i++)
                Assert.That(range.TryGetInfectedLocomotion(0, out _), Is.EqualTo(range.PresentationEnabled));
            Assert.That(range.TryGetInfectedLocomotion(255, out _), Is.False);
            range.SetViewMode(PlayerViewMode.ThirdPerson); range.SetViewMode(PlayerViewMode.FirstPerson);
            Assert.That(enemy.position, Is.EqualTo(position)); Assert.That(enemy.localScale, Is.EqualTo(scale));
            Assert.That(enemy.rotation, Is.EqualTo(rotation)); Assert.That(enemy.gameObject.activeSelf, Is.EqualTo(active));
            Assert.That(collider.enabled, Is.EqualTo(collision));
            Assert.That(range.CurrentSnapshot, Is.SameAs(before));
            Assert.That(LanRangeProtocol.Encode(range.CurrentSnapshot), Is.EqualTo(bytes));
            Assert.That(root.GetComponentsInChildren<Animator>(true), Is.Empty);
        }
        [UnityTest]
        public IEnumerator LobbyRestartAndStopClearCosmeticIdentity()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Requires graphics-enabled Unity to verify that populated presentation is cleared.");
            yield return StartEncounter(true);
            Assert.That(range.PresentationEnabled, Is.True);
            Assert.That(range.TryGetInfectedLocomotion(0, out _), Is.EqualTo(range.PresentationEnabled));
            long oldRound = range.CurrentSnapshot.RoundId;
            Assert.That(range.ReturnToLobby(), Is.True);
            Assert.That(range.TryGetInfectedLocomotion(0, out _), Is.False);
            Assert.That(range.BeginRound(), Is.True); Assert.That(range.StartEncounter(), Is.True);
            Assert.That(range.CurrentSnapshot.RoundId, Is.GreaterThan(oldRound));
            bool found = range.TryGetInfectedLocomotion(0, out InfectedLocomotionState state);
            Assert.That(found, Is.EqualTo(range.PresentationEnabled));
            if (found) { Assert.That(state.IsMoving, Is.False); Assert.That(state.Speed, Is.Zero); }
            range.StopSession(); Assert.That(range.TryGetInfectedLocomotion(0, out _), Is.False);
        }
    }
}
