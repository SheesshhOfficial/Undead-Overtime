# Undead Overtime

Unity project for the planned solo and 1–10-player LAN cooperative zombie shooter, with first-person and third-person views. The long-term setting is a realistic American city.

## Current playable scope

The first implementation is an **isolated, single-player mechanics test range**, not the game or a multiplayer build. It uses temporary primitives: movement, jumping, FPS/TPS switching, cover-aware hitscan, three stationary targets, and reset controls. There are no imported production assets, zombies, waves, networking, city content, or progression yet.

- Unity: **6000.6.5f1** (use the exact project version).
- Open `Assets/UndeadOvertime/Scenes/PrototypeRange.unity`, then enter Play Mode.
- The greybox is generated at runtime. Opening the scene in Edit Mode does not create or save gameplay objects.
- `Assets/Scenes/SampleScene.unity` remains the original template. The existing build-scene list is unchanged.

See [range controls, test commands, and acceptance checks](Documentation/PROTOTYPE_RANGE.md) and [the next multiplayer boundary](Documentation/NETWORKING_PLAN.md).

## Verification status

Cloud static checks cover text structure, assembly definitions, metadata GUIDs, and scene references. Unity import/compilation, EditMode/PlayMode execution, graphical behavior, and a standalone build must be verified with the installed Unity editor. Do not interpret static checks or test source files as a successful Unity test run.
