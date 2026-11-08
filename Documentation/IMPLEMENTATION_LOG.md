# Implementation and test record

## 2026-11-08: host lifecycle and fire-admission core

### Goal

Prepare a bounded, transport-independent host authority slice without changing the solo mechanics range or adding unverified network packages. This is groundwork for the requested 1–10-player LAN co-op game, not an operational multiplayer feature.

### Change scope

- Added `Assets/UndeadOvertime/Core/LanSession.cs`, `HostFireGate.cs` and a no-engine-references assembly.
- Added a separate `Tests/CoreEditMode` test assembly with 36 parameter-expanded NUnit cases.
- Added all folder/source/assembly metadata with unique GUIDs.
- Updated the static validator to recognize the engine-independent assembly and its tests.
- Added the host-core runbook and linked it from the networking plan.
- No scenes, existing runtime mechanics, project settings, package manifest/lock, renderer settings, build list, asset source files, network settings or local PC files changed.

### Basis and authority boundary

Authored from the mechanics range at `afbbe7b48d169db8bf426b6c67baa30a2c47bce9`, rebased for publication on workflow commit `79178364b3035c29652bcc046f814afe7424d10b`. Public game repository contains only original code/docs/metadata here. No private third-party art or account data is included.

### Cloud checks actually run

- `python Tools/validate_prototype.py`: PASS; 17 source/assets, 5 assemblies, 25 metadata GUIDs.
- `python -m py_compile Tools/validate_prototype.py`: PASS.
- Independent source review: no high-impact findings after clock-cap/session-discriminator fixes. It did not execute C#.
- Source/test inspection: host is included in capacity, old tickets/rounds fail, snapshots cannot mutate membership, accepted shots use host origin and constants, rejected fresh sequences cannot replay later, finite-data and per-player cooldown/ammo checks present.
- All added Markdown relative links and selected publication paths checked before publication.

### Not run / not established

- Unity import/compilation, NUnit EditMode cases and original PlayMode regression.
- Actual host/join transport, player spawns, remote ownership, replication, gameplay raycasts/damage, movement validation, real LAN connectivity, ten-client load, UI and performance.
- The core is not wired into the solo range; no new player-visible gameplay is claimed.

### Review constraints retained

Tickets are correlation values, not secrets. Adapter sender binding and trusted host pose/time are mandatory. Single-threaded use only. Session IDs are unique only within one `LanSession` instance; replacing the instance requires tearing down its transport and discarding queued old messages. Reload completion is command-driven; polling `Ammo()` alone does not advance it. New rounds construct new fire gates. The sidearm values are proposed test constants, not final game balance.

### Local execution handoff

Use the existing project, Unity 6000.6.5f1, and the exact new revision supplied with the published result. Record clean state and remote/head first, fetch and fast-forward only, then run all EditMode tests and the original range's PlayMode tests according to [PROTOTYPE_RANGE.md](PROTOTYPE_RANGE.md). Include the new core assembly in EditMode results. Return exact commit, editor version, exit codes, XML, logs and any runtime screenshots to cloud authoring. Do not repair code or upgrade packages on the local execution side.

Current acceptance: **authored and statically reviewed only; local compilation/tests pending**. Add execution evidence as a new dated entry without replacing this record.
