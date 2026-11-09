using System.Collections;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UndeadOvertime.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class LanDamageFeedbackTests
    {
        private GameObject root, cameraObject;
        private LanRange range;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            root = new GameObject("Damage feedback lifecycle test");
            cameraObject = new GameObject("Damage feedback camera");
            range = root.AddComponent<LanRange>(); range.Build(cameraObject.AddComponent<Camera>(), false);
            ushort port;
            using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
                port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            Assert.That(range.StartHosting("127.0.0.1", port), Is.True);
            for (int i = 0; i < 60 && !range.IsConnected; i++) yield return null;
            Assert.That(range.BeginRound(), Is.True);
        }
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (range != null) range.StopSession();
            for (int i = 0; i < 120 && range != null && range.IsBusy; i++) yield return null;
            Object.Destroy(root); Object.Destroy(cameraObject); yield return null; yield return null;
        }
        [UnityTest]
        public IEnumerator BorderPreferenceIsPresentationOnlyAndSurvivesRoundReset()
        {
            Assert.That(range.DamageBorderEnabled, Is.True);
            Assert.That(range.TryGetLocalMember(out RangeMember before), Is.True);
            long round = range.CurrentSnapshot.RoundId;
            for (int i = 0; i < 4; i++) range.SetDamageBorderEnabled(i % 2 == 0);
            Assert.That(range.DamageBorderEnabled, Is.False);
            Assert.That(range.ControlsCaptured, Is.False);
            Assert.That(range.CurrentSnapshot.RoundId, Is.EqualTo(round));
            Assert.That(range.TryGetLocalMember(out RangeMember after), Is.True);
            Assert.That(after.Health, Is.EqualTo(before.Health)); Assert.That(after.Ammo, Is.EqualTo(before.Ammo));
            Assert.That(after.Position, Is.EqualTo(before.Position));
            Assert.That(range.DamageFeedback.DamagePulse, Is.Zero); Assert.That(range.DamageFeedbackText, Is.Empty);
            Assert.That(range.ReturnToLobby(), Is.True); Assert.That(range.BeginRound(), Is.True);
            Assert.That(range.DamageBorderEnabled, Is.False);
            Assert.That(range.DamageFeedback.IsDowned, Is.False); Assert.That(range.DamageFeedback.DamagePulse, Is.Zero);
            yield return null;
        }
        [UnityTest]
        public IEnumerator DisconnectClearsFeedbackAndDoesNotAlterViewOrPreference()
        {
            range.SetViewMode(PlayerViewMode.ThirdPerson); range.SetDamageBorderEnabled(false);
            Assert.That(range.StartEncounter(), Is.True);
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (range.CurrentSnapshot.Member(0).Health == 100 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(range.CurrentSnapshot.Member(0).Health, Is.InRange(1, 90));
            Assert.That(range.DamageFeedback.DamagePulse, Is.GreaterThan(0), "Exercise teardown with active damage, not an empty tracker.");
            Assert.That(range.DamageFeedback.LastDamage, Is.GreaterThan(0));
            range.StopSession();
            Assert.That(range.DamageFeedback.DamagePulse, Is.Zero, "StopSession clears feedback immediately, before waiting for shutdown.");
            Assert.That(range.DamageFeedbackText, Is.Empty);
            for (int i = 0; i < 120 && range.IsBusy; i++) yield return null;
            Assert.That(range.IsBusy, Is.False); Assert.That(range.Phase, Is.EqualTo(SessionPhase.Stopped));
            Assert.That(range.DamageFeedback.DamagePulse, Is.Zero); Assert.That(range.DamageFeedback.LastDamage, Is.Zero);
            Assert.That(range.DamageFeedback.IsDowned, Is.False); Assert.That(range.DamageFeedbackText, Is.Empty);
            Assert.That(range.DamageBorderEnabled, Is.False); Assert.That(range.ViewMode, Is.EqualTo(PlayerViewMode.ThirdPerson));
            Assert.That(range.ControlsCaptured, Is.False);
        }
    }
}
