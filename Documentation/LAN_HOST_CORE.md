# Host-owned LAN foundation

Date: 2026-11-08. Status: **implementation candidate; Unity compilation and NUnit execution pending**.

This additive slice separates a small authoritative rule core from the solo mechanics range. It does not make the range multiplayer, open a socket, install a networking package, start a server, or change project settings. The range remains unchanged and is still the first local execution gate.

## Implemented boundary

`Assets/UndeadOvertime/Core` is a no-engine-references assembly using only the C# standard library. It is deliberately not referenced by the existing range yet. Its purpose is to make session and shot rules inspectable/testable before a transport adapter is added.

### Session lifecycle

`LanSession` owns a stopped/lobby/playing lifecycle and supports a configurable capacity from one to ten, **including the host**. A ten-member in-memory test is not evidence of ten-player LAN performance.

- Host creation allocates a new session lifetime and immediately reserves one slot.
- Lobby admission checks protocol version, duplicate peer ID and capacity without mutation on rejection.
- A host-issued `PlayerTicket` identifies one connection lifetime. Disconnect/rejoin with the same transport peer ID receives a new connection ID; stale tickets cannot mutate the new member.
- Only the current host ticket may begin a round, return to lobby or stop the session.
- Host disconnect clears all members. No migration is attempted.
- Joining is allowed only in the lobby. Mid-round late join/rejoin is intentionally not implemented.
- Member snapshots are copies. The adapter cannot modify membership by editing a snapshot.
- Repeated start/stop is supported, but calling start twice without stopping throws rather than silently replacing a live session.

These are provisional integration rules consistent with the existing LAN plan, not a final product UI or save/reconnect policy.

### Host fire admission

`HostFireGate` is constructed for one active round, with bounded per-player state. A new round needs a new gate. It uses server-supplied monotonic seconds, current host-issued membership, host-accepted origin/aim, server weapon constants, ammo and cooldown.

`FireRequest` carries only a session/round discriminator, increasing sequence, claimed origin and unit aim. It carries **no target ID, claimed damage, hit location, range or client timestamp**. The claimed origin is compared with the host pose but is never used as the shot's actual origin.

The gate rejects stale rounds/sessions/connections, zero or replayed sequences, non-finite input, non-unit direction, origin displacement, aim disagreement, downed players, reload, cooldown and empty magazines. Every fresh sequence on a valid round/member/clock is consumed even if later rejected. A rejected packet cannot turn into a delayed shot when retransmitted after cooldown or revival. Invalid host-clock values do not consume the sequence.

On acceptance, the gate emits an `AuthorizedShot` using the authoritative muzzle origin, normalized request aim and server-selected range/damage, and consumes one round of ammunition. The later host adapter must raycast this shot against the authoritative world and apply any damage there. Accepted admission is **not** proof of a hit. No damage or client outcome is applied by this core.

Prototype constants, deliberately not a final balance decision:

| Rule | Candidate value |
| --- | --- |
| Magazine | 12 |
| Shot damage/range | 25 / 100 m |
| Shot interval | 0.18 s |
| Reload | 1.5 s, unlimited reserve for this isolated rule |
| Origin tolerance | 0.35 m |
| Aim tolerance | 5 degrees from host-accepted aim |
| Maximum host clock | 100 Julian years in seconds, preserving cooldown precision |

Reload begins only through a trusted host call, cannot be repeatedly restarted while active, and completes on the next fire/reload command at or after its deadline. Downing cancels an unfinished reload; reviving does not refill ammo or reset cooldown/sequence. `Ammo()` is a current state view, not a clock tick; adapters should not assume it advances a reload timer.

## Transport integration contract

The adapter is not implemented. These conditions are mandatory when it is:

1. Derive the sender from the transport's authenticated connection/RPC context, not any payload ID. Keep the current `PlayerTicket` in a host-owned mapping. A ticket is a correlation value, **not a secret or authentication mechanism**. Never let a client manufacture authority by constructing one.
2. Update pose/alive state only from host-authoritative movement/gameplay. Do not copy the claimed fire origin or aim straight into `SetPoseFromHost` before validation. If movement is client predicted, first implement and test host validation/reconciliation and define how aim samples are ordered. The present five-degree rule is not a substitute for that work.
3. Capture a single monotonic session clock on the host. Never use client time. Serialize rule calls on the Unity main/server thread; these classes are not thread-safe.
4. Bound/decode payloads and rate-limit requests **before** the gate. The gate is O(1) per shot, not protection against transport floods. Define a sequence-wrap/reconnect policy before deployment; sequences do not wrap within a round.
5. Use the emitted origin for a host raycast, respecting walls and a documented collision mask. Validate the resulting target and apply server-owned damage. Replicate results, not client claims. No lag compensation is present.
6. Deliver host-owned spawn/state to clients. One camera/input owner per local player; remote bodies stay visible independent of their owner's FPS/TPS mode.
7. On lobby transition, disconnect or stop, tear down old round state and presentation. Never reuse an old gate for a new round. Process restart persistence is not implemented.
8. Use compatible pinned Netcode for GameObjects/Unity Transport only after verifying versions for Unity 6000.6.5f1. Do not change the package manifest just to make this pure core compile. No network ports, firewall rules, credentials or paid service are configured by this patch.

## Tests and evidence

New tests live in `Assets/UndeadOvertime/Tests/CoreEditMode`, a separate test assembly referencing the core only. Run them with the existing EditMode command in [PROTOTYPE_RANGE.md](PROTOTYPE_RANGE.md); it includes all EditMode assemblies. Then run the original PlayMode suite to check the additive change did not affect the range. Inspect XML and Console logs, not only exit code.

Authored cases cover:

- Constructor bounds; host-inclusive capacity; protocol/duplicate rejection.
- Remote lifecycle denial; safe lobby-only rejoin; stale ticket rejection.
- Host shutdown, round changes, restarts and repeated sessions.
- Server-owned origin/damage, independent per-player rate/ammo.
- Sequence replay/reorder and rejected-request replay after cooldown/revival.
- NaN/infinity/oversized time and aim, clock regression, origin/aim spoofing.
- Downing, reload cancellation/completion, repeated reload and empty magazines.

Cloud checks can run without Unity:

```text
python Tools/validate_prototype.py
```

The static check validates metadata, assembly references and scene/source consistency only. It does **not** execute the C# logic. This cloud environment has no Unity editor or C# compiler, so compilation, NUnit cases, local mechanics regression, actual LAN join/disconnect, and ten-client load are pending. Do not mark them passed based on source inspection.

## Next execution gates

1. Pull the exact approved game revision in the existing project and run compilation, all EditMode and original PlayMode tests on Unity 6000.6.5f1.
2. Return any failures to cloud authoring with commit, logs, XML and reproduction. Do not repair source locally under the execution-only workflow.
3. Add the transport adapter and minimal host/join UI only after the core/range checks pass. Verify two real processes, then LAN on two machines, then ten clients, repeated connect/reconnect/shutdown and malformed-request rejection.
4. Build the first host-owned zombie encounter/wave loop on that tested session foundation. Production assets can be integrated independently, but must not be mistaken for tested gameplay.
