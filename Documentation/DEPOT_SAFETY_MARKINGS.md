# Depot loading-bay and safety paint candidate

## Scope

A bounded original floor-paint pass follows the cabinet candidate on main. It adds industrial bay outlines, cabinet-approach clearance paint and dashed edges alongside the centre gate lane. This is static visual dressing, not route authorization or a new objective system. The user requested building the depot before a combined native test batch.

It is wired under the rendered mission's architectural-detail condition. Default range, headless physics, power/gate authority, waves and extraction rules do not change. There are no arrows promising an unlocked gate and no paint within the extraction marker. The accepted Windows player remains 917d94 until the later batch passes.

## Geometry and ownership

- One combined mesh (41 disjoint quads, 82 triangles, 164 vertices) and one renderer, sharing the existing amber mission-control material. No new material, shader, texture, binary download or mesh collider.
- Four thin loading-bay rings: x[-13,-5] or[5,13], crossed with z[1,5] or[7,11]. Their central path and extraction rectangle remain clear.
- A U-shaped clearance outline around the cabinet. Side strips lie at x[-10.15,-10.05] and[-7.95,-7.85], z[-6.7,-5.25]. The south cross-strip spans x[-10.15,-7.85], z[-6.8,-6.7]. Its centre approach is inside the unchanged two-metre power range, but the host's actual range/LOS/hold checks remain authoritative.
- Dashed centre-lane edges near x=+/-1.65, from the south yard toward the north yard. The closed gate band z[-2.35,-1.65] is left clear. These are painted lane boundaries, not barriers.
- Planar strips at y=0.012m, with up-facing normals and triangles, cast no shadows and receive lighting/shadows. Thin non-overlapping joins avoid stacked coplanar paint surfaces. The offset above the existing visual floor must still be inspected for floating/z-fighting in native views.
- Ownership is attached only under an active identity-world parent. Missing materials, transformed/non-finite parents and non-rendered execution are rejected before allocating owned geometry. Only the generated mesh and object are destroyed; the shared amber material stays caller-owned.

The repeated/lobby lifecycle is covered by new component tests plus expanded existing rendered mission integration assertions. Headless layouts still have their original four physical colliders and no paint mesh/component. This is not a decal-projection system, a wet-paint simulation or finished environment weathering.

## Verification boundary and combined native gate

See `DEPOT_SAFETY_MARKINGS_CHECKS.json` for exact cloud checks. The combined source inventory is **340 EditMode / 94 PlayMode**, including 12 new paint cases. The graphics-aware contract checks only the branch supported by its actual device; it is not proof that both graphics configurations ran. Source tests are authored, not native results. C# syntax parsing does not bind Unity APIs.

Use the exact pinned final candidate with the preservation, import, full-suite, build, range/mission loopback and capture steps in [DEPOT_CONSOLE.md](DEPOT_CONSOLE.md). Do not interrupt an active user play session. No floor reimport/material save, project setting change, new branch or local source repair is needed.

In native FPS/TPS views inspect bay proportions, clear centre travel, cabinet approach, gate-band separation, extraction visibility and any paint shimmer. Confirm all paint disappears on lobby/disposal and returns once on a new mission; confirm collision/line-of-sight and gameplay outcomes remain unchanged. Record new import normalization separately and preserve protected local state. Real LAN, ten players, performance and hand-play quality still need their own evidence.
