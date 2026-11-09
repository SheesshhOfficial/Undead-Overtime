using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace UndeadOvertime.LanPrototype
{
    /// <summary>Four original brick facade strips. Existing range walls retain all collision and source assets.</summary>
    public sealed class DepotBrickPerimeter : MonoBehaviour, IDisposable
    {
        public const string ResourceRoot = "UndeadOvertime/DepotFacade/";
        private const float BoundsTolerance = .001f;
        private const string MaterialName = "Owned depot perimeter brick PBR";
        private static readonly string[] WallNames = { "North wall", "South wall", "West wall", "East wall" };
        private static readonly Vector3[] StripPositions = {
            new Vector3(-16, 0, 15.93f), new Vector3(16, 0, -15.93f),
            new Vector3(-15.93f, 0, -16), new Vector3(15.93f, 0, 16) };
        private static readonly float[] StripYaw = { 0, 180, 270, 90 };
        private static readonly Bounds StripBounds = new Bounds(new Vector3(16, 2, 0), new Vector3(32, 4, .36f));
        private readonly Renderer[] originals = new Renderer[4];
        private readonly bool[] enabledBefore = new bool[4];
        private Material ownedMaterial;
        private bool disposed;
        public Transform Root { get; private set; }

        public static DepotBrickPerimeter Create(Transform activeIdentityArena, Material acceptedConcrete)
        {
            // Validate ownership and world coordinates before allocating any presentation object or material.
            if (activeIdentityArena == null) throw new ArgumentNullException(nameof(activeIdentityArena));
            if (acceptedConcrete == null) throw new ArgumentNullException(nameof(acceptedConcrete));
            if (!activeIdentityArena.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Depot brick ownership requires an active arena.");
            if (!IdentityWorld(activeIdentityArena))
                throw new ArgumentException("Depot brick perimeter requires a finite identity world-space arena.", nameof(activeIdentityArena));
            if (!Application.isPlaying || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Depot brick perimeter requires rendered Play Mode.");
            if (acceptedConcrete.shader == null || acceptedConcrete.shader.name != "Universal Render Pipeline/Lit" ||
                !acceptedConcrete.IsKeywordEnabled("_NORMALMAP") || !acceptedConcrete.IsKeywordEnabled("_METALLICSPECGLOSSMAP"))
                throw new InvalidOperationException("Depot brick perimeter requires the accepted Lit/PBR material and anchored variants.");
            foreach (var existing in activeIdentityArena.GetComponentsInChildren<DepotBrickPerimeter>(true))
                if (!existing.disposed && existing.Root != null && existing.Root.parent == activeIdentityArena)
                    throw new InvalidOperationException("This arena already has an owned depot brick perimeter.");

            // Shared Resources are loaded once, never cloned, rewritten or destroyed by this owner.
            Texture2D baseColor = LoadTexture("brick_basecolor"), normal = LoadTexture("brick_normal_opengl"),
                packed = LoadTexture("brick_metallic_smoothness");
            var models = new GameObject[3];
            for (int lod = 0; lod < models.Length; lod++)
            {
                models[lod] = Resources.Load<GameObject>(ResourceRoot + "UO_DepotFacade32m_LOD" + lod);
                if (models[lod] == null) throw new InvalidOperationException("Missing original depot facade LOD" + lod + ".");
            }

            var root = new GameObject("Original depot brick perimeter");
            DepotBrickPerimeter owner = null;
            try
            {
                root.transform.SetParent(activeIdentityArena, false);
                // Attach while active, even if all four source walls are inactive.
                owner = root.AddComponent<DepotBrickPerimeter>(); owner.Root = root.transform;
                owner.ownedMaterial = new Material(acceptedConcrete) { name = MaterialName };
                ConfigureBrick(owner.ownedMaterial, baseColor, normal, packed);
                for (int wall = 0; wall < WallNames.Length; wall++)
                    owner.BuildStrip(activeIdentityArena, wall, models, acceptedConcrete);
                return owner;
            }
            catch
            {
                if (owner != null) owner.Dispose();
                else { root.SetActive(false); Destroy(root); }
                throw;
            }
        }

        private void BuildStrip(Transform arena, int wall, GameObject[] models, Material concrete)
        {
            Transform source = FindWall(arena, WallNames[wall]);
            var sourceRenderer = source.GetComponent<MeshRenderer>();
            var sourceFilter = source.GetComponent<MeshFilter>();
            var collider = source.GetComponent<BoxCollider>();
            if (sourceRenderer == null || sourceFilter == null || sourceFilter.sharedMesh == null || collider == null)
                throw new InvalidOperationException("Existing range wall needs its mesh, renderer and BoxCollider: " + WallNames[wall] + ".");
            Vector3 position = wall < 2 ? new Vector3(0, 2, wall == 0 ? 16 : -16) : new Vector3(wall == 2 ? -16 : 16, 2, 0);
            Vector3 scale = wall < 2 ? new Vector3(32, 4, .5f) : new Vector3(.5f, 4, 32);
            if (!Near(source.localPosition, position) || !Near(source.localScale, scale) ||
                !Finite(source.localRotation) || !FiniteIdentityMatrix(Matrix4x4.Rotate(source.localRotation)) ||
                !Near(collider.center, Vector3.zero) || !Near(collider.size, Vector3.one))
                throw new ArgumentException("Existing range wall does not match its accepted collider envelope: " + WallNames[wall] + ".");

            originals[wall] = sourceRenderer; enabledBefore[wall] = sourceRenderer.enabled;
            // This envelope does not depend on Collider.bounds, which is empty for an inactive source.
            var envelope = new Bounds(position, scale);
            var strip = new GameObject("Brick " + WallNames[wall]); strip.transform.SetParent(Root, false);
            strip.transform.localPosition = StripPositions[wall]; strip.transform.localRotation = Quaternion.Euler(0, StripYaw[wall], 0);
            strip.layer = source.gameObject.layer;
            Bounds expected = WorldBounds(strip.transform, StripBounds);
            var lods = new LOD[3];
            for (int lod = 0; lod < models.Length; lod++)
            {
                var instance = Instantiate(models[lod], strip.transform, false); instance.name = "Original facade LOD" + lod;
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                MeshFilter[] filters = instance.GetComponentsInChildren<MeshFilter>(true);
                if (renderers.Length != 1 || !(renderers[0] is MeshRenderer) || renderers[0].sharedMaterials.Length != 2 ||
                    filters.Length != 1 || filters[0].transform != renderers[0].transform || filters[0].sharedMesh == null ||
                    filters[0].sharedMesh.subMeshCount != 2 || instance.GetComponentsInChildren<Collider>(true).Length != 0 ||
                    instance.GetComponentsInChildren<Rigidbody>(true).Length != 0 || instance.GetComponentsInChildren<Animation>(true).Length != 0 ||
                    instance.GetComponentsInChildren<Animator>(true).Length != 0 || instance.GetComponentsInChildren<LODGroup>(true).Length != 0 ||
                    instance.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                    throw new InvalidOperationException("Depot facade must have one two-slot static mesh renderer and no physics, scripts, animation or nested LODGroup.");
                Renderer renderer = renderers[0];
                if (!renderer.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Imported depot facade hierarchy must be active before mirroring source visibility.");
                // Bounds are checked on the actual instance before applying a disabled source's visibility.
                renderer.enabled = true;
                if (!Matches(renderer.bounds, expected) || !Contained(renderer.bounds, envelope))
                    throw new InvalidOperationException("Depot facade import axes, scale or bounds exceed the accepted wall envelope: " + WallNames[wall] + ".");
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = source.gameObject.layer;
                renderer.sharedMaterials = new[] { ownedMaterial, concrete };
                renderer.shadowCastingMode = sourceRenderer.shadowCastingMode; renderer.receiveShadows = sourceRenderer.receiveShadows;
                lods[lod] = new LOD(lod == 0 ? .25f : lod == 1 ? .08f : 0, renderers);
            }
            var group = strip.AddComponent<LODGroup>(); group.fadeMode = LODFadeMode.None; group.animateCrossFading = false;
            group.SetLODs(lods); group.RecalculateBounds();
            // Keep the full 32 m strip in LOD selection, and retain the solid final LOD down to zero height.
            foreach (LOD lod in lods)
            {
                lod.renderers[0].forceRenderingOff = sourceRenderer.forceRenderingOff;
                lod.renderers[0].enabled = enabledBefore[wall];
            }
            strip.SetActive(source.gameObject.activeSelf);
            sourceRenderer.enabled = false; // Only hide an original after its entire three-LOD replacement is ready.
        }

        private static Transform FindWall(Transform arena, string name)
        {
            Transform found = null;
            for (int i = 0; i < arena.childCount; i++)
            {
                Transform child = arena.GetChild(i);
                if (child.name != name) continue;
                if (found != null) throw new InvalidOperationException("Ambiguous existing range wall " + name + ".");
                found = child;
            }
            if (found == null) throw new InvalidOperationException("Missing existing range wall " + name + ".");
            return found;
        }

        private static Texture2D LoadTexture(string name)
        {
            Texture2D texture = Resources.Load<Texture2D>(ResourceRoot + name);
            if (texture == null || texture.width != 2048 || texture.height != 1024)
                throw new InvalidOperationException("Missing or incorrectly sized original depot brick texture " + name + ".");
            return texture;
        }

        private static void ConfigureBrick(Material material, Texture baseColor, Texture normal, Texture packed)
        {
            material.SetTexture("_BaseMap", baseColor); material.SetTexture("_BumpMap", normal); material.SetTexture("_MetallicGlossMap", packed);
            material.SetColor("_BaseColor", Color.white); material.SetFloat("_BumpScale", 1);
            material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1); material.SetFloat("_SmoothnessTextureChannel", 0);
            material.SetFloat("_WorkflowMode", 1); material.SetFloat("_Surface", 0); material.SetFloat("_AlphaClip", 0);
            material.SetFloat("_Blend", 0); material.SetFloat("_SrcBlend", (float)BlendMode.One); material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1); material.SetOverrideTag("RenderType", "Opaque"); material.renderQueue = (int)RenderQueue.Geometry;
            material.SetTexture("_OcclusionMap", null); material.SetTexture("_EmissionMap", null); material.SetColor("_EmissionColor", Color.black);
            foreach (string keyword in new[] { "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", "_ALPHATEST_ON", "_ALPHAPREMULTIPLY_ON",
                "_ALPHAMODULATE_ON", "_EMISSION", "_SPECULAR_SETUP", "_OCCLUSIONMAP", "_SURFACE_TYPE_TRANSPARENT", "_DETAIL_MULX2", "_DETAIL_SCALED" })
                material.DisableKeyword(keyword);
            foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" })
            { material.SetTextureScale(property, Vector2.one); material.SetTextureOffset(property, Vector2.zero); }
        }

        private static Bounds WorldBounds(Transform parent, Bounds local)
        {
            var result = new Bounds(parent.TransformPoint(local.min), Vector3.zero);
            for (int i = 0; i < 8; i++)
                result.Encapsulate(parent.TransformPoint(new Vector3((i & 1) == 0 ? local.min.x : local.max.x,
                    (i & 2) == 0 ? local.min.y : local.max.y, (i & 4) == 0 ? local.min.z : local.max.z)));
            return result;
        }
        private static bool Matches(Bounds actual, Bounds expected) =>
            Near(actual.min, expected.min, BoundsTolerance) && Near(actual.max, expected.max, BoundsTolerance);
        private static bool Contained(Bounds actual, Bounds envelope) => Finite(actual.min) && Finite(actual.max) &&
            actual.min.x >= envelope.min.x - BoundsTolerance && actual.max.x <= envelope.max.x + BoundsTolerance &&
            actual.min.y >= envelope.min.y - BoundsTolerance && actual.max.y <= envelope.max.y + BoundsTolerance &&
            actual.min.z >= envelope.min.z - BoundsTolerance && actual.max.z <= envelope.max.z + BoundsTolerance;
        private static bool Near(Vector3 value, Vector3 expected, float tolerance = .000001f) => Finite(value) &&
            Mathf.Abs(value.x - expected.x) <= tolerance && Mathf.Abs(value.y - expected.y) <= tolerance && Mathf.Abs(value.z - expected.z) <= tolerance;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
        private static bool IdentityWorld(Transform parent) => Finite(parent.position) && Finite(parent.lossyScale) &&
            Finite(parent.rotation) && FiniteIdentityMatrix(parent.localToWorldMatrix);
        private static bool FiniteIdentityMatrix(Matrix4x4 world)
        {
            // Includes inherited shear/reflection and non-finite entries, which distance/angle tests alone miss.
            for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++)
                if (!Finite(world[row, column]) || Mathf.Abs(world[row, column] - (row == column ? 1 : 0)) > .000001f) return false;
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Transform retired = Root; Root = null;
            if (retired != null) retired.gameObject.SetActive(false);
            for (int i = 0; i < originals.Length; i++)
                if (originals[i] != null) originals[i].enabled = enabledBefore[i];
            if (ownedMaterial != null) Destroy(ownedMaterial);
            ownedMaterial = null;
            if (retired != null) Destroy(retired.gameObject);
        }
        private void OnDestroy() { Dispose(); }
    }
}
