# Host mission coordinator candidate

## Scope and status

This slice connects the engine-independent mission rules to a real `HostInfectedEncounter` and current `LanSession` membership. It is an integration seam for the next opt-in gameplay mode, not an active scene feature. The existing LAN range, wire protocol v5, player controls, snapshots, materials, import settings and local acceptance candidate are unchanged.

The class owns its encounter rather than accepting an arbitrary encounter argument. Session, round, member lifetimes, health, revive and the three-wave state therefore come from the same authoritative objects. The constructor accepts an original power-control position and extraction rectangle inside the existing flat arena bounds. Gate collision and rendered objective props remain subsequent Unity work.

Cloud static checks and source review are distinct from native acceptance. The new C# tests are authored; they have not been compiled or run in Unity. No new player-build, rendered mission, network mission, physical LAN or performance result is claimed. The independent two-material save approval is still pending and is not exercised by this change.

## Trust boundary

`TrySetPowerIntent` accepts only held/released input, session/round identifiers and a monotonically increasing sequence. Its sender ticket must come from the transport's current host-held connection mapping. Never decode a ticket, elapsed hold duration, health, position, LOS, objective completion or gate state from a client as authority. Host time is supplied by the adapter, not a packet.

Positive input is paced at 20 ms and expires at 250 ms. Release bypasses positive-input pacing. Fresh rejected commands consume their sequence so they cannot become a delayed valid action. Repeated timestamps cannot advance objectives. A release/repress or expiry between host ticks breaks the previous countdown even if a fresh held packet arrives before the next tick. Another continuously eligible teammate may keep the shared hold alive, but does not speed it up. A newly eligible helper without overlap establishes a fresh baseline, conservatively costing up to two host ticks before progress resumes.

The host supplies collision-resolved eye positions every tick. Invalid or 250-ms-stale poses cannot operate power or count at extraction. Observed out-of-zone or invalid pose updates latch an extraction interruption until the next tick, even if the player reenters before that tick. An observed exit from power range cancels held power intent. Power requires a living member within two meters and a current host LOS callback. Extraction requires every connected member to have a fresh in-zone pose and positive encounter health. A downed member blocks the countdown until the real encounter's revive logic restores health; a disconnected member no longer blocks it. None of these facts is a client-supplied aggregate.

## Tick ordering for the future Unity adapter

1. Start the session round and construct one `HostCoopMission`. Use its `Encounter` as the round's encounter; do not also construct a separate encounter or manually start a test encounter in mission mode.
2. Map transport sender to its current host-held ticket, validate/version/size the packet, and pass bounded held/released intent. Use a separate command sequence or the existing shared monotonic command sequence consistently. The class is not a packet codec or traffic-bandwidth limiter.
3. Apply existing authoritative movement and physics. Call `SetPlayerPositionFromHost` for every connected member with the resulting eye pose and current host clock.
4. Process damage and revive against this exact owned encounter. Synchronize the existing movement/fire gates with encounter life state as the range currently does.
5. Call `CancelPowerFromHost` for authoritative firing, reload, nonzero movement, revive interaction or injury. The coordinator also observes health loss at ticks, but event cancellation covers actions occurring between ticks.
6. Call `AdvanceFromHost` once, after those updates. Supply a host physics LOS function and the existing collision-safe wave-placement function adapted to accept this encounter.
7. Publish the coordinator snapshot for presentation only. Apply a gate unlock transition once on the host, retain it for backtracking, and replicate the gate state. Add a versioned mission protocol extension and a HUD in a separate slice before exposing mission mode to players.

The coordinator starts waves only on the actual RestorePower → DefendDepot transition. It advances the owned encounter's five-second intermissions and calls placement once for each newly created wave. **Do not also call `AdvanceWavesFromHost` from the Unity adapter** in mission mode. Continue enemy movement, attacks, authorized hit damage and revival normally.

The placement callback must validate all proposed enemy positions against world collision, current players and chosen enemy positions, then commit the full placement atomically. Its boolean is a trusted host result, never a client claim. A false result fails the mission and aborts the active encounter. Missing callbacks also fail closed. Callback exceptions fail/abort first and then propagate for diagnostics; they are not silently swallowed or retried. Recursive advancement and new pose/input writes during callbacks are rejected.

The owned encounter is exposed so the host can run combat/revive/physics. That exposure is not an untrusted API: trusted code must not manually start a separate single-infected mode, advance waves twice or inject damage. A separate test encounter started during RestorePower is detected and fails closed. Tick processing must stop or reset after an exception; never keep simulating a failed setup as success.

## Authored coverage

The 29 authored coordinator test cases use actual session tickets and actual encounter health, damage, wave and revive APIs. Their placement/LOS callbacks are deterministic test doubles, not engine physics. Coverage includes:

- Full power hold, three waves with exactly three placements, and separate whole-team extraction.
- Current sender/session/round and sequence admission, paced positive input and immediate release.
- Late initial input, release/repress, input expiry, pause, explicit action cancellation and another helper maintaining the shared hold.
- Host-derived range/LOS, invalid/stale poses, stale extraction poses, and unchanged shared progress under extra helpers.
- Failed first/later wave placement, missing callbacks, exceptions and callback reentrancy.
- Real encounter downing, extraction blocked until actual revival, team wipe and terminal success/failure.
- Teammates leaving extraction/disconnecting, stale/replaced tickets, new rounds and host shutdown.
- Finite bounded layout configuration.

Native compilation and EditMode execution remain required. Before enabling this in the range, add Unity physics tests for generator LOS, gate collision/backtracking and safe wave spawns, a bounded protocol extension with malformed-message tests, rendered objective/HUD checks, and an expanded real two-process mission harness. Retain all existing movement, firing, reload, reconnect, infected, revive, wave and shutdown regression scenarios. Two local processes still do not establish physical LAN or ten-machine acceptance.

## Change record

2026-11-08: added `HostCoopMission`, its EditMode tests and metadata, and this integration contract. No existing source file, material, asset, scene, protocol, package, local setting or pending validation operation changed.
