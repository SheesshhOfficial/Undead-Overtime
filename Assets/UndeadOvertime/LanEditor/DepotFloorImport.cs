using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UndeadOvertime.LanPrototype.Editor
{
    /// <summary>Explicit import contract only for the selected original runtime floor packet.</summary>
    public sealed class DepotFloorImport : AssetPostprocessor
    {
        public const string AssetRoot = "Assets/UndeadOvertime/Resources/UndeadOvertime/DepotFloor/";
        public override uint GetVersion() => 2;
        private bool IsFloor => assetPath.StartsWith(AssetRoot, StringComparison.Ordinal);
        private void OnPreprocessModel()
        {
            if (!IsFloor || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false; importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Off; importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.generateSecondaryUV = false; importer.preserveHierarchy = true;
        }
        private void OnPreprocessTexture()
        {
            if (!IsFloor || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;
            var importer = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("_normal_opengl.png", StringComparison.Ordinal);
            bool color = assetPath.EndsWith("_basecolor.png", StringComparison.Ordinal);
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.convertToNormalmap = false; importer.flipGreenChannel = false;
            importer.sRGBTexture = color; importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false; importer.mipmapEnabled = true; importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed; // First import gate prioritizes fidelity; budget optimization is separate.
            importer.npotScale = TextureImporterNPOTScale.None;
        }
        [MenuItem("Undead Overtime/Reimport original depot floor")]
        public static void Reimport()
        {
            // A request marker is not acceptance. Import dependencies synchronously, then validate that
            // Unity actually produced assets before reporting success. Native geometry tests still follow.
            string[] textures = { "floor_basecolor.png", "floor_normal_opengl.png", "floor_metallic_smoothness.png",
                "concrete_basecolor.png", "concrete_normal_opengl.png", "concrete_metallic_smoothness.png" };
            foreach (string file in textures) RequireImporterMetadata(file, "TextureImporter", 10);
            for (int lod = 0; lod < 3; lod++) RequireImporterMetadata("UO_DepotFloor_4m_LOD" + lod + ".fbx", "ModelImporter", 25);
            foreach (string file in textures) Import(file);
            for (int lod = 0; lod < 3; lod++) Import("UO_DepotFloor_4m_LOD" + lod + ".fbx");
            Import("floor_material.mat"); Import("concrete_material.mat");
            foreach (string file in textures)
            {
                string path = AssetRoot + file;
                if (!(AssetImporter.GetAtPath(path) is TextureImporter) || AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
                    throw new InvalidOperationException("Depot floor texture did not import: " + path + ". Inspect importer schema and native Console diagnostics.");
            }
            for (int lod = 0; lod < 3; lod++)
            {
                string path = AssetRoot + "UO_DepotFloor_4m_LOD" + lod + ".fbx";
                if (!(AssetImporter.GetAtPath(path) is ModelImporter) || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    throw new InvalidOperationException("Depot floor model did not import: " + path + ". Inspect importer schema and native Console diagnostics.");
            }
            // Persist only the two approved material assets after all typed dependencies resolve.
            // Runtime-only keyword changes cannot preserve build-time shader variants.
            int savedMaterials = 0;
            Material[] materials = { ValidateMaterialDependencies("floor"), ValidateMaterialDependencies("concrete") };
            foreach (Material material in materials)
            {
                if (ConfigureRequiredKeywords(material))
                {
                    EditorUtility.SetDirty(material);
                    AssetDatabase.SaveAssetIfDirty(material);
                    savedMaterials++;
                }
            }
            // One real serialization round trip, not an in-memory success assertion or retry loop.
            Import("floor_material.mat"); Import("concrete_material.mat");
            ValidateMaterialAssets();
            Debug.Log("UO_DEPOT_FLOOR_IMPORT_OK files=11 saved-materials=" + savedMaterials + " reloaded-keywords-and-dependencies=verified; geometry/rendering acceptance still requires tests.");
        }
        /// <summary>Read-only player-build gate. Does not repair or save project state implicitly.</summary>
        public static void ValidateMaterialAssets()
        {
            foreach (string prefix in new[] { "floor", "concrete" })
            {
                Material material = ValidateMaterialDependencies(prefix);
                foreach (string name in new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP" })
                {
                    LocalKeyword keyword = material.shader.keywordSpace.FindKeyword(name);
                    if (!keyword.isValid || !material.IsKeywordEnabled(keyword) || !material.IsKeywordEnabled(name))
                        throw new InvalidOperationException("Depot floor material " + prefix + " is missing declared/enabled PBR keyword " + name + ". Run the explicit depot-floor reimport/setup command before building.");
                }
            }
        }
        /// <summary>Normalize only the two required local shader flags. File persistence is separately scoped.</summary>
        public static bool ConfigureRequiredKeywords(Material material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (material.shader == null || material.shader.name != "Universal Render Pipeline/Lit")
                throw new InvalidOperationException("Depot floor keyword setup requires the installed URP Lit shader.");
            foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" })
                if (!(material.GetTexture(property) is Texture2D))
                    throw new InvalidOperationException("Depot floor keyword setup requires resolved Texture2D dependency " + property + ".");
            LocalKeyword normal = material.shader.keywordSpace.FindKeyword("_NORMALMAP");
            LocalKeyword metallic = material.shader.keywordSpace.FindKeyword("_METALLICSPECGLOSSMAP");
            if (!normal.isValid || !metallic.isValid)
                throw new InvalidOperationException("Installed URP Lit shader does not declare the required depot floor PBR keywords.");
            bool needsNormal = !material.IsKeywordEnabled(normal), needsMetallic = !material.IsKeywordEnabled(metallic);
            if (needsNormal) material.SetKeyword(normal, true);
            if (needsMetallic) material.SetKeyword(metallic, true);
            return needsNormal || needsMetallic;
        }
        private static Material ValidateMaterialDependencies(string prefix)
        {
            string path = AssetRoot + prefix + "_material.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == null || material.shader.name != "Universal Render Pipeline/Lit" ||
                AssetDatabase.GetAssetPath(material.shader) != "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader")
                throw new InvalidOperationException("Depot floor material or installed URP Lit shader did not resolve: " + path);
            string[] properties = { "_BaseMap", "_BumpMap", "_MetallicGlossMap" };
            string[] suffixes = { "basecolor", "normal_opengl", "metallic_smoothness" };
            for (int i = 0; i < properties.Length; i++)
            {
                Texture2D expected = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetRoot + prefix + "_" + suffixes[i] + ".png");
                if (expected == null || material.GetTexture(properties[i]) != expected)
                    throw new InvalidOperationException("Depot floor material dependency did not resolve: " + path + " property " + properties[i]);
            }
            return material;
        }
        private static void RequireImporterMetadata(string file, string importerName, int minimumVersion)
        {
            string path = AssetRoot + file + ".meta";
            if (!System.IO.File.Exists(path)) throw new InvalidOperationException("Missing depot floor importer metadata: " + path);
            string contents = System.IO.File.ReadAllText(path).Replace("\r\n", "\n");
            var block = System.Text.RegularExpressions.Regex.Match(contents, "(?m)^" + importerName + ":\n((?:[ \t].*\n|\n)*)");
            var version = System.Text.RegularExpressions.Regex.Match(block.Success ? block.Groups[1].Value : "", "(?m)^  serializedVersion: ([0-9]+)$");
            if (!version.Success || !int.TryParse(version.Groups[1].Value, out int value) || value < minimumVersion)
                throw new InvalidOperationException("Unsupported " + importerName + " metadata in " + path + ". Use the published complete metadata, not a GUID-only stub.");
        }
        private static void Import(string file)
        {
            if (!System.IO.File.Exists(AssetRoot + file)) throw new InvalidOperationException("Missing original floor file " + file);
            AssetDatabase.ImportAsset(AssetRoot + file, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
