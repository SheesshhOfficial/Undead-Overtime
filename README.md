# Undead Overtime

Unity prototype for a planned Windows solo and 1–10-player LAN co-op zombie shooter in an original American-style city. This repository contains the game implementation; it is not yet a finished game.

## Current acceptance state

**Current main is ahead of the last accepted Unity build.** The original depot-floor material import gate is still unresolved locally. Do not treat the quick-start below as a verified launch of current main, reset a dirty checkout, or save materials without the required local scope. The native acceptance candidate remains pinned to `4b7e1ab63201afabd3db172ebc4eaf87447eb1dd`; newer mission code and cloud test reports do not silently replace it.

The last accepted Unity camera baseline is `eaf20a6cb99dbe4b4e7102b9a655f8a45445f918`. It passed 132 EditMode / 39 PlayMode, Windows build, two-process loopback and inspected FPS/TPS captures. This describes historical evidence, not an instruction to change the user's checkout.

The inactive mission rules, coordinator and codec now have executable cloud core coverage. See [cloud verification](Documentation/CLOUD_CORE_VERIFICATION.md), [mission replication tests](Documentation/COOP_MISSION_REPLICATION_TESTS.md) and [host frame capture](Documentation/HOST_MISSION_CAPTURE.md). Those checks are not Unity physics, materials, scene wiring or real LAN acceptance.

## Playable prototype

Use **Unity 6000.6.5f1** and open `Assets/UndeadOvertime/Scenes/LanRange.unity`.

The isolated LAN arena supports host/join, planar movement, host-authoritative shooting/reload, single infected or three bounded waves (2/3/4 enemies), damage/downing, timed teammate revive, intermissions and round reset. Current camera/control work adds a local FPS/TPS switch and clearer HUD. Models are temporary primitives; the city, production characters/animation, missions, audio and polish are not integrated.

Quick start: enter Play Mode → Host on `127.0.0.1:7777` → Start round → Start waves → Capture mouse. WASD moves, mouse aims/fires, R reloads, E revives, V switches view, Esc releases capture. Join a second process before the host starts a round. See [the hands-on guide](Documentation/LAN_CONTROLS.md).

Build explicitly with **Undead Overtime > Build LAN test player (Windows)**. The output is `Builds/LanRange/UndeadOvertimeLanRange.exe`. This helper includes only LanRange and does not rewrite the existing build-scene list. Normal launch is manual; automation requires explicit opt-in flags.

The original `PrototypeRange.unity` mechanics scene and `SurvivalYard.unity` solo scene remain separate and preserved. `Assets/Scenes/SampleScene.unity` is the original template. Runtime greyboxes are generated on entry; simply opening a scene in Edit Mode does not save generated geometry.

## Verified baseline and current candidate

Wave baseline `6b3df43657689622e70189704072893b0e899ada` passed compilation, 132 EditMode / 32 PlayMode tests, Windows build and an actual two-process loopback test covering movement, combat/reload, rejoin/reset, downing/revive and all three waves. Full logs retained Unity cloud Curl diagnostics, documented separately from zero gameplay callback error counts.

The FPS/TPS camera revision eaf20a6 passed 132 EditMode / 39 PlayMode, build and full loopback, with actual rendered views inspected. The separately pinned floor material candidate expects 146 EditMode / 42 PlayMode and still needs its own acceptance; see [native floor integration](Documentation/DEPOT_FLOOR_INTEGRATION.md). Real LAN, ten-player scale, hands-on input quality and performance remain unverified. Do not expose this unauthenticated prototype to the Internet.

## Documentation

- [Agent workflow and repository boundaries](AGENTS.md)
- [LAN runbook](Documentation/LAN_RANGE.md)
- [Co-op waves](Documentation/LAN_HORDE.md)
- [Revive and reload correction](Documentation/LAN_REVIVE.md)
- [Two-process automation and exact execution evidence](Documentation/LAN_LOOPBACK_AUTOMATION.md)
- [Implementation history](Documentation/IMPLEMENTATION_LOG.md)

Run `python Tools/validate_prototype.py` for cloud static consistency and `python Tools/CoreTests/run_core_tests.py` with the approved pinned SDK for pure-core compilation/tests. Compile, run tests and build with the actual Unity editor. Preserve local user/editor changes and follow the exact-revision cloud-authoring/local-execution workflow.
