# Native depot acceptance: a063050

This records the local execution coordinator's verified results for **a0630500599742cb3a4d64ebda7a6719815ba28d**, not the later infected presentation or team HUD code. The cloud author did not execute Unity or recapture these files during this documentation update.

## Automated acceptance

- Unity 6000.6.5f1: fresh compilation clean; discovered inventory 383 EditMode / 112 PlayMode.
- EditMode: 383/383 passed, 29.01 seconds, zero failed/skipped/inconclusive.
- PlayMode: 112/112 passed, 142.54 seconds, zero failed/skipped/inconclusive. Rendered brick and feedback cases actually ran.
- Windows player built once. Fresh `UO_LAN_BUILD_OK` marker sequence 526 at 2026-11-09T10:36:57.3447462Z. Build Console: zero errors, 42 warnings. Unchanged Unity EXE stub was not used as sole freshness proof.
- Default range loopback: run ddc96f18b07b499e979d01f9f3596c3c, all 29 host / 21 client markers; both exits 0.
- Mission loopback: run 6259182bc89045a192a20b11475952d4, all 17 host / 15 client markers, client connection 2 → 3; both exits 0.
- Both runs: reports success, managed warning/error counts 0; complete log diagnostic scan found 0 matches. This is two processes on one PC, not separate-PC LAN or ten clients.

Native artifact receipts (project-local Logs):

| Artifact | Bytes | SHA256 |
|---|---:|---|
| acceptance-a063-editmode-383-response.json | 74351 | 8ab25fd1efc1751981f358d2b8b085434efc6a3fb7f251515c3be179083d9e90 |
| acceptance-a063-playmode-112-response.json | 22565 | fc21db0b930a8be50b52ad0c6522303da9d3cd4da2da4cbdf361010703c37704 |
| acceptance-a063-build-console-response.json | 91490 | 8a7a84bd389bc865a6d07b8e92456f484d254cb54af1843b788f4188de2afe73 |

Managed build fingerprints:

- Core.dll: 49,152 bytes / de4320689bd29070f5701205bbec1faadd1795f20ff82d6ada6f1cbccea957f3.
- LanPrototype.dll: 112,128 bytes / 11acf86849542599fd5712988cd72493ad18cfbb6d51fe79496a75f25a7f8fac.
- Prototype.dll: 30,208 bytes / f075b582d6daea540ca316a26a31e102b7e9ef72d3abb21c230db2a5e635ddb7.

## Actual visual review

An explicitly approved temporary Play Mode session produced actual 1280x720 FPS, TPS and elevated inspection-overview captures. Frames advanced in RestorePower with zero enemies. The coordinator inspected visible brick perimeter/foundation, floor/concrete, cabinet, closed gate, markings and extraction area; no obvious missing/pink materials. The result remains a sparse prototype, not final art quality.

Project-local screenshots:

- Logs/acceptance-a063-fps.png: SHA256 6c66a5e20b9117c21e511507410b6baf92c79ea35d07485809d6791f1d2a8726.
- Logs/acceptance-a063-tps.png: SHA256 8c10ac60a4836b505c036c162ee0adc07037392741c185355056da33be4cc1b1.
- Logs/acceptance-a063-inspection-overview.png: SHA256 2355155fcda7f892b3c4ec08765f72f3451c996716d8e59c16f1fa9e39e8da5b.

Explicit forced-LOD corner/seam views and combat/damage visuals were **not performed**. Do not describe those as complete visual acceptance. No character, grounding, manual full-input, separate-PC LAN, ten-player or performance claim follows.

## Preservation and cleanup

The original 29 protected file hashes survived the initial pull/tests. Build produced the previously documented InputSystem preload serialization transition. Six new facade importer metadata files normalized and became protected; binary packet hashes remained exact. Final main/origin target was a063050, index clean, 33 status entries, no unexplained drift. No reset/stash/source repair was performed locally.

After captures: host/connection stopped, Play Mode off, background false, temporary camera removed, original scene clean with matching hash; all protected hashes remained unchanged. Local metadata/material/settings edits remain preserved for deliberate later reconciliation.
