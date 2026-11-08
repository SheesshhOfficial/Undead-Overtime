# Host-owned infected encounter milestone

## Scope and status

One optional infected encounter is added to the isolated LanRange arena. After the host starts a normal round, the host may click **Start infected**. Normal movement/target tests remain available without starting it. The enemy is an original capsule placeholder, not a Mixamo character or finished zombie art.

The previous moving-client revision `0f78c51a78ce8cd0d9bec6d77336d187fcd0d868` passed compilation, 94 EditMode / 22 PlayMode, Windows build and automated two-process loopback, including movement/expiry and prior lifecycle/combat checks. This new encounter is authored and statically reviewed; its own local acceptance remains pending until exact-revision results are recorded.

## Behavior

- One infected spawns at arena position (0, eye height 1.6, z 6) with 100 HP.
- It chooses the nearest current, alive survivor from host-owned positions. Stable connection order resolves ties. It pursues at up to 1.6 units/second with the same bounded host step and swept collision as walking.
- It stops about 1.1 units away and can melee within 1.3 units. Each accepted attack deals 10 HP, with a one-second cooldown shared by that enemy across all victims. A trusted host line-of-sight check must be clear.
- Players have 100 HP per round. At zero HP, host fire/reload admission and movement are disabled, pending walk intent is cleared, mouse capture is released and the HUD says DOWNED. The current slice has no revive or bleed-out timer.
- The enemy retargets another alive connected player when its previous target is downed or disconnected. If none remain, the encounter enters Failed and stops attacking/pursuing.
- Host-authorized raycasts can damage the infected for the existing 25 damage. Four accepted hits kill it and mark Cleared. Its collider is disabled on death, preventing a dead invisible blocker.
- Return-to-lobby/new-round resets encounter, health, ammunition and positions. Starting another encounter without a new round is rejected. Existing disconnect/rejoin identity checks still apply.

## Authority and physics

All enemy decisions, position, health, damage, cooldowns and clear/failure state live on the host. Clients display snapshots; they do not run duplicate enemy AI, choose targets, report hits or set player health. The transport sender is still mapped to its host-issued ticket. Downed players cannot continue acting by sending raw valid packets.

The host sweeps the enemy capsule against scene collision. Only its own collider is temporarily disabled within a try/finally during its sweep and melee line query, then restored. Player bodies are intentionally nonblocking, as in the walking slice. Obstacles block both pursuit and shooting; melee also requires line of sight.

This is direct pursuit with slide resolution, not navigation/pathfinding. An enemy can stop at a wall rather than find a route around it. No slopes, stairs, jumping, hordes, obstacle-avoidance navigation, animation, gore, sound, revives, missions or production performance claim is included. Movement remains unsmoothed host snapshots.

## Protocol v3

Both peers must rebuild from the same revision. Version-2 clients are rejected. Commands remain 44 bytes and contain no new client authority. Snapshot header is now 51 bytes, including one fixed infected state (phase, HP, position). Each member is 32 bytes after adding player HP; ten-member maximum is 371 bytes. Finite/bounded positions, known phases, HP 0–100 and phase/health consistency are checked on decode.

Enemy phases: Dormant (0 HP, hidden), Active (positive HP), Cleared (0 HP, hidden), Failed (remaining enemy HP, frozen). Player HP is separate from the three existing firing-target HP values.

## Acceptance

Expected source inventory: **104 EditMode cases and 26 PlayMode cases**. New core tests cover opt-in start, nearest target, host step bounds, clock rejection, attack range/occlusion/cooldown, killing, downing/retargeting/wipe, disconnects, movement lockout and bounded health/encounter serialization. New PlayMode tests exercise pursuit/injury/kill/reset, a downed host's actual action lockout, a physical wall blocking pursuit/shots, and a thin wall preventing melee even with the player inside attack range.

Preserve the nine known editor-generated files, fast-forward only without overlap, compile, run all suites and rebuild the Windows player. The [loopback harness](LAN_LOOPBACK_AUTOMATION.md) now retains the previous movement/firing/rejoin checks, then starts one infected. The client waits for replicated pursuit and injury, fires four shots, sees Cleared and acknowledges the result before host shutdown. The launcher requires the new encounter markers, so an old successful report cannot stand in for this scenario.

Return exact revision, build/CLI test evidence, both reports/full logs and cleanup results. Unity log-callback error counts do not cover every native log message; continue inspecting full logs for the known Unity cloud Curl failures. No execution-policy, firewall, telemetry or package-setting changes should be made as a workaround.

After this gate, next slices can add revive, bounded wave spawning and gameplay objectives. Real-LAN tests, ten-player load and visual/manual input acceptance remain independent gates.
