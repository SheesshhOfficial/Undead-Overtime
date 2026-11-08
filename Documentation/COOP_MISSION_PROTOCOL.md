# Bounded mission protocol candidate

## Status and compatibility boundary

This adds an inactive transport-independent codec and view-only receiver guard. It does not register an NGO handler, change the active range v5 handshake, alter controls or scenes, or send any traffic. The host mission coordinator is still not connected to the current range. The two local material files and pending material validation operation are untouched.

All 36 codec cases now pass as executed NUnit cases in the standalone core harness; see [cloud evidence](CLOUD_CORE_VERIFICATION.md) and [combined mission replication checks](COOP_MISSION_REPLICATION_TESTS.md). This is not Unity compilation or network acceptance. No runtime mission, physical LAN, ten-machine throughput, latency or rendering result is claimed.

The candidate uses distinct mission magic `0x554F4D31`, little-endian version 1 and handshake bytes `55 4F 4D 01`. Current range packets use different magic and handshake, and are deliberately rejected as top-level mission packets. A mission-capable transport session must explicitly negotiate the new mission handshake before accepting a peer. **Do not silently enable mission mode for a peer admitted only by the old range handshake.** The future adapter must adopt the new handshake/session version as one coordinated change; this candidate alone does not make a mixed-version session safe.

The atomic frame embeds the existing validated range v5 combat snapshot as its bounded payload. This reuses membership, health, ammo, revive, enemy and wave validation without changing the running range decoder. If that nested format changes, re-review and version the outer mission contract as well; do not assume outer version 1 remains compatible. The authored compatibility regression explicitly expects the current range version to remain 5.

## Fixed wire layout

All integer fields are little-endian. No string, JSON, variable identity, client clock, asset path or arbitrary object deserialization is accepted.

Power intent is exactly **32 bytes**:

| Offset | Bytes | Field |
| --- | --- | --- |
| 0 | 4 | Mission magic |
| 4 | 2 | Version 1 |
| 6 | 1 | Kind 1 |
| 7 | 8 | Positive session ID |
| 15 | 8 | Positive round ID |
| 23 | 8 | Nonzero command sequence |
| 31 | 1 | Held 0 or 1, all other values rejected |

The command contains no position, elapsed time, target, health, objective phase or unlock result. Its decoder proves only syntax. The host must resolve the transport sender to its current host-held ticket and pass the command into `HostCoopMission.TrySetPowerIntent` with the real host clock. Keep this sequence distinct from the host snapshot sequence. Rate limiting, input expiry, replay rejection, life, range and LOS are host coordinator/transport work, not client claims.

Mission/combat frame is a **23-byte header plus at most 442 bytes**, maximum **465 bytes**:

| Offset | Bytes | Field |
| --- | --- | --- |
| 0 | 4 | Mission magic |
| 4 | 2 | Version 1 |
| 6 | 1 | Kind 2 |
| 7 | 8 | Nonzero host frame sequence |
| 15 | 1 | Defined mission phase |
| 16 | 1 | Gate unlocked 0 or 1 |
| 17 | 4 | Finite nonnegative float hold seconds |
| 21 | 2 | Exact bounded nested range length |
| 23 | length | One complete validated range v5 snapshot |

The length is checked against packet length and the fixed maximum before allocation. Every truncated or surplus-byte packet is rejected. The nested decoder validates the exact enemy/member counts, unique member slots, bounded health/ammo/positions, revive relationships and wave/encounter consistency. Mission and combat state become one accepted immutable frame, avoiding independently arriving mission and combat snapshots from different rounds.

Session and round IDs are not duplicated on the wire: the mission view inherits them from the same nested snapshot. Encoding rejects a mismatched `CoopMissionSnapshot` or an `IsCurrent=false` source. This `IsCurrent` flag describes the accepted frame's bound round, not evidence that the real host remains connected forever.

## Semantic validation

- RestorePower: locked gate, dormant encounter, disabled horde, hold at most three seconds.
- DefendDepot: unlocked gate, active encounter, fighting/intermission horde, zero objective hold.
- ReachExtraction: unlocked gate, cleared final horde, hold at most two seconds.
- Completed: unlocked gate, cleared final horde, zero hold.
- Failed: zero hold; an unstarted/aborted encounter, or an unlocked mission with failed/completed horde. This preserves failure-before-start, failure during waves and explicit failure after waves without manufacturing victory.

Every nonfailed mission needs at least one living member. Positive extraction progress and completion require every connected member alive; a downed teammate is permitted at extraction only with zero hold. Nested combat validation is still applied for every phase, including failure. Undefined enums, noncanonical booleans, NaN/infinity/negative/excess holds, nonplaying rounds and identity mismatches are rejected. Float hold precision is view-only and never fed back into authoritative objective timing.

## Client view guard

Create `CoopMissionReplica` only after accepting a host-issued positive session and round through the authorized session transition. Feed it packets **only after verifying their transport sender is the current server/host**. A valid magic or matching ID is not authentication, and this class intentionally has no transport access with which to prove the sender.

The replica:

- Rejects packets for another session/round, zero/duplicate/older frame sequences and malformed frames without changing its current accepted value.
- Permits skipped forward phases after packet loss, and allows hold progress to reset within a phase.
- Rejects phase regression, gate relocking and a changed terminal outcome.
- Allows refreshes of the same terminal outcome, but freezes its gate state too.
- Decodes into copied immutable state; changing the original packet buffer cannot alter the accepted frame.

Create a new replica on an authorized new round. Discard the old replica on disconnect/stop; do not keep displaying its `IsCurrent` field as proof of a live session. Do not reset a same-round receiver just to admit an older sequence. Host frame counters must not wrap within a round; start a new authorized round/session rather than recycling a counter at `ulong.MaxValue`.

Received values drive HUD/visuals only. They never award rewards, enable host collision, set health, advance mission objectives or make damage decisions. Transport bandwidth/queue caps, snapshot cadence and host authentication remain required in the future adapter.

## Authored checks and remaining acceptance

Thirty-six authored NUnit cases cover held/released command round trips, strict command IDs/sequence/boolean, all truncations and extra bytes, every mission phase, the 465-byte maximum, both nested/outer headers, length constraints, bad holds, mission/encounter/gate coherence, nested malformed members, explicit failures/intermission, distinct handshake/type compatibility, replica replay/order/round/terminal guards, dropped transitions/countdown resets, immutable copied bytes and invalid replica bindings. A deterministic random malformed sweep and mutations throughout a valid maximum-size frame exercise nonthrowing decoding. These cases and their bounded mutation loops now execute successfully in the standalone harness. They are not a coverage-guided fuzzer, allocation profile or native Unity result.

Before integrating into the active range:

1. Compile and run the pending coordinator, rules and codec tests in the actual Unity editor.
2. Connect the mission-capable handshake and reject older peers, with tests for wrong sender, stale session, wrong message kind, malformed sizes and oversized NGO messages before copying bytes.
3. Add the F power command and exact session/round sequencing without changing E revive semantics.
4. Build each frame once after host movement, combat, revival and objective updates. Do not combine states sampled on different ticks.
5. Send bounded snapshots and apply them through a per-round replica, with explicit lifecycle cleanup and stale-network-message tests.
6. Add scene gate/LOS/spawn tests and rendered HUD controls, then expand the real two-process harness through power, backtracking, all waves, revive, extraction and shutdown. Preserve current baseline regression scenarios.

## Change record

2026-11-08: added the separate bounded mission codec, immutable frame/command values, per-round view guard, authored tests and this wire/integration contract. No existing runtime code, scene, protocol v5 implementation, local material, project setting, branch or pending validation action was changed.
