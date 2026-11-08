# Direct-IP LAN firing-range candidate

## Scope and acceptance status

This isolated `Assets/UndeadOvertime/Scenes/LanRange.unity` scene is the first transport integration, not the multiplayer zombie game. It provides host/join by explicit IPv4/UDP port, a host-owned lobby with a ten-member cap including host, host-authoritative planar walking from assigned spawn slots, mouse aiming, host-validated shots/reloads and three shared targets, plus a host-started one-infected encounter and hold-E teammate revive. The host starts rounds and returns everyone to the lobby. Existing PrototypeRange and SurvivalYard remain separate and unchanged.

Protocol v2 adds walking after the fixed-station loopback gate passed. The host receives only bounded horizontal intent, resolves movement against arena collision, and replicates positions. There is no client prediction, interpolation, jump, slope handling, latency compensation or production enemy navigation yet. The new optional encounter is described in [LAN_INFECTED.md](LAN_INFECTED.md). Read [LAN_MOVEMENT.md](LAN_MOVEMENT.md) for the movement contract and test gate. There are no Netcode NetworkObjects/NetworkTransforms or runtime network prefabs in this slice. NGO custom messages carry bounded original protocol data.

Current revive candidate: source authored and statically checked, with new runtime acceptance pending. The fixed-station baseline `4be3f12c6c0841cacba167604ee06388187c34ea` passed compilation, 81 EditMode / 20 PlayMode, Windows build and an automated two-process loopback run. The later moving-client baseline `0f78c51a78ce8cd0d9bec6d77336d187fcd0d868` also passed 94 EditMode / 22 PlayMode, build and expanded loopback. The later infected baseline `5aaf489bf34e65a8dacaed31c582a822c078f24a` passed 104 EditMode / 26 PlayMode, build and expanded infected loopback. These results do not validate the new revive extension or real LAN. Prior revision `0396ea46014893235bc295c892dd0ed56b283a13` was reported by the local execution worker to compile cleanly, pass 50 EditMode plus 13 PlayMode tests and render a basic SurvivalYard smoke view. That result does not validate this new revision or full solo play acceptance.

## Packages and rationale

Pinned additions only:

- `com.unity.netcode.gameobjects` **2.13.3**, official package requires Unity 6000.0 or newer, Unity Transport 2.6.0 and Mono Cecil 1.11.4.
- `com.unity.transport` **2.6.0**, official package requires Unity 2022.3 or newer, Burst 1.8.24, Collections 2.2.1 and Mathematics 1.3.2.
- Existing lock resolves newer dependency versions (Burst 2.0.0, Collections 6.6.0, Mathematics 1.4.0 and Mono Cecil 1.11.6); none were deliberately upgraded or downgraded. The two new entries use the official registry dependency declarations. Actual Unity package resolution must be checked locally, including any generated lock differences.
- Chose stable NGO 2.x rather than newly released 3.x, whose additional Entities dependencies and newer editor requirements are unnecessary for this slice. No Relay/Lobby/cloud-account/paid service dependencies.

Sources checked 2026-11-08:

- [Official NGO package metadata at the pinned tag](https://github.com/Unity-Technologies/com.unity.netcode.gameobjects/blob/v2.13.3/com.unity.netcode.gameobjects/package.json)
- [Official NGO registry](https://packages.unity.com/com.unity.netcode.gameobjects)
- [Official Transport registry](https://packages.unity.com/com.unity.transport)
- [NGO source: connection lifecycle](https://github.com/Unity-Technologies/com.unity.netcode.gameobjects/blob/v2.13.3/com.unity.netcode.gameobjects/Runtime/Core/NetworkManager.cs)
- [NGO source: named-message buffer behavior](https://github.com/Unity-Technologies/com.unity.netcode.gameobjects/blob/v2.13.3/com.unity.netcode.gameobjects/Runtime/Messaging/Messages/NamedMessage.cs)

## Local execution procedure

Continue inside the existing Undead Overtime project chat, using the requested local execution configuration. Record remote, branch, HEAD, git status and Unity version. Fetch and fast-forward only to the exact published commit. Do not discard/stash user work, create a branch, rewrite the project, or make local source fixes. Return failures and any generated package-lock changes to cloud authoring.

1. Open the existing Unity **6000.6.5f1** project. Wait for package resolution and compilation. Record installed NGO/UTP versions, Console errors and resulting lock diff. Do not change editor/URP or upgrade unrelated packages.
2. Run all EditMode suites: **117 authored parameter-expanded cases** (107 core, 9 original scene/rules, 1 LAN scene). Run all PlayMode suites: **27 authored cases** (13 original + 14 LAN). Record XML, logs and actual counts. The LAN tests use a loopback host socket and an ephemeral port; they do not create a remote client and are not end-to-end proof.
3. Recheck PrototypeRange and SurvivalYard after adding packages. New networking code starts no socket merely by opening a scene or building geometry.
4. Open **LanRange** only. Verify one assigned scene camera/audio listener and no missing scripts. Click Host with `127.0.0.1` and port `7777`, then Start round. Capture mouse, WASD walk, aim/fire, R reload, Esc release. Verify wall collision, movement after direction changes and no continued walking after release. The first capture click must not fire. Four accepted shots kill a 100-HP target. Ammo and reload should update even without further shots. Return to lobby, then start again to reset targets and ammunition.
5. Build the separate Windows test player through **Undead Overtime > Build LAN test player (Windows)**. The explicit helper writes `Builds/LanRange/UndeadOvertimeLanRange.exe` and supplies only LanRange to BuildPipeline; it does not rewrite EditorBuildSettings or launch/listen automatically. Windows Build Support must already be available; report a missing module rather than installing silently.

Batch equivalent, only when the project is not locked by another editor process:

`Unity.exe -batchmode -quit -projectPath "G:\Unity Projects\Undead Overtime" -executeMethod UndeadOvertime.LanPrototype.Editor.LanRangeBuild.BuildWindows -logFile "<local log path>"`

Use the verified installed editor path, not a guessed Unity.exe location. Build output is generated local content, not source to upload.

## Two-process loopback acceptance

Use editor plus one built player, or two built player processes. Give both the same exact commit/build.

- Process A Host: `127.0.0.1:7777`; Process B Join: `127.0.0.1:7777` while A remains in lobby. Both must show two members and separate stations. Both views must have only their own camera/input. Remote body capsules stay visible; local body is hidden.
- Host starts round. Client aims and fires: **host and client** must show the same damaged target and the client's reduced ammo. Walk first and shoot from the new position; both peers must see the same location. Host fire must replicate in the other direction. Reload must complete without another fire command.
- Test a late third join during an active round: reject with the lobby-only reason. Return to lobby and join successfully.
- Client disconnect/rejoin gets a fresh connection identity and one station, no duplicate. Host return/start resets target state and round identity everywhere. Old round requests cannot damage the new round.
- Host shutdown disconnects the client; client can cancel a pending join or retry after timeout. Repeated host/start/stop uses the same owned manager and leaves no old members or ghost targets.
- A busy/invalid bind must fail clearly. Do not change firewall/router/VPN/security settings to make it pass.
- Capture screenshots/logs of both processes and record number of actual processes. A host-only PlayMode test or a single screenshot is not this test.

## Real LAN and ten-member gates

Loopback is not LAN. On two authorized PCs on the same trusted LAN, verify the host's current private IPv4 and enter that exact address on both host and client. Host binds only the explicit address; it does not bind `0.0.0.0`. UI accepts canonical loopback, RFC1918 and link-local IPv4, no DNS/public IP/wildcard binding. No firewall exception, port forwarding or security configuration is created by code. Report an OS permission prompt for approval.

Repeat the two-process lifecycle/damage checks across the two PCs. Then separately scale to **ten real connected processes including host** and reject an eleventh. Source capacity checks alone do not prove ten-player performance. Record transport errors, frame timing and disconnect behavior. Do not expose this unauthenticated, unencrypted prototype to the Internet or untrusted network.

## Authority and protocol boundaries

- NGO callback sender ID is mapped to the current **host-held** PlayerTicket. The wire never grants a ticket or lets a client choose its identity, position, target, damage, hit result or clock.
- Connection approval validates an exact protocol handshake and reserves one capacity slot in LanSession. Reservations have a ten-second expiry; host cannot begin until all admitted peers finish connecting. Late join is disabled during a round.
- Commands contain only action (fire/reload/move/revive), session ID, round ID, increasing sequence and a finite vector: near-unit aim for firing/reload or horizontal movement intent of length at most one (including zero to stop), or the exact revive hold/release encoding. They are exactly **44 bytes**. Host derives muzzle origin from its collision-resolved position, checks round/sequence, independently paces movement and weapon requests, and uses HostFireGate's ammo/reload/cooldown. Aim remains user input; no anti-cheat/security claim is made.
- Host performs actual physics raycasts, respects occluding geometry and changes target HP. An accepted miss still consumes a round. Player body colliders remain disabled: teammates do not obstruct walking or shots, and friendly fire is absent.
- Snapshots originate only from the server, contain 1–10 unique peers/slots and exactly three bounded HP values, with a maximum **391-byte** payload, including each member's authoritative position/health and one bounded infected state. They are sent reliably at up to ten periodic updates per second plus explicit transitions. Members and target data are copied into immutable view records.
- NGO can pass named-message readers with an already-consumed header. The adapter validates **Length minus Position**, then reads only that unread payload; a prefix regression test covers this.
- Host-only start/return functions are not remote commands. Reload completion has an explicit trusted host tick; it no longer depends on another shot request.
- Escape/focus loss releases only the local mouse. Network simulation and peers continue. No global timescale changes.
- On scene teardown, unregister callbacks/handlers, stop owned transport, destroy only the owned NetworkManager and generated materials, and restore the previous background-running setting.

## Remaining production work

Networked movement/collision/prediction, FPS/TPS ownership, enemies/waves, health/revival, mission progression, asset/animation import, audio/effects, reconnect policy, rate/abuse hardening, bandwidth profiling, ten-process load and robust whole-game UX remain separate work. This small firing range is an integration gate, not a finished co-op build.

## Automated two-process route

A later opt-in [loopback automation harness](LAN_LOOPBACK_AUTOMATION.md) can run the two built peers without native window clicking. It does not change the normal manual startup path. Rebuild at its exact revision and follow its evidence/dirty-checkout preservation steps; do not reuse the prior executable or infer LAN success from its existence.

Protocol version 4 rejects earlier handshakes, including infected version 3. Both peers must be rebuilt from the same current commit; do not mix old and new players.

Optional encounter: after Start round, the host can Start infected. Read the encounter runbook for pursuit, melee, downing, kill and reset checks. These are prototype systems, not complete co-op game acceptance.

Co-op revive: hold E while alive, stationary and within 2 m of a downed teammate with clear line of sight. Host validates a continuous 3-second channel and restores 40 HP. Read [LAN_REVIVE.md](LAN_REVIVE.md) for interruptions, protocol and acceptance.
