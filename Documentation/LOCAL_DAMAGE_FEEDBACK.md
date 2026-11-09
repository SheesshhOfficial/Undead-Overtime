# Host-confirmed local damage feedback

## Scope

This cloud candidate adds feedback for the local survivor's **observed health loss**, in both the default range and depot mission. It does not add damage, damage direction, shooter attribution, a weapon hit marker or client prediction. Host rules, packets and imported art are unchanged. The earlier brick facade and this feedback still await their combined native validation; the last accepted PC baseline is b7e07802.

Every accepted playing snapshot updates a small engine-independent tracker, including while the mouse is released. Only the selected local peer is compared, within the same session, round and connection identity. An initial/rejoined snapshot establishes a baseline without inventing a hit. A positive-to-positive health reduction starts a 0.65-second linear fade; the displayed loss may combine several host hits between snapshots. Duplicate health updates do not extend the fade.

Healing, revival, downing, lobby/reset, disconnect or missing/invalid local state clears old damage feedback. Downed survivors keep the existing explicit downed/revive instructions rather than a red pulse. A new round or reused peer with a different connection cannot inherit old feedback. The tracker reads a monotonic local clock and fails closed for invalid/backward times; it is not a network freshness validator because RangeSnapshot has no snapshot tick.

## Presentation

- A healthy injured survivor sees `HIT: -N HP` in the captured HUD or open menu while the fade is active.
- During captured gameplay, four narrow translucent red screen-edge rectangles fade away. The centre and reticle stay uncovered. No full-screen flash, camera shake, new sound, texture allocation or damage-direction arrow is added.
- The **Damage border (text feedback stays on)** toggle is available in the connected menu before or during a round. It changes only this border. It does not hide health-loss text, affect host state or write preferences/settings to disk.
- The preference survives round reset/disconnect on the current component; a new game process/component starts with the border on.
- GUI colour is restored after drawing. No feedback rendering occurs on a null graphics device. The core tracker still runs there so acceptance can inspect it without graphics.

## Changes and tests

New core `LocalDamageFeedback` and 24 core cases cover fade endpoints at several clock origins, duplicate updates, repeated damage, health increase, downed/revive, session/round/peer/connection changes, another player's health, malformed state, invalid/backward clocks, explicit reset and snapshot byte immutability.

New runtime `LanRange.Feedback.cs` is wired only into accepted snapshot consumption, session cleanup and existing OnGUI. Two native lifecycle cases check the presentation preference and disconnect reset. Existing native infected cases now additionally require confirmed local injury feedback, retained text with the border off, expiry after combat and quiet downed/new-round state. These assertions are authored, not yet executed on the offline PC.

`LOCAL_DAMAGE_CORE_RESULT.json` records the final pure-core run. An initial run passed all 334 test cases but the harness correctly rejected its source-integrity gate because a final boundary correction overlapped execution. It is not the accepted result; the frozen-source rerun is the verification receipt.

Expected combined native discovery: **370 EditMode** (334 core +9 solo +27 LAN) and **112 PlayMode** (99 LAN +13 solo). Run fresh compilation/discovery before accepting counts. Preserve all unrelated local generated files, including the six normalized cabinet metadata files. No floor/cabinet reimport or material save is required by this slice.

After passing both native suites, rebuild once and run the existing range then mission two-process harnesses. Their protocol, commands and required step markers are unchanged. Inspect a real rendered injury in FPS/TPS, border off/text on, Esc/menu during damage, downing, revive and round reset. Edge visibility/readability and comfort are not established by core or headless tests. No separate-PC LAN, ten-player or performance claim is made.
