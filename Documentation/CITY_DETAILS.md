# City detail pass: staged, additive environment

This pass prepares `Assets/UndeadOvertime/Environment/City/Details/CityDetails.prefab`. It does **not** replace or regenerate `Assets/Scenes/CityMap.unity` and does not save or reload the user's open scene.

The prefab is an editable hierarchy of authored geometry, shared meshes/materials and simple colliders. It contains no scripts, camera, light, mission logic or runtime generation. Its intended parent is the existing CityMap root, with local position/rotation zero and scale one, after the user's current scene edits have been preserved and integration is authorized.

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

The open native CityMap contains unsaved user edits. No instruction to save was assumed, and this pass does not change that scene. Preserve those edits before any exact-scene cloud merge. The intended next step is to obtain the confirmed user-saved scene, compare it with its source revision, append the detail group and preserve every existing object/property, including the user's lighting changes. Do not apply a whole-scene replacement from an older cloud snapshot.

The prefab is staging data, not evidence that these details are already present in the user's open CityMap. Native import/rendering, collision, navigation and performance remain pending. No Play Mode, build, LAN run or test suite was run for this pass. A Blender preview is only an authoring preview.

## File integrity performed

The final serialized-data audit passed: 69 referenced native meshes, 906 prefab mesh renderers and 3,089 GUID references were checked for buffer/index bounds, material slots, reference resolution and uniqueness. A UV-count mismatch in three road-patch meshes was corrected before publication. This is file integrity, not a Unity import or runtime test.
