# Bounded host-owned co-op waves

## Scope and acceptance

The host can choose **Start waves** after Start round, instead of the existing single-infected test. This isolated arena has three waves containing 2, 3 and 4 infected, with five-second intermissions. All nine enemies must be defeated. A team wipe fails the encounter; the host can return to lobby and begin a fresh round.

Revision `6b3df43657689622e70189704072893b0e899ada` passed local compilation, 132 EditMode / 32 PlayMode, Windows build and a complete 66.571-second two-process wave run. The later [camera/control candidate](LAN_CONTROLS.md) requires its own validation; real LAN, ten-player capacity and full-game quality are not established. See [the exact execution record](LAN_LOOPBACK_AUTOMATION.md).

## Rules and authority

- The host alone starts waves, chooses spawn locations, advances intermission time, simulates each enemy, applies melee/fire damage and decides clear/wipe. Clients send only existing movement/aim/fire/reload/revive intents.
- One round-level owner retains survivor health and revive state. The weapon gate also persists across waves: there is no free healing, ammo refill or implicit resurrection at a wave boundary. Returning to lobby and starting a new round is the explicit full reset.
- Each enemy has its own bounded movement tick and melee cooldown. All enemies can advance on the same host tick; the first cannot consume the others' movement time or attack allowance. Pursuit selects the nearest current living teammate, with stable tie order.
- Enemy IDs are unique within a round: wave one uses 4–5, wave two 8–10, wave three 12–15. The legacy single enemy uses ID 0. Old wave IDs cannot move, hurt or receive damage on behalf of replacements. IDs are scoped by the session/round identity, so a new round can reuse the finite ID range safely.
- A fixed pool of four capsule objects bounds scene allocation. A death immediately disables that collider. All-dead starts a five-second break after waves one/two and completes the encounter after wave three. Duplicate damage to a dead ID is rejected and cannot skip a wave.
- Revive remains available during fighting, intermission and after final clear, subject to the existing living-rescuer, range, line-of-sight, continuous hold and interruption rules. The next wave does not erase an in-progress rescue. New enemy contact can interrupt it normally.
- The host checks for any living connected survivor during intermission before spawning. A last living disconnect cannot leave an unwinnable encounter silently spawning more enemies.

## Spawn and collision behavior

Each new wave uses distinct preferred points. The host searches at most 17 candidates per enemy: the preferred point, then a fixed 4×4 grid inside the arena. A candidate must clear the world capsule query, remain at least 2.5 m from every connected player and at least 1 m from other chosen wave spawns. All points are chosen before any new enemy object is enabled. If a complete placement cannot be found, the encounter fails closed without teleporting players or enabling intersecting enemies; the host can reset.

Movement uses the existing flat-ground sweep/slide helper. During one enemy's movement/line-of-sight query, only that enemy's collider is temporarily excluded and restored in a finally block. Other infected, walls and targets still obstruct the query. These are direct-seeking capsules, not a NavMesh horde: congestion and getting stuck behind geometry remain production work, not something this slice claims solved.

## Protocol v5

Both peers must rebuild; v4 and older handshakes are rejected. Commands remain 44 bytes with no client-chosen enemy, health, damage, spawn or wave fields.

Snapshots carry a 46-byte fixed header, 0–4 enemy records of 14 bytes, and 1–10 player records of 34 bytes. The maximum is **442 bytes**. Enemy records contain a host ID, 0–100 HP and bounded finite position. Header data includes encounter phase, horde phase, wave/total, bounded intermission countdown and enemy count.

The decoder checks counts and exact packet length before allocating enemy/member arrays. It rejects duplicate or wrong-wave IDs, nonfinite coordinates/countdown, out-of-range health, impossible enemy counts, fighting with no live enemy, a break with live enemies, a break after the final wave, and completion before all final enemies are dead. Existing member identity, position, health and revive-target consistency checks remain. Snapshot arrays are copied; callers cannot mutate published state through input arrays.

Legacy single-infected accessors derive from the same core state. They remain for the prior single-enemy tests, not as an independent second AI. During waves, consumers use the full enemy array; the legacy primary health alone does not describe whether the whole wave is alive.

## Required execution

Expected source inventory: **132 EditMode cases and 32 PlayMode cases**. Fifteen new core cases cover lifecycle, stale IDs, independent ticks/cooldowns, retained health/ammo, intermission rescue/disconnect, wipe, stale rounds, bounded spawn points, maximum packet roundtrip, truncation/counts, identity and phase validation. Four new PlayMode cases exercise physical multi-enemy movement and full three-wave shooting/reload, blocked-anchor fallback, no-free-spawn failure and round reset. Existing headless collider-count tests now expect four inactive enemy capsules.

The two-process harness keeps every previous scenario, then starts a fourth round. Both peers must observe all three wave numbers and both intermissions. The client shoots the enemies with real host raycasts, reloads as needed and verifies ammunition is retained across breaks. Both must survive and observe all final enemy HP at zero. A final reload cycle and harmless upward miss acknowledge that the client received completion before host shutdown. The launcher requires the new wave terminal markers.

The 90-second per-peer and 120-second launcher deadlines are unchanged. The prior revive run took 43.745 seconds; the added bounded combat plus ten seconds of breaks is intended to fit, but only execution can confirm that. A timeout remains a failed run, not a reason to quietly relax the test.

Preserve the nine known local generated-file hashes. Pull only the exact non-overlapping fast-forward, compile, run all suites, rebuild the dedicated Windows player, and run [the documented script](LAN_LOOPBACK_AUTOMATION.md). Return actual CLI evidence, reports, UTC steps, full logs, executable/gameplay assembly hashes and cleanup. Record native Unity cloud Curl diagnostics separately from the callback error count.

## Still not validated or finished

This does not establish real-LAN connectivity, ten-player capacity, performance, visual quality or finished game acceptance. Manual tests must cover Start waves UI, player movement/aim/reload/revive during active packs, obstruction/congestion, occupied spawn areas, all-down/restart, intermission disconnect and host shutdown. Production NavMesh navigation, animation/Mixamo integration, streaming city, weapons, mission progression, save data, audio and polish remain separate milestones.
