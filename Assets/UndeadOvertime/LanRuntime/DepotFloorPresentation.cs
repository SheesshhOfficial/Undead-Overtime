using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace UndeadOvertime.LanPrototype
{
    /// <summary>Original AST-GEO-MOD-005 rendered floor only. The existing range ground owns all collision.</summary>
    public sealed class DepotFloorPresentation : MonoBehaviour
    {
        public const string ResourceRoot = "UndeadOvertime/DepotFloor/";
        public const int GridSize = 8;
        public const float TileSize = 4;
        public int TileCount { get; private set; }
        public static DepotFloorPresentation Create(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var models = new GameObject[3];
            for (int i = 0; i < models.Length; i++)
            {
                models[i] = Resources.Load<GameObject>(ResourceRoot + "UO_DepotFloor_4m_LOD" + i);
                if (models[i] == null) throw new InvalidOperationException("Missing original depot floor LOD" + i + ". Import the complete runtime packet.");
            }
            Material topMaterial = LoadMaterial("floor_material"), sideMaterial = LoadMaterial("concrete_material");
            var root = new GameObject("Original depot floor tiles (visual only)"); root.transform.SetParent(parent, false);
            var result = root.AddComponent<DepotFloorPresentation>();
            try
            {
                for (int x = 0; x < GridSize; x++) for (int z = 0; z < GridSize; z++)
                {
                    var tile = new GameObject("Depot floor " + x + "," + z); tile.transform.SetParent(root.transform, false);
                    tile.transform.localPosition = new Vector3(-16 + x * TileSize, 0, -16 + z * TileSize);
                    var lods = new LOD[3];
                    for (int i = 0; i < models.Length; i++)
                    {
                        var instance = Instantiate(models[i], tile.transform, false); instance.name = "Original floor LOD" + i;
                        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                        if (renderers.Length != 1 || renderers[0].sharedMaterials.Length != 2 || instance.GetComponentsInChildren<Collider>(true).Length != 0 ||
                            instance.GetComponentsInChildren<LODGroup>(true).Length != 0)
                            throw new InvalidOperationException("Depot floor import must have one two-slot renderer, no collider and no nested LODGroup.");
                        renderers[0].sharedMaterials = new[] { topMaterial, sideMaterial };
                        renderers[0].shadowCastingMode = ShadowCastingMode.Off; renderers[0].receiveShadows = true;
                        lods[i] = new LOD(i == 0 ? .25f : i == 1 ? .08f : 0, renderers);
                    }
                    var group = tile.AddComponent<LODGroup>(); group.fadeMode = LODFadeMode.None;
                    group.SetLODs(lods); group.RecalculateBounds(); result.TileCount++;
                }
                return result;
            }
            catch { Destroy(root); throw; }
        }
        public static Material LoadConcreteMaterial() => LoadMaterial("concrete_material");
        private static Material LoadMaterial(string name)
        {
            // Serialized Resources materials anchor the required Lit/PBR variants and texture dependencies in player builds.
            // These shared assets are never mutated or destroyed by an arena instance.
            Material material = Resources.Load<Material>(ResourceRoot + name);
            if (material == null) throw new InvalidOperationException("Missing serialized depot floor material " + name + ".");
            if (material.shader == null || material.shader.name != "Universal Render Pipeline/Lit")
                throw new InvalidOperationException("Depot floor material " + name + " has invalid shader " + (material.shader == null ? "null" : material.shader.name) + ".");
            foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" })
                if (material.GetTexture(property) == null)
                    throw new InvalidOperationException("Depot floor material " + name + " has missing texture " + property + ".");
            foreach (string keyword in new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP" })
                if (!material.IsKeywordEnabled(keyword))
                    throw new InvalidOperationException("Depot floor material " + name + " has missing shader keyword " + keyword + ".");
            return material;
        }
    }
}
