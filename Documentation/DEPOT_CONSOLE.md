# Original depot power cabinet candidate

## Status and scope

Cloud-authored on top of `12873870e64bd9cdd4f4eb52fedbe233d45a69e9`, for the user's requested combined depot-build acceptance. Native Unity import, compilation, rendering, collision, full suites and player/loopback validation of this candidate have **not run**. The last accepted Windows player remains 917d94. Offline Blender export/atlas checks and source review are separate evidence, not Unity acceptance.

This replaces the mission's orange control cube visual with the project's original **AST-GEO-PROP-009** utility cabinet. It is not a purchased model, a new character, or a claim that the whole depot/city is finished. See [provenance](DEPOT_CONSOLE_PROVENANCE.md), [exact six-file packet](DEPOT_CONSOLE_PACKET.json) and [cloud check receipt](DEPOT_CONSOLE_CHECKS.json).

## Selected runtime content

- Three FBX meshes, at 7,748 / 4,028 / 702 triangles. One shared atlas slot per mesh; original UV0 and UV1 are retained. Internal names use Detail0/1/2 so Unity does not infer incomplete LOD groups from separately imported files.
- Three unchanged 1024×1024 source maps: sRGB base color, linear normal map and linear URP metallic/smoothness. RGB of the packed map is metallic, A is 255 minus source roughness; all 1,048,576 pixels were checked. No rebake or resampling.
- Only these six binary files are copied from the selected source packet. No .blend, concept images, private source tree, unrelated asset, Mixamo file or font binary is included.
- Narrow ordinary-Git exceptions cover those six paths. Existing floor exceptions and unrelated LFS rules remain intact.

The cabinet loads only for rendered missions with the accepted concrete PBR template available. Its material is a runtime-owned clone of that already-included URP/Lit variant, using the cabinet's maps and unit atlas scale. The original concrete/floor materials are never changed, reimported or saved by this feature. The manual LOD group changes at screen-relative heights 0.18 and 0.06; the last LOD threshold is zero to avoid a distant invisible solid cabinet. This is one cabinet with three meshes, not three simultaneously intended visible levels. Actual culling and transition quality still need native review.

The owner releases only its created material and objects. Imported shared meshes/textures and the accepted template remain shared. Inactive-parent creation is rejected before allocation; partial creation disables and disposes its owned hierarchy.

## Approved collision and interaction contract

The source uses metres and a ground-centre pivot. At uniform scale **0.65**, world origin **(-9, 0, -5.6)** and yaw **0**, the expected front faces **-Z**, toward the player's initial approach. The FBX axis interpretation is checked by authored native bounds tests and must still be inspected in engine.

The old placeholder collider (1.2 × 1.2 × 0.8 m) is intentionally replaced by the measured LOD0/1 axis-aligned bounds, using the same box in rendered and headless peers:

- Centre: **(-9, 0.5900375, -5.6039)**.
- Size: **(0.741, 1.180075, 0.4238) m**.
- Minimum: **(-9.3705, 0, -5.8158)**; maximum **(-8.6295, 1.180075, -5.392)**.
- LOD2 is contained within that box. This is a conservative tight bounding box, not a per-polygon or moving-door collider.

The change removes roughly 23 cm of unnecessary padding on either side of the old cube. The interaction point stays **(-9, 1.6, -5)**, 0.419925 m above the cabinet top. Its range, host-owned three-second F hold, line-of-sight validation, power/gate state, waves and extraction are unchanged. New native tests cover clear eye-height interaction, a side route outside the tightened proxy, a blocked central approach and rendered/headless parity. These are authored tests, not yet passed tests. The model's doors remain static; no opening interaction is implied.

## Import and build safeguards

Committed metadata uses the established Unity 6000.6.5f1 schemas: ModelImporter 24600, no animation/colliders/material import, original normals with Mikk tangents, existing UV channels; TextureImporter 13, explicit Texture2D shape, correct colour/data/normal types, no normal conversion or green flip, 1024 maximum and uncompressed first-acceptance textures. Compression/memory optimization is a later measured choice, not a present performance claim.

The scoped postprocessor applies only to the new DepotConsole directory. The build helper adds a **read-only** typed-dependency check for six imported cabinet assets. It does not repair, reimport, save or modify the existing floor. Standard Unity normalization of the new files' metadata must be recorded and preserved, not silently discarded.

## Combined native acceptance plan

Only run when the user/parent schedules the batch and it will not interrupt their player session. Use the sole existing local execution chat, no local source authoring.

1. Record branch/HEAD, fresh dirty-file hashes and editor state. Guarded fast-forward only to the explicitly published final candidate after proving changed paths do not overlap existing protected files. No resets, automatic stash, backup branch or settings/permission changes.
2. Let native import/compilation complete, retaining import warnings/errors. Do **not** rerun floor setup or save its materials. Stop on failure and return exact evidence to the cloud author.
3. Full expected inventory: **340 EditMode / 82 PlayMode**. Preserve full synchronous results. The six new EditMode cases cover three FBXs and three typed atlas maps. Five new PlayMode cases cover original atlas/LOD bounds, inactive ownership, disposal, collision/interaction and rendered integration. Render-dependent skipped tests do not constitute visual acceptance.
4. After suites pass, build once with **Undead Overtime > Build LAN test player (Windows)**. Verify the fresh build marker and managed assembly hashes, not only the unchanged EXE stub.
5. Run the exact-revision `Tools/run_lan_loopback_smoke.ps1 -Revision <40-character-sha> -Mode range`, then `-Mode mission`. The current mission harness requires 17 host/15 client markers, including a completed mission, lobby cleanup, fresh rejoin and a reset second round. Verify owned process exits and complete logs. No broad process-enumeration workaround.
6. Inspect actual FPS/TPS Game View: textured floor and concrete perimeter, metre-scaled partition UVs, gate relief without z-fighting, correctly oriented cabinet with readable proportions and sensible LOD transitions. Approach around both sides and the front; confirm F hold/power/gate operation, lobby cleanup, restart and extraction. Keep the distinction between programmatic captures and real manual input.
7. Preserve and report all unrelated dirty state, any new importer normalization, known InputSystem preload serialization and cleanup. No claim of real-network LAN, ten players, performance or final art until separately measured.

This candidate also includes the already-published objective bearing/distance aid, reversible concrete perimeter and gate relief. Those features and extended mission replay are included in this later combined gate, not retroactively accepted by the 917d94 results.
