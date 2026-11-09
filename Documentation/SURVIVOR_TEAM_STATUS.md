# Survivor team status and private character preparation

## Scope

This candidate adds a read-only co-op team panel to the existing playable range/mission. It displays actual host-confirmed slot numbers, health, downed state, straight-line planar distance from the local player, and the greatest **individual** reported rescue channel with its rescuer slot. Equal progress selects the lower slot deterministically. Different rescuers' percentages are never added, advanced locally or treated as permission to revive.

This is survivor usability work, not a skinned character or named-survivor implementation. Capsules remain. No model, animation, weapon, controller, aim/facing field, movement rule, damage rule, revive range/timer, packet or authority changes are included.

The team panel appears for two or more players during a round and remains available after downing releases mouse capture. It is separate from the local health/ammo HUD. A menu preference hides only the panel; it does not change or discard host data. The panel and main menu scroll when needed. The separate roster requires at least **640x480**; smaller views show only a compact downed-count line in the existing HUD/menu, preserving the data without overlapping a second panel. At supported sizes the menu reserves space above the panel. This policy is unit-tested, but actual rendered layout acceptance is still required.

## Data and lifecycle

`CoopTeamStatus` projects only an active, identified round with a unique local member, unique slots/peers/connections, bounded health/positions and valid rescue references. Rows are immutable and sorted by slot. Slot numbers are temporary session identifiers, not persistent character identities. Rejoining, disconnecting, healing or restarting does not reuse historical HUD state.

`LanRange.CurrentTeamStatus` caches only the immutable accepted snapshot object, not a timer or authoritative state. Lobby, stopped and disconnected views immediately return no roster. The headless path can query data but creates no GUI, renderer or character. The renderer never writes physics, resends a snapshot or sends a gameplay command.

Distances are straight-line estimates, including through walls. They are **not** a route length, line-of-sight check or proof a teammate can be revived. The host still checks proximity, sight, input and time. Rescue guidance is shown only with a living teammate, an active/cleared encounter and a nonterminal mission. A wipe, failed encounter or terminal mission shows restart advice instead of promising an impossible rescue. Rescue percentages are latest host reports, not predicted completion; network v5 has no new snapshot timestamp/freshness field in this change.

## Validation

The full cloud core suite passed **466/466** cases, with zero failures, warnings, skipped or inconclusive cases, using the approved SDK 10.0.403 and the pinned NUnit 3.14.0 harness. Forty-nine cases are new team-status checks, including small-viewport policy and failure guidance. Exact sources, lockfiles and result hashes are recorded in [SURVIVOR_TEAM_CORE_RESULT.json](SURVIVOR_TEAM_CORE_RESULT.json). The initial uncached restore did not complete; the successful run used the previously restored official packages, with locked restore and source/lock identity checks intact. This is not Unity compilation or rendering acceptance. Authored Unity tests cover actual host roster data, caching, preference independence, lobby/round/stop lifecycle and unchanged protocol bytes. They do not prove two-peer GUI rendering or ten-player network acceptance.

The next native batch must include:

1. Preserve all existing dirty metadata, materials and settings; fast-forward only after non-overlap checks.
2. Discover and run all current EditMode and PlayMode tests, with results for the exact candidate.
3. Build once and run the existing range/mission loopbacks after suites pass. No protocol or harness changes are intended.
4. Actual rendered two-player observations at 1280x720: local/remote slot labels and HP, a downed row, rescuer slot/progress, rescue cancellation, revive, disconnect/rejoin, and no panel for a solo game.
5. With capture released/downed, verify the main menu and panel scroll without overlapping or hiding disconnect/capture controls. Check the preference and 10-row synthetic UI separately from ten actual clients. Do not claim graphical acceptance from the headless harness.

No user-PC activity or native acceptance occurred during cloud authoring. Licensed SWAT/zombie source packets remain in the private extras repository; no such binaries enter this public change.

## Final combined candidate inventory

The private-content boundary adds 16 passing synthetic Git regressions and 12 authored Unity EditMode cases. Aggregate static checks, 33 malformed importer fixtures, 96 Git-attribute assertions and diff whitespace checks pass. Independent HUD review identified and verified fixes for short-view overlap and impossible post-failure rescue hints.

Expected native inventory at this combined candidate is **514 EditMode / 121 PlayMode**, including the previously unrun infected presentation changes. Discovery must verify these counts rather than treating them as executed. Existing protocol v5 range and mission harnesses are unchanged. No private directory or content is created by this commit. See [private-content boundary](PRIVATE_CONTENT_BOUNDARY.md) for the default build guard and its unrun native dispatch tests.
