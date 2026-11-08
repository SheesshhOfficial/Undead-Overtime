using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class DepotFloorImportTests
    {
        private const string Root = "Assets/UndeadOvertime/Resources/UndeadOvertime/DepotFloor/";
        [TestCase(0, 318, 200, 118)] [TestCase(1, 142, 72, 70)] [TestCase(2, 12, 2, 10)]
        public void NativeFbxPreservesMetersTrianglesChannelsAndNoCollider(int lod, int triangles, int topTriangles, int sideTriangles)
        {
            string path = Root + "UO_DepotFloor_4m_LOD" + lod + ".fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path); Assert.That(asset, Is.Not.Null, path);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.That(importer, Is.Not.Null, path + " ModelImporter"); Assert.That(importer.globalScale, Is.EqualTo(1), path + " globalScale");
            Assert.That(importer.importAnimation, Is.False); Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.None));
            Assert.That(importer.addCollider, Is.False);
            Assert.That(importer.materialImportMode, Is.EqualTo(ModelImporterMaterialImportMode.None));
            Assert.That(importer.generateSecondaryUV, Is.False); Assert.That(importer.importTangents, Is.EqualTo(ModelImporterTangents.CalculateMikk));
            var instance = Object.Instantiate(asset);
            try
            {
                Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one));
                var filters = instance.GetComponentsInChildren<MeshFilter>(true); Assert.That(filters.Length, Is.EqualTo(1));
                Mesh mesh = filters[0].sharedMesh; Assert.That(mesh, Is.Not.Null); Assert.That(mesh.subMeshCount, Is.EqualTo(2));
                long indices = 0; for (int i = 0; i < mesh.subMeshCount; i++) { Assert.That(mesh.GetIndexCount(i), Is.GreaterThan(0), "Both top and side submeshes must survive export."); indices += mesh.GetIndexCount(i); }
                Assert.That(indices / 3, Is.EqualTo(triangles));
                Assert.That(mesh.GetIndexCount(0) / 3, Is.EqualTo(topTriangles)); Assert.That(mesh.GetIndexCount(1) / 3, Is.EqualTo(sideTriangles));
                Assert.That(mesh.HasVertexAttribute(VertexAttribute.Normal), Is.True);
                Assert.That(mesh.HasVertexAttribute(VertexAttribute.Tangent), Is.True);
                Assert.That(mesh.HasVertexAttribute(VertexAttribute.TexCoord0), Is.True);
                Assert.That(mesh.HasVertexAttribute(VertexAttribute.TexCoord1), Is.True);
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true); Assert.That(renderers.Length, Is.EqualTo(1));
                Assert.That(renderers[0].sharedMaterials.Length, Is.EqualTo(2)); Bounds bounds = renderers[0].bounds;
                Assert.That(bounds.min.x, Is.EqualTo(0).Within(.001)); Assert.That(bounds.max.x, Is.EqualTo(4).Within(.001));
                Assert.That(bounds.min.y, Is.EqualTo(-.2).Within(.001)); Assert.That(bounds.max.y, Is.EqualTo(0).Within(.001));
                Assert.That(bounds.min.z, Is.EqualTo(0).Within(.001)); Assert.That(bounds.max.z, Is.EqualTo(4).Within(.001));
                Assert.That(instance.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(instance.GetComponentsInChildren<Animation>(true), Is.Empty);
                Assert.That(instance.GetComponentsInChildren<LODGroup>(true), Is.Empty, "The runtime tile owns the only LODGroup; each standalone FBX must not create its own.");
            }
            finally { Object.DestroyImmediate(instance); }
        }
        [TestCase("floor")] [TestCase("concrete")]
        public void SerializedMaterialsAnchorLitVariantsAndOriginalTextureDependencies(string prefix)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(Root + prefix + "_material.mat");
            Assert.That(material, Is.Not.Null, prefix + " serialized material"); Assert.That(material.shader, Is.Not.Null, prefix + " shader reference");
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), prefix + " shader name");
            Assert.That(AssetDatabase.GetAssetPath(material.shader), Is.EqualTo("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader"));
            Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True, prefix + " _NORMALMAP keyword");
            Assert.That(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True, prefix + " _METALLICSPECGLOSSMAP keyword");
            Texture2D color = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + prefix + "_basecolor.png");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + prefix + "_normal_opengl.png");
            Texture2D packed = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + prefix + "_metallic_smoothness.png");
            Assert.That(color, Is.Not.Null, prefix + " basecolor asset"); Assert.That(normal, Is.Not.Null, prefix + " normal asset");
            Assert.That(packed, Is.Not.Null, prefix + " packed metallic/smoothness asset");
            Assert.That(material.GetTexture("_BaseMap"), Is.EqualTo(color), prefix + " _BaseMap reference");
            Assert.That(material.GetTexture("_BumpMap"), Is.EqualTo(normal), prefix + " _BumpMap reference");
            Assert.That(material.GetTexture("_MetallicGlossMap"), Is.EqualTo(packed), prefix + " _MetallicGlossMap reference");
            Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(1)); Assert.That(material.GetFloat("_SmoothnessTextureChannel"), Is.Zero);
        }
        [TestCase("floor", "basecolor", 2048)] [TestCase("floor", "normal_opengl", 2048)] [TestCase("floor", "metallic_smoothness", 2048)]
        [TestCase("concrete", "basecolor", 1024)] [TestCase("concrete", "normal_opengl", 1024)] [TestCase("concrete", "metallic_smoothness", 1024)]
        public void FullResolutionMapsHaveExplicitColorNormalAndDataImport(string prefix, string suffix, int dimension)
        {
            string path = Root + prefix + "_" + suffix + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter; Assert.That(importer, Is.Not.Null, path + " TextureImporter");
            Assert.That(importer.textureShape, Is.EqualTo(TextureImporterShape.Texture2D), path + " textureShape");
            Assert.That(importer.sRGBTexture, Is.EqualTo(suffix == "basecolor"), path + " sRGBTexture");
            Assert.That(importer.textureType, Is.EqualTo(suffix == "normal_opengl" ? TextureImporterType.NormalMap : TextureImporterType.Default), path + " textureType");
            Assert.That(importer.convertToNormalmap, Is.False, path + " convertToNormalmap");
            Assert.That(importer.flipGreenChannel, Is.False, path + " flipGreenChannel");
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path + " textureCompression");
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat), path + " wrapMode");
            Assert.That(importer.mipmapEnabled, Is.True, path + " mipmapEnabled");
            Assert.That(importer.alphaIsTransparency, Is.False, path + " alphaIsTransparency");
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path); Assert.That(texture, Is.Not.Null, path + " Texture2D asset");
            Assert.That(texture.width, Is.EqualTo(dimension)); Assert.That(texture.height, Is.EqualTo(dimension));
        }
    }
}
