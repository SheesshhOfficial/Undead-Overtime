# Undead Overtime

Unity prototype for a planned Windows solo and 1–10-player LAN co-op zombie shooter in an original American-style city. This repository contains the game implementation; it is not yet a finished game.

## Current acceptance state

The accepted native mission baseline is **917d94**: clean compilation, **305 EditMode /65 PlayMode**, one Windows build and successful default-range **and opt-in mission** two-process loopbacks. Remote power, replicated gate, three co-op waves and whole-team extraction passed on loopback. [Exact native evidence and limits](Documentation/NATIVE_ACCEPTANCE_917D94.md).

Select **Depot mission** before Host/Join on every peer; restore power with F, survive three waves, then gather at extraction. Range mode remains the default. [Mission controls and authority](Documentation/PLAYABLE_DEPOT_MISSION.md).

**Current candidate builds the depot area before combined testing**, as requested: view-relative objective guidance, original concrete on partitions/perimeter, gate relief and the selected original electrical cabinet with three LODs and a measured collision proxy. It also expands mission replay/reconnect checks. These changes are cloud-authored and source-checked, **not yet accepted in native Unity**. [Combined depot acceptance plan](Documentation/DEPOT_CONSOLE.md). The accepted player remains the 917d94 baseline above; preserve local files and do not reimport or save the existing floor materials.

## Playable prototype

Use **Unity 6000.6.5f1** and open `Assets/UndeadOvertime/Scenes/LanRange.unity`.

The isolated LAN arena supports host/join, planar movement, host-authoritative shooting/reload, single infected or three bounded waves (2/3/4 enemies), damage/downing, timed teammate revive, intermissions and round reset. Current camera/control work adds a local FPS/TPS switch and clearer HUD. The accepted original floor is textured; the later cabinet/concrete candidate awaits native validation. Many other models remain temporary primitives. The city, production characters/animation, story, audio and polish are not integrated. The accepted opt-in mission and current validation candidate are described above.

Quick start: enter Play Mode → Host on `127.0.0.1:7777` → Start round → Start waves → Capture mouse. WASD moves, mouse aims/fires, R reloads, E revives, V switches view, Esc releases capture. Join a second process before the host starts a round. See [the hands-on guide](Documentation/LAN_CONTROLS.md).

Build explicitly with **Undead Overtime > Build LAN test player (Windows)**. The output is `Builds/LanRange/UndeadOvertimeLanRange.exe`. This helper includes only LanRange and does not rewrite the existing build-scene list. Normal launch is manual; automation requires explicit opt-in flags.

The original `PrototypeRange.unity` mechanics scene and `SurvivalYard.unity` solo scene remain separate and preserved. `Assets/Scenes/SampleScene.unity` is the original template. Runtime greyboxes are generated on entry; simply opening a scene in Edit Mode does not save generated geometry.

## Verified baseline and current candidate

Wave baseline `6b3df43657689622e70189704072893b0e899ada` passed compilation, 132 EditMode / 32 PlayMode tests, Windows build and an actual two-process loopback test covering movement, combat/reload, rejoin/reset, downing/revive and all three waves. Full logs retained Unity cloud Curl diagnostics, documented separately from zero gameplay callback error counts.

The FPS/TPS camera revision eaf20a6 passed 132 EditMode / 39 PlayMode, build and full loopback, with actual rendered views inspected. The floor material revision passed 146 EditMode /42 PlayMode, build, loopback and actual rendered captures; see [native floor integration](Documentation/DEPOT_FLOOR_INTEGRATION.md). Real LAN, ten-player scale, hands-on input quality and performance remain unverified. Do not expose this unauthenticated prototype to the Internet.

## Documentation

- [Agent workflow and repository boundaries](AGENTS.md)
- [LAN runbook](Documentation/LAN_RANGE.md)
- [Co-op waves](Documentation/LAN_HORDE.md)
- [Revive and reload correction](Documentation/LAN_REVIVE.md)
- [Two-process automation and exact execution evidence](Documentation/LAN_LOOPBACK_AUTOMATION.md)
- [Implementation history](Documentation/IMPLEMENTATION_LOG.md)

Run `python Tools/validate_prototype.py` for cloud static consistency and `python Tools/CoreTests/run_core_tests.py` with the approved pinned SDK for pure-core compilation/tests. Compile, run tests and build with the actual Unity editor. Preserve local user/editor changes and follow the exact-revision cloud-authoring/local-execution workflow.
