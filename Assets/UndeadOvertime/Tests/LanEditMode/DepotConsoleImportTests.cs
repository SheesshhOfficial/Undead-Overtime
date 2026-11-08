using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class DepotConsoleImportTests
    {
        private const string Root = "Assets/UndeadOvertime/Resources/UndeadOvertime/DepotConsole/";
        [TestCase(0, 7748)] [TestCase(1, 4028)] [TestCase(2, 702)]
        public void CabinetFbxPreservesAtlasChannelsMetersAndOneSlot(int lod, int triangles)
        {
            string path = Root + "UO_DepotConsole_LOD" + lod + ".fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path); Assert.That(asset, Is.Not.Null);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter; Assert.That(importer, Is.Not.Null);
            Assert.That(importer.globalScale, Is.EqualTo(1)); Assert.That(importer.importAnimation, Is.False);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.None)); Assert.That(importer.addCollider, Is.False);
            Assert.That(importer.materialImportMode, Is.EqualTo(ModelImporterMaterialImportMode.None));
            Assert.That(importer.importTangents, Is.EqualTo(ModelImporterTangents.CalculateMikk)); Assert.That(importer.generateSecondaryUV, Is.False);
            var instance = Object.Instantiate(asset);
            try
            {
                Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one));
                var filters = instance.GetComponentsInChildren<MeshFilter>(true); Assert.That(filters.Length, Is.EqualTo(1));
                Mesh mesh = filters[0].sharedMesh; Assert.That(mesh.subMeshCount, Is.EqualTo(1));
                Assert.That(mesh.GetIndexCount(0) / 3, Is.EqualTo(triangles));
                foreach (var attribute in new[] { VertexAttribute.Normal, VertexAttribute.Tangent, VertexAttribute.TexCoord0, VertexAttribute.TexCoord1 })
                    Assert.That(mesh.HasVertexAttribute(attribute), Is.True);
                var renderers = instance.GetComponentsInChildren<Renderer>(true); Assert.That(renderers.Length, Is.EqualTo(1));
                Assert.That(renderers[0].sharedMaterials.Length, Is.EqualTo(1)); Bounds b = renderers[0].bounds;
                Assert.That(b.min.x, Is.EqualTo(-.57f).Within(.001)); Assert.That(b.max.x, Is.EqualTo(.57f).Within(.001));
                Assert.That(b.min.y, Is.EqualTo(0).Within(.001)); Assert.That(b.max.y, Is.EqualTo(lod == 2 ? 1.813978315f : 1.8155f).Within(.001));
                Assert.That(b.min.z, Is.EqualTo(lod == 2 ? -.32f : -.332f).Within(.001)); Assert.That(b.max.z, Is.EqualTo(.32f).Within(.001));
                Assert.That(instance.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(instance.GetComponentsInChildren<Animation>(true), Is.Empty);
                Assert.That(instance.GetComponentsInChildren<LODGroup>(true), Is.Empty);
            }
            finally { Object.DestroyImmediate(instance); }
        }
        [TestCase("basecolor")] [TestCase("normal_opengl")] [TestCase("metallic_smoothness")]
        public void OriginalAtlasMapsImportAsCorrectTypedOneKImages(string suffix)
        {
            string path = Root + "cabinet_" + suffix + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter; Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureShape, Is.EqualTo(TextureImporterShape.Texture2D));
            Assert.That(importer.textureType, Is.EqualTo(suffix == "normal_opengl" ? TextureImporterType.NormalMap : TextureImporterType.Default));
            Assert.That(importer.sRGBTexture, Is.EqualTo(suffix == "basecolor"));
            Assert.That(importer.convertToNormalmap, Is.False); Assert.That(importer.flipGreenChannel, Is.False);
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput)); Assert.That(importer.alphaIsTransparency, Is.False);
            Assert.That(importer.mipmapEnabled, Is.True); Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path); Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(1024)); Assert.That(texture.height, Is.EqualTo(1024));
        }
    }
}
