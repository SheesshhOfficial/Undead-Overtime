# Infected damage feedback

## Scope

This public-code-only presentation change adds an optional brief warm color tint to a living infected's existing capsule when an accepted host snapshot reports a nonlethal health loss. It builds on the read-only locomotion foundation at `7ba10b22e1e2b53378cfe7a28e6bf552c480a874`.

This is combat readability for the existing prototype body, **not character/animation integration**. No licensed file, new mesh, shader, texture, Animator, corpse, sound or source download is included. The tint is not a local-shooter hit marker: another teammate may have caused the damage, and one observed health drop can combine hits.

## Behavior and authority boundary

- `Core/InfectedDamageFeedback.cs` tracks at most four live identities by session + round + enemy ID. First observations, health increases, changed identities and health drops after a gap of 0.5 seconds or more establish a quiet baseline. Array reordering cannot exchange identities.
- A fresh nonlethal health drop starts a **0.22-second linear fade**. Duplicate health observations and repeated render-frame reads do not extend it. New damage can restart only that enemy's pulse.
- Death, removal, inactive encounter, lobby, reset or stop clears feedback. Dead objects disappear on the existing schedule; no death animation or cosmetic delay changes wave/extraction progress. Malformed observations and invalid/backward clocks fail closed.
- `LanRange.InfectedPresentation.cs` observes feedback once per accepted snapshot, alongside locomotion. Both ordinary range and mission frames already pass through this hook. `LateUpdate` reads the state to update the fallback renderers only. It does not replay snapshot positions into host physics.
- `InfectedDamageTint.cs` uses a separate `MaterialPropertyBlock` for each generated infected renderer. The URP `_BaseColor` or Standard `_Color` moves at most 65% toward a warm amber, preserving alpha. It never accesses `renderer.material`, clones a material, mutates the shared material, or changes transform, collider, activity or lifetime. Original property-block overrides are restored at pulse expiry/reset.
- This helper owns the generated fallback renderer's property block while active. There are no other block writers on those renderers in this revision. A future private-character adapter must coordinate ownership rather than stack unrelated property-block writers or assume this single-renderer helper covers a skinned hierarchy.
- **Infected damage tint** in the connected menu can disable the effect. Toggling clears it immediately. Re-enabling requires a fresh quiet baseline and never replays an earlier hit. The setting is not written to disk and does not alter health or packets.
- Null-graphics and explicitly disabled presentation do not construct tint helpers, renderers or property blocks. The pure managed bounded feedback object contains no Unity resources.

Host damage, fire/reload validation, enemy movement/LOS/melee, snapshots, range protocol v5, mission protocol, colliders and player controls remain unchanged. The source attack/death animation timing and grounding issues remain separate work.

## Checks and limits

Cloud verification on the exact source hashes recorded in `INFECTED_DAMAGE_CORE_RESULT.json`:

- **417/417** unfiltered pure-core NUnit cases pass, including **31 new feedback cases**. Zero failures, warnings, skipped or inconclusive cases.
- Separate .NET Standard 2.1/C# 9 core and .NET 10 runner compilation: zero errors/warnings, pinned SDK **10.0.403**, locked NUnit/NUnitLite **3.14.0**. The approved SDK was restored only into a replacement cloud workspace after verification against Microsoft's official SHA-512 metadata.
- Source and dependency lock hashes unchanged during the run.
- Static consistency: **123 source/assets**, **9 assemblies**, **163 metadata GUIDs**. **96** Git-attribute assertions and **33** malformed-importer fixtures pass. This full public clone includes original Unity metadata absent from earlier partial source mirrors; the GUID count is not a count of new assets.
- Four additional native tests are authored: three renderer isolation/restore/invalid-input cases and one actual host-damage/toggle integration case. Existing headless and stop tests also assert feedback is absent. Graphics-dependent tests explicitly skip on a Null device so a headless run cannot claim their coverage.

**Not run here:** Unity compilation, native EditMode/PlayMode tests, rendered color/visibility inspection, a player build, two-process networking, real LAN or hardware-performance tests. A property-block assertion is not pixel or visual-quality acceptance. Capsule readability and the tint's comfort/intensity require actual native observation.

## Exact-revision native acceptance

Use the existing Undead Overtime execution chat and the exact published commit. Record remote/branch/HEAD, dirty files and Unity **6000.6.5f1** / URP **17.6.0** before a safe fast-forward. Preserve user edits and accepted material files; do not reset, stash or automatically overwrite them.

1. Compile and run the complete current native EditMode and PlayMode suites, returning XML and logs. Run graphics-dependent tests with graphics enabled; report headless skips separately. Do not substitute focused tests for full regression coverage.
2. In graphical range and depot mission sessions, shoot one infected and verify only that living body briefly changes color. Observe another infected sharing its material to confirm isolation. Inspect a short moving capture for fade/comfort, not only a still image.
3. Test fatal hits, multiple teammates damaging the same enemy, missed/blocked shots, new waves, lobby/replay, stop/reconnect, FPS/TPS changes and repeated toggle-off/on. No missed shot may create a pulse; no old pulse may carry across reuse. The tint must not identify the local shooter.
4. Build and execute the existing range and mission loopback harnesses on the exact revision. Their `-nographics` execution checks gameplay regression, not visible feedback. A separate graphical two-process observation is needed for remote presentation. No private character content is needed.
5. Return exact counts, warnings, logs, build hashes and labeled actual captures. Keep authored, compiled, tested and visually accepted states separate. This slice does not establish production characters, realistic animation, real-LAN scale or minimum hardware requirements.
