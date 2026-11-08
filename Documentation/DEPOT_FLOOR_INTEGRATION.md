# Original depot floor: native Unity import candidate

## Scope and provenance

This candidate selects **AST-GEO-MOD-005**, the project's original 4 × 4 m concrete floor, from the private extras source library. Only three runtime LOD meshes and six required original/derived texture maps are copied into the game repository. Editable Blender sources, hidden kit-proof geometry, damp alternatives, references, unrelated private files and third-party licensed downloads are excluded.

The source provenance records original AI-assisted Blender geometry and deterministic project-generated texture pixels, without external photographs, stock textures, downloaded meshes, characters or logos. The side concrete maps are original shared project inputs from MOD-001. This is selected runtime implementation under the cross-repository workflow, not a transfer of the entire private library or a new open-source license. The selected asset's [provenance](DEPOT_FLOOR_PROVENANCE.md) and [packet receipt](DEPOT_FLOOR_PACKET.json) preserve exact source/export hashes.

No Mixamo mesh is represented by this integration. Survivors and infected remain placeholder capsules.

## Native import contract

No glTF package or editor/package upgrade is introduced. Three FBX files use Unity's built-in ModelImporter. Each must import as one mesh renderer with two material slots, preserved normals, primary tiled PBR UV0 and unique UV1, calculated Mikk tangents and no animation/collider. Expected triangles: LOD0 318, LOD1 142, LOD2 12. Expected Unity meter-space bounds: X 0..4, Y -0.2..0, Z 0..4, root scale one. These are acceptance assertions, not proof until native Unity tests pass.

Material slots are explicitly 0=original aged concrete top, 1=original worn concrete sides. The runtime assigns two serialized shared URP Lit Resources materials, which retain texture dependencies and the required normal/metallic shader keywords in player builds; they are not runtime-only Shader.Find variants. The Lit shader GUID was verified against the official Unity Graphics package source, and local import tests must confirm it resolves to the installed package shader. Top maps remain 2048² and side maps 1024². Base color is sRGB; normal maps are imported as NormalMap with the original OpenGL green convention, and metallic/smoothness maps are non-color. Packed-map R=0 (non-metal concrete), A=255 minus original roughness byte. No silent downsampling, rebaking or new texture content is intended. Original PBR coordinates intentionally repeat; unique lightmap coordinates are preserved separately and are not a claim of baked lighting.

`DepotFloorImport` applies only within the selected Resources/UndeadOvertime/DepotFloor directory. Its explicit menu is **Undead Overtime > Reimport original depot floor**. It processes exactly nine runtime files. It does not change packages, project graphics settings, other importers or source art. Unity may serialize importer metadata for these newly added files; preserve and return any such new diffs for cloud review rather than editing or discarding them locally.

The first import gate uses uncompressed textures and mipmaps to verify fidelity. Full RGBA mip chains would be approximately 80 MiB across these six maps before platform-specific formats/overheads; this is a planning estimate, not a measured GPU allocation. Compression/budget optimization, lightmapping and target-hardware performance are separate gates.

## Arena integration and collision

Only the LAN range's rendered ground changes. An 8 × 8 grid repeats the original 4 m module over the existing 32 × 32 m arena. Three LODs per tile use proposed screen-height thresholds 0.25/0.08/0; the last LOD never distance-culls into a hole. The grid shares two materials, rather than creating per-tile materials. LOD transitions remain subject to visual review.

The original continuous 32 × 32 m ground BoxCollider remains the sole floor collision. Its renderer is hidden to prevent overlapping surfaces. Tile FBXs have no collider, so the visual sawcuts do not create player snags or network physics differences. Source joints recess up to 6 mm below the support plane; the intentional simple collider bridges them. No new obstacle, spawn, enemy navigation rule or network protocol is introduced.

Headless execution creates no floor meshes, renderers, materials or texture loads, and retains the same ground collider. Missing runtime files or malformed renderer/slot counts fail explicitly in rendered mode; they are not silently replaced by an alleged successful asset import.

## Verification sequence

1. Preserve the nine existing local generated-file hashes and confirm no changed source path overlaps them. Fast-forward only to the published candidate; do not reset/stash/overwrite.
2. Allow compilation/asset import to finish. Invoke the exact reimport menu once to apply the committed importer contract to these nine files. Record `UO_DEPOT_FLOOR_IMPORT_OK`; the marker alone is not asset acceptance.
3. Run full suites. Expected source inventory: **143 EditMode / 42 PlayMode**. Eleven new EditMode cases check native model counts/bounds/channels and all texture import settings/resolutions. Three new PlayMode cases check tile coverage/shared PBR/LOD configuration, continuous support at tile boundaries and headless exclusion. Two rendered tests explicitly ignore NullGfxDevice, so record passed/skipped counts; do not report ignored tests as passes.
4. Build the Windows LAN player and run the unchanged full v5 two-process wave harness. Record exact revision, managed-DLL hashes and all earlier gameplay markers.
5. Capture actual rendered Game View from the exact candidate, using the documented public-API host/round/FPS/TPS selection route. Record advancing frames and state. Inspect original surface visibility, UV scale, normals/material response, seams and distance LODs. A screenshot is not manual movement/performance acceptance.
6. Restore the session, stop Play Mode, ensure no player process remains, and compare existing dirty hashes. Return any new importer-generated changes separately for cloud review; never silently absorb unrelated settings.

Before execution, cloud static checks, Blender export/roundtrip checks and source review only establish a candidate. Real LAN, ten-player behavior, manual controls, final environment appearance and performance remain unverified.
