# Pure-core mission replication integration tests

## Scope and outcome

This change adds 11 **executable core integration cases**, without adding another active gameplay feature or changing production code. The cases combine the actual `LanSession`, `HostCoopMission`, owned `HostInfectedEncounter`, `HostFireGate`, mission/range codecs and `CoopMissionReplica`. Every simulated host tick is encoded and decoded, then delivered to independent view-state replicas.

The complete standalone core suite now passes **219/219**, with **0 failures, 0 skips, 0 inconclusive results, 0 warnings and 0 compiler errors**. Core and tests compile separately using the existing .NET Standard 2.1 / C# 9 / .NET 10 harness. Exact run counts, times, fixture results and source hashes are in `COOP_MISSION_REPLICATION_RESULT.json`. The earlier 208-case report remains the historical result for its own source baseline.

This is **not a network session**: the replicas are in-memory objects in one process. No Unity scene, transport socket, physical LAN, collision world, rendered image or ten-computer performance result is implied. Actual Unity testing remains pending for the newer mission layers; the local material approval and its pinned acceptance revision are not changed.

## Cases run

1. Full mission for **one**, **two** and **ten** members, as three separate cases. Serializes power holds, the actual three waves, 36 authorized 25-damage shots, two required magazine reloads, intermissions and separate whole-team extraction. Ten-member/four-enemy frames reach the documented 465-byte bound.
2. Actual encounter downing and three-second revival. A downed teammate blocks extraction; serialized revive target/progress remains consistent; the teammate returns with 40 HP before the final extraction hold. The fire gate rejects a shot in the exact downing tick, rejects replay of that consumed sequence after revival, and admits a fresh shot in the exact revive tick.
3. Missing physics callback before power and failed placement after power, as two separate cases. Both produce valid terminal failure frames; the gate retains its appropriate locked/unlocked state.
4. Actual team wipe produces replicated failure with both members downed, without invented completion or revival.
5. Serialized power release cancels progress. Replaying an older held command cannot restart the countdown; an unknown host-mapped ticket is rejected.
6. A disconnected teammate no longer blocks extraction, while connected survivors retain their original slots. A mid-round join is refused and the stale ticket cannot submit power intent.
7. The temporary state immediately after the final kill but before the mission coordinator runs is rejected as an incoherent frame. A post-coordinator frame is accepted, validating the required publish-after-host-tick order.
8. Return to lobby, replacement connection, next round and host session restart reject old round/session commands and frames. This verifies invalidation, not a real reconnect protocol.

All ordinary simulation ticks encode the actual current aggregate and compare decoded member identity, health, ammo, reload, revive and objective/wave state. Each view skips every fifth applicable frame on a deterministic staggered schedule. Delivered frames are followed by a duplicate and an older frame, both of which must be rejected without replacing accepted state. Final flushes ensure all still-connected views see the terminal outcome. This is deterministic dropped/reordered **delivery simulation**, not a bandwidth, reliability or adversarial-authentication claim.

## Explicit test doubles

- Host transport identity is supplied directly as a current host-held `PlayerTicket`; these tests do not implement or authenticate a transport.
- Host poses are fixture-controlled valid eye positions. Moving everyone to the extraction area is a fixture update, not tested travel through the depot gate.
- LOS is a deterministic callback. Wave placement reports success/failure without a collision engine.
- Shots pass through the real ammo, rate and reload gate. A trusted fixture chooses the enemy hit after authorization; no raycast, aim selection or client hit claim is being validated.
- Enemies do not pursue autonomously in this fixture; explicit real melee calls create the down/revive/wipe scenarios.

Keep these boundaries when quoting the result. The tests are valuable because they exercise production components together across their real serialized representations; they do not replace the pending Unity geometry/physics tests or the future actual two-process mission harness.

## Reproduce and next gate

Run the existing `Tools/CoreTests/run_core_tests.py` with the approved pinned SDK. It runs this fixture along with all existing core tests, uses locked package restores, and records before/after source hashes and NUnit XML.

Before exposing mission controls, validate the actual Unity adapter's coherent tick and snapshot assembly, transport host/sender and capability checks, gate/backtracking collision, valid wave placement, cleanup and objective HUD. Preserve the existing range v5 behavior and reject incompatible mission peers explicitly. Continue using the independently pinned local material validation process; these new tests do not authorize saving either material.
