# LAN co-op boundary

## Product target

The intended game supports solo play and up to 10 players on a LAN, with FPS/TPS switching and cooperative shooting against zombies. This is a target, not a capability of the current mechanics range.

## Proposed next implementation

After the additive scene imports and its local tests pass, verify versions of **Netcode for GameObjects** and **Unity Transport** compatible with Unity 6000.6.5f1 before changing the package manifest. Use a host-authoritative direct-IP LAN session first. Relay, lobby services, account sign-in, Internet hosting, and paid services are not needed for that test.

The prototype controller is disposable presentation/test code, not an authoritative networking layer. Do not attach a NetworkTransform and assume the range has become secure multiplayer. The next slice should separate:

- Local input, cursor ownership, camera, and view-only body visibility, enabled only for the owning player.
- Host-owned player spawns, capacity limits, target health, hit validation, enemy state, and match state.
- Explicit movement/shot requests with host-side validation of shot rate, origin, aim, range, and damage. Account for latency before a larger movement system is selected.
- Camera mode as owner-local presentation. Remote players retain visible bodies regardless of their owner's camera mode.
- A shared reset/round transition issued by the host, not arbitrary client scene mutation.

Keep transport and gameplay concerns separate from input and visuals. Reuse the small deterministic damage model where appropriate, but do not preserve prototype-only globals, HUD, runtime construction, or direct input-to-damage calls merely for convenience.

## Acceptance gate for the next slice

1. Two real processes on the same LAN can host/join by a verified IP and port. Loopback alone does not validate LAN connectivity.
2. Exactly one camera, audio listener, and input owner per local player; remote players cannot control the local camera.
3. Host accepts and replicates damage. Invalid client damage/fire-rate requests are rejected.
4. Joining, disconnecting, rejoining, host shutdown, capacity rejection, and repeated session start/stop leave no orphan objects or input maps.
5. Scale testing reaches ten actual connected clients/processes before advertising ten-player support. Performance and packet behavior are measured, not inferred.
6. Any required OS firewall/security prompt is reported for authorization; do not silently open ports or change network policy.

Production art, a city block, moving zombies, and a wave loop follow the verified session foundation. They are not prerequisites for proving the local mechanics range.


## Implemented preparatory slice (2026-11-08)

A transport-independent host lifecycle and fire-admission core now exists in `Assets/UndeadOvertime/Core`, with separate EditMode test sources. Read [LAN_HOST_CORE.md](LAN_HOST_CORE.md) for integration rules and current verification limits. It does not activate networking, change the solo range or replace the real-process acceptance gates above. Local Unity compilation/tests remain required before transport wiring.
