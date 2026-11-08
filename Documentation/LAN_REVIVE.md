# Host-owned co-op revive milestone

## Scope and status

A living player can rescue a downed teammate by standing within 2 metres and holding E for 3 continuous seconds. The host checks all eligibility, timing and line of sight. A successful revive restores 40 HP at the downed player's current position, without refilling ammunition. The player can capture controls again to move/fire.

This is a candidate extension to the tested one-infected arena, not waves or finished co-op gameplay. Baseline `5aaf489bf34e65a8dacaed31c582a822c078f24a` passed compilation, 104 EditMode / 26 PlayMode, Windows build and actual two-process infected pursuit/injury/kill. Current revive acceptance is pending its own exact-revision execution.

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

Expected source inventory: **117 EditMode cases and 27 PlayMode cases**. Thirteen new core cases cover hold duration, silence, release, occlusion, range loss, damage interruption, downed rescuer/wipe, rescue after clear, disconnect/new round, two rescuers, invalid clock/tickets, wire intent and snapshot consistency. The new PlayMode test checks an ineligible revive has no health effect and does not eat same-frame weapon input.

The actual two-process script retains movement, target combat, rejoin and infected-kill checks. It then starts a third round, allows the client to be downed, checks that client's movement/fire probes fail, has the host clear the enemy and approach, and performs the timed revive. The client must receive progress and 40 HP, then move and fire successfully after rescue before acknowledging the result. Both sides must reach the new revive terminal markers before shutdown can count as a pass.

Preserve the known nine local generated files, use non-overlapping fast-forward only, compile, execute all suites, rebuild the Windows player and run the [harness](LAN_LOOPBACK_AUTOMATION.md) with the exact revision. Return actual CLI results/artifacts, full player logs, both reports/summary, cleanup and preserved hashes. Do not invent XML files or suppress native cloud diagnostics.

Manual acceptance remains separate: capture/release focus, E key hold/release, visible progress/cancel feedback, rescuing from either peer, moving/fire/damage interruptions and control recapture after revival. Real LAN, ten-player scale, waves, production animation and performance remain open.
