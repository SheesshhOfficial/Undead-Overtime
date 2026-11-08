# Solo Survival Yard: first combat-loop candidate

Date: 2026-11-08. **Authored in cloud; actual Unity compilation, tests and play acceptance are pending.** This scene is a small playable-loop implementation candidate, not the completed game.

## Scope

Open `Assets/UndeadOvertime/Scenes/SurvivalYard.unity` in the existing Unity 6000.6.5f1 project and enter Play Mode. The scene opts into survival mode on the existing range bootstrap; the original `PrototypeRange.unity` remains the stationary-target mechanics test. The default mode is still the original range. No automatic editor execution or scene mutation occurs when importing source.

The yard is intentionally an open 32 × 32 m temporary arena. It has no obstructing interior cover because its capsule enemies use direct pursuit with CharacterController collision, **not navigation/pathfinding**. It is not a city map or production level.

Implemented loop:

1. Three-second preparation period.
2. Three waves of 5, 8 and 12 temporary capsule infected, spawned at 0.8-second intervals with at most six active at once.
3. Infected follow the player, collide with the arena and other controllers, and attack at close range. World geometry between the infected and player blocks attacks. Prototype damage is 15, minimum attack interval 1.2 seconds.
4. Player has 100 health, a twelve-round magazine, 25 shot damage and a 1.5-second reload with unlimited reserve ammo. Infected have 100 health, so four body hits defeat one. These are test values, not final game balance.
5. A living player recovers up to 20 HP before subsequent waves. A cleared wave requires every scheduled enemy to have spawned and been defeated.
6. After the third wave, the green extraction marker activates at the south end. Stand inside the marked area and hold interact continuously for two seconds to win. Leaving or releasing resets the hold.
7. At zero health, the encounter stops and shows a loss. Restart rebuilds the encounter state without duplicating the player, director, or input map.

An enemy falling below the arena is removed as defeated to prevent a prototype soft lock. That fallback is not a production enemy-death/reward policy.

## Controls

| Action | Keyboard/mouse | Gamepad |
| --- | --- | --- |
| Move/look | WASD/mouse | Left/right stick |
| Sprint/jump | Shift/Space | L3/South |
| Fire | Left click | Right trigger |
| Reload | R | West |
| FPS/TPS | V | Select |
| Extract | Hold E | Hold North |
| Restart | T | Start |
| Release controls | Escape | East |
| Capture controls | Click, then release before firing | RT, then release |

In the **original mechanics range**, R still resets. The survival-only R/T bindings do not replace the original controls.

Releasing controls or losing application focus pauses survival enemy/wave/reload timers. Player gravity and cosmetic hit flashes may still settle while controls are released; this is not a global `Time.timeScale` change. Returning focus does not recapture the pointer. The recapture click is consumed and cannot fire. Terminal win/loss screens release the pointer and accept restart without requiring another capture click.

## Architecture and boundaries

- `WaveProgression`: deterministic counters and state transitions in the engine-independent core. Spawning/timing/identity deduplication remain in the adapter.
- `PrototypeMagazine`: solo clock-based ammo/reload/cooldown state. It is not network authority.
- `PrototypeSurvival`: transient director, bounded active-enemy list, spawn selection, extraction, cleanup and explicit simulation clock.
- `PrototypeZombie`: direct movement/contact attack adapter using the existing `PrototypeTarget` health/hitscan path.
- `PrototypePlayer`/`PrototypeRange`: opt-in health, reload, interaction, reset and HUD extensions. The original range keeps its targets, covers, unlimited shots and old control bindings.

The LAN host core is not wired into this solo loop. Client-side damage and the solo magazine must **not** be repurposed as authoritative multiplayer simply by adding a network component. This parallel solo experiment changes neither the LAN transport acceptance gates nor the need for host-owned movement/damage/spawns when that adapter is implemented.

No Mixamo data, Blender sources or third-party media are included. The green capsules are temporary gameplay placeholders, not substitute character deliverables. Mixamo character/animation integration, weapon presentation, art, audio, effects and gore remain separate work.

## Automated coverage

Five new core EditMode test methods cover wave caps/completion/terminal states/config copying and magazine timing/reload/reset. The scene test now covers both dedicated scenes and verifies their opt-in mode.

Seven new PlayMode tests cover:

- Survival construction and idempotent bootstrap.
- Spawn budget and rejection of false/duplicate death reporting.
- Actual pursuit, close attack, wall occlusion and attack cooldown.
- Actual four-shot hitscan against a moving enemy, ammo use and reload.
- Death freeze and repeated restart/cleanup.
- All three waves through actual spawned enemies, interrupted extraction and victory.
- Cursor-release pause.

The tests drive the director with bounded simulation steps where useful. They do not prove frame timing, crowd navigation, art, input-device support or real user play by themselves.

Run `python Tools/validate_prototype.py` first. It now checks both scenes, unique metadata, required assembly references and the mode boundary. It does not compile or execute C#.

Then use the Unity commands in [PROTOTYPE_RANGE.md](PROTOTYPE_RANGE.md) to run **all EditMode and PlayMode suites** on the exact published commit. Do not skip the original range or host-core tests. Keep XML/logs and report every failed/not-run case.

## Manual acceptance

1. Open the yard explicitly, enter Play Mode, confirm exactly one player/camera/director, and check Console for errors.
2. Test FPS and TPS movement, sprint, jump, shooting and reload. Hold fire through empty magazine and reload; ensure no free/duplicate shots and the HUD stays consistent.
3. Let an infected reach you. Observe attack cadence and HP loss. Move away and verify attacks stop outside range. Test the camera close to a wall and an infected in both views.
4. Clear all three waves. Confirm the maximum active count remains six and no wave advances while unspawned or living infected remain.
5. Begin extraction then release the key or leave the zone. Verify hold resets. Complete the hold; verify victory stops new shots/damage.
6. Lose intentionally, then restart from the released-cursor screen. Repeat restart during a wave, reload and extraction. Confirm no inactive/dead colliders linger or block new play.
7. Release controls, switch applications and return. Verify timers/enemies remain paused until controls are captured; the capture click must not fire. Repeat rapidly.
8. Exit/reenter Play Mode three times. No duplicated objects, stuck cursor, material leaks or action-map errors should remain.
9. Open the original mechanics range and run its manual regression checklist; especially R reset, four-hit target death and unlimited shooting.
10. Record one FPS and one TPS screenshot, a victory or defeat screenshot, editor version, revision and Console/test output. A standalone player build remains another test gate.

## Known limitations

Solo only; no networking, revive, save/progression persistence, missions/city, pathfinding or obstacle avoidance, production character animation, weapon rig/viewmodel, sounds, gore, pickups, settings/rebinding/accessibility UI, score/rewards, balance/playfeel tuning or measured performance. Enemy controllers can bunch behind one another; this open arena is a deliberate constraint. HUD styling and gamepad use need real-device review. Scene is not added to the build list automatically.
