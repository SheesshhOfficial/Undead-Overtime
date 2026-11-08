# Mission replay and reconnect verification candidate

## Scope

Base `917d94aea47d25911b730fac6f2a5cd599783d14` passed 305 EditMode, 65 PlayMode, Windows build and both default-range and mission two-process loopbacks. The mission was completed once and then shut down. This next bounded slice validates practical replay with a friend: returning to the lobby, leaving/rejoining, resetting the mission and operating the power control again.

The normal game already exposes those operations. This candidate strengthens verification, **not normal gameplay**. Production authority, movement, mission timing, transport format, materials, assets, settings and packages are unchanged. Smoke logic runs only with explicit standalone test flags, never a normal player session or editor launch.

## Extended mission smoke

The existing full mission flow and completion receipt remain required. After the host receives the client's completed-frame receipt:

1. Host returns to the lobby; both peers require their mission frame and gate state to be cleared while the same session and closed round remain bound. A read-only movement sweep across x=5 at the former partition verifies that the formerly solid wall section no longer blocks physics. The center gate was already open, so it is not used as cleanup evidence.
2. Client stops, checks stopped/null state, waits the existing bounded reconnect pause, and rejoins the same loopback host. The host must observe departure before admitting the new connection. Both verify that the client's connection lifetime changed.
3. Host starts round 2. Both require the same session, next round, exactly two members, RestorePower with zero progress, gate closed, no enemies and disabled horde. Each member must have 100 HP, 12 ammo, no reload/revive progress and its reset spawn. A read-only center-gate movement sweep verifies physical blocking in the new round.
4. The remote client walks to the console and supplies fresh held-F input. Both observe DefendDepot and the opened gate. This demonstrates that the reused client/manager/session replica can act on the new mission rather than merely display a reset frame.
5. Client makes a deliberate miss, observes 11 ammo, reloads, observes Reloading and then 12 ammo, and sends another deliberate miss as receipt. The host observes those state changes in order before shutdown. This confirms post-reconnect action and reload replication without claiming another complete mission clear.
6. Host stops; both require mission state cleared and Stopped. The launcher verifies both owned process exits and report/build identities.

The same **90-second peer /120-second launcher** deadlines, localhost address restriction and owned-process cleanup remain. The accepted mission run took approximately 33 seconds; the added sequence is expected to fit, but actual timing is unverified until execution. No deadline is widened preemptively.

New required host markers: mission-lobby-reset, mission-client-disconnected, mission-fresh-client-rejoin, mission-second-round-reset, mission-second-round-power, mission-second-round-client-acknowledged. New required client markers: replicated-mission-lobby-reset, mission-client-disconnected, replicated-mission-second-round-reset, replicated-mission-second-round-power, mission-second-round-reload-completed. Together with the original markers, expect **17 host /15 client** steps. The launcher additionally requires positive, unequal first/second remote connection IDs in the host report.

## Added automated cases

- Two parameter-expanded pure-core cases require closing lobby before Completed or Failed may transition into a new locked round, rejecting old terminal packets after retirement.
- One pure-core case cycles 100 completed rounds, rejecting stale lobbies and retired terminal frames even with maximum sequence values.
- One new Unity PlayMode case powers a mission, spends ammo, returns to lobby and starts round 2. It checks reset identity, physical state flags, waves, health, ammo and pose, then verifies fresh power progress starts from zero. It uses ordinary public gameplay operations, no teleport or runtime test hook.

The first core fixture incorrectly paired failed single-encounter state with zero enemies, which the existing codec correctly rejected. That new fixture was corrected to a coherent failure-before-power state with dormant encounter and no enemies. No production validator or assertion was weakened. Initial run: 283 pass /one fixture error. Final run: **284/284 pass**, no build errors/warnings, failures, skips or inconclusive results. Exact source, lock and result hashes are in `MISSION_LIFECYCLE_CORE_RESULT.json`.

Cloud static checks pass 82 source/assets, nine assemblies, 97 GUIDs, 48 Git attribute assertions and 15 malformed-importer fixtures. C# 9 syntax parsing passes 59 files; it does not bind Unity/NGO APIs. The two-process coroutine and new native case remain unrun. Independent source review checked lifecycle receipts and prompted the explicit old-partition physics probe.

## Guarded native acceptance

Use only the existing local execution coordinator. Pin the exact published correcting revision and confirm incoming paths do not overlap the protected dirty files. Preserve their current hashes and the known automatic InputSystem preload serialization; no floor setup/reimport or material save is requested.

1. Native compilation, full **308 EditMode** and **66 PlayMode** cases once. Retain each complete response before another suite overwrites status.
2. Only on success, one explicit Windows player build using the existing helper; verify fresh managed DLLs/success marker.
3. One `Tools/run_lan_loopback_smoke.ps1 -Revision <exact SHA> -Mode range`.
4. Only if range passes, one invocation with the same SHA and `-Mode mission` for the expanded sequence.
5. Stop on failure, preserve logs and current state, return diagnostics to cloud. No local repair or blind rerun. Process cleanup should use owned launcher handles, not retry a denied broad enumeration.

This candidate does not establish mission-specific downing/revive end-to-end, rejoin while a mission is active (which remains refused), deliberate incompatible-client connection rejection, ten machines, real LAN, manually tested input, final art, mission screenshots or performance. The accepted baseline and the new unrun checks must remain distinct.
