# Native mission placement-failure boundary correction

## Failed native candidate

On 2026-11-08 the local execution coordinator tested exact main revision `d2b035b761245a8092c145485a79892be2774977` in Unity 6000.6.5f1. Guarded fast-forward preserved the existing dirty files. Native compilation passed; all **305 EditMode cases passed**. PlayMode finished with **64 passed / one failed / 65 total**, zero skips or inconclusive cases, in 125.52 seconds.

The failing case was `LanMissionTests.FullyBlockedWavePlacementFailsMissionClosed`, duration 3.6072961 seconds. Its `HoldPower` helper asserted that `SendPowerIntent(true)` returned true at line 58, but it returned false. The other eight mission cases passed, including the actual solo power/waves/extraction flow. This is useful partial coverage, not full candidate acceptance.

Native response artifacts retained by the local executor:
- `Logs/mission-d2b035b-editmode-response.json`: full EditMode response.
- `Logs/mission-d2b035b-playmode-status-06.json`: completed PlayMode response, 13,178 bytes, SHA-256 `88e07f4439ea1d564ddc6a8df953acde92c5ef2e194988545ba69670837683d8`.

No Windows build or either loopback was run for this candidate after the failed gate. No local code repair or test retry was performed.

## Diagnosis and bounded test correction

The production host rejects new actions immediately when its owned mission becomes Failed. The public `CurrentMission` frame is updated after the coherent host tick at approximately 10 Hz. The helper was polling that published frame and assuming power submission must succeed while the prior RestorePower frame was still visible. Deliberately blocking every spawn can therefore cause the correct terminal rejection before the next failed frame reaches the helper.

Only the negative placement test now opts into a bounded publication wait when submission is rejected. It must remain connected and Playing in the same session and round, and receive a **newer Failed frame within 300 milliseconds**. A disconnect, null state, round/session change, DefendDepot frame, old sequence, or missing terminal publication still fails. All other helper callers still require successful submission. The caller still requires both mission and horde Failed, then checks that held/released power, fire, reload and movement are rejected before lobby cleanup.

No production gameplay, authority, publish cadence, protocol, thresholds or timeout was changed. There is no runtime test hook. Native test inventory stays **305 EditMode / 65 PlayMode**.

## Correction verification

- Independent read-only source review confirmed the authority/publication boundary and recommended checking newer sequence and retained connection/round binding.
- Actual unchanged production core rebuilt and executed with the approved workspace-only .NET SDK: **281/281 NUnit cases passed**, zero build errors/warnings, skips or inconclusive cases. This core run does not execute the changed Unity coroutine.
- Core run UTC: 2026-11-08T21:20:55.8364548Z to 2026-11-08T21:21:14.8106116Z; duration 18.974103 seconds. Result XML SHA-256: `04198dd2d9b172c3afce9da588c0cf6928cf924ef09ba6b79c1e984b29a92443`.
- Static consistency: 82 source/assets, nine assemblies and 97 metadata GUIDs passed; all 48 Git-attribute assertions and 15 malformed-importer fixtures passed.
- Roslyn C# 9 syntax-only parse: 59 files, zero syntax errors. This is not Unity/NGO API compilation.
- Corrected native PlayMode, player build and loopbacks remain **unrun** pending guarded retest. No real LAN, ten-player, visual mission or performance claim.

## Exact-revision native retest

The coordinator should pin the correcting commit, verify its four changed paths have no overlap with protected local files, and fast-forward without reset/stash. Do not reimport floor assets or modify materials/settings. Run native compilation, full 305 EditMode and 65 PlayMode cases once, preserving each complete response before the next suite. Stop on failure and return exact evidence. Only after both suites pass, perform one explicit Windows rebuild, then one existing `-Mode range` smoke and one `-Mode mission` smoke with the exact correcting revision. A passing range run is required before the mission run. Preserve cleanup, managed DLL fingerprints, per-peer reports and full log caveats. No screenshots are required in this acceptance pass.
