# Selected original depot facade provenance

Source asset: **AST-GEO-MOD-001 v001**, authored for Undead Overtime on 2026-11-08 with Blender 4.3.2 and deterministic original procedural textures. The original 4 ×3 m wall kit has brick, concrete plinth/coping, three LODs and its own source provenance record.

The selected source Blender SHA-256 is `dd65e7112fc238fc97e8926a86538a8ea01d39c7137f15ae97bc0b10e0d54465`. Its original provenance file SHA-256 is `691ceb7f50e91c8c8de03392828b03639585ab27d523a650589b90c2fd5ad15e`. Cloud verification matched all 34 local source files against the current private extras tree and its manifest before adapting the runtime files.

The source record identifies original AI-assisted geometry and deterministic NumPy/Pillow texture authorship. No downloaded mesh, photographic texture, decal, font, brand or logo is included. A project-owned concept reference informed the old-brick/concrete palette but was not used as a texture and is not in this packet. Its reference rights are not expanded or represented as exclusively owned.

The user authorized publishing selected original project runtime content to the game repository. This record does not assign a new public license or make an exhaustive legal-clearance claim. The private extras repository and public game repository were checked separately. Only six selected runtime binaries are transferred; no editable source bundle, concept pixels, third-party raw content or private source-tree snapshot is published.

## Deliberate runtime adaptations

- Eight copies of the original 4 m module form one 32 m strip, raised 1 m over a new concrete foundation. Existing surface PBR coordinates and appearance are retained except the explicitly clipped endpoints.
- The endpoints are trimmed to the original installed corner footprint. Surviving triangle attributes are interpolated, separate cut components are capped, and exposed coping overlap is removed without moving the existing wall collision. The unexpanded exterior notch is retained.
- LOD0/1 foundation top has two 8 mm shoulder strips where the original beveled plinth does not cover it; its covered central top is intentionally absent. LOD2 has no such shoulder. The assembly is not claimed as a single welded manifold.
- FBX object/mesh names use Detail0/1/2 internally to avoid Unity's automatic name-based LOD grouping. Unity creates the three-LOD groups explicitly at runtime.
- UV0 is the original PBR surface coordinate set. UV1 is a unique atlas with separate cells for original modules/foundation/caps; no positive-area overlap is accepted. Native baked-lightmap padding remains untested.
- Valid per-corner tangent/binormal data is derived from each triangle's actual UV derivatives, because original micro-triangle Mikk output and the installed exporter's multi-UV tangent arrays were defective. The raw source geometry/maps remain unchanged. Import tangents in Unity and check orientation/handedness, not just channel presence.
- The brick basecolor and OpenGL normal are byte-identical originals. Metallic/smoothness is a new exact channel pack: RGB0 and alpha255 minus original roughness. No baked reference image or photo is introduced.
- Accepted concrete maps/material are reused. Basecolor and normal bytes match the wall source; the existing packed concrete map matches its original roughness transformation. No duplicate concrete maps are added.

The exact selected hashes and offline checks are in `DEPOT_FACADE_PACKET.json` and `DEPOT_FACADE_CHECKS.json`. Native Unity import, culling, shader lighting, corner appearance, collision, build and loopback must be accepted separately for this revision.
