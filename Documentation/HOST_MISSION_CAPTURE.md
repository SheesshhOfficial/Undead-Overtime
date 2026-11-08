# Coherent host mission frame capture

**Status update, 2026-11-08:** This document retains the original slice's scope and historical status below. The combined components subsequently passed native acceptance at [802ce58](NATIVE_ACCEPTANCE_802CE58.md). The new [playable mission integration candidate](PLAYABLE_DEPOT_MISSION.md) wires them into opt-in LanRange sessions but has not yet passed its own native acceptance. Earlier pending-material/inactive-mode statements describe the original publication, not the latest candidate.

## Scope

`HostCoopMission.TryCaptureFrame` is the small read-only sampling seam for later Unity/network wiring. It assembles one immutable mission/combat frame and its encoded payload from the coordinator's owned host state, rather than requiring the Unity adapter to duplicate health, pose, revive and wave assembly. It does not send traffic, register handlers, change the current v5 range, create a scene or save any local material.

The method owns only its per-round frame counter. It advances that counter after successful validation/encoding, never on a rejected capture. It refuses wraparound at `ulong.MaxValue`; replace the coordinator through a new authorized round instead of recycling sequence values.

## Inputs and ownership

- The exact current `HostFireGate` for the same `LanSession` object and captured session/round. The new `IsBoundToCurrentRound` query rejects another session object even if all numeric IDs and tickets happen to match.
- A host-held mapping from each current `PlayerTicket` to its stable byte slot. Every connected ticket must have a unique slot below ten. Disconnected/stale map entries are ignored; current survivors are not renumbered.
- Three explicit host-owned legacy range target-health values, each 0–100. The helper does not invent three full-health targets. The nested range protocol still requires those fields even if a future mission UI hides training targets.
- A finite host sample time at least as new as the coordinator's observed clock. Every connected pose must have been supplied by the host and remain younger than 250 ms. Missing, invalid, future or stale poses fail capture instead of being filled with spawn positions.

Member positions and health come from this coordinator and its owned encounter. Ammo/reloading comes from the identity-checked fire gate. Revive target/progress is sampled from the same encounter and translated through the same current-ticket slot mapping. Enemy/wave state and mission phase are sampled together. The existing codec validates the full nested and outer aggregate before any output is returned.

No new public data-mutating accessor is needed. Input target-health arrays and member arrays are copied into the immutable snapshot; later mutations of caller arrays/maps cannot rewrite an accepted frame. The returned bytes are caller-owned and should be sent unchanged.

## Required caller order

Historical slot stability and cross-component mutation/clock ordering remain the caller’s responsibility. Capture checks the current map’s coverage, uniqueness and bounds; it does not remember earlier slot assignments or inspect every dependency’s last mutation time.

Capture immediately after one coherent authoritative host tick:

1. Apply host collision-resolved movement and refresh all mission poses.
2. Process combat, downing and revival on the owned encounter.
3. Synchronize weapon/movement life state and finish due weapon reloads.
4. Advance mission/waves once, including verified placement and objective transitions.
5. Capture the validated frame before any further state mutation; cache/send it at the intended snapshot cadence.

The helper must not be called recursively from a placement/LOS callback while the mission is advancing. It does not advance clocks, reloads, revives or objectives for the caller. A valid codec result proves structural/state consistency, **not** that the adapter obeyed this tick order. An incoherent post-kill/pre-mission-transition aggregate is rejected; the integration fixture tests that boundary explicitly.

A failure before the first successful mission tick may still be captured if initial valid host poses exist. That sends the actual terminal failure and locked gate, rather than withholding failure because no successful advancement occurred. Terminal captures may refresh with newer sequences; the client replica retains its existing terminal guards.

Failure returns false and leaves both outputs null. Trusted map implementations that throw are not silently swallowed. Only the codec's invalid-aggregate ArgumentException becomes a false capture. Capture performs no rollback, retry, forced pose refresh or weapon repair.

## Verification

Actual cloud result: **234/234 pure-core NUnit cases passed**, including 15 dedicated capture cases and all 11 combined mission replication cases. Separate core/test compilation completed with **0 errors and 0 warnings**, and the unfiltered run had **0 skipped or inconclusive cases**. Runtime: .NET 10.0.14; core target: .NET Standard 2.1; language: C# 9. UTC execution: `2026-11-08T18:46:58.8168988Z` to `2026-11-08T18:47:06.5695146Z` (7.752510 seconds, excluding restore/build).

Exact source/fixture/lockfile hashes and counts are recorded in `HOST_MISSION_CAPTURE_RESULT.json`. Source and lockfile bytes remained unchanged during the run. Static consistency also passed (75 source/assets, nine assemblies, 90 GUIDs), along with 48 Git-attribute assertions and 15 malformed-importer fixtures. These static checks are separate from the executed core results.

The combined mission fixture now uses this production capture path on every simulated publication and independently assembles expected data for comparison. Its one/two/ten-member flows, actual shot/reload/health/revive cases, failure cases, disconnect behavior and duplicate/reordered frame rejection remain covered. No active Unity integration is implied.

Dedicated cases cover same-object binding, colliding numeric IDs from another session, old-round weapons, exact pose expiry, missing/invalid/future poses, slot coverage/uniqueness/range, null inputs, explicit target-health validation/copying, read-only clock behavior, early terminal failure, recursive capture rejection and sequence exhaustion without wrap or partial output.

The first standalone cloud compiler run caught an underscore lambda parameter shadowing an `out _` discard in a new test. Renaming that test parameter fixed the compile error before any publication. No production workaround or relaxed assertion was used.

Native Unity compilation, full EditMode/PlayMode, floor setup, scene wiring and actual mission transport remain pending. Keep the local material candidate and user-file preservation process separate; this helper neither grants nor exercises permission to save the two materials.
