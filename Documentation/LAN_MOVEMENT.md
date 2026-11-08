# Host-authoritative walking milestone

## Scope

The separate LanRange scene now advances from fixed firing stations to walking players in its flat greybox arena. The existing solo SurvivalYard, original range, game settings and production assets remain separate. This is the movement foundation for future co-op infected, not the complete multiplayer zombie game.

Current state: cloud-authored candidate. Static/source review can validate structure; Unity tests and a fresh two-process run are required before calling this runtime-tested. Previous fixed-station revision 4be3f12 passed the documented loopback gate.

## Authority contract

- Clients send WASD-derived horizontal direction only, rotated by their local yaw. Intent length cannot exceed one; vertical, non-finite and oversized vectors are rejected. Zero is a stop request.
- The NGO sender maps to its current host-held ticket. Session, round and increasing sequence must match. No client position, speed, delta time, timestamp or claimed hit is transmitted.
- Movement requests are emitted at most every 50 ms by normal input. Host pacing is independent of weapon pacing, so walking and shooting in the same update do not discard each other.
- HostMoveGate updates intent without moving anything. Only a trusted host tick returns a displacement, capped to 4 units/second and 50 ms per step. Packet floods do not multiply travel. A held intent expires after 250 ms without accepted refresh. Intent changes take effect at the next host tick boundary, after the prior held intent is stepped, so turning does not delete part of a tick or apply input retroactively. Explicit zero intent cancels immediately, including inside movement pacing.
- Unity resolves each proposed displacement with a swept capsule, radius 0.35, height 1.7, bottom clearance 0.05, and 0.02 skin. At most two sweep/slide passes are used. Arena coordinates are additionally bounded to ±15.3. This supports this flat arena only, not stairs, slopes, gravity, jumping or arbitrary map traversal.
- Player bodies do not collide with teammates. Arena walls and living sphere targets block walking. Destroying a target removes that blocker, as it did for shooting.
- Shots originate from the host's current resolved eye position, not an assigned spawn after movement. Host physics still decides occlusion and damage.
- Host snapshots carry finite bounded position for each member. Camera and remote capsules use those replicated positions; local body remains hidden. There is no prediction or smoothing, so movement can visibly step at the 10 Hz snapshot cadence. This limitation is deliberate and must not be mistaken for polished movement.
- Esc/focus loss submits a stop where possible and releases controls. Missing stop messages still expire. Return-to-lobby destroys the movement gate. A new round resets all positions to assigned spawns; rejoined connection identities cannot reuse old movement state.

## Protocol

Version 2 and named-message suffix v2 reject the old handshake. Command size remains 44 bytes: action 3 uses the same three-float vector field for planar movement, while fire/reload keep their existing aim semantics. Member snapshots grow from 19 to 31 bytes by adding three position floats; maximum snapshot size is 347 bytes for ten members. Strict length, finite values, bounded arena positions and unique member/slot checks apply before accepting a snapshot.

All clients and host must be built from the same current revision. No package upgrade, NetworkTransform, NetworkObject prefab, online lobby, authentication, encryption, firewall or security-setting change was introduced.

## Tests and local execution

Expected source inventory: 94 EditMode and 22 PlayMode cases. New coverage includes packet flood versus host tick, fixed speed/diagonal normalization, expiry/stop, clock and step caps, stale/disconnected tickets, wire stop/rejection, position snapshot bounds, host walking and shooting from a moved origin, round spawn reset and capsule collision/sliding.

Preserve the nine known editor-generated files before a non-overlapping fast-forward, compile, run all suites, and rebuild the Windows test player. Run the existing [two-process harness](LAN_LOOPBACK_AUTOMATION.md) with the exact newly built revision. It now requires both `remote-movement-observed` and `replicated-movement-and-expiry` before its prior firing/reload/rejoin/reset/shutdown checks can pass. It identifies the local player through its actual transport ID, not by matching a fixed station camera.

Manual checks when native controls are available: WASD forward/back/strafe relative to yaw, diagonal speed, camera/body ownership, wall sliding, target obstruction, stop on Esc/focus loss, no lobby movement, moving-shot origin, and repeated round/rejoin reset. Record visual stepping/latency rather than claiming polished motion. Keep real-LAN and ten-player acceptance separate.

## Next gate

After moving-client acceptance, add a bounded host-owned infected encounter with replicated health/death and player damage. Reuse this authority boundary; do not attach the solo zombie scripts to every client and call that co-op. Production characters, animation/aim integration, revive, city progression and performance remain later documented slices.
