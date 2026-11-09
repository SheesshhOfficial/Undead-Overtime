using System.Collections;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UndeadOvertime.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class LanTeamStatusTests
    {
        private GameObject root;
        private LanRange range;
        [UnitySetUp] public IEnumerator Setup()
        {
            root = new GameObject("Team HUD lifecycle"); var camera = new GameObject("Team HUD camera"); camera.transform.SetParent(root.transform);
            range = root.AddComponent<LanRange>(); range.Build(camera.AddComponent<Camera>(), false);
            ushort port; using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0))) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            Assert.That(range.StartHosting("127.0.0.1", port), Is.True);
            for (int i = 0; i < 60 && !range.IsConnected; i++) yield return null;
            Assert.That(range.BeginRound(), Is.True);
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            if (range != null) range.StopSession();
            for (int i = 0; i < 120 && range != null && range.IsBusy; i++) yield return null;
            Object.Destroy(root); yield return null; yield return null;
        }
        [UnityTest] public IEnumerator ActualHostFeedsImmutableRosterWithoutRenderingOrGameMutation()
        {
            byte[] before = LanRangeProtocol.Encode(range.CurrentSnapshot);
            CoopTeamStatus first = range.CurrentTeamStatus;
            Assert.That(first, Is.Not.Null); Assert.That(first.Count, Is.EqualTo(1)); Assert.That(first.Member(0).IsLocal, Is.True);
            for (int i = 0; i < 20; i++) Assert.That(range.CurrentTeamStatus, Is.SameAs(first));
            range.SetTeamStatusEnabled(false); Assert.That(range.TeamStatusEnabled, Is.False);
            Assert.That(range.CurrentTeamStatus, Is.SameAs(first), "Preference affects only rendering, not host state.");
            Assert.That(LanRangeProtocol.Encode(range.CurrentSnapshot), Is.EqualTo(before));
            Assert.That(root.GetComponentsInChildren<Renderer>(true), Is.Empty); yield return null;
        }
        [UnityTest] public IEnumerator LobbyRestartAndStopNeverLeaveAStaleRoster()
        {
            CoopTeamStatus first = range.CurrentTeamStatus; Assert.That(first, Is.Not.Null);
            range.SetTeamStatusEnabled(false);
            Assert.That(range.ReturnToLobby(), Is.True); Assert.That(range.CurrentTeamStatus, Is.Null);
            Assert.That(range.BeginRound(), Is.True); Assert.That(range.CurrentTeamStatus, Is.Not.Null);
            Assert.That(range.CurrentTeamStatus, Is.Not.SameAs(first)); Assert.That(range.TeamStatusEnabled, Is.False);
            range.StopSession(); Assert.That(range.CurrentTeamStatus, Is.Null); yield return null;
        }
    }
}
