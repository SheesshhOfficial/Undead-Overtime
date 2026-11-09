using System;
using UnityEditor;
using UnityEngine;

namespace UndeadOvertime.LanPrototype.Editor
{
    /// <summary>Six selected original brick facade files; no automatic material repair or saves.</summary>
    public sealed class DepotFacadeImport : AssetPostprocessor
    {
        public const string AssetRoot = "Assets/UndeadOvertime/Resources/UndeadOvertime/DepotFacade/";
        private bool SelectedModel => assetPath == AssetRoot + "UO_DepotFacade32m_LOD0.fbx" ||
            assetPath == AssetRoot + "UO_DepotFacade32m_LOD1.fbx" || assetPath == AssetRoot + "UO_DepotFacade32m_LOD2.fbx";
        private bool SelectedTexture => assetPath == AssetRoot + "brick_basecolor.png" ||
            assetPath == AssetRoot + "brick_normal_opengl.png" || assetPath == AssetRoot + "brick_metallic_smoothness.png";
        public override uint GetVersion() => 1;
        private void OnPreprocessModel()
        {
            if (!SelectedModel) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false; importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Off; importer.isReadable = false;
            // Exported, per-corner UV0 tangent basis is validated in the asset packet. Tiny cap
            // triangles in this source produced zero Mikk tangents; do not silently recalculate them.
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.Import;
            importer.generateSecondaryUV = false; importer.preserveHierarchy = true;
        }
        private void OnPreprocessTexture()
        {
            if (!SelectedTexture) return;
            var importer = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("_normal_opengl.png", StringComparison.Ordinal);
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.convertToNormalmap = false; importer.flipGreenChannel = false;
            importer.sRGBTexture = assetPath.EndsWith("_basecolor.png", StringComparison.Ordinal);
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true; importer.isReadable = false; importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.npotScale = TextureImporterNPOTScale.None;
        }
        /// <summary>Read-only native build gate; never reimports, repairs or saves a material.</summary>
        public static void ValidateAssets()
        {
            for (int lod = 0; lod < 3; lod++)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(AssetRoot + "UO_DepotFacade32m_LOD" + lod + ".fbx") == null)
                    throw new InvalidOperationException("Depot facade FBX did not import; inspect native import tests before building.");
            foreach (string suffix in new[] { "basecolor", "normal_opengl", "metallic_smoothness" })
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetRoot + "brick_" + suffix + ".png");
                if (texture == null || texture.width != 2048 || texture.height != 1024)
                    throw new InvalidOperationException("Depot facade texture must import as the selected 2048 by 1024 Texture2D: " + suffix);
            }
        }
    }
}
