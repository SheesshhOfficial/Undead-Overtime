# Infected presentation foundation

## Scope and status

This is an asset-free, read-only idle/walk state seam for the existing LAN range and depot mission. It does **not** replace the visible capsules, import a character, create an Animator, or play an attack/death clip. It prepares a bounded input for a later privately validated zombie visual without changing gameplay while that content is unavailable.

Baseline: `a0630500599742cb3a4d64ebda7a6719815ba28d` on `main`. Unity remains **6000.6.5f1**. No editor, package, scene, build, importer, protocol or source-asset settings change.

## Implementation

- `Core/InfectedLocomotionPresentation.cs`: engine-independent, fixed four-entry cosmetic history. Identity is session ID + round ID + enemy ID, not snapshot array index. The value exposed is visibility, moving/still, estimated planar speed and movement-facing direction. It deliberately supplies no authoritative position, attack flag, damage event, victim, death time or clip phase.
- `LanRuntime/LanRange.InfectedPresentation.cs`: `TryGetInfectedLocomotion(enemyId, out state)` exposes that state. It does not load content or mutate transforms, renderers, materials, physics, health, ammo, waves or mission progress. With presentation disabled, including a Null graphics device, the observer resets and reads return false.
- `LanRuntime/LanRange.cs`: a single observation call in `ApplySnapshot`, plus resets at prepare, stop and completed shutdown. Both ordinary range snapshots and accepted mission frames already converge through `ApplySnapshot`. Render-frame reads and view-mode changes do not resample an old snapshot.

The existing `ApplyVisuals` capsule/physics behavior is unchanged. A killed enemy is not kept alive for an animation. A future visual adapter must use a separate identity-scale cosmetic child/root and must never feed this estimate back into the host simulation or replay an old snapshot into host physics.

### Sampling policy

The caller provides already accepted, ordered host snapshots and one monotonic local receipt clock. Range v5 carries no snapshot timestamp or sequence, so this helper cannot validate network freshness or reconstruct precise host velocity. It is not a replacement for transport or mission sequence validation.

1. The first live observation is idle and faces local positive Z. Motion is measured only between valid observations of the same living identity.
2. Planar speed is bounded to the unchanged host speed, **1.6 m/s**. Movement starts above **0.15 m/s** and stops at or below **0.10 m/s**. Direction follows nontrivial movement and is retained when standing. This is movement-facing, not replicated aim or facing toward the current victim.
3. Same-time samples, observation gaps of **0.5 seconds or longer**, and displacement beyond `1.6 * elapsed + 0.08 metres` establish an idle baseline. The 0.08 m allowance only tolerates receipt bunching for this estimate; it does not authorize movement or relax a host gate.
4. After **0.5 seconds** without a fresh observation, a read keeps the last standing visual eligible but stops movement and invalidates the motion baseline. Reads never extend freshness. The next observation starts idle before resuming motion from a later valid sample.
5. Observed death, removal, inactive encounter, lobby, stop, or changed session/round clears the relevant history. Reappearance after removal/death and a health increase start a new life. Snapshot array reordering does not exchange identities.
6. Invalid positions, duplicate enemy IDs, health above 100, pool overflow or invalid/backward clocks fail closed. Invalid time cannot lower the last valid clock boundary. Explicit reset permits a new clock domain.

No source action is sped up to force it onto gameplay timing. The current host remains at **1 second between melee hits**, **10 damage**, **1.3 m range**, with existing LOS/authority checks. Protocol v5 enemy entries remain **14 bytes**.

## Verification

Authored: **39 new pure-core cases** and **3 new Unity PlayMode integration tests**. The focused core cases cover standing, diagonal motion, hysteresis, bunching, teleports, same-time updates, stale reads/gaps, identity reuse/reordering, death/removal, new round/session, bounded pool, malformed observations/clocks, and unchanged encoded snapshot bytes/host constants.

Cloud checks:

- Complete unfiltered core suite: **386/386 passed**, zero failures, warnings, inconclusive or skipped cases. Separate .NET Standard 2.1/C# 9 core compilation and .NET 10 runner compilation succeeded with zero errors/warnings using the already approved SDK **10.0.403** and locked NUnit **3.14.0** dependencies.
- Exact source/lockfile hashes remained unchanged during the final run. See `INFECTED_PRESENTATION_CORE_RESULT.json` for hashes and fixture counts.
- Static consistency, metadata, ordinary-Git binary attribute checks and malformed-importer fixtures passed. No new binary or licensed-content path is in the publication set.
- The initial full run found one overly strict exact floating-point assertion (`1.5999999999999999` versus `1.6000000000000001`). The test now uses a `1e-9` tolerance; no production rule or test path was removed. The complete suite was rerun afterward.

**Not run in this cloud task:** native Unity compilation, EditMode/PlayMode execution, graphical rendering, private character/avatar import, player build, two-process loopback, real LAN or performance tests. The three new native tests are authored, not passed evidence. No visual acceptance or completed character integration is claimed.

## Next native execution

Use the existing project execution chat and the exact published revision. Record remote, branch, HEAD, dirty paths and editor/package versions before a fast-forward. Preserve unrelated user changes; do not reset, stash or overwrite them.

1. Compile and run the complete current native EditMode and PlayMode suites, returning XML and editor logs. The new `LanInfectedPresentationTests` cover headless/no-animation construction, repeated reads preserving newer host physics and unchanged snapshot bytes, and lobby/round/stop identity reset. The latter two require graphics-enabled Unity and explicitly skip on a Null graphics device; a headless run cannot count as populated-presentation/reset coverage. Run them with graphics enabled before accepting this slice. Do not substitute these three focused tests for the full suite.
2. Exercise ordinary range and mission lifecycle paths, including disconnect/reconnect and repeated stop/start, on the exact revision. Confirm stale/dead/missing identities cannot leave cosmetic movement behind.
3. Run the existing range and mission loopback harnesses if native acceptance includes a new build. Their `-nographics` path verifies gameplay regression, not a visible zombie. No private source asset is needed for any test in this change.

## Private visual integration gate

The later idle/walk adapter must remain opt-in and retain capsules when content is absent, disabled or invalid. Establish and verify the private-content ignore and build boundary before transferring any licensed file. Validate Humanoid Avatar mapping, units, foot origin, materials, skin weights, clip timing/contact, per-instance state and dependency confinement in native Unity. Use root motion off, no clip events or controller behaviours with gameplay effects, no physics/network components on private visuals, and no automatic source downloads. Reject unvalidated content rather than silently promoting a source review to runtime acceptance.

Attack and death are explicitly separate follow-ups. The current source swipe lasts about **2.633 seconds**, while gameplay allows one melee hit per second. Enemy snapshots have no attack sequence/target/phase or death timestamp. The untouched death source also showed about **217 mm** floor penetration; a later clearance derivative is not proof of physically grounded settling. Do not conceal these issues with a speed multiplier, global lift or gameplay delay. A corpse needs a bounded cosmetic lifetime and cleanup, with no live collider, wave/extraction delay or reused-identity carryover. Neither attack nor corpse behavior belongs to this foundation.

Keep licensed source FBXs, Blender files, textures, source packets and private derivatives out of public Git history. This change contains only original code, tests, metadata and implementation documentation.
