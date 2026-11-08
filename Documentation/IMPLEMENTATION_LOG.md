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

## 2026-11-08: solo Survival Yard candidate

Goal: produce an isolated end-to-end solo combat loop while LAN transport and production asset work remain separate. The user requested starting the game now, so this is explicitly a parallel gameplay experiment, not a claim that the network acceptance gates have passed.

Changes: new opt-in SurvivalYard scene, WaveProgression/PrototypeMagazine core rules, runtime survival director and placeholder infected, player health/reload/restart/extraction, HUD, five core tests, seven PlayMode tests, expanded scene test and static validator. Existing mechanics scene and template scene bytes remain unchanged; its runtime behavior is intended to remain compatible. No package/settings/build-list/network/security/local-PC changes, new branches or backup copies.

Cloud checks: static consistency PASS (24 source/assets, five assemblies, 32 metadata GUIDs); Python validator syntax PASS. Independent source review and publication hashes are recorded with the completion handoff. No Unity/C# runtime exists in this cloud environment; compilation, all test execution and user play acceptance remain unrun until local execution.

Local handoff: exact published revision, existing Unity 6000.6.5f1 project; fast-forward only after a clean/divergence check. Run all EditMode and PlayMode suites, then the original range regression and [Survival Yard checklist](SURVIVAL_YARD.md). Return exact XML/logs/screenshots and reproduction for cloud fixes, with no local source repairs.

Acceptance: authored playable-loop candidate only. Caps and timer rules are proposed test balance. Real LAN, Mixamo/production art, audio/effects/gore, city progression and runtime performance remain open.

## 2026-11-08: direct-IP LAN firing-range candidate

Prior local result reported to cloud: exact `0396ea46014893235bc295c892dd0ed56b283a13` clean compile, 50 EditMode/13 PlayMode passed, basic scene smoke rendered. No full-game, LAN or performance acceptance was implied.

Next goal: connect the already-tested host membership/fire core to a real transport without making the solo prototype authoritative by accident. Added opt-in LanRange scene, host/join/stop UI, ten bounded fixed firing stations, host round controls, server-origin shooting/shared target HP, ammunition/reload snapshots, binary protocol and endpoint validation. Movement and zombies are deliberately excluded until the networking gate passes. Added trusted reload tick to HostFireGate without changing its existing rules.

Package selection: official NGO 2.13.3 and Unity Transport 2.6.0, compatible minimum editor declarations checked against Unity 6000.6.5f1. Manifest and lock gain only those package entries; existing versions remain. Local resolution is still required. New original code, scene, tests, metadata and [LAN runbook](LAN_RANGE.md); no source art or third-party character content. Build helper is explicit opt-in and does not edit the persistent scene build list.

Cloud checks: static metadata/scene/assembly consistency; validator Python syntax; dependency-minimum comparison against current lock; source/codec boundary review. Independent review found and corrected NGO named-message reader offset handling before publication, with an eight-byte-prefix regression test. New authored suites bring the total to 73 EditMode and 18 PlayMode cases. These counts are source inventory, not new test results.

Local handoff: exact published revision; clean fast-forward; resolve packages/compile; run all suites and original scene regressions; build the dedicated player; test two real processes on loopback, then separately on LAN. Ten-member behavior/performance needs ten actual connections. Return compiler/test/log/screenshots and package-resolution diff for cloud fixes. No local authored repairs, no firewall/security changes without approval.

Acceptance at publication: authored and statically checked candidate. New C#/Unity tests, player build, remote client exchange, actual LAN and performance remain unrun here.
