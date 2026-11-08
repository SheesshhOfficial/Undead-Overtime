# Playable depot mission integration candidate

## Acceptance status

This candidate wires the previously tested mission rules, owner, codec and geometry into an **opt-in mode in LanRange**. The default range mode remains separate. The new integration has cloud core execution and source/syntax checks only until its exact revision passes native acceptance. It is a small greybox depot mission, not the planned complete city or finished game.

Base `802ce58b7aa30639291c9e21834e52e84fd5fdc7` passed native compilation, 288 EditMode, 56 PlayMode, a Windows build and the existing two-process v5 loopback. The original floor also passed native import and inspected FPS/TPS rendering at `4b7e1ab`. See `NATIVE_ACCEPTANCE_802CE58.md` for evidence and limitations. Those results do not cover this integration's new transport, controls or pursuit route.

Current cloud core result: **281/281 cases passed**, zero failures, warnings, skipped or inconclusive cases; actual production core compiled separately as .NET Standard 2.1/C# 9. This adds 12 mission session-boundary cases and five smoke-mode parsing cases to the prior 264. The new nine native mission cases are authored, not yet run. Expected full inventory: **305 EditMode and 65 PlayMode**. C# syntax-only parsing is not Unity/NGO API compilation.

No material, texture, mesh, import metadata, scene, package, project setting, branch or user-local file is modified in this candidate. Runtime geometry uses existing caller-owned materials. No new third-party asset or external service is involved.

## Starting the mission

1. Open the existing `Assets/UndeadOvertime/Scenes/LanRange.unity` and enter Play Mode, or use the explicitly rebuilt player.
2. Before connecting, select **Depot mission** on both peers. Host/Join still use an explicit private/loopback address. Changing mode is rejected while connected or stopping.
3. Join everyone before the host chooses **Start round**. The round starts with the central gate physically closed and the infected dormant.
4. Capture controls, approach the orange console on the south-west side and **hold F while stationary for three seconds**. The eye-level point is (-9, 1.6, -5), with a two-meter host-checked range and physics line of sight. The console body itself blocks walking, not the interaction ray above it.
5. The gate opens and the host starts three bounded waves, with 2, 3 and 4 infected. Shoot, reload and revive teammates using the existing controls. The open gate allows backtracking.
6. After all waves, gather every connected living survivor on the blue northern extraction marker for two seconds. A downed survivor must be revived; a disconnected survivor no longer blocks the team.
7. Completion/failure blocks further movement/combat/interaction. The host can return to lobby and start a clean new round.

WASD walks; mouse aims/shoots; R reloads; E revives; F operates power; V switches FPS/TPS; Esc releases controls. Neutral move/revive packets do not interrupt F. Positive movement/revive, fire/reload, injury, expired/released power intent and focus release interrupt power. Multiple helpers never accelerate the shared hold.

Training target spheres are inactive in mission mode, including their colliders, so they cannot obstruct the extraction approach or absorb shots. They return in ordinary range mode. Characters/infected, console, partition and marker remain prototypes. There is no story dialogue, final character animation, audio, inventory, save system, city streaming or performance acceptance in this slice.

## Transport and authority

- Default range remains NGO protocol 5 / `UOR5` handshake.
- Mission uses NGO protocol **101** / `UOM1` handshake. A fresh runtime `NetworkConfig` is assigned before every connection start, including reuse of an idle manager. Peers admitted with the other mode are rejected.
- The existing range command messages still carry movement/fire/reload/revive inside a mission session. Mission power is a separate fixed **32-byte** held/released command. Both streams resolve sender IDs to current host-held tickets; neither accepts client position, elapsed time, damage, phase or gate state.
- Power and combat deliberately have separate monotonic sequence counters and independent pacing. Power's sequence/replay rules remain inside the coordinator. They reset only with round/session lifecycle.
- One reliable-sequenced mission state channel carries lobby range snapshots and coherent mission frames (maximum **465 bytes**). Range mode keeps its existing 442-byte maximum. The shared payload reader remains bounded at 465, and each handler supplies its own narrower bound.
- Only the authenticated server sender can feed client mission state; decoded state must contain the local peer. `CoopMissionSessionReplica` must receive a lobby before the next round can open. Session IDs cannot change mid-connection, only lobby round N to mission round N+1 is allowed, delayed initial lobby packets cannot close an active round, and the closing lobby retires all frames from the closed round. The host sends an explicit lobby binding before beginning a round.
- Playing frames use the tested monotonic `CoopMissionReplica`; duplicates, stale or regressing states cannot mutate presentation. A legacy range Playing snapshot cannot substitute for an atomic mission frame.

This direct-IP prototype has no Internet authentication or production anti-cheat. Keep it on the intended trusted LAN/loopback; no firewall, security or network settings are changed by the implementation or tests.

## Coherent host tick and cleanup

Host physics tick order is collision-resolved movement, fresh owner poses, infected pursuit/melee, revive validation, the mission coordinator, and weapon/movement life synchronization. The coordinator is the sole owner of wave advancement and placement; the old range wave-advance path is skipped in mission mode. The power transition applies the opened gate before the bounded collision-safe placement search. New waves cannot act until their placement has succeeded.

Mission captures are published after that coherent tick, at approximately 10 Hz, with an initial fresh-pose frame at round creation. Update/fire callbacks do not publish a torn final-kill frame. Reload advancement remains in the host update loop; captures sample its current state. Applying a captured frame does not replay stale enemy transforms between host physics steps.

The separate `DepotMissionPresentation` is created before the first round movement tick. Return to lobby, stop, disconnect and failed initialization dispose its owned geometry immediately from physics, then Unity destroys it normally. Shared materials survive. Manual **Start infected/Start waves** actions are disabled in mission mode. Terminal host state clears pending movement and rejects new actions, not merely UI buttons.

Infected use an explicit center-gate waypoint when their target is across the partition, then the existing physics sweep. This is a narrow route for the flat depot only, **not NavMesh navigation, general obstacle search or a claim that city traversal is solved**. Native tests must confirm both directions, as a straight-line pursuer would otherwise stall against the partition.

## New native coverage and acceptance sequence

Nine authored PlayMode cases cover opt-in/no-network construction, prefixed maximum mission payload and narrower legacy bounds, range/mission/range manager reuse, locked gate and manual-mode rejection, round/lobby cleanup, real stationary power plus release/fire interruption, blocked wave placement failing closed, physical gate pursuit in both directions, and a full solo power/waves/extraction loop with terminal action rejection.

Use the existing local execution coordinator and preserve the recorded dirty checkout. Verify exact revision and non-overlap, then:

1. Native compile; full EditMode **305**; save the actual result before running another suite.
2. Full PlayMode **65**; stop and return diagnostics on failure. Do not weaken assertions locally.
3. One explicit Windows build using `Undead Overtime/Build LAN test player (Windows)`. The material validation is read-only. Do not rerun floor setup or overwrite existing material serialization.
4. Run the existing launcher once with `-Mode range -Revision <exact 40-character commit>` to regress movement/combat/rejoin/revive/waves.
5. If range passes, run it once with `-Mode mission -Revision <same commit>`. The same player now selects mission mode through a strictly validated optional smoke flag.
6. Capture exact reports, per-step timestamps, managed DLL fingerprints, full log diagnostics and cleanup. On failure stop the affected run and return the evidence for a cloud fix; do not repeat it blindly.

The mission smoke launches exactly two loopback peers. The remote client alone walks to the console and supplies held-F intent; both peers verify gate replication, separately confirm an authorized shot (12 to 11 ammo) and a started/completed reload back to 12 through their own authoritative/replicated state, fight three physical waves, then walk through the gate to extraction. The client sends a bounded **test-only, non-gameplay completion receipt** after it observes completion; the host waits for that receipt before shutdown. This avoids mistaking host-only completion or a fixed delay for client confirmation. The receipt handler is installed only by explicit standalone smoke execution, never a normal game session, and cannot change mission authority.

This mission smoke does not cover mission rejoin/second round, deliberate incompatible-client connection attempts, downing during the mission-specific full flow, or manual controls. The native lifecycle cases and prior range smoke cover parts of those behaviors, while the core mission fixtures cover downing/revive/extraction logic. They are not substitutes for future multiplayer mission scenarios, real LAN/ten-player tests, visual review and performance profiling.
