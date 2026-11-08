# Mission-only original concrete perimeter

This follow-up to the bounded partition/gate candidate `4e7bffc9ef703ed53af18ec6ab7a9703dac05fb0` carries the same accepted original concrete treatment around the four existing range walls during the depot mission. It reuses the already distributed wall-concrete PBR maps and material. No new binary asset, material asset or importer setting is involved.

## Exact scope and preservation

Each existing outer wall keeps its original transform, BoxCollider, mesh, shared material and game object. During a rendered mission, a coincident **renderer-only** visual copy uses a private meter-scaled UV mesh and the accepted shared concrete. The original renderer is hidden to prevent coplanar surfaces. Thus four visible wall draws replace the previous four; this is not a second visible wall layer or an extra collider enclosure.

The default range and mission lobby keep the original presentation. Round cleanup immediately hides the copies and restores the prior enabled state of every original renderer. Reset/reconnect creates fresh copies; no accumulated visibility changes or shared-asset mutations are intended. A late constructor failure follows the same rollback, including walls already processed.

Copies inherit each source's local position, rotation, scale, layer, shadow settings and active state. Ownership is attached while the copy is active, before mirroring an inactive source, so Unity can run the mesh owner's destruction callback. Direct inactive wall-owner attachment is rejected before allocating a mesh. Existing source renderers that were disabled remain disabled after cleanup.

Including the two partitions, there are six small owned cube UV copies during a rendered mission. Their geometry is unchanged. The twenty shared-cube gate relief pieces from the prior candidate remain. No texture duplication, extra lights, fog, post-processing, network packet, objective, weapon, spawn, physics, scene serialization or saved configuration change is introduced. No frame-rate claim is made without profiling.

Headless mission construction bypasses perimeter creation entirely. The current floor collision and all four original perimeter colliders remain the only relevant floor/enclosure physics. The user PC and running player have not been changed by this cloud work.

## Validation status

- Independent source review found no blocking issue in ownership, rollback, renderer restoration, physics parity or mission lifecycle wiring.
- Static checks passed: 88 source/assets, nine assemblies, 103 GUIDs; 48 attribute assertions; 15 importer fixtures. C# 9 syntax-only parse accepted 65 files with zero errors. This is not Unity API compilation.
- The exact pure-core source/lock hashes still match the prior actual **310/310** passing run in `DEPOT_ARCHITECTURE_CORE_RESULT.json`. The rules were not changed and a new core execution is not claimed for this follow-up.
- Three native cases are authored for exact wall coincidence/collider preservation, repeated same-frame disposal with an inactive source and late-construction rollback. The existing rendered mission integration case now checks six wall surfaces, original renderer restoration in lobby, and another round. These native checks remain unrun.
- The combined expected native inventory is now **334 EditMode /77 PlayMode**. Graphics-dependent skips do not constitute rendering or cleanup acceptance.

The later batched acceptance remains native compile, full suites, one rebuild, default-range and expanded mission replay loopbacks, then actual FPS/TPS visual review with objective guidance and all wall/gate changes. Preserve local dirty files, do not reimport/save floor materials, and do not interrupt the user playing without coordination. Refer to `DEPOT_ARCHITECTURE.md`, `MISSION_NAVIGATION.md` and `MISSION_LIFECYCLE.md` for the other candidate contracts and required markers.
