# Deterministic mission team-size validation

## Why this pass

The Unity PC was offline, and brick-perimeter/damage-feedback native validation was already queued. This pass adds core regression coverage instead of another untested runtime integration. Production code, protocols, scenes, imported assets, materials, packages and settings are unchanged by this validation-only commit.

## Scope and limits

The scenarios use the actual `LanSession`, `HostCoopMission`, owned infected encounter, `HostFireGate`, mission codec and `CoopMissionSessionReplica`. They cover teams of one through ten **modelled members, including the host**, using a deterministic synthetic host clock and explicit valid host poses. Power and extraction holds must remain shared, not become faster with more participants. Captured rosters/slots and serialized state must agree with the host throughout the mission.

LOS and wave-placement callbacks are explicitly simulated successful observations. Enemy damage is applied through host-owned rule APIs. These tests do not exercise collision, navigation, camera/rendering, input, actual shooting/raycasting or any socket/transport. A ten-member model passing is not ten Unity players, ten network processes or a capacity/performance benchmark. Encoded packet size excludes transport/IP/NGO overhead and is not a bandwidth measurement.

The prototype still uses the same three bounded waves (2/3/4 infected) at every team size. This pass does not tune difficulty or establish balanced ten-player combat.

The added lifecycle scenarios exercise disconnect/rejoin, retired connection tickets and a fresh round after lobby reset. They do not implement host migration, save games or join-in-progress.

## Verification

The exact final pure-core run and source hashes are recorded in `MISSION_TEAM_SIZE_CORE_RESULT.json`. Native Unity discovery and the existing pending combined suite still need to run when the PC is connected. The previous accepted native baseline remains b7e07802; the later brick and damage changes have not acquired native acceptance through these core tests.

The frozen-source final run passed **347/347 pure-core tests**, with zero failures, warnings, skips or inconclusive results. Compilation passed with zero warnings/errors. Source and package-lock hashes remained unchanged. The 13 new cases captured, decoded, re-encoded and applied 8,742 coherent host frames across 19 completed mission rounds and 57 wave placements. The largest ten-member/four-enemy payload was 465 bytes, excluding transport overhead.

A first aggregate run passed the 334 existing cases and failed all 13 new cases on the same test-only exact floating-point comparison: the five-second intermission serialized as `4.9999999999999991`. The fixture now uses a 1e-9 tolerance for that duration, consistent with its double arithmetic. No production code was changed. The full frozen-source suite was rerun, not just the failing cases.

Cloud static checks passed: 115 source/assets, nine assemblies and 132 GUIDs; 96 attribute assertions; 33 malformed importer fixtures rejected; and 80 files parsed without C# 9 syntax errors. Syntax parsing is not Unity API compilation.

## Pending combined native batch

The test-only addition raises expected native discovery to **383 EditMode /112 PlayMode** cases. The accepted native result remains **340/94 at b7e07802**, not 383/112. Wait for the connected PC and a coordinated exact-revision fast-forward before running the queued facade import, fresh compile/discovery, full suites, one build, range and mission two-process harnesses, and visual inspection of brick corners/LODs and damage feedback. Preserve all local generated metadata, materials and settings. This pass adds no runtime feature or native visual review requirement.
