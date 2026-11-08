using System;
using UnityEditor;
using UnityEngine;

namespace UndeadOvertime.LanPrototype.Editor
{
    /// <summary>Explicit import contract only for the selected original runtime floor packet.</summary>
    public sealed class DepotFloorImport : AssetPostprocessor
    {
        public const string AssetRoot = "Assets/UndeadOvertime/Resources/UndeadOvertime/DepotFloor/";
        public override uint GetVersion() => 1;
        private bool IsFloor => assetPath.StartsWith(AssetRoot, StringComparison.Ordinal);
        private void OnPreprocessModel()
        {
            if (!IsFloor || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.importAnimation = false; importer.importBlendShapes = false; importer.addCollider = false;
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
            string[] files = { "UO_DepotFloor_4m_LOD0.fbx", "UO_DepotFloor_4m_LOD1.fbx", "UO_DepotFloor_4m_LOD2.fbx",
                "floor_basecolor.png", "floor_normal_opengl.png", "floor_metallic_smoothness.png",
                "concrete_basecolor.png", "concrete_normal_opengl.png", "concrete_metallic_smoothness.png" };
            foreach (string file in files)
            {
                if (!System.IO.File.Exists(AssetRoot + file)) throw new InvalidOperationException("Missing original floor file " + file);
                AssetDatabase.ImportAsset(AssetRoot + file, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log("UO_DEPOT_FLOOR_IMPORT_OK files=9; run import tests before treating this as acceptance.");
        }
    }
}
