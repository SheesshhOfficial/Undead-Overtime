# Mechanics range: first additive slice

## Purpose and limits

This scene establishes the smallest inspectable movement/camera/shooting loop without requiring production art or a network package. All content is temporary, and all runtime code is under `UndeadOvertime.Prototype`. It is explicitly **solo only**.

The patch adds `Assets/UndeadOvertime`, documentation, and a standard-library Python static validator. It does not change the template scene, package manifest/lock, Input System project asset, renderer assets, project settings, or build list. There is no automatic editor startup hook or scene generation on import. Opening a different scene never starts this range.

## Run

1. Use the existing Undead Overtime checkout and Unity **6000.6.5f1**. Do not make a second project or upgrade the editor/packages.
2. Ensure Unity is closed and the checkout is on the expected branch with no user changes. Record `git status --short --branch` and `git rev-parse HEAD`.
3. Fetch the approved remote revision, compare it with the supplied expected commit, then fast-forward only. If the branch has diverged, stop instead of resetting or discarding files.
4. Open the project. Wait for import and compilation; capture any Console errors before attempting fixes.
5. Open `Assets/UndeadOvertime/Scenes/PrototypeRange.unity` and enter Play Mode. A camera, directional light, and volume are serialized; the range geometry, player, and targets are built only at runtime.
6. Exit Play Mode when finished. Do not save generated Play Mode objects into the scene.

If execution discovers a problem, return the exact editor version, revision, test XML/log, reproduction steps, and screenshot. Code changes belong in the authored patch, not an improvised local repair.

## Controls

| Action | Keyboard / mouse | Gamepad |
| --- | --- | --- |
| Move | WASD or arrow keys | Left stick |
| Look | Mouse | Right stick |
| Sprint (hold) | Left Shift | Left-stick press |
| Jump | Space | South button |
| Fire (hold) | Left mouse | Right trigger |
| FPS / TPS | V | Select / View |
| Reset range | R | Start / Menu |
| Release controls | Escape | East button |
| Capture controls again | Click, then release | Right trigger, then release |

The recapture press is consumed until it is released. Losing focus releases controls; returning focus does not automatically capture the pointer. Shooting resumes only after a separate press. Gravity continues while controls are released; this is not a game pause menu.

## Scene contents

- 32 m × 32 m floor, four 3 m boundary walls, two cover blocks, a 1 m calibration cube, and a firing-line marker.
- 1.8 m capsule controller with a 0.35 m radius. Built-in Ignore Raycast layer excludes its body from its own camera/fire casts while retaining normal movement collisions.
- FPS eye height 1.62 m; shoulder camera with collision clearance; 75° FOV.
- Three stationary orange targets, 100 HP each, 25 damage per hit. A fourth hit disables the target's renderer and collider. Reset restores them without respawning duplicates.
- A 100 m hitscan test weapon, 0.18 s minimum shot interval, short tracer, and hit marker. Camera aim is resolved from an offset inside the player's clearance, so a shoulder camera looking past cover does not directly damage a target through the cover.
- Temporary HUD showing current view, controls, shots, hits, and targets destroyed.

## Automated checks

Static check, no Unity required:

```text
python Tools/validate_prototype.py
```

Unity tests live in separate EditMode and PlayMode assemblies. Use **Window → General → Test Runner** to run both suites, or the CLI below with Unity closed. Set `$editor` to the actual installed 6000.6.5f1 executable. `Logs` is already ignored by the repository.

```powershell
$project = 'G:\Unity Projects\Undead Overtime'
$editor = '<verified path to Unity 6000.6.5f1\Editor\Unity.exe>'
New-Item -ItemType Directory -Force "$project\Logs" | Out-Null
& $editor -batchmode -projectPath $project -runTests -testPlatform EditMode -testResults "$project\Logs\prototype-editmode.xml" -logFile "$project\Logs\prototype-editmode.log"
$editExit = $LASTEXITCODE
& $editor -batchmode -projectPath $project -runTests -testPlatform PlayMode -testResults "$project\Logs\prototype-playmode.xml" -logFile "$project\Logs\prototype-playmode.log"
$playExit = $LASTEXITCODE
Write-Host "EditMode exit: $editExit; PlayMode exit: $playExit"
```

Do not add `-quit` to the test command: it can end the process before asynchronous tests finish. Inspect the XML results and editor logs, not just the process exit code. GUI execution is the fallback when CLI access is unavailable. A graphics-device failure is an execution blocker, not evidence of a gameplay failure or permission to change renderer settings.

Test coverage:

- **8 EditMode cases:** bounded health/damage, repeated death, reset, zero/invalid damage, invalid maximum health, and serialized scene/bootstrap/camera/missing-script checks.
- **6 PlayMode cases:** idempotent build, ground collision, actual four-shot hitscan and cooldown, cover occlusion, shoulder-camera wall clearance/FPS return, and reset state.

The two suites are committed test sources. Their actual results must be recorded after local execution.

## Manual acceptance checklist

- No compile errors or unexpected runtime Console errors. The startup log explicitly says no networking is active.
- Movement speed does not increase diagonally. Player cannot walk through walls or cover. Jumping lands on the floor; overhead collision cancels upward velocity.
- Switch FPS/TPS repeatedly while moving. Capsule is hidden in FPS, visible in TPS. Back up to each wall and orbit the camera; it stays inside geometry.
- Hit each target four times in both views. HUD increments accurately. Shoot cover and confirm targets behind it do not take damage.
- Reset after partial damage, all targets destroyed, movement, and camera switching. All targets return, counters clear, and the player returns to the spawn in FPS.
- Escape, click-and-hold to recapture, release, then click again. The recapture hold must not fire. Repeat after losing/returning application focus.
- Enter/exit Play Mode three times. No duplicate players, action-map leaks, stuck cursor, or growing generated hierarchy remains after exit.
- Test keyboard/mouse first. Test a gamepad if available; label it untested if unavailable.
- Capture one FPS screenshot, one TPS screenshot, Console status, and both test XML summaries. Do not claim standalone or LAN readiness from this scene check.

## Known omissions

No production meshes, weapon rig, animations, audio, zombie navigation, networking, health for players, death/revive flow, UI scaling pass, accessibility settings, rebinding, save system, player build, performance budget, or polished visual treatment. Materials are transient copies of the active pipeline's default material; a standalone build still needs shader/build validation.


## Separate solo encounter scene

`Scenes/SurvivalYard.unity` now opts into an isolated zombie-wave/extraction experiment. Read [SURVIVAL_YARD.md](SURVIVAL_YARD.md) for its different reload/restart controls and verification limits. This original mechanics scene remains the regression baseline with stationary targets and R reset.
