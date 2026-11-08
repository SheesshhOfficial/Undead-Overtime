# Co-op mission progression: isolated rules candidate

## Status and boundary

This is an engine-independent rules candidate, not an active mission in the current LAN range. It adds only a core class, its immutable observation/snapshot values, tests and this document. It does not change a scene, network protocol, player build, material, imported asset, local settings or current gameplay. The pending depot-floor material setup is independent and must not be resumed without its required approval.

Cloud static consistency and independent source review pass. Native C# compilation and these new EditMode cases have not run. No game-engine acceptance, actual LAN behavior, rendered objective, persistence or performance claim follows from this candidate.

## First mission proposal: power the depot and escape

The original American-style depot is the opening objective location, not a copy of a reference franchise map. This bounded loop introduces the larger game's progression-gated city and backtracking pattern:

1. **Restore power.** A living connected survivor holds interaction at the generator for three uninterrupted seconds. The host verifies fresh input, proximity and line of sight. Several teammates can help maintain the shared interaction but do not multiply its speed.
2. **Open the depot route.** Once power is restored, the shared gate remains unlocked for the rest of that round, including backtracking. Leaving the area does not relock it.
3. **Defend the depot.** Start the existing host-owned three-wave encounter only after the power objective completes. The mission advances only when that exact current-round encounter is completed, not during an intermission or when a single infected dies.
4. **Gather at extraction.** Every currently connected survivor must be alive and in the extraction zone for two continuous seconds. Downed teammates block extraction until revived. Anyone leaving the zone resets the countdown. A disconnected player no longer blocks the remaining connected team.
5. **Complete the mission.** Completion is terminal for the mission state and cannot be awarded twice or overwritten by a late failure event. It does not itself stop networking, teleport players, disable movement, save progression or prevent further exploration. Those adapter/UI decisions remain separate.

The three-second power and two-second extraction values are initial testable tuning choices. There is no permanent player progression, currency, inventory reward, checkpoint save or multi-district mission content in this slice.

## Core contract

`CoopMissionProgression` captures the active LanSession's session and round IDs at construction. A new round requires a new instance. Snapshot data includes those IDs, phase, whether the instance is current, persistent gate-unlock state and current continuous hold seconds. A stale snapshot is not a valid result for a newer round.

`CoopMissionObservation` is a **trusted host aggregate**, never a network request. Its fields do not prove authority by themselves. The future host adapter must calculate them from current server-owned state:

- `LivingPlayers`: count connected current members whose authoritative health is above zero.
- `PowerHeld`: at least one eligible living member has a fresh held interaction, is within the tested range and has an unobstructed host physics ray to the power control.
- `AllConnectedAtExit`: every connected member is in the host-owned extraction volume. Life-state equality is checked separately in the core, so a downed survivor inside the zone does not count as ready.
- `Horde`: the current round's actual HostInfectedEncounter phase. Never use a client claim, previous round or unrelated encounter.

The class rejects impossible life counts, undefined horde phases and phase-inconsistent observations. The power stage does not accept an already-running/completed horde. Extraction does not accept a regressed fighting/intermission state. Invalid observations cancel hold progress without completing an objective.

One authoritative tick advances the shared timer once, regardless of player count. Duplicate or backwards timestamps cannot add progress. The first eligible observation earns no retroactive time. Per-tick credit is capped at 0.1 seconds and a gap above 0.25 seconds breaks continuity; pausing the host cannot instantly complete an interaction on resume. Loss of eligibility resets only the current hold, not completed objectives or the unlocked gate.

A host-observed team wipe, failed horde or explicit host failure is terminal. A stopped/replaced session or new round invalidates the old mission instance; it cannot modify the new round. The core performs no I/O and has no Unity, socket or persistence dependencies.

## Next adapter work, deliberately not implemented here

1. Bind a new mission instance to the same session/round and authoritative encounter used by LanRange. Do not mix snapshots from distinct rounds.
2. Add an opt-in mission mode, preserving the existing firing range, single-infected and three-wave regression modes.
3. Implement held **F** interaction for the generator, separate from existing **E** revive. Derive sender identity from transport context and enforce current session/round, monotonic command sequence, rate limits and a short input lifetime. A client must never transmit completed objectives, elapsed hold time, health, position, LOS or unlock decisions as authority.
4. Add an original generator/control placeholder and a bounded gate/extraction layout. Validate host movement collision before/after gate opening, safe spawn placement and backtracking. Only provenance-approved runtime assets may replace placeholders.
5. Start waves once on the transition into DefendDepot, not whenever AdvanceFromHost returns true; true means an accepted observation, not a phase change. Call FailFromHost if wave startup fails. Read the existing encounter's actual terminal state; integrate failure/revive and disconnections before building the aggregate observation. Process the Completed transition once per session/round if future rewards are added.
6. Extend the bounded network snapshot and explicitly version the protocol. Current protocol v5 is unchanged by this candidate. Reject mixed-version peers rather than interpreting an old message as a mission event.
7. Replicate mission phase, hold progress and gate state for HUD/presentation only. The host remains the authority for collision, interaction timing and completion. Clients interpolate presentation without completing objectives themselves.
8. Add a clear objective HUD, power/extraction countdowns, missing-teammate/downed hints and an ending summary. Completion must be presented distinctly from connection shutdown.

## Native acceptance before integration claims

The 21 new EditMode cases cover full objective order, first-frame credit, duplicate ticks with ten simulated members, interruption and host pauses, bounded slow ticks, invalid counts/phases/times, downed teammate extraction blocking, leaving/rejoining the zone, disconnection, persistent gate state, terminal completion/failure, and stale session/round isolation. These are authored tests, not passed native results yet.

After the rules compile and all tests pass, adapter acceptance must additionally include:

- A real host/client two-process mission run with power hold, shared gate state, backtracking, all three waves, down/revive and extraction.
- Release/LOS/range failure and stale held packets canceling power progress.
- A client unable to unlock, change mission phase or finish extraction by sending a claimed state.
- Correct behavior when a teammate leaves the extraction zone, disconnects, is downed or is revived during its countdown.
- Mission reset on the next round and rejection of delayed prior-round commands/snapshots.
- A full regression of current movement, damage, reload, rejoin, revive, waves and host-shutdown tests.
- Actual rendered objective/HUD/collision captures and a separate manual control test. Two local processes are not evidence of physical LAN, ten-machine scale or target-hardware performance.

## Change record

2026-11-08: authored an isolated round-scoped mission rules candidate and regression cases while independent material approval was pending. No pending material operation was performed or bypassed. No scene/network integration, local source authoring, new branch, backup, asset transfer or external service action is part of this slice.
