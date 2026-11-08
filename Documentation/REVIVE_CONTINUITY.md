# Revive continuity correction

**Status update, 2026-11-08:** This document retains the original slice's scope and historical status below. The combined components subsequently passed native acceptance at [802ce58](NATIVE_ACCEPTANCE_802CE58.md). The new [playable mission integration candidate](PLAYABLE_DEPOT_MISSION.md) wires them into opt-in LanRange sessions but has not yet passed its own native acceptance. Earlier pending-material/inactive-mode statements describe the original publication, not the latest candidate.

## Scope and executed result

This is a correction to the existing host-owned revive rules, not a new game feature or scene integration. It changes `HostInfectedEncounter` and forwards invalid pose observations through its `HostCoopMission` owner, plus tests/documentation. Base: `253b43879fc65a61c1bb69cf485d0deafb9d1756`.

**264/264 pure-core NUnit cases passed** in the cloud, including all previous 248 cases and 16 new continuity cases. Compilation had **0 errors and 0 warnings**; no cases were skipped or inconclusive. The initial 14-case fixture reproduced **13 failures / 249 passes / 0 errors** before the production change (262 total). The passing pre-fix new case established duplicate-timestamp behavior. Review then found that the mission owner swallowed invalid poses before its encounter received them. Two additional coordinator-path regressions reproduced that gap (264 total, 262 passed, two failed) before a small propagation correction. The final run passes all 264.

Native Unity compilation, the full current EditMode/PlayMode inventory, Windows build and two-process regression have not run for this correction. The separately pending floor material candidate remains pinned to `4b7e1ab63201afabd3db172ebc4eaf87447eb1dd`. No local files or approval-dependent material actions were touched.

## What failed

The old channel computed `hostTime - StartedAt`. Fresh held input could keep the channel alive while the host stopped calling the target/range/line-of-sight validation step. On resumption, old wall-clock time could immediately complete the revive. The progress query similarly extrapolated time without a recent verified advancement.

The production adapter processes input in Update/network callbacks and rescue validation in FixedUpdate. These are therefore scheduling and host-state robustness gaps, not evidence that a remote client successfully exploited a normal running Unity session. The cloud reproduction invokes the real core APIs in memory.

Additional reproduced gaps:

- A valid out-of-range pose followed by a return before the next validation could preserve an old channel.
- Rejecting an invalid new pose left the old pose and channel usable.
- A wrong target passed to the host advancement method returned false without clearing prior progress.
- Only the completing rescuer's channel was cleared on revival. Another rescuer could retain almost-complete progress if the same target was downed again before their next validation. The regression uses four real wave-three enemies to remove the restored 40 HP in the same host tick, not a test-only health setter.

## Corrected contract

- A channel stores **verified accumulated seconds** and the timestamp of its last successful target/range/LOS check.
- A first successful check starts at zero. Each subsequent valid check contributes at most **0.1 seconds**. Duplicate timestamps contribute zero.
- A validation gap of **0.25 seconds or longer** restarts from zero, even if fresh input continued arriving throughout the gap.
- Only `AdvanceReviveFromHost` with the current expected target and clear host-provided LOS may add credit or refresh the validation timestamp. Holding input, proposing a target and reading progress do not add credit.
- Three verified seconds restore 40 HP. A 1e-9 comparison tolerance prevents floating-point summation noise from requiring an extra ordinary tick.
- The displayed progress reports verified accumulated time, not extrapolated time. It disappears on stale validation/input, backward observation, an invalid pair, or a completed/interrupted channel.
- Valid pose updates immediately check affected channels. Leaving range invalidates progress even when the player returns before the next advancement. Invalid pose input invalidates that member's prior pose and affected channels rather than retaining stale geometry.
- A failed target or LOS check resets the channel. Release, expired input, damage and the existing movement/fire/reload cancellation continue to reset it.
- Downing and successful revival reset all channels targeting that survivor. A later downing requires a fresh three-second channel from every helper.

The mission owner now also forwards rejected pose observations to its owned encounter, so invalid-then-corrected observations cannot be hidden between those layers. This is core state propagation, not scene wiring.

The input format, protocol version, three-second requirement, two-metre range, 40-HP restoration and unlimited-reserve prototype weapon behavior are unchanged. No permission, networking, material or rendering code changed. Channel invalidation iterates a bounded collection of at most ten rescuers; no unbounded history is retained.

Sparse validation is intentionally conservative: at a 5 Hz verified cadence, only 0.1 seconds is credited per 0.2-second interval, so completing a revive can take longer than three wall-clock seconds. At gaps of at least 0.25 seconds it must restart. Normal fixed-step operation is expected to be faster than these thresholds, but actual Unity timing and smoke-test deadlines still require native verification.

## Coverage

The 16 new cases cover:

- Input refreshed for over three seconds while validation is withheld, followed by a fresh full-length recovery.
- Progress queries that do not advance time and expire when validation stops.
- Queries before the last verification or most recent input.
- Rescuer and target out-and-back range excursions.
- Rescuer and target invalid poses followed by immediate correction.
- The same invalid-pose interruption propagated through the actual mission owner, after real power-to-wave activation, for both rescuer and target.
- A wrong validated target followed by a new zero-progress channel.
- Exact 0.25-second, 0.251-second and one-second validation gaps while input remains fresh.
- Sparse 0.2-second checks receiving at most 0.1-second credit each.
- One hundred repeated checks at the same timestamp without progress gain or loss.
- Two rescuers near completion, one successful revive, four same-tick enemy hits, and rejection of the second rescuer's stale progress.

The earlier three-second hold, occlusion, injury, disconnect/new round, post-clear revive, two-helper, weapon/movement, mission replication/capture and packet robustness cases all remain in the unfiltered passing run.

Run UTC: `2026-11-08T19:12:11.6882903Z` to `2026-11-08T19:12:30.1713986Z`, **18.483058 seconds** excluding restore/build. Toolchain: approved Microsoft .NET SDK 10.0.403, runtime 10.0.14, separate production .NET Standard 2.1 assembly, C# 9, NUnit/NUnitLite 3.14.0. Exact source and lock hashes are in `REVIVE_CONTINUITY_RESULT.json`; they remained unchanged during execution.

Static checks passed: 77 source/assets, nine assemblies, 92 GUIDs, 48 Git-attribute assertions and 15 malformed-importer fixtures. These are not native engine results.

## Remaining boundary

The adapter must still provide trusted current poses and a real current physics LOS result. The pose setter does not receive a timestamp, so the core cannot independently prove that an otherwise valid unchanged pose is fresh. This change detects an observed invalid pose/range transition and missing validation cadence; it is not a substitute for collision resolution or authentication.

Current-main native acceptance must repeat the existing revive/weapon/movement/wave smoke scenarios on the exact approved build, including ordinary timing and interrupted/resumed verification. The previous camera/loopback pass remains historical evidence. Real LAN, ten-player scale, performance, character animations and manual controls have not been verified by this cloud run.
