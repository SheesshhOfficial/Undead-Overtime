# Original brick depot perimeter candidate

## Scope and current status

The preceding native baseline **b7e07802** passed 340 EditMode /94 PlayMode, a Windows build, both same-PC loopback scenarios and actual visual capture. See `NATIVE_ACCEPTANCE_B7E078.md`. The new brick perimeter is a separate cloud-authored candidate; it does not inherit that native acceptance.

This pass replaces the four **rendered mission perimeter walls** with selected original AST-GEO-MOD-001 brick-wall kit geometry. Eight 4 m original modules form each 32 m strip, raised onto a new 1 m concrete foundation. Three LOD meshes share the original brick maps and the already accepted concrete material. No new downloaded material or generated reference image is used in runtime.

The concrete partitions, gate, power cabinet, paint, floor and HUD remain as accepted. The old concrete perimeter helper remains in source/tests but is no longer selected by the rendered mission. Default range and lobby retain their original grey walls. Headless peers do not load facade art.

## Fixed layout and collision

Each selected FBX has meter bounds X=[0,32], Y=[0,4], Z=[-.18,.18] after Unity import, two material slots (brick then concrete) and no physics or animation. Canonical placement uses:

- North: (-16,0,15.93), yaw 0.
- South: (16,0,-15.93), yaw 180.
- West: (-15.93,0,-16), yaw 270.
- East: (15.93,0,16), yaw 90.

The foundation's inward face meets the existing collider at 15.75 m. All source wall GameObjects, transforms, shared meshes/materials, layers and BoxColliders remain intact. Rendering hides their MeshRenderer only after a complete replacement is ready and restores its previous enabled state on lobby/reset/disposal/failure. Changed source envelopes are rejected rather than silently fitted. Gate, power reach/hold/line-of-sight, extraction and host-authoritative movement do not change.

### Corner correction before publication

Independent inspection of the first straight strip found exposed coplanar coping overlap at every installed corner: approximately 0.0552 m² for LOD0/1 and 0.0576 m² for LOD2. Offsetting the walls vertically would hide rather than correct that problem.

The selected runtime geometry therefore trims the endpoints against Unity-local planes **x+z >= .07** and **x-z <=31.93**, intersected with the existing mesh. This partitions the shared corner diagonally without expanding its original bounds or moving physics. Surviving surfaces retain their original PBR mapping; endpoint topology and cut caps are deliberate adaptations. The small exterior notch outside the old strip endpoints is retained, not presented as a newly extended exterior corner. Hidden mating caps may coincide internally; exposed horizontal overlap must stay within the measured numerical tolerance, rather than the previous broad intersecting faces. Final counts are **2092 /832 /390 triangles** (brick/concrete slots 684+1408, 264+568, 120+270). Independent float32-world inspection found no coping overlap above 1e-12 m² across all 36 corner/adjacent-LOD combinations. A separate conservative ideal-double transform inspection of all horizontal elevations bounds individual triangle-pair residual overlap below **8e-8 m²** (0.08 mm²), with halfspace tolerance 3e-6 m. These checks differ in rounding and scope; neither is a universal exact-zero claim. Actual four-wall and coping/inner-corner Blender renders were inspected with no visible broad overlap or open interior seam.

## Tangents, maps and import

Original brick basecolor and OpenGL normal bytes are preserved. The only new map packs metallic=0 into RGB and smoothness=255 minus the original roughness into alpha. It imports linearly; basecolor is sRGB, normal is explicitly a normal map with no green flip/conversion. All are 2048 ×1024, Texture2D, Repeat, mipmapped, uncompressed, maximum 2048.

During cloud QA, the original tiny wall-cap triangles produced zero stock Mikk tangents, and the installed Blender FBX exporter wrote the last UV layer's tangents for both UV layers. The selected export instead derives each per-corner tangent/binormal from that triangle's actual UV derivatives, with finite/orthogonal/nonzero/handedness checks. Unity imports the validated normals and tangents rather than silently recalculating Mikk. UV0 is the original tiled PBR mapping. UV1 is a separate unique atlas, not an overlapping duplicated lightmap claim.

Only six selected runtime files are transferred, with exact hashes and narrow ordinary-Git exceptions. Sources, source reference images and private asset trees stay out of the public game repository. Full importer metadata is supplied; GUID-only metadata and cubemap shape are rejected by static fixtures. No existing floor or cabinet metadata/material is changed by this candidate. The build helper adds a read-only dependency gate, never a repair/reimport/save.

## Ownership and bounded cost

Four strip instances share three imported meshes and three brick maps. Twelve renderers belong to four LODGroups; only the selected LOD is rendered per strip. Material slot 0 shares one runtime-owned brick clone of the accepted Lit variant, slot 1 borrows the accepted concrete material. Disposal destroys only owned root/material. Imported meshes, maps and concrete remain unchanged. Partial construction and destruction while sources are inactive are covered by authored tests.

LOD transitions are .25/.08/0 with no crossfade. The full 32 m strip is used for screen-size selection, so the highest LOD may remain active inside much of the arena. This is an honest bounded geometry choice, not a claimed optimization benchmark. No instancing switch or performance claim is made.

## Cloud validation

The unchanged engine-independent rules were compiled and executed again with the approved pinned SDK: **310/310 passed**, zero failures/warnings/skips, 18.225 seconds on 2026-11-08. Source and lock hashes remained unchanged. See `DEPOT_FACADE_CORE_RESULT.json`. This is actual pure-core execution, not compilation or execution of the Unity facade classes. Static consistency (110 assets, nine assemblies, 127 GUIDs), 96 attribute assertions, 33 malformed metadata fixtures and 75-file C#9 syntax checks pass. Independent source/binary review passed after the corner correction. See DEPOT_FACADE_CHECKS.json for hashes, numerical tolerances and limits. Native expected inventory is **346 EditMode /110 PlayMode**, authored but unrun for this candidate.

## Native acceptance still required

Use the exact published candidate and the existing local execution chat. Preserve all original protected paths plus six locally normalized cabinet metadata files. Do not discard unrelated changes, reimport existing floor assets, save protected materials, reset/stash or create a branch.

1. Verify fresh branch/head, incoming path non-overlap and all protected hashes. Fast-forward only to the reviewed candidate.
2. Normal import of the six new facade files; compile and fresh discovery. If stale results return, establish assembly freshness before counting them as this candidate.
3. Full EditMode and PlayMode suites. New EditMode cases check dimensions, slots, UV/tangent channels, UV0 derivative orientation/handedness, normal winding, trimmed half-planes, explicit import settings and texture sizes. New PlayMode cases exercise canonical poses, renderer bounds, ownership, unchanged source collision/materials, inactive sources, rollback, repeated cleanup and headless exclusion.
4. One explicit Windows build, then the existing default-range and expanded mission loopbacks. Preserve complete results and exact managed DLL fingerprints. Report native log diagnostics separately from managed counters.
5. Actual FPS/TPS and labelled inspection captures in RestorePower with zero enemies. Inspect brick scale/normal lighting on both sides, foundation/coping seams, all four trimmed corner joints and forced LODs. Check normal mouse/camera and collision if separately authorized; captures alone are not manual playtesting.
6. End stopped/disconnected, restore only session-owned inspection camera state, verify clean scene/background state and protected hashes. Record importer normalization rather than overwriting it automatically.

This candidate does not provide the rest of the city, production characters/animation, audio, ten-player or separate-PC LAN acceptance.
