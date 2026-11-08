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

## 2026-11-08: opt-in two-process loopback harness

Local result for b18b57b: explicit package resolution cleared missing Netcode types without code changes; 73 EditMode and 18 PlayMode passed, Windows player build succeeded. Transport resolved to builtin6.6.0. Seven editor-generated tracked changes and the empty default network-prefab asset remain preserved locally; none is overwritten or adopted by this commit. Exact distinctions and warning limits are recorded in [LAN_LOOPBACK_AUTOMATION.md](LAN_LOOPBACK_AUTOMATION.md).

Added explicit standalone test flags, bounded loopback host/client coroutine assertions and per-process reports, a PowerShell launcher with monotonic deadlines and owned-process cleanup, and eight parser test cases. A normal player launch stays manual. The harness tests remote target damage/reload, disconnect/rejoin identity, round reset and host shutdown. Both endpoints are fixed to 127.0.0.1; no native UI automation or security-setting change is needed. The launcher records gameplay binary hashes; its supplied revision remains input that must be corroborated by the build runner.

Cloud static consistency: 39 source/assets, nine assemblies and 51 metadata GUIDs. Unity C# compilation, the new expected81EditMode/18PlayMode suites, rebuilt Windows player and the two-process script remain unrun in cloud. Independent review and exact remote publication verification accompany the completion handoff. A passing host-only test is not a substitute for the new two-process test, and loopback is not real-LAN acceptance.

## 2026-11-08: null-graphics LAN startup correction

The local `bf881d8` candidate compiled and passed 81 EditMode / 18 PlayMode tests and a rebuilt Windows player. Its first automated headless host failed before networking because a null shader was passed into Material construction on NullGfxDevice. The launcher timed out at host readiness, exited 1, never launched the client, and left no running player. No peer results or summary were produced; no multiplayer success is claimed. Nine local generated-file hashes remained unchanged.

The fix builds matching collider-only range geometry when graphics are unavailable, without invoking shader lookup, material creation or mesh generation. Camera rendering, GUI and cursor capture are skipped, while fixed station transforms, collision, shooting and all networking paths remain active. Two new PlayMode tests force this same path even on graphical editors: geometry/raycast checks plus authoritative firing/reload/round reset and shutdown. Expected totals are now 81 EditMode / 20 PlayMode cases. Existing rendered behavior and protocol are preserved; no package/settings/security change, branch or backup was introduced.

Cloud static consistency PASS: 40 source/assets, nine assemblies, 52 metadata GUIDs. The new source requires local compilation, both full test suites, a freshly rebuilt player and another real two-process loopback attempt. Local instructions and exact earlier package-resolution commands are in [LAN_LOOPBACK_AUTOMATION.md](LAN_LOOPBACK_AUTOMATION.md). This is a documented candidate fix, not runtime acceptance.

## 2026-11-08: host-owned walking candidate after loopback acceptance

The 4be3f12 fixed-station baseline passed compilation, 81 EditMode / 20 PlayMode tests, Windows build and the actual two-process script. Both process reports/exits passed, including damage/reload/rejoin/reset/shutdown; no player process remained and nine generated local files stayed unchanged. Full logs contained Unity cloud Curl failures outside the report's runtime error count. Exact run evidence and that caveat are recorded in [LAN_LOOPBACK_AUTOMATION.md](LAN_LOOPBACK_AUTOMATION.md).

Next bounded slice: host-authoritative flat-arena walking. Added HostMoveGate, protocol-v2 movement intent/position snapshots, host capsule sweep/slide resolution, WASD input, camera/remote-body position updates, moved shot origins and spawn reset. No client-controlled positions or time; stale input expires. Existing damage/reload and lifecycle paths remain. No zombie networking is claimed yet.

Thirteen core cases and two PlayMode regressions bring expected inventory to 94 EditMode / 22 PlayMode. The two-process harness now requires replicated movement and input-expiry markers before its existing checks. Cloud static consistency: 42 source/assets, nine assemblies, 54 GUIDs. New Unity compilation, test execution, rebuilt player and moving-client runtime checks are pending local execution. No package/settings/asset-source change, new branch, backup or local code repair.

## 2026-11-08: one host-owned infected candidate

Movement baseline 0f78c51 passed local compile, 94 EditMode / 22 PlayMode, Windows build and moving-client loopback. Both exits and lifecycle/movement checks passed; full player logs each contained five native Unity cloud Curl failures outside the JSON runtime-error count. Nine generated local files remained unchanged, editor stopped and no player remained. Detailed timestamps/hashes and the absence of exported XML are recorded in [LAN_LOOPBACK_AUTOMATION.md](LAN_LOOPBACK_AUTOMATION.md).

Added an opt-in one-infected host encounter, nearest-alive direct pursuit with host collision/line-of-sight, enemy/player health, melee cooldown, host-authorized gun damage, clear/wipe and downed input/fire lockout. This is a placeholder capsule, not a finished Mixamo character. No duplicate client AI, revive, horde, navigation or full-game acceptance is claimed. Protocol v3 adds bounded enemy/player-health snapshots; both peers must rebuild.

Ten new core and four PlayMode cases bring expected inventory to 104 EditMode / 26 PlayMode. The loopback script now requires the client to observe enemy pursuit/injury/kill and acknowledge the result before host shutdown, after all prior movement/lifecycle tests. Static consistency PASS: 45 source/assets, nine assemblies, 57 GUIDs. Source review and exact publication checks accompany the handoff. New Unity compile/tests/build and two-process encounter acceptance are pending. No package/settings/security changes, branches, backups or local authored fixes.

## 2026-11-08: timed co-op revive candidate

Infected baseline 5aaf489 passed local compile, 104 EditMode / 26 PlayMode, rebuilt Windows player and actual two-process pursuit/injury/kill plus prior movement/combat/lifecycle tests. Both reports/exits passed. Known native Unity cloud Curl errors remained outside callback error counts; nine generated files stayed unchanged and no player remained. The exact run is recorded in [LAN_LOOPBACK_AUTOMATION.md](LAN_LOOPBACK_AUTOMATION.md).

Added a host-selected, timed teammate revive: hold E, 2 m, clear line of sight, 3 seconds, 40 HP, continuous refreshed intent. Release/silence/range/obstruction/movement/fire/reload/damage reset progress; downed, disconnected, living and self targets are rejected. A simultaneous second rescuer cannot apply extra healing. Protocol v4 carries only held/released intent and host-owned progress/health; no client victim ID or completion clock. Player controls require fresh input after revive.

Thirteen new core cases and one PlayMode test bring expected source counts to 117 EditMode / 27 PlayMode. The loopback test adds third-round downing, lockout probes, host rescue, progress/40 HP replication and successful rescued movement/shooting before shutdown. Static consistency: 46 source/assets, nine assemblies, 58 GUIDs. New Unity tests/build and co-op revive execution are pending. No package/settings/security changes, branches, backups or local authored repairs.

## 2026-11-08: immediate reload input and final smoke receipt correction

Revive cae6b4d passed compilation, 117 EditMode / 27 PlayMode and rebuilt Windows player. The full two-process run failed: host reached teammate-revived then timed out at 90 seconds; client had observed restored movement/fire but its shutdown observation came from host failure cleanup. No overall acceptance, summary or successful retry is claimed. Exact evidence and six native Curl diagnostics per player log are retained in LAN_LOOPBACK_AUTOMATION.md. Nine dirty-file hashes stayed intact.

Cloud source review identified shared 20 ms fire/reload adapter pacing that could discard an immediate reload acknowledgment. Reload now has its own pacing clock; alive/ammo/round/sequence validation and idempotent weapon reload remain. Added a same-frame fire/reload regression with repeated reload requests across the timer. Expected source inventory is 117 EditMode / 28 PlayMode. The smoke now observes reload start, reload completion and a harmless client receipt before the host shuts down; the launcher mandates the new completion markers. Added aligned UTC step timestamps, without changing protocol v4 or deadlines.

Cloud static consistency PASS: 46 source/assets, nine assemblies, 58 GUIDs. Local compilation/tests/new build and expanded corrected loopback remain pending. No package/settings/security changes, local source fix, new branch or backup.

## 2026-11-08: bounded multi-infected waves candidate

Corrected revive baseline 90c7b28 passed clean compile, 117 EditMode / 28 PlayMode, rebuilt Windows player and a complete 43.745-second two-process run, including confirmed reload receipt before shutdown. Native Unity cloud Curl diagnostics remained in full logs. The nine generated local files were preserved and no player remained. Exact evidence is in LAN_LOOPBACK_AUTOMATION.md.

Added optional three-wave mode with 2/3/4 host-owned infected and five-second breaks. The original single-infected mode remains. Every enemy has independent host tick/cooldown and a round-unique ID. One survivor/revive owner and weapon gate persist across waves, preserving HP/ammo/rescue progress. Spawn placement checks world, connected players and other chosen enemies using a bounded deterministic search, failing closed if no complete placement exists. No client spawn, damage or target claim is accepted.

Protocol v5 carries bounded enemy-array and wave state (maximum 442 bytes), with exact-length/count/identity/phase validation. Four capsule objects form the fixed pool. Fifteen new core and four PlayMode cases bring expected totals to 132 EditMode / 32 PlayMode. The actual two-process harness retains all previous checks then clears all three waves in round four and confirms completion through a reload/miss receipt. No deadline was increased.

Cloud static consistency PASS: 48 source/assets, nine assemblies, 60 GUIDs. Independent review and exact remote checks accompany publication. Compilation/tests/player build/new wave loopback remain unrun in cloud. No package/settings/security changes, licensed-asset import, branch, backup or local source repair. Full game, real LAN, ten-player scale, animation and performance remain open.

## 2026-11-08: FPS/TPS camera and hands-on control candidate

Wave baseline 6b3df43 passed compile, 132 EditMode / 32 PlayMode, Windows build and the full three-wave two-process run, with the same preserved generated files and known native Unity cloud Curl diagnostics. Full evidence/fingerprints are in LAN_LOOPBACK_AUTOMATION.md.

Added a local V/menu FPS/TPS switch, sphere-swept shoulder camera, reticle convergence from the actual player eye, backwards-hit guard, third-person local placeholder body, compact captured HUD, bounded mouse-sensitivity slider and clear start/downed hints. View changes do not capture the mouse or alter player/round authority. Headless presentation stays disabled. No protocol or network behavior is intentionally changed.

Seven new physics/view-lifecycle PlayMode cases bring expected source totals to 132 EditMode / 39 PlayMode. Static consistency PASS: 50 source/assets, nine assemblies, 62 GUIDs. Compilation/tests/build, unchanged full network regression and actual rendered/manual camera checks remain pending. Root README was updated from the obsolete first-range-only description to distinguish the verified co-op baseline from this new candidate.

No private source asset, licensed Mixamo download, package/settings/security change, new branch, backup or local authored fix is included. Production assets require a separately verified import/provenance route; placeholder capsules are not represented as completed realistic characters.

Independent camera review found and corrected a presentation/authority crossover before publication: changing view originally called the broad snapshot visual application, which could rewind host enemy colliders to an older snapshot before the next fixed tick. View switching now touches only local-avatar visibility and camera placement. A seventh regression explicitly exercises newer-than-snapshot enemy physics and verifies position/active state is unchanged by repeated toggles.


## 2026-11-08: original depot floor native-import candidate

Camera eaf20a6 passed 132 EditMode / 39 PlayMode, Windows build, full two-process wave regression and actual programmatically selected FPS/TPS captures. Camera and loopback documents retain exact evidence, an initial Console-evidence retrieval failure, native cloud Curl caveats and clean restoration.

Selected original AST-GEO-MOD-005 runtime meshes/maps for a native FBX/PBR import route, without adding a glTF plugin or copying restricted character content. A visual-only 8×8 floor grid replaces the primitive ground appearance while keeping its continuous collider and every networking rule. Headless mode skips rendered art. Explicit scoped importer, shared URP materials, three LODs, missing-asset failures, original-resolution texture handling and eleven import/three runtime regressions form the new candidate. Expected totals143 EditMode/42 PlayMode, not new execution results. Source/export provenance and hashes accompany the packet. Local import, bounds/material/normal verification, tests/build/full loopback and actual surface captures remain required.

Before publication, independent integration review identified runtime-only PBR keywords as a player shader-stripping risk. Replaced constructed materials with serialized Resources material templates that retain required variants and texture dependencies. Export review separately caught Blender resetting material indices during texture-free FBX material replacement; the exporter was held for correction and pre-replacement population assertions before any floor upload. Neither issue is represented as an accepted runtime result.


## 2026-11-08: selected raw asset Git-attribute correction

Local be0c582 checkout preserved nine existing generated-file hashes but exposed a missed repository contract: global FBX/PNG LFS attributes conflicted with the new raw runtime blobs. Pull warned on all nine assets and later Git status failed while LFS tried a temporary write. Unity import/tests had not started, and bare Unity CLI discovery also required investigation. The floor candidate therefore remains unaccepted.

Appended exact-nine-file raw-binary exceptions, retaining every unrelated LFS rule and all asset bytes. Added a standalone Git-attribute check: 48 assertions pass in an isolated temporary repository, including unrelated FBX/PNG/OBJ controls. Cloud static65assets/9assemblies/80GUIDs and packet hashes still pass. No Git permissions, security settings, pointers, package or runtime code changed. A temporary Git experiment confirmed that copying incoming attributes only to a worktree fails a fast-forward preflight, while a guarded exact incoming index-and-worktree application can fast-forward; this is a tested recovery option, not permission to overwrite local edits.

## 2026-11-08: correct native floor texture shape, static import and LOD names

Candidate 8c56168 compiled and all nine runtime binary hashes matched, but native execution failed: 143 EditMode cases gave 132 passes and 11 failures; 42 PlayMode cases gave 34 passes and eight failures, all rendered failures stopping on the invalid floor material. No Windows build or loopback followed. The original nine local generated-file preservation check became 8/9: ProjectSettings.asset automatically returned to the HEAD form with empty preloadedAssets. The exact current bytes and unrelated local changes are retained; this correction does not restore or adopt that settings change.

Recovered model assertions identified unwanted Legacy Animation components on all three static FBXs. Read-only asset inspection verified expected model bounds, two submeshes and original triangle populations. All six PNGs were conclusively imported as UnityEngine.Cubemap (TextureCube shape 2), so Texture2D loads returned null and the materials could not resolve textures. Existing GUID-only metadata also produced unsupported schema-version warnings before Unity expanded it. These were native importer-contract defects, not corrupted binary files.

The cloud correction supplies complete schema metadata using the actual Unity 6000.6.5f1 serialization layout, preserves all GUIDs, explicitly selects Texture2D shape 1 and static animation type None, and enforces both in preprocess code. GetVersion is 2. Synchronous reimport now processes textures, models and both material dependencies in that order and checks loaded objects/shader/texture references before a completion marker. Native assertions and runtime errors identify individual properties. Static checks reject incomplete importer schemas and wrong texture shape; 15 in-memory malformed-metadata fixtures pass rejection without modifying any project file.

Separate export name correction avoids Unity's implicit _LOD grouping trigger. Filenames/GUIDs stay stable; internal Model and mesh names use Detail0/1/2. All Geometry child payloads and Model transforms are exactly equal to the previous exports, and six texture files are byte-identical. Blender source/material/UV/normal/bounds roundtrip checks pass. Runtime and native tests now reject nested imported LODGroups. No gameplay, network protocol, collider, package or project-setting change is authored.

Cloud static checks pass for 65 source/assets, nine assemblies and 80 metadata GUIDs, plus 48 Git-attribute assertions and the 15 malformed-schema/shape fixtures. Native corrected import, 143 EditMode / 42 PlayMode tests, player build, full v5 loopback and visual inspection remain unrun. Before local application, reconcile only the exact nine generated floor metadata paths under the approved scope; preserve the two generated material files, URPProjectSettings and all unrelated local state. Do not reset/stash, silently overwrite or claim acceptance from these cloud checks.

## 2026-11-08: explicit persistent floor PBR keyword setup

Native edde182 compiled and confirmed the Texture2D, static Animation and neutral LOD fixes, but its import gate stopped before tests/build/harness: both materials had disabled PBR keywords in memory despite valid declared shader handles, resolved texture references, and intact keyword names in raw material YAML. No new test pass is claimed for that revision.

The cloud correction adds scoped explicit material setup after dependency import. Only missing _NORMALMAP/_METALLICSPECGLOSSMAP local flags are enabled on the two approved depot materials; only changed materials are saved. One synchronous reload must retain flags and references or the operation fails. No runtime-only mutation, forced shader inclusion/global graphics change, retry loop or material-property reset is used. The Windows build now checks readiness read-only before invoking BuildPipeline.

Three regression cases operate on unsaved clones and verify recovery, idempotence, preservation of other flags/properties/references and failure on missing input. The Editor test assembly explicitly references the existing Editor helper assembly. Current expected native suites: 146 EditMode / 42 PlayMode. Cloud static/attribute/malformed-meta checks pass, but native setup, reload persistence, tests, build, full loopback and surface capture remain pending.

This source-only commit changes no imported binaries, metadata, material YAML, package or project settings, so it need not overwrite the existing generated import files during pull. Executing the setup deliberately saves the two named material assets and requires that scope in the user's requested upfront local approval. Preserve all other generated changes and report native serialization diffs rather than silently adopting them.


## 2026-11-08: executable cloud core verification and host frame capture

Approved official Microsoft SDK 10.0.403 was checksum-verified and installed only in the cloud workspace. Added the locked standalone harness under Tools/CoreTests. It compiles the real core separately for .NET Standard 2.1/C# 9 and runs the real CoreEditMode sources using NUnit/NUnitLite 3.14.0 on .NET 10.0.14. First baseline a0f56fe recorded 208/208 passing cases; a45dc7f added 11 production-component replication simulations and recorded 219/219. Those historical records remain intact.

The next seam adds HostCoopMission.TryCaptureFrame and a read-only exact-owner fire-gate binding check. It uses owned fresh poses, life/revive/wave state, explicit host target health and stable connection slots; it encodes one valid aggregate before committing a per-round sequence. Failed captures return no partial output, consume no sequence and cannot wrap. It does not advance simulation or substitute for correct host tick order. The existing active Unity range/transport/materials remain untouched.

Fifteen dedicated capture cases and the updated full mission simulation now pass in the cloud: 234/234, zero failures/skips/warnings and a clean separate-assembly build. An initial new-test lambda/discard naming error was caught by actual compilation and fixed before publication. No assertion was relaxed. Exact results are in HOST_MISSION_CAPTURE_RESULT.json. The README and mission docs now distinguish verified core tests, historical Unity acceptance, unresolved floor material setup and inactive mission features.

No VENOM files were touched. Native material acceptance remains pinned to 4b7e1ab under its existing approval gate; it still expects 146 EditMode / 42 PlayMode for that revision. Latest main has additional authored tests and is not a silent substitute. Unity compilation/physics/rendering, the current floor build and actual mission transport remain pending. Do not call the cloud simulation a ten-machine LAN result.
