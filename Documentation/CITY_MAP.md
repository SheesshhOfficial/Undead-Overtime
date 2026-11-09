# CityMap: editable full-city environment pass

Open `Assets/Scenes/CityMap.unity` in Unity **6000.6.5f1**. Unity scene files use `.unity`, not `.scene`. All environment geometry is saved in the scene and is visible in Edit Mode. No runtime generation, custom runtime component, menu command or Play Mode step is required.

## Delivered layout

- 960 by 720 metre connected street grid: 48 city blocks within a 1,270 by 1,040 metre ground/waterfront layout.
- Six named district roots: Freight Ward, Ash Street, Civic Mile, St Alder Medical Quarter, Southbank Works and Breakwater. These are the previously proposed world-plan names, now used as editable layout labels.
- 111 individually selectable building instances across 22 original architecture types. Each has a near and distant mesh in an editable LODGroup; individual facade elements are combined in reusable meshes rather than thousands of separate window GameObjects.
- 2,285 named scene GameObjects and 79 reusable meshes, including street furniture, parked cars, delivery trucks, trees, containers, piers, rail sidings and utility structures.
- Diner, laundry, corner market, residential streets, apartments, downtown offices, broadcast tower, civic hall, police station, fire station, school, hospital/clinic, supermarket, fuel station, depot, power station, water plant and ferry/quarantine terminal.
- Raised sidewalks and crosswalks, double-yellow lanes, parking bays, street lamps, traffic-signal geometry, hydrants, benches and dumpsters.
- Three simple enterable staging shelters with individually editable wall/floor/roof colliders and unobstructed entry openings.
- Ten co-op staging transforms and six story-route/objective transforms. These are **design markers only**, not active spawn, networking, mission or progression systems.

## Edit Mode workflow

1. Open `Assets/Scenes/CityMap.unity` without entering Play Mode.
2. Select `CITY MAP | Full-city environment layout` in Hierarchy and press F to frame the map. Expand a district, then a named block, to move a building or prop.
3. For a building, edit its parent transform to keep near/distant meshes and collision aligned. Materials are shared under `Assets/UndeadOvertime/Environment/City/Materials`; duplicate a material deliberately if only one building should change.
4. The saved `City Overview Camera` supplies an initial Game-view overview. Scene-view navigation remains normal Unity navigation.
5. Other scenes and build settings are unchanged. CityMap is deliberately not added to the existing LAN build helper or connected to the old runtime-generated arena.

## Art and provenance

This is an original, low-detail full-city environment/layout pass, not a finished photorealistic city. New geometry was authored in cloud Blender and saved as editable Blender source plus an FBX interchange kit in the private fbxfiles counterpart. Native Unity mesh assets use stable explicit IDs, so opening the scene does not depend on an FBX importer assigning unknown internal IDs.

The city materials reuse the existing original depot brick/concrete base-color textures already in this public game repository. Those sources and their metadata remain unchanged. New materials are shared URP Lit materials. No purchased or Mixamo content, third-party source mesh, private character, unresolved-provenance pallet or recovery packet was copied into this public scene.

New runtime meshes are the original artist-authored geometry, with per-material submeshes, vertex normals and tiled UV0. They do not yet carry production UV1/lightmap unwraps or normal-map tangents. Native near/distant architectural meshes have the same footprint; LOD visuals and transition distances still need the combined native review.

Private source: `Undead Overtime Extrafiles/assets/environment/CITY-001/full_city_v001/` in `SheesshhOfficial/fbxfiles`. This private location is not a dependency for opening the public Unity scene.

## Deliberate limits and combined pass

The user requested building the environment first and deferring repeated checks. No Unity tests, Play Mode, build, LAN run or performance benchmark was run for this change. A Blender overview verifies the authoring layout only; it is not a Unity screenshot or native acceptance.

Most buildings are exterior shells. The three staging shelters are the only deliberately enterable interiors. The evacuation ramp has visual geometry but its final traversable collider is pending. Waterfront/outer-world collision, seamless walkability, cover proportions, street/doorway clearance, AI navigation, occlusion baking, baked lighting, water effects, destruction, moving vehicles, missions, gates and final environmental storytelling remain later passes. Street lamps/signals are geometry, not hundreds of realtime lights.

The approximate RTX 20-series / 4 GB target is a design goal, not a verified minimum. Shared meshes/materials, LODs, simple box collisions and no per-prop realtime lights reduce initial cost, but actual editor/game draw calls, memory and frame time must be measured later.

The combined native pass should first inspect import/reference/rendering correctness and screenshot the map in Edit Mode, then cover collision/navigation/LOD/performance once gameplay is integrated. Do not reset, stash, overwrite or include the user's pre-existing local metadata/material/settings changes. Keep the same main branch and preserve the original scenes.

## Editor daylight follow-up

The scene registered and opened cleanly on the native PC after a normal synchronous AssetDatabase refresh. The initial far-away Scene View overview was obscured by exponential fog. The lighting follow-up disables fog for this editable layout, assigns the scene sun explicitly, uses balanced sky/equator/ground ambient fill and softer directional shadows. It changes CityMap only. The corrected native visual capture remains the acceptance step; no Play Mode, build, gameplay or performance acceptance is implied.
