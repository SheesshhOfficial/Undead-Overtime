using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UndeadOvertime.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UndeadOvertime.LanPrototype
{
    /// <summary>
    /// Opt-in standalone automated loopback peer. No GUI automation, editor execution, firewall change,
    /// public/LAN address, arbitrary output path, or additional process launch from game code.
    /// </summary>
    public sealed class LanLoopbackSmoke : MonoBehaviour
    {
        private LanRange range;
        private LoopbackSmokeOptions options;
        private SmokeReport report;
        private readonly List<string> steps = new List<string>();
        private double deadline;
        private bool finished;
        [Serializable]
        private sealed class SmokeReport
        {
            public int formatVersion = 1;
            public string role, runId, requestedRevision, unityVersion, applicationVersion, startedUtc, finishedUtc;
            public int processId, port, warningCount, runtimeErrorCount;
            public string firstRuntimeError;
            public bool success;
            public string error;
            public string[] steps;
            public long firstRemoteConnection, secondRemoteConnection;
            public string scope = "Two standalone processes on 127.0.0.1 only; not real LAN or ten-player acceptance.";
        }
        public static void TryStart(LanRange range)
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!LoopbackSmokeOptions.Requested(args)) return;
            if (Application.isEditor) { Debug.LogWarning("UO_LAN_SMOKE: standalone player only; no editor automation started."); return; }
            if (!LoopbackSmokeOptions.TryParse(args, out LoopbackSmokeOptions parsed, out string error))
            { Debug.LogWarning("UO_LAN_SMOKE_ARGUMENT_ERROR " + error); Application.Quit(2); return; }
            var runner = range.gameObject.AddComponent<LanLoopbackSmoke>();
            runner.range = range; runner.options = parsed; runner.Begin();
        }
        private void Begin()
        {
            report = new SmokeReport
            {
                role = options.Host ? "host" : "client", runId = options.RunId, requestedRevision = options.Revision,
                unityVersion = Application.unityVersion, applicationVersion = Application.version,
                startedUtc = DateTime.UtcNow.ToString("O"), processId = Process.GetCurrentProcess().Id, port = options.Port
            };
            Application.logMessageReceived += ObserveLog;
            deadline = Time.realtimeSinceStartupAsDouble + 90;
            StartCoroutine(Guarded(options.Host ? HostRun() : ClientRun()));
        }
        private IEnumerator Guarded(IEnumerator work)
        {
            while (!finished)
            {
                if (report.runtimeErrorCount > 0) { Finish(false, "Unity runtime error: " + report.firstRuntimeError); yield break; }
                if (Time.realtimeSinceStartupAsDouble > deadline) { Finish(false, "90-second test deadline reached. Last status: " + range.Status); yield break; }
                Exception failure = null; bool more = false;
                try { more = work.MoveNext(); } catch (Exception exception) { failure = exception; }
                if (failure != null) { Finish(false, failure.Message); yield break; }
                if (!more) { Finish(true, null); yield break; }
                yield return work.Current;
            }
        }
        private IEnumerator HostRun()
        {
            Require(range.StartHosting("127.0.0.1", options.Port), "Host did not start: " + range.Status);
            Record("host-started");
            while (range.MemberCount < 2) { Require(range.IsConnected, "Host disconnected before client joined."); yield return null; }
            Require(range.MemberCount == 2, "Unexpected extra peer in controlled test.");
            report.firstRemoteConnection = Remote().ConnectionId; Record("two-process-membership");
            Require(range.BeginRound(), "Could not begin first round."); Record("round-one-started");
            Vector3 spawn = LanRange.Station(Remote().Slot);
            while (Remote().Position.Z - spawn.z < 1) yield return null;
            Require(Math.Abs(Remote().Position.X - spawn.x) < .05 && Remote().Position.Z - spawn.z < 5,
                "Remote movement was not bounded and planar.");
            Record("remote-movement-observed");
            while (range.CurrentSnapshot.TargetHealth(1) != 0) { Require(range.MemberCount == 2, "Client left before damage test."); yield return null; }
            Require(Remote().Ammo == 8, "Four target hits did not consume exactly four remote rounds."); Record("remote-four-hit-kill");
            while (!Remote().Reloading) yield return null;
            Record("remote-reload-started");
            while (Remote().Reloading || Remote().Ammo != 12) yield return null;
            Record("remote-reload-completed");
            // Wait for the client to acknowledge that it actually observed the full magazine.
            while (Remote().Ammo != 11) yield return null;
            Require(range.CurrentSnapshot.TargetHealth(1) == 0, "Reload acknowledgement changed the destroyed target.");
            Record("client-reload-result-acknowledged");
            Require(range.ReturnToLobby(), "Could not return to lobby.");
            while (range.MemberCount != 1) yield return null;
            Record("client-disconnect-observed");
            while (range.MemberCount < 2) yield return null;
            Require(range.MemberCount == 2, "Unexpected extra peer on rejoin.");
            report.secondRemoteConnection = Remote().ConnectionId;
            Require(report.secondRemoteConnection != report.firstRemoteConnection, "Rejoin reused the old connection identity.");
            Record("fresh-client-rejoin"); Require(range.BeginRound(), "Could not begin second round.");
            Require(range.CurrentSnapshot.RoundId == 2, "Second round identity is incorrect.");
            Require(range.CurrentSnapshot.TargetHealth(0) == 100 && range.CurrentSnapshot.TargetHealth(1) == 100, "Round reset did not restore targets.");
            Record("round-two-reset");
            while (range.CurrentSnapshot.TargetHealth(0) != 75) yield return null;
            Require(Remote().Ammo == 11, "Second-round hit did not consume one remote round.");
            // Client sends reload only after seeing the replicated 75 HP result, acting as a receipt.
            while (!Remote().Reloading) yield return null;
            Record("second-round-replication-acknowledged");
            Require(range.StartEncounter(), "Host could not start the infected encounter."); Record("infected-encounter-started");
            while (Remote().Health == 100) { Require(range.MemberCount == 2, "Client left before infected contact."); yield return null; }
            Require(Remote().Health > 0, "Client was downed before the planned encounter shot test.");
            Record("remote-player-injured");
            while (range.CurrentSnapshot.Encounter.Phase != EncounterPhase.Cleared)
            { Require(range.CurrentSnapshot.Encounter.Phase == EncounterPhase.Active, "Encounter failed before kill."); yield return null; }
            Require(range.CurrentSnapshot.Encounter.Health == 0 && Remote().Health > 0, "Host kill/health result mismatch.");
            Record("infected-killed");
            while (!Remote().Reloading) yield return null;
            Record("infected-result-acknowledged");
            range.StopSession("Automated loopback host shutdown.");
            while (range.IsBusy) yield return null;
            Record("host-shutdown-completed");
        }
        private IEnumerator ClientRun()
        {
            Require(range.Join("127.0.0.1", options.Port), "Client did not start: " + range.Status);
            Record("client-started");
            while (range.Phase != SessionPhase.Playing) { Require(range.IsBusy, "Client connection failed: " + range.Status); yield return null; }
            Require(range.MemberCount == 2 && range.CurrentSnapshot.RoundId == 1, "First round membership/identity mismatch.");
            Record("first-round-received");
            Vector3 spawn = LanRange.Station(Local().Slot);
            double walkingUntil = Time.realtimeSinceStartupAsDouble + .7;
            while (Time.realtimeSinceStartupAsDouble < walkingUntil)
            {
                Require(range.SendAction(RangeAction.Move, Vector3.forward), "Could not submit walking intent.");
                yield return new WaitForSecondsRealtime(.05f);
            }
            // Stop transmitting without an explicit stop packet; the host must expire held intent.
            yield return new WaitForSecondsRealtime(.6f);
            AimVector settled = Local().Position;
            Require(settled.Z - spawn.z > 1 && settled.Z - spawn.z < 5 && Math.Abs(settled.X - spawn.x) < .05,
                "Client did not receive bounded host-resolved movement.");
            yield return new WaitForSecondsRealtime(.4f);
            Require((Local().Position - settled).LengthSquared < .0001, "Expired walking intent kept moving the client.");
            Record("replicated-movement-and-expiry");
            for (int i = 0; i < 4; i++)
            {
                Require(range.SendAction(RangeAction.Fire, AimAt(1)), "Could not submit first-round shot.");
                yield return new WaitForSecondsRealtime(.25f);
            }
            while (range.CurrentSnapshot.TargetHealth(1) != 0 || Local().Ammo != 8) yield return null;
            Record("replicated-four-hit-kill");
            Require(range.SendAction(RangeAction.Reload, AimAt(1)), "Could not submit reload.");
            while (!Local().Reloading) yield return null;
            Record("replicated-reload-started");
            while (Local().Reloading || Local().Ammo != 12) yield return null;
            Record("replicated-reload-completed");
            Require(range.SendAction(RangeAction.Fire, Vector3.up), "Could not acknowledge the observed reload with a deliberate miss.");
            while (range.Phase != SessionPhase.Lobby) yield return null;
            range.StopSession("Automated client reconnect test.");
            while (range.IsBusy) yield return null;
            Record("client-disconnected"); yield return new WaitForSecondsRealtime(.5f);
            Require(range.Join("127.0.0.1", options.Port), "Rejoin did not start: " + range.Status);
            while (range.Phase != SessionPhase.Playing) { Require(range.IsBusy, "Rejoin failed: " + range.Status); yield return null; }
            Require(range.MemberCount == 2 && range.CurrentSnapshot.RoundId == 2, "Second round membership/identity mismatch.");
            Require(range.CurrentSnapshot.TargetHealth(0) == 100 && range.CurrentSnapshot.TargetHealth(1) == 100 && Local().Ammo == 12,
                "Client did not receive the complete round reset.");
            Require((Local().Position - HostMoveGate.Spawn(Local().Slot)).LengthSquared < .0001,
                "Round reset did not restore the assigned spawn.");
            Record("rejoined-reset-received");
            Require(range.SendAction(RangeAction.Fire, AimAt(0)), "Could not submit second-round shot.");
            while (range.CurrentSnapshot.TargetHealth(0) != 75 || Local().Ammo != 11) yield return null;
            Record("second-round-damage-received"); yield return new WaitForSecondsRealtime(.05f);
            Require(range.SendAction(RangeAction.Reload, AimAt(0)), "Could not acknowledge second-round result.");
            while (range.CurrentSnapshot.Encounter.Phase != EncounterPhase.Active) yield return null;
            Record("infected-encounter-received");
            while (Local().Health == 100) yield return null;
            Require(Local().Health > 0, "Client downed before the scripted response.");
            Require((range.CurrentSnapshot.Encounter.Position - HostInfectedEncounter.Spawn).LengthSquared > 1,
                "Infected contact occurred without replicated pursuit.");
            Record("replicated-player-injury");
            for (int i = 0; i < 4; i++)
            {
                AimVector enemy = range.CurrentSnapshot.Encounter.Position, eye = Local().Position;
                Vector3 aim = new Vector3((float)(enemy.X - eye.X), (float)(enemy.Y - eye.Y), (float)(enemy.Z - eye.Z)).normalized;
                Require(range.SendAction(RangeAction.Fire, aim), "Could not submit infected shot.");
                yield return new WaitForSecondsRealtime(.25f);
            }
            while (range.CurrentSnapshot.Encounter.Phase != EncounterPhase.Cleared) yield return null;
            Require(range.CurrentSnapshot.Encounter.Health == 0 && Local().Health > 0, "Client infected kill/health mismatch.");
            Record("replicated-infected-kill");
            Require(range.SendAction(RangeAction.Reload, AimAt(0)), "Could not acknowledge infected result.");
            while (range.IsBusy || range.IsConnected) yield return null;
            Require(range.MemberCount == 0 && range.Phase == SessionPhase.Stopped, "Host shutdown left stale client membership.");
            Record("host-shutdown-observed");
        }
        private RangeMember Local()
        {
            Require(range.TryGetLocalMember(out RangeMember member), "Local member missing from snapshot.");
            return member;
        }
        private RangeMember Remote()
        {
            RangeSnapshot state = range.CurrentSnapshot;
            Require(state != null && state.MemberCount == 2, "Expected exactly two members.");
            for (int i = 0; i < state.MemberCount; i++) if (state.Member(i).PeerId != 0) return state.Member(i);
            throw new InvalidOperationException("Remote member missing from snapshot.");
        }
        private Vector3 AimAt(int target)
        {
            AimVector eye = Local().Position;
            return (new Vector3((target - 1) * 5, 1.6f, 10) - new Vector3((float)eye.X, (float)eye.Y, (float)eye.Z)).normalized;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void Record(string step) { steps.Add(step); Debug.Log("UO_LAN_SMOKE_STEP " + report.role + " " + step); }
        private void ObserveLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning) report.warningCount++;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            report.runtimeErrorCount++;
            if (report.firstRuntimeError == null) report.firstRuntimeError = condition.Length > 1024 ? condition.Substring(0, 1024) : condition;
        }
        private void Finish(bool success, string error)
        {
            if (finished) return;
            finished = true; report.success = success; report.error = error; report.steps = steps.ToArray(); report.finishedUtc = DateTime.UtcNow.ToString("O");
            range.StopSession(success ? "Automated smoke test completed." : "Automated smoke test failed.");
            if (report.runtimeErrorCount > 0) { report.success = false; report.error = report.error ?? report.firstRuntimeError; }
            int exit = report.success ? 0 : 1;
            try
            {
                string directory = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "SmokeResults", options.RunId);
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, report.role + ".json");
                // Each run/role is new. Do not overwrite an existing evidence file.
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(report, true));
                Debug.Log("UO_LAN_SMOKE_RESULT " + (report.success ? "PASS " : "FAIL ") + path);
            }
            catch (Exception exception) { Debug.LogWarning("UO_LAN_SMOKE_REPORT_FAILED " + exception.Message); exit = 2; }
            Application.logMessageReceived -= ObserveLog;
            Application.Quit(exit);
        }
    }
}
