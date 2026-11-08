# Depot mission geometry and physics adapter

**Status update, 2026-11-08:** This document retains the original slice's scope and historical status below. The combined components subsequently passed native acceptance at [802ce58](NATIVE_ACCEPTANCE_802CE58.md). The new [playable mission integration candidate](PLAYABLE_DEPOT_MISSION.md) wires them into opt-in LanRange sessions but has not yet passed its own native acceptance. Earlier pending-material/inactive-mode statements describe the original publication, not the latest candidate.

## Status

This adds reusable original objective geometry and a host/state-validated presentation adapter, plus 14 authored PlayMode cases. It is not instantiated by the existing LanRange yet. No scene file, active transport handler, existing control, saved material, asset import or local project setting changes in this slice. Pending material validation remains independent.

Cloud static checks and independent source review are not Unity physics results. The new tests have not compiled or executed in the native editor. No rendered review, successful player build, actual mission network session or final visual-quality acceptance is claimed. The geometry deliberately remains a small blockout for validating the opening mission, not the final city or finished environment art.

## Original flat-depot layout

Coordinates are world meters within the existing 32-by-32-meter arena. The parent transform must have identity world position/rotation/scale; construction rejects transformed parents rather than misaligning the host coordinator and physics.

- **Power control:** eye-space interaction point (-9, 1.6, -5). The original physical pedestal is 1.2 by 1.2 by 0.8 meters, centered at (-9, 0.6, -5.6). Its top stays below the interaction ray, so its own collider does not obstruct intended use from the front.
- **Progression partition:** at z=-2, three meters high and 0.4 meters thick. The west/east segments extend from x=-16 to -2 and x=2 to 16, meeting the existing range's outer walls.
- **Route gate:** a four-meter central opening at x=0. Initially blocked by a matching collider/rendered cube. Unlocking removes that gate object from active collision and presentation; adjacent partition collision remains. The route then supports backtracking in both directions.
- **Extraction rectangle:** centered at (0, 1.6, 12), half-width/depth two meters. The low visible marker has no enabled collider or trigger and does not create a second floor. The existing continuous floor stays the only ground collider.

All geometry is authored procedurally for this project. No third-party model, stock texture, franchise screenshot, Mixamo content or external asset is imported. Three shared caller-owned materials supply partition/control/marker colors when rendering is enabled. The adapter performs no shader lookup, Resources load, material construction, asset save or file I/O.

## Physics and state contract

`DepotMissionPresentation` owns only its generated root and children. Instantiate it during Play Mode with the active host-issued session/round IDs. Construction rejects Edit Mode, so this runtime helper cannot accidentally leave authored geometry in an editor scene. Dispose it at round teardown; create a new instance for a new round. It does not start networking or create a camera, listener, player or enemy.

`HasPowerLine` accepts a host-resolved finite eye pose, checks the same two-meter range used by `HostCoopMission`, synchronizes transforms, and linecasts to the power-control point using default raycast layers while ignoring triggers. The host coordinator still checks current membership, life and fresh input. The physics helper does not independently authorize a client or complete an objective.

`ContainsExtraction` uses the exact same center and rectangle extents passed into the coordinator, plus the existing planar eye-height/arena validity check. It does not infer life state or complete a mission. Feed host movement positions, not client-claimed positions, to authoritative checks.

`TryApply` accepts only current, correctly bound session/round snapshots with valid phase/gate/hold relationships. It rejects backward phase changes, relocking, and changed terminal outcomes. Failure after unlock leaves the route open; failure before unlock cannot open it later. The gate's collision changes are immediate and synchronized.

On the host, apply its coordinator snapshot after the authoritative mission tick. On a client, apply only state accepted by the round-bound `CoopMissionReplica` after transport sender checks. This adapter has no frame sequence or network sender; never feed unvalidated network data straight into it. Client presentation must never drive host collision or objective results.

The current gate is an instantaneous blockout switch, not a moving door. There is no tween, swept moving-gate collision, NavMesh bake, interaction animation, sound or final prop replacement yet. Those changes require their own physics and visual acceptance. Closed-gate geometry must be present before a round's first movement tick; unlock is applied once the host mission transition occurs. A newly spawned wave must be collision-placed before acting.

`Dispose` is idempotent. It deactivates and synchronizes the owned root immediately, then schedules destruction through Unity's normal runtime lifecycle. Shared caller materials and unrelated objects are not destroyed. The owner must not alter/delete individual child objects while the adapter is active.

## Headless support

When presentation is explicitly disabled, or the graphics device is Null, only GameObjects and required box colliders are created. No mesh, renderer, material or shader is needed. The extraction marker remains a nonblocking transform. This preserves the existing collider-only acceptance route without relying on the pending depot-floor material setup.

Rendered construction requires all three supplied materials before any geometry is created. That path still needs native visual inspection. Headless physics tests do not establish lighting, color, art quality or the rendered marker's appearance.

## Authored acceptance cases

The 14 cases exercise:

- Collider-only construction and initial locked state without asset/shader dependencies.
- The existing `LanRange.ResolvePlanarMovement` capsule sweep stopping at the closed gate, passing through the open gate, and returning through it.
- Both adjacent partition segments remaining solid after unlock.
- Reachable control LOS above the console, out-of-range/invalid-eye rejection, physical occlusion and ignored triggers.
- Exact extraction rectangle boundaries and absence of another floor collider.
- Rejection of stale identities, invalid phases/holds and backward or changed terminal state.
- Gate persistence after failure and prevention of an unlock after early terminal failure.
- Immediate collision removal and deferred owned-object cleanup on disposal.
- Rejection of invalid IDs and translated/scaled/rotated parent transforms.
- A coordinator fixture using real Unity LOS calls whose power transition opens the actual gate and starts the actual two-enemy first wave. Its spawn callback is deliberately a test stub; this does not prove collision-safe wave placement.

All remain authored, unrun cases. Before activating the mission mode, run native compilation and the full EditMode/PlayMode regression set; integrate the opt-in mission-capable handshake, F input, coherent host tick and snapshot, lifecycle cleanup and objective HUD; then run real two-process power/wave/revive/extraction scenarios and inspect actual rendered frames. Verify all wave spawns against this new partition and gate, including blocked-spawn failure, and retest baseline modes with mission geometry absent.

## Change record

2026-11-08: added the original depot objective geometry/physics adapter, its metadata, PlayMode regression sources and this contract. Existing range code/scene, current v5 network behavior, local materials and pending acceptance remain unchanged.
