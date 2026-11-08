# Hands-on controls and camera readiness

## Current scope

The LAN arena now has a presentation-only FPS/TPS switch, a collision-aware shoulder camera, reticle-converging aim, a compact captured HUD, sensitivity control and clearer start/downed hints. This is a new candidate awaiting exact-revision execution. The wave baseline `6b3df43657689622e70189704072893b0e899ada` already passed compilation, 132 EditMode / 32 PlayMode, Windows build and the complete two-process wave scenario.

No original Blender asset or Mixamo character is imported by this change. Characters/enemies remain honest placeholder capsules. The private source library and restricted licensed downloads remain separate; a verified Unity import/material/rig route and distribution rights are required before copying selected runtime content into the public game repository.

## Start a local solo session

1. Use Unity 6000.6.5f1 and open `Assets/UndeadOvertime/Scenes/LanRange.unity`, then enter Play Mode. Alternatively launch the freshly built `Builds/LanRange/UndeadOvertimeLanRange.exe` normally, without smoke flags.
2. Leave `127.0.0.1` and port `7777` for a session on this PC and click Host. If the port is occupied, choose another free unprivileged port; do not alter firewall/security settings as a workaround.
3. Click Start round. For target practice, leave the encounter dormant. For combat, choose either Start infected or Start waves. Waves contain 2/3/4 enemies with two five-second breaks.
4. Click Capture mouse. The capture click is suppressed as a shot until the button is released. Press Esc to release controls and show session options again.

To test with another process on this same PC, launch another copy and Join the same address/port while the host remains in the lobby. Real LAN needs two authorized PCs and the host's actual private address, and is a separate unverified gate. The prototype is not for Internet exposure.

## Controls

- WASD: planar walking. No jump or slope movement in this LAN slice.
- Mouse movement: aim. Left button: fire while held; host ammo/cooldown/occlusion remain authoritative.
- R: reload. A reload immediately after a shot is supported; duplicate requests do not restart the timer.
- E held: revive the nearest eligible downed teammate within 2 m and clear line of sight for three continuous seconds. Revival restores 40 HP, not a full heal or ammo refill.
- V: switch first-person/third-person while controls are captured. The menu also offers a view-switch button.
- Esc or lost focus: release capture, stop held walking/revive intents and show the menu. Capture again to resume. A downed player cannot capture controls until revived.
- Mouse sensitivity: menu slider 0.03–0.35, default 0.12 degrees per input delta unit. It is a per-running-session presentation value, not a new saved system setting.

View mode and sensitivity do not change host position, movement speed, health, ammo, round identity or cursor capture ownership. View mode persists across round resets in that running instance. Clients choose their own view independently.

## Camera and shot contract

First person is located at the replicated player eye. Third person uses a local shoulder offset (0.55 right, 0.4 up, 3 behind), rotated by the view. A radius-0.18 sphere sweep from the eye stops the camera before world/target/enemy geometry, with a 0.03 skin. The local placeholder body is visible only in rendered third person when the resolved camera is at least 1.5 m from the eye; compression near a wall hides it to reduce head/reticle obstruction; teammate bodies remain visible. Player colliders remain disabled under the existing no-friendly-blocking rule.

The reticle ray chooses a point from the camera view. The aim vector converges from the authoritative player eye toward that point, fixing third-person offset aiming. A camera hit behind the muzzle cannot turn the shot backward. The host still derives the actual origin from its own player position and performs its own raycast: seeing around cover does not let a shot pass through cover at the muzzle.

There is no client prediction, camera smoothing or animation claim. Camera movement follows received positions and may visibly step at the snapshot cadence. Collision is a simple sphere sweep, not a polished cinematic camera system. Teammate visual overlap can occur because player colliders are intentionally non-blocking.

A view toggle changes only local-avatar visibility and the camera; it does not replay the latest network snapshot into enemy transforms. This preserves host physics that may already be newer than the last published snapshot.

Headless execution still constructs no renderers/materials and leaves the inactive camera at the player eye. It skips graphical camera work. Networking, wave simulation and the existing automated loopback scenario stay unchanged; protocol remains v5.

## Verification and review

Expected inventory: **132 EditMode / 39 PlayMode**. Seven new PlayMode cases cover view offsets and invalid inputs, camera-wall clearance, reticle convergence, retained muzzle occlusion, backward-hit prevention, switching view without mutating round/player state or capturing controls, and not replaying stale enemy snapshot poses into host physics when changing view. These are source expectations, not a claimed run.

Local acceptance requires compilation, all suites, new Windows build and the unchanged complete wave loopback. If supported camera/Game View capture is available, collect actual FPS and TPS screenshots from this revision, identify the capture method and scene state, and retain the corresponding logs. A static image does not prove input or collision behavior.

Manual acceptance remains necessary: capture click suppression; WASD and aim while toggling V; shoulder camera near walls/floor/targets; crosshair hit placement at short/long range; muzzle blocked while camera can see; Esc/refocus; UI resize/readability; downed/revived control recapture; independent views on host/client. Do not label these manual checks passed based only on the automated headless run.
