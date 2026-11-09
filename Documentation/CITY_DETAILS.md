# City detail pass: additive environment

This pass provides `Assets/UndeadOvertime/Environment/City/Details/CityDetails.prefab` and an authored copy in `Assets/Scenes/CityMap.unity`. The scene copy is expanded editable geometry under the existing city root, not runtime generation. The reusable prefab is also retained; do not add a second copy to this scene.

The prefab is an editable hierarchy of authored geometry, shared meshes/materials and simple colliders. It contains no scripts, camera, light, mission logic or runtime generation. The scene detail group's parent is the existing CityMap root, with local position/rotation zero and scale one.

## Authored detail

- Readable landmark and business signs for the diner, laundry, market, police/fire stations, municipal hall, hospital, clinic, school, supermarket, fuel station, depot, terminal, ferry, broadcast tower, power station and water plant.
- Street-name boards across the grid and six furnished transit stops.
- Planters, bins, newspaper boxes, bollards, parked cars, supply crates, original newly authored pallets, trash clusters and localized repair barriers/cones.
- Paved service yards and parking courts, marked bays and wheel stops, civic entrance paving and residential garden strips.
- Fire escapes, roof ventilation equipment, manholes and irregular road patches.
- Existing original depot doorway/corner modules on the three staging shelters, at their original metre dimensions. The doorway's 1.4 by 2.4 metre opening has separate jamb/header collision, not an enclosing box.

This is an environment dressing pass, not final production art or completed gameplay. Decorative fire escapes are not a climbing system. Bus shelters are not vehicles. Added signage does not implement missions, quarantine rules, shops or evacuation.

## Existing assets and provenance

The selected doorway and corner originate from the project's private `AST-GEO-MOD-003` and `AST-GEO-MOD-002` runtime candidates at fbxfiles commit `69811e37b93dfe2cd352a6820fe712e0a1a0b021`. Their original provenance accompanies the native derivatives. Geometry, original UV0 and custom normals are retained; native streams include tangents for URP normal mapping. Coordinate reflection includes matching triangle reversal and tangent-handedness correction. Original UV1 remains in private source, not the native 48-byte stream; no baked-lightmap readiness is claimed.

Five exact source texture hashes match the already accepted public depot textures, so those canonical texture imports are reused. The distinct original concrete metallic/smoothness map is retained unchanged with its own metadata. Existing accepted texture files and metadata are not modified.

The legacy depot-sign candidate is deliberately not included because two exact texture files could not be materialized. The new business/street lettering is original scene dressing generated with Blender's bundled Bfont and delivered as geometry; no font binary is distributed. The new pallet is fresh box/board geometry from the included source script, not the unresolved-provenance recovered pallet packet. No private characters, Mixamo content, purchased models or unrelated recovered files are included.

## Integration and preservation

The user-saved CityMap was received with SHA-256 `5a7d057b47ba610862bec6669f51540ab4ac60d082aaeccbd588fad76c5db88f`. The integration adds 921 objects under `11 CITY DETAILS | Street life and landmarks`. All 9,118 existing serialized documents are retained. The only edit to an existing document is the new child entry in city root transform 1001. Removing that entry and the new documents restores the exact saved input byte-for-byte, including user lighting and road changes.

`CITY_DETAILS_INTEGRATION.json` records exact source/result hashes and counts. Native import and visual acceptance are a separate stage; the source integration itself is not evidence that the user's editor already displays this revision. No Play Mode, build, LAN run or test suite was run for this pass. A Blender preview is only an authoring preview.

`Tools/apply_city_details_sync.py` is a narrowly guarded execution handoff for the recorded saved scene and native main revision. It refuses changed scene bytes, unexpected branch/HEAD, staged scene edits and overlaps with other dirty files. It preserves all unrelated local index/content states, never resets/stashes/discards work, and does not control Unity or save an open scene. The local executor runs the handoff inside one synchronous Unity editor evaluation, keeping the editor main thread occupied so scene edits/saves cannot occur between the clean-scene guard and installation. It confirms idle, clean Edit Mode before work and again before refresh/reopen. If the user starts editing again, stop rather than reloading.

## File integrity performed

The final serialized-data audit passed: 69 referenced native meshes, 906 prefab mesh renderers and 3,089 GUID references were checked for buffer/index bounds, material slots, reference resolution and uniqueness. A UV-count mismatch in three road-patch meshes was corrected before publication. This is file integrity, not a Unity import or runtime test.

The installed standard Git LFS post-merge hook remains enabled and unchanged. The guard permits only its exact reviewed text, refuses reference-transaction hooks and rejects lockable files or incoming lockable attributes. No hook or Git configuration is suppressed or changed. The upstream post-merge implementation only adjusts lockable-file write flags: https://github.com/git-lfs/git-lfs/blob/main/commands/command_post_merge.go . The isolated preservation fixture also passed with the normal Git LFS hook enabled.
