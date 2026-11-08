using System;
using UnityEditor;
using UnityEngine;

namespace UndeadOvertime.LanPrototype.Editor
{
    /// <summary>Explicit contract for six selected original cabinet binaries. No automatic asset/material saves.</summary>
    public sealed class DepotConsoleImport : AssetPostprocessor
    {
        public const string AssetRoot = "Assets/UndeadOvertime/Resources/UndeadOvertime/DepotConsole/";
        private bool Selected => assetPath.StartsWith(AssetRoot, StringComparison.Ordinal);
        public override uint GetVersion() => 1;
        private void OnPreprocessModel()
        {
            if (!Selected || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return;
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
            if (!Selected || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;
            var importer = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("_normal_opengl.png", StringComparison.Ordinal);
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.convertToNormalmap = false; importer.flipGreenChannel = false;
            importer.sRGBTexture = assetPath.EndsWith("_basecolor.png", StringComparison.Ordinal);
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true; importer.isReadable = false; importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4; importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.npotScale = TextureImporterNPOTScale.None;
        }
        /// <summary>Read-only typed-dependency gate. Never reimport, repair or save anything from the build command.</summary>
        public static void ValidateAssets()
        {
            for (int lod = 0; lod < 3; lod++)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(AssetRoot + "UO_DepotConsole_LOD" + lod + ".fbx") == null)
                    throw new InvalidOperationException("Cabinet FBX did not import; inspect native import tests before building.");
            foreach (string suffix in new[] { "basecolor", "normal_opengl", "metallic_smoothness" })
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetRoot + "cabinet_" + suffix + ".png");
                if (texture == null || texture.width != 1024 || texture.height != 1024)
                    throw new InvalidOperationException("Cabinet texture did not import as the original 1K Texture2D: " + suffix);
            }
        }
    }
}
