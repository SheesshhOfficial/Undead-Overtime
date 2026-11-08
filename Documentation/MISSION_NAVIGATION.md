# View-relative mission waypoint aid

## Candidate scope and status

This commit publishes a **cloud-tested candidate, not yet natively accepted** small gameplay-readability change on top of `a150bc3e218b099f8b50b0bbc0b5347c7a4f803c`. The accepted player remains `917d94`; the user may be playing it. No PC, running player, editor, build or local repository action is part of this preparation. The separately published replay validation retains its own pinned acceptance.

The new helper is wired directly into the existing mission HUD and menu, not an unused subsystem. It shows a line such as **Waypoint: Power console | 3 m | ahead** while restoring power, and a corresponding extraction hint after defense. This helps a first-time player find the two fixed objective locations in the greybox.

## Read-only behavior

- Uses the current replicated mission phase and local player's replicated eye position, plus the local camera's horizontal facing. It supports the existing first- and third-person view without moving the camera or player.
- Shows one of eight relative directions: ahead, ahead-right, right, behind-right, behind, behind-left, left, ahead-left. Within 0.5 meters of the point it says nearby. Display distance rounds upward to whole meters; the core value retains precise planar distance.
- This is a fixed-objective compass, not a route planner. It can indicate a point behind walls, and does not prove line of sight, legal interaction range, fresh input, hold completion, a clear path or whole-team extraction readiness. Existing host checks remain authoritative.
- No enemy waypoint or hidden-enemy reveal is added. During defense, completion/failure, lobby/disconnect or local downing, the waypoint line is absent. Other objective/HUD messages remain.
- Invalid identity, stale round flag, incompatible phase/gate/hold, invalid arena coordinates, non-finite or near-vertical/degenerate facing all return no hint. No normalization can overflow into a rendered NaN.
- OnGUI still returns immediately in headless mode. This adds no renderers, materials, file writes, packets, input capture, saved settings or external service.
- The captured HUD reserves one additional line (205 pixels rather than 180 when a hint exists). Native visual/layout review remains required; syntax and numeric tests do not establish UI acceptance.

## Verification performed

Actual production core compiled separately for .NET Standard 2.1/C# 9 and ran **310/310 NUnit cases**, zero warnings/errors, failures, skips or inconclusive results. This includes 26 new cases for all eight bearings, four rotated frames, angle boundaries, near threshold, pitch/scale invariance, extraction selection, excluded phases and invalid values. Source/lock hashes stayed unchanged. Exact timestamps and source/result hashes: `MISSION_NAVIGATION_CORE_RESULT.json`.

Static validation: 84 source/assets, nine assemblies, 99 metadata GUIDs; 48 Git-attribute assertions and 15 malformed importer fixtures pass. C# 9 syntax-only parse: 61 files, zero syntax errors. None of these substitutes for native Unity/NGO API binding or rendering.

A new authored PlayMode case reads the waypoint in opposing camera directions, verifies exact current-frame and position non-mutation, intact health/ammo/gate/capture state, then checks empty guidance after lobby and stop. The spawn comparison uses tolerance for Unity float conversion; non-mutation compares the same published double-valued pose exactly. The existing power-flow test also requires the waypoint to disappear during defense.

## Future guarded native acceptance, not requested while the user plays

Once the user is finished or explicitly authorizes interruption, coordinate with the sole local executor. Preserve all dirty local files and pin the exact published revision. Expected inventory after this change: **334 EditMode /67 PlayMode**. Run native compile and full suites first; then one rebuild and the required range/expanded mission loopbacks if all gates pass. Finally, use authorized actual Game View capture to inspect readable waypoint text in FPS/TPS, opposite facing, menu/capture transitions and extraction. Do not claim any of these native checks passed yet.

No change to mission authority, weapons, collision, protocol, timing, asset/import settings, scene serialization or the running accepted build is intended. Manual input quality and visual review remain separate from automated loopback acceptance.
