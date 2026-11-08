# Bounded depot architecture presentation

## Scope

This cloud candidate builds on the objective-guidance candidate `fb3cf92aa94d7e45c95c70221a0b56788fce5704`. The two existing depot partitions now reuse the accepted original concrete PBR material with one texture repeat per meter, rather than stretching a single texture over each 14-meter wall. The existing gate receives a modest painted-shutter silhouette: eight narrow horizontal ribs and two caution edges on each face. Orange control and blue extraction cues remain distinct.

This is one bounded presentation improvement, not the finished depot, city, character art, lighting or animation pass. The outer range walls and all existing gameplay geometry remain unchanged. The user's currently accepted player is not overwritten or interrupted by cloud publication.

## Reused assets and provenance

Only the already accepted `UndeadOvertime/DepotFloor/concrete_material` and its three existing textures are reused. The concrete maps originated in original project wall asset AST-GEO-MOD-001 and were already included on the accepted AST-GEO-MOD-005 floor sides. See `DEPOT_FLOOR_PROVENANCE.md` and `DEPOT_FLOOR_PACKET.json`. No private extras-tree copy, new raw mesh, Mixamo content, binary upload, material save, texture rewrite, importer change or new dependency is included. Floor-joint textures are not placed on walls.

The gate retains the existing shared slate and orange materials. The concrete remains nonmetallic. This treatment does not claim a new realistic metal PBR material.

## Runtime ownership and invariants

- The two existing cube renderers receive private mesh copies only to change UV0 and recalculate tangents. Positions, normals, bounds, transforms and BoxCollider data remain unchanged. All six faces use signed axis projections with one repeat per meter, preserving outward tangent orientation.
- Never mutate the built-in cube mesh, shared concrete material, texture tiling/offset or imported texture assets. Each `DepotWallSurface` owns and destroys only its runtime mesh copy when its wall is destroyed.
- Twenty small gate detail objects share the built-in cube mesh and existing materials. They have no colliders, rigid bodies or scripts. Their maximum surface relief is two centimeters beyond the gate face, a rendering detail, not a new physical barrier. Rib ends stop three centimeters before the caution strips to avoid overlapping coplanar color patches. They are parented beneath the existing gate and disappear with it when the authoritative gate opens.
- Existing west/east partitions, gate and console retain their four blocking colliders, exact dimensions and transforms. Console line-of-sight, extraction rectangle, movement, aiming, projectiles, spawning, path checks and host authority are not changed.
- Rendered mission creation requests the existing validated concrete material; headless construction never loads it or creates surfaces, mesh copies or relief. The default range has no new architecture objects. Idle/lobby mode still contains only the prior accepted range.
- Disposal disables the whole mission root immediately, preserving existing physics cleanup, then destroys owned objects normally. Shared assets remain available for the next round.
- No scene, material, importer, shader, package, project setting, protocol, input timing or network serialization file is changed. No user PC operation is included.

## Checks performed and remaining

The unchanged production pure C# rules were compiled for .NET Standard 2.1/C# 9 and **310/310 NUnit cases passed**, with zero warnings, failures, skips or inconclusive cases. Exact source hashes and run timestamps appear in `DEPOT_ARCHITECTURE_CORE_RESULT.json`. These tests exercise the existing gameplay rules and guidance, **not Unity mesh operations or the new rendered environment**.

Static validation passed for 86 source/assets, nine assemblies and 101 metadata GUIDs. All 48 Git attribute assertions and 15 malformed-importer fixtures passed. The C# 9 syntax-only parser accepted 63 files with zero syntax errors; it does not bind Unity/NGO APIs.

Independent source review found and corrected coplanar rib/stripe overlap before publication. Native bounds assertions now guard the separation.

Seven native cases are authored for per-face UV density and tangent validity, original-mesh/material/collider preservation, invalid/repeated setup rejection, owned-copy disposal, actual rendered gate/collider behavior, headless exclusion and the full LanRange mission/lobby wiring. They have not yet run. The expected combined inventory after guidance and this presentation change is **334 EditMode /74 PlayMode**.

## One later guarded native acceptance batch

Per the user's latest instruction, finish the bounded build first and test together afterward. Coordinate the exact final published SHA with the sole native runner, after the user finishes playing or authorizes interruption. Preserve the existing dirty metadata/materials/settings and do not reimport the floor packet or run a material-save command for this change.

1. Record fresh local state and hashes, verify the exact incoming changed paths do not overlap protected local changes, then guarded fast-forward only.
2. Native compile, full EditMode and PlayMode suites; stop on failure and preserve the full result files.
3. One Windows rebuild only if suites pass, then one default-range loopback and one expanded mission replay/reconnect loopback. Expected mission markers remain 17 host and 15 client, including a fresh second connection and round-two power/reload.
4. Actual rendered FPS/TPS Game View review: concrete grain has consistent scale without severe stretch, gate relief follows opening/lobby reset, console/extraction remain legible, and waypoint line does not overlap the HUD or disappear incorrectly.
5. Verify owned process cleanup and protected hashes. Report automatic Unity serialization changes rather than restoring user files.

No native compile/test/build result, screenshot acceptance, manual play quality, cross-computer LAN, ten-player behavior or performance benchmark is claimed for this candidate yet.

## Later perimeter extension

`DEPOT_PERIMETER.md` documents the follow-up reuse on the four existing outer walls, with exact renderer restoration and no collision changes. Its combined expected test count supersedes this earlier candidate's count; original stage results above remain historical evidence.
