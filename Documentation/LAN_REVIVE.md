# Host-owned co-op revive milestone

## Scope and status

A living player can rescue a downed teammate by standing within 2 metres and holding E for 3 continuous seconds. The host checks all eligibility, timing and line of sight. A successful revive restores 40 HP at the downed player's current position, without refilling ammunition. The player can capture controls again to move/fire.

This is a candidate extension to the tested one-infected arena, not waves or finished co-op gameplay. Baseline `5aaf489bf34e65a8dacaed31c582a822c078f24a` passed compilation, 104 EditMode / 26 PlayMode, Windows build and actual two-process infected pursuit/injury/kill. Revive revision `cae6b4db487acdf44c41a8ceaff1ec1b945fbfe3` compiled and passed 117 EditMode / 27 PlayMode cases, but its complete loopback acceptance failed at the final reload acknowledgment. Both peers observed the revive and the client restored movement/fire. This correction separates reload pacing from fire and strengthens the final receipt; its exact-revision execution is pending.

## Host contract

- The client sends only held/released revive intent, plus existing session/round/sequence identity. It does not name a victim, position, duration or health result.
- The host chooses the nearest current downed teammate within 2 metres. Self, living players, stale tickets and disconnected players are ineligible. Equal-distance ties keep stable connection order.
- The rescuer must be alive, in an active or cleared encounter, with a fresh accepted hold update. Normal input refreshes at 20 Hz. Missing input for 250 ms cancels the channel. A single packet cannot become a delayed revive.
- Three uninterrupted host-time seconds with clear eye-height line of sight are required. Release, range loss, target change, obstruction, walking intent, firing, reloading or taking damage resets progress. Fresh release bypasses pacing; dropping a release still cannot exceed the hold-expiry window.
- Taking damage cancels held state. If E remains pressed, a later accepted refresh may start a fresh channel; previously accumulated time is not retained.
- Two rescuers can channel toward one victim, but only the first valid completion changes zero health to 40. The other channel becomes ineligible, with no extra healing.
- Downed players cannot rescue themselves or others. If everybody is down, the encounter stays Failed and the host must restart the round.
- Revive is allowed after the infected is cleared, so the rescuer can first remove the threat. It does not restart the infected or give extra ammo. Movement's held/pending input was cleared when the player was downed; fresh input is needed afterward.
- Health, revive progress and completion are host snapshots. Clients do not run an independent healing timer. Progress is shown for the selected teammate; no timer UI is proof of completion without replicated health.

## Protocol v4

Both peers must rebuild. Older handshakes are rejected. Commands remain 44 bytes; action 4 accepts only the vector encodings (0,0,1) for held and (0,0,0) for release. No target field is accepted.

Each snapshot member gains target-slot and progress bytes: 255/0 means no current channel, otherwise a different existing downed slot and progress 0–99. The rescuer must be alive and encounter phase Active or Cleared. Completion clears the channel and changes target HP to 40. Member size is 34 bytes, header stays 51, maximum payload 391 bytes for ten peers.

## Tests

Expected source inventory: **117 EditMode cases and 28 PlayMode cases**. Thirteen new core cases cover hold duration, silence, release, occlusion, range loss, damage interruption, downed rescuer/wipe, rescue after clear, disconnect/new round, two rescuers, invalid clock/tickets, wire intent and snapshot consistency. PlayMode coverage checks an ineligible revive has no health effect and does not eat same-frame weapon input, plus immediate fire-then-reload acceptance and repeated reload requests not restarting the original timer.

The actual two-process script retains movement, target combat, rejoin and infected-kill checks. It then starts a third round, allows the client to be downed, checks that client's movement/fire probes fail, has the host clear the enemy and approach, and performs the timed revive. The client must receive progress and 40 HP, then move and fire successfully after rescue. The client must observe reload started and completed, then send a deliberate upward miss as a final receipt. The host must observe these same stages and unchanged target health before shutting down. Both sides must reach the new revive terminal markers before shutdown can count as a pass.

Preserve the known nine local generated files, use non-overlapping fast-forward only, compile, execute all suites, rebuild the Windows player and run the [harness](LAN_LOOPBACK_AUTOMATION.md) with the exact revision. Return actual CLI results/artifacts, full player logs, both reports/summary, cleanup and preserved hashes. Do not invent XML files or suppress native cloud diagnostics.

Manual acceptance remains separate: capture/release focus, E key hold/release, visible progress/cancel feedback, rescuing from either peer, moving/fire/damage interruptions and control recapture after revival. Real LAN, ten-player scale, waves, production animation and performance remain open.

## Reload acknowledgment correction

The failed run exposed a gameplay input issue, not failed revive replication: the adapter shared a 20 ms pacing gate for fire and reload. A valid reload arriving immediately after an accepted shot could be discarded even though SendAction returned that it submitted the command. Reload now has its own 20 ms pacing clock, reset each round. HostFireGate still checks alive/full-magazine/in-progress state, and repeated requests cannot extend the reload timer. No protocol change or longer smoke deadline is used.

The final smoke exchange now verifies the client actually sees reload completion before the host accepts its upward-miss receipt. New host/client completion markers are mandatory in the launcher. UTC timestamps accompany every step in logs and report stepUtcTimes, aiding diagnosis without guessing timing. A client-only success or host-disconnect observation is never overall acceptance; both reports, exits and complete required steps must pass.

## Validated baseline and later wave extension

Corrected revision `90c7b28cc7b6a3c0e951732d3111be367f413199` passed all 117 EditMode / 28 PlayMode cases, build and the complete two-process revive/reload receipt run. See [the execution record](LAN_LOOPBACK_AUTOMATION.md) for exact timestamps, hashes and retained cloud-connectivity diagnostics.

The later [wave candidate](LAN_HORDE.md) preserves revive in one round-level owner through intermissions and moves snapshots to protocol v5. The v4 layout above describes this revive milestone's historical protocol, not the latest wave wire layout. Its source inventory is now 132 EditMode / 32 PlayMode; new wave acceptance is pending.
