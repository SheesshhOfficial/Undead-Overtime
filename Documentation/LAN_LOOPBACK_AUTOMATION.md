# Automated two-process loopback check

## Why this exists

The LAN firing range needs two real peers exchanging messages before any networking claim. Native window controls are not required for this check: the built player now has an explicit test-only command-line mode, and a PowerShell launcher starts two separate copies. Normal launches remain manual. Neither the game nor launcher changes firewall rules, router settings, security settings or PowerShell execution policy.

This proves only the scripted **127.0.0.1** flow when successfully executed. It does not prove another PC can connect, ten-player load, full-game co-op or visual/input acceptance.

## Preserve the current checkout

The previous local test of `b18b57b0144927363f05636723a0a360ad4cd39d` left seven tracked editor-generated changes and an empty DefaultNetworkPrefabs asset/meta. They are not part of this harness change. Before pulling:

1. Record HEAD, branch, remote, status, exact known generated diffs and their file hashes.
2. Fetch and verify the supplied cloud commit is a descendant of current main. Compare its changed paths with the locally modified/untracked paths.
3. Fast-forward only if there is no overlap and no unexpected user edit. Preserve the known local generated files byte-for-byte. Stop/report overlap or divergence; do not reset, stash, discard, overwrite or commit them as an automatic workaround.
4. After the fast-forward, verify those local file hashes/diffs are unchanged.

The seven generated paths are DefaultVolumeProfile, PC_RPAsset, UniversalRenderPipelineGlobalSettings, packages-lock, GraphicsSettings, ProjectSettings and UnityConnectSettings. See the execution record below. The harness commit deliberately avoids them.

## Compile, test and rebuild

Use the existing Unity 6000.6.5f1 project. Explicitly finish package resolution before assessing compiler errors; an idle package status alone did not establish that the initial new package references had resolved. Use the current editor's supported package-resolution operation, without changing package versions as a workaround.

Run all suites after pulling: **132 authored EditMode cases and 39 authored PlayMode cases**. These are expected source counts, not new pass results. The eight new cases check that only complete explicit smoke flags can activate automation, including rejected duplicate/unknown flags and path-like run IDs. Two further PlayMode regressions force the collider-only path on any editor graphics device, checking headless geometry and host-authoritative damage/reload/reset without renderers. The movement slice adds thirteen core cases and two more PlayMode cases; see [LAN_MOVEMENT.md](LAN_MOVEMENT.md). The infected encounter adds ten core cases and four more PlayMode cases; see [LAN_INFECTED.md](LAN_INFECTED.md). Revive adds thirteen core cases and one PlayMode case; a later reload correction adds one PlayMode regression for immediate fire/reload plus idempotent retry timing; see [LAN_REVIVE.md](LAN_REVIVE.md). The bounded horde adds fifteen core and four PlayMode cases; see [LAN_HORDE.md](LAN_HORDE.md). The later camera/control slice adds seven PlayMode cases; see [LAN_CONTROLS.md](LAN_CONTROLS.md).

Rebuild the Windows LAN player with the existing menu/build helper described in [LAN_RANGE.md](LAN_RANGE.md). Verify exact source HEAD and successful build result before launching. Do not run an old player from the preceding revision: it does not implement these flags.

## Launch from PowerShell

From the project directory, using the exact 40-character commit that was just built:

```powershell
.\Tools\run_lan_loopback_smoke.ps1 -Revision '<exact-40-character-cloud-commit>'
```

Optional parameters:

- `-Player 'G:\...\UndeadOvertimeLanRange.exe'` chooses the already-built test player.
- `-Port 7777` chooses a free non-privileged loopback UDP port. By default, the launcher briefly reserves an ephemeral loopback port, releases it, then lets Unity bind. A bind race is reported as failure, not silently worked around.

If execution policy, SmartScreen, an OS permission prompt or another security restriction blocks execution, report the exact blocker. Do not use policy-bypass flags, change security settings or switch routes to defeat it.

The launcher starts the host with normal Unity standalone `-batchmode -nographics` arguments. With a null graphics device, the range builds colliders only and disables camera rendering, HUD and cursor capture. No shaders or materials are looked up or constructed on this path; the authoritative target geometry, camera transform used for aiming, and networking remain active. It waits up to 30 seconds for the host's own ready log before launching the client. Each player has a 90-second monotonic test deadline; the launcher has a 120-second monotonic deadline from the host launch. On failure/timeout, it terminates only its retained child-process objects, never the editor or pre-existing player processes.

No game code launches another executable. The game accepts only role, port, run ID and revision test flags, always uses `127.0.0.1`, and writes results only into its own build directory under a validated hexadecimal run ID. Editor executions do not start this mode.

## What must pass

Host and client each assert their side of the flow:

1. Two members appear in the host-owned lobby and receive round one. The client walks forward with repeated bounded intents; the host observes movement and the client receives the new position. The client then sends no movement input and verifies the host expires its held intent, with no further displacement.
2. The client sends four valid shots. The host kills the shared center target and accounts for exactly four rounds; the client receives the same HP/ammunition result.
3. The client reloads. Both observe reload start/completion. A deliberate upward miss acknowledges that the client actually received the refilled magazine before the host changes phase.
4. The host returns to lobby. The client disconnects and rejoins; the host must observe the departure and a different connection identity on return.
5. Round two restores targets/ammunition. The assigned spawn position is restored. One client shot produces 75 HP on the left target, replicated back to that client. A reload request acknowledges receipt.
6. The host starts the optional infected encounter. The client receives pursuing enemy movement and damage to its own health, then fires four shots and receives the enemy kill/Cleared state. A reload request acknowledges that result.
7. In a third round, the client is downed. Its movement/fire probes must have no effect. The host kills the infected, approaches the teammate and holds revive. Both peers must observe timed progress and 40 HP restoration. The rescued client must move and damage a target, then observe reload started/completed and send a harmless upward shot as a final receipt. The host must see that receipt without extra target damage before the next round.
8. In a fourth round, both peers observe waves 1/2/3 and both five-second intermissions. The client kills all nine enemies, reloads as needed, and verifies no automatic ammo refill at wave boundaries. Both peers observe completion; another reload-completion/upward-miss receipt confirms that result.
9. The host shuts down cleanly. The client observes disconnection and an empty/stopped session.

Unexpected Unity Error/Exception/Assert log messages observed during the test fail the per-process result. Warnings are counted and retained for review; they are not automatically declared harmless. Timeout, extra peers, missing/mismatched reports, wrong process IDs, a nonzero process exit or missing terminal steps fail the launcher.

## Evidence and limits

Evidence is written under:

`Builds/LanRange/SmokeResults/<run-id>/`

- `host.log`, `client.log`: separate player logs.
- `host.json`, `client.json`: role, run ID, PID, port, timestamps, runtime version, checked steps and aligned UTC step timestamps, error/warning counts, result and failure reason. Existing result files are never overwritten.
- `summary.json`: only written after both peers pass; includes distinct launched PIDs, exit codes, requested source revision, executable SHA-256, and gameplay assembly hashes when present (managed UndeadOvertime assemblies or IL2CPP GameAssembly).

`requestedRevision` is an echoed test argument, not a cryptographic claim about which source built the executable. Exact-source evidence still comes from the runner's verified HEAD, build log and file hashes. The Unity executable alone may be identical across gameplay builds, hence gameplay DLL hashes are also recorded.

Return both reports, summary, build log and relevant warnings for cloud review. A successful script result is automated loopback acceptance only. Separate real-LAN, capacity, mouse ownership, rendering and actual game tests still apply.

## Previous local execution record and generated changes

Reported for exact revision `b18b57b0144927363f05636723a0a360ad4cd39d` on Unity 6000.6.5f1:

- An explicit supported package-resolution operation cleared the initial missing-Netcode-type errors; no source fix was needed.
- 73/73 EditMode and 18/18 PlayMode tests passed; Windows LAN test player build succeeded.
- Actual Transport resolution is **builtin 6.6.0**, despite the manifest minimum/pin 2.6.0. Its dependency declarations list Collections 6.5.0, Burst 1.8.24 and Mathematics 1.4.0. Those are dependency declarations, not evidence that the existing installed dependencies were downgraded. NGO remains registry 2.13.3.
- A current Console snapshot reported zero errors and 66 warnings. The retained lifetime warning buffer includes ConvGeneric/Sentis shader variants and two stripped debug shaders; available evidence cannot assign exact categories/counts to the current 66 warnings. They remain unclassified for this run.
- Editor-generated URP changes include a volume filter default, shadow/prefilter fields and runtime-setting references; GraphicsSettings gained state-collection/warmup/shader defaults. These remain local, uncommitted and not cloud-adopted.
- The ProjectSettings preloaded-asset GUID `052faaac586de48259a63d0c4782560b` resolves to the existing InputSystem_Actions asset and matches its existing EditorBuildSettings registration.
- UnityConnectSettings changed only `m_Enabled: 0` to `1`. Its cause/intent is not established; it is not silently adopted by this work.
- The generated DefaultNetworkPrefabs asset is an empty NGO NetworkPrefabsList. It and its metadata remain local and unchanged.

These findings establish the previous build/test baseline, not this new harness's execution result. No two-process or real-LAN pass has been claimed yet.

## 2026-11-08: first harness execution and headless startup fix

Exact tested revision `bf881d8d3f7d7c01f5dd4f17121cb0fb1a8867d2`: compilation succeeded, 81/81 EditMode and 18/18 PlayMode tests passed, and the Windows player rebuilt successfully. The first standalone script invocation **failed**, exit 1, waiting 30 seconds for the host-ready marker. `host.log` recorded `ArgumentNullException` in `Material(Shader)` through `LanRange.Material`, `Build` and `Start` with NullGfxDevice. It failed before the client was launched; there were no per-peer JSON reports or summary. No player process remained afterward. All nine preserved local generated-file hashes remained unchanged. Player logs also contained Unity telemetry connection failures; no telemetry, cloud or security setting was changed as part of diagnosis.

The follow-up source fix separates presentation from collision construction. `Build(camera)` automatically uses collider-only geometry on `GraphicsDeviceType.Null`; `Build(camera, false)` exercises the same path in editor tests. Five boxes, three sphere targets and ten disabled station capsules retain the normal primitive dimensions/transforms. Disabled rendering does not disable physics or host networking. A graphical build still reports a missing shader as an explicit error instead of silently hiding a broken rendered scene.

Rebuild the player after pulling this fix, then run the script again with the new exact revision. The two new tests are authored regressions, not a substitute for rerunning the actual null-graphics standalone processes. This entry records a failure and candidate fix, **not** a two-process pass.

For reference, the previously successful supported local package-resolution commands were:

```powershell
unity command package resolve --project-path 'G:\Unity Projects\Undead Overtime' --format json
unity command script recompile_status --project-path 'G:\Unity Projects\Undead Overtime' --format json
```

Resolve returned completed/applied with `requiresRecompile: true`. Recompile status was polled through compiling and a transient editor-unavailable domain reload until ready, with completed/failed:false and no errors. Do not change package versions or treat that transient reload status as a reason to launch a second editor.

## 2026-11-08: fixed-station baseline loopback passed

Local execution reported exact source `4be3f12c6c0841cacba167604ee06388187c34ea` compiled cleanly, passed 81/81 EditMode and 20/20 PlayMode, and rebuilt its Windows LAN player. Run `3c0205911a8643b8bb7f1797c9ed46db` passed with script exit 0, host PID 13708 and client PID 26200 on port 64575, from `2026-11-08T12:41:31.4208346Z` to `2026-11-08T12:41:37.9975748Z`. Both process exits were 0. Both reports had success:true, runtimeErrorCount:0 and warningCount:0; host observed connection identity change from 2 to 3. The reports covered membership, remote damage/reload, departure/rejoin, round reset, second-round acknowledgement and host shutdown.

No player process remained. Editor was ready/stopped; all nine preserved generated files remained byte-for-byte unchanged. Full logs were not entirely free of diagnostic failures: host.log had five and client.log four Curl error 7 messages to unreachable Unity cloud/telemetry endpoints. Those lines were not counted by the game's Unity log callback. No cloud/security setting was changed, and this pass does not establish telemetry connectivity.

This validates only the fixed-station two-process loopback baseline. The current protocol-v2 movement extension must pass a newly rebuilt run with the additional movement/expiry markers. Real LAN, ten-player capacity/performance and manual visual/control acceptance remain open.

## 2026-11-08: moving-client baseline passed

Exact revision `0f78c51a78ce8cd0d9bec6d77336d187fcd0d868`: compilation clean, 94/94 EditMode and 22/22 PlayMode passed via the Unity CLI result evidence; Windows player rebuilt. The runner did not find exported XML in the documented Logs directory, so do not claim an XML artifact exists for this run. The latest PlayMode result was available in the local pipeline status artifact.

Run `d64b1f1213ba4bb49a5b7d12691c6b80`, port 59445, UTC `2026-11-08T12:54:49.6313925Z` through `2026-11-08T12:54:57.9490606Z`, host PID 26384/client PID 13452: script and both processes exited 0. All 13 host and 10 client steps completed, including movement/expiry and the existing lifecycle/combat checks. Rejoin identity changed from 2 to 3. Both JSON reports counted zero Unity runtime errors/warnings; each full player log nevertheless contained five Curl error 7 messages to Unity cloud endpoints. No other Error/Exception/Assert/Warning matches were reported in that log review.

Gameplay build hashes recorded by the runner:
- Core.dll (24,576 bytes): `1ccfd8ddc37894c346ecd4fabe6095373932d8a386c8115b95414abb15611fee`
- LanPrototype.dll (40,960 bytes): `13914d12ec46e81335bdb75202647e97523ec35fda4e6c184fd6a6b07044be10`
- Prototype.dll (30,208 bytes): `97d6ca08a4ddc36792ff7688beba98a7e802d7403e3ddfbfa450869a50e254a4`
- Player EXE (667,136 bytes): `b70442f90f50cd91f7af7bf2272602645fc5d02c4d3156fb221fc27c328d2436`

No player remained; the editor was ready/stopped and nine generated-file hashes stayed identical. This passes moving-client loopback only. The current infected extension requires a fresh build and its additional encounter markers; it is not covered by this baseline result.

## 2026-11-08: one-infected baseline passed

Exact `5aaf489bf34e65a8dacaed31c582a822c078f24a`: clean compilation, 104/104 EditMode and 26/26 PlayMode (56.19 seconds), Windows build succeeded. The runner reported CLI/pipeline result evidence; no exported XML was found in Logs.

Run `5d59a9d0f73445c4a00fd285009f138a`, reported UTC 13:10:26 to 13:10:45 on 2026-11-08 (18.948-second script duration), host PID 19768/client PID 28116: script and both players exited 0. The new host `infected-result-acknowledged` and client `replicated-infected-kill` markers and all prior steps completed. Both JSON reports had zero runtime errors/warnings. Full logs still contained five native Curl error 7 cloud-endpoint messages each, with no other matching Error/Exception/Assert/Warning lines reported. Host log was 30,696 bytes; client log 24,778 bytes.

All nine preserved generated-file hashes stayed identical. No player remained and the editor was ready/stopped. This establishes the one-infected loopback baseline only; the current revive scenario requires new exact-revision execution. Real-LAN and capacity/visual acceptance remain separate.

## Revive candidate execution failure and correction

Revision `cae6b4db487acdf44c41a8ceaff1ec1b945fbfe3`: local compilation, 117/117 EditMode, 27/27 PlayMode (56.65 seconds), and v4 Windows player build passed. Actual two-process run `24dfbc2192f94273816295f235332487`, port 51772, host PID 9640/client PID 17640, **failed**: launcher exit 1 after 93.871 seconds, no summary.json. Host started at `2026-11-08T13:27:32.2766183Z` and failed at `13:29:02.2825789Z` with its 90-second deadline. It reached teammate-revived but not revived-client-action-acknowledged. Client observed revive and restored movement/fire, then interpreted the host's failure cleanup as shutdown; its isolated success result does not make this test pass.

Source diagnosis found a concrete path matching this evidence: a reload immediately following fire was swallowed by their shared 20 ms adapter pacing window. The former final receipt sent reload immediately after receiving the shot result, without checking reload acknowledgment. The fix gives reload its own pacing clock, retains host weapon validation, adds an immediate fire/reload and repeated-request regression, and requires both sides to observe the reload cycle plus a final deliberate-miss receipt. New step UTC timestamps improve future evidence. Deadlines, networking scope and cleanup policy stay unchanged.

Both failed-run JSON reports counted zero callback runtime errors/warnings; full logs each had six native Curl error 7 Unity cloud-endpoint messages and no other matching Error/Exception/Assert/Warning lines. All nine preserved local generated hashes were unchanged, no player remained, and the editor was ready/stopped. The corrected revision needs a fresh build and one new test run; no blind retry of the unchanged failure is permitted.

## Corrected revive acceptance (baseline for waves)

Revision `90c7b28cc7b6a3c0e951732d3111be367f413199` passed local compilation, 117/117 EditMode, 28/28 PlayMode (58.38 seconds) and Windows build. Run `ed89efd8cae64d9d83c5dc6096bdcf33`, port 62718, host PID 23980/client PID 21384, UTC `2026-11-08T13:39:13.1229076Z` through `13:39:56.4794699Z`, passed in 43.745 seconds. Script and both players exited 0. All 24 host and 18 client steps completed.

Host revive was recorded at 13:39:53.0447645Z; restored shot 53.5931829, reload start 53.5941837, reload completion 55.0941181, final receipt 55.0956221 and shutdown 55.1142579. Client observed reload completion at 55.0951181 and shutdown at 55.1061841. This establishes actual successful acknowledgment, not the failed predecessor's timeout cleanup.

Both reports had zero callback runtime errors/warnings. Full logs (host 42,039 bytes/client 32,983 bytes) each retained six native Curl error 7 cloud-endpoint lines, with no other matching diagnostic lines reported. All nine generated-file hashes were unchanged; no player remained and the editor was stopped/ready. This baseline does not validate the new v5 wave extension or real LAN.

Verified baseline build SHA-256 fingerprints from the local summary:
- UndeadOvertime.Core.dll, 30,208 bytes: `0ad3eaa21291ef80da80e7cbaa7624f8369c0db5d51446dc94753334cb26bbfe`
- UndeadOvertime.LanPrototype.dll, 52,224 bytes: `9721f1b08054a7dc1573acea899c2ce3d125be8cdec42c85d187d5165c790230`
- UndeadOvertime.Prototype.dll, 30,208 bytes: `28fa5a86be70b588164ba5b367cb03e0ed7437f0caeed35d032e34c8b378316e`
- Player EXE, 667,136 bytes: `b70442f90f50cd91f7af7bf2272602645fc5d02c4d3156fb221fc27c328d2436`

## Three-wave acceptance (baseline for camera controls)

Revision `6b3df43657689622e70189704072893b0e899ada` passed local compilation, 132/132 EditMode, 32/32 PlayMode (79.73 seconds) and Windows build. The synchronous helper logged UO_LAN_BUILD_OK at `2026-11-08T13:59:01.520815Z`. An unchanged Unity EXE timestamp alone was not a stale-build failure: fresh gameplay DLLs and the successful BuildPipeline result established the rebuild.

One harness run `aa91c2db915b4bbc91df984c7ae644dd` passed in 66.571 seconds, script exit 0. Summary UTC `2026-11-08T14:04:19.8961976Z` through `14:05:26.1235081Z`, port 64843, host PID 28460/client PID 23992; both players exited 0. All prior and new wave acknowledgment markers passed. Host clear was 14:05:22.9558485Z, reload completion 24.6774973Z, final receipt 24.6785021Z and shutdown 24.6961383Z. Client clear was 23.1760653Z, reload completion 24.6774973Z and shutdown observed 24.6886002Z.

Both callback reports had zero errors/warnings. Full logs each retained six Unity cloud/analytics Curl errors, with no other matching Error/Exception/Assert/Warning lines reported. All nine local generated hashes remained unchanged, no test player process remained, and the editor was stopped/ready. This is loopback acceptance, not real LAN or camera/manual acceptance.

Summary SHA-256 fingerprints:
- UndeadOvertime.Core.dll, 34,304 bytes: `29ab9a518c0967f614ae6b9f900960b2121caa4678d3a412f9b5796cfe37e0d6`
- UndeadOvertime.LanPrototype.dll, 57,856 bytes: `90bf340187f5a3bd4c3d11765cdfd4223b614f5123a7a8f3d4218548106f28cc`
- UndeadOvertime.Prototype.dll, 30,208 bytes: `b468aff0356482fc9ff6f6ce5c7f7207c61296b9c2805e91d59e36bb39007aaa`
- Player EXE, 667,136 bytes: `b70442f90f50cd91f7af7bf2272602645fc5d02c4d3156fb221fc27c328d2436`
