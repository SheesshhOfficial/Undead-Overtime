using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class DepotFacadeImportTests
    {
        private const string Root = "Assets/UndeadOvertime/Resources/UndeadOvertime/DepotFacade/";
        [TestCase(0, 684, 1408)] [TestCase(1, 264, 568)] [TestCase(2, 120, 270)]
        public void FacadePreservesMetersSlotsChannelsAndValidatedTangentBasis(int lod, int brickTriangles, int concreteTriangles)
        {
            string path = Root + "UO_DepotFacade32m_LOD" + lod + ".fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path); Assert.That(asset, Is.Not.Null);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter; Assert.That(importer, Is.Not.Null);
            Assert.That(importer.globalScale, Is.EqualTo(1)); Assert.That(importer.importAnimation, Is.False);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.None)); Assert.That(importer.addCollider, Is.False);
            Assert.That(importer.materialImportMode, Is.EqualTo(ModelImporterMaterialImportMode.None));
            Assert.That(importer.importNormals, Is.EqualTo(ModelImporterNormals.Import));
            Assert.That(importer.importTangents, Is.EqualTo(ModelImporterTangents.Import));
            Assert.That(importer.generateSecondaryUV, Is.False); Assert.That(importer.isReadable, Is.False);
            var instance = Object.Instantiate(asset);
            try
            {
                Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one));
                var filters = instance.GetComponentsInChildren<MeshFilter>(true); Assert.That(filters.Length, Is.EqualTo(1));
                Mesh mesh = filters[0].sharedMesh; Assert.That(mesh.subMeshCount, Is.EqualTo(2));
                Assert.That(mesh.GetIndexCount(0) / 3, Is.EqualTo(brickTriangles));
                Assert.That(mesh.GetIndexCount(1) / 3, Is.EqualTo(concreteTriangles));
                foreach (var channel in new[] { VertexAttribute.Normal, VertexAttribute.Tangent, VertexAttribute.TexCoord0, VertexAttribute.TexCoord1 })
                    Assert.That(mesh.HasVertexAttribute(channel), Is.True);
                // Editor-only inspection: retain non-readable player imports. Unity permits Mesh
                // array reads outside the game/rendering loop in the editor (these are EditMode tests).
                Vector3[] normals = mesh.normals; Vector4[] tangents = mesh.tangents;
                Assert.That(normals.Length, Is.EqualTo(mesh.vertexCount)); Assert.That(tangents.Length, Is.EqualTo(mesh.vertexCount));
                for (int i = 0; i < tangents.Length; i++)
                {
                    Vector3 n = normals[i]; Vector4 t = tangents[i]; Vector3 xyz = new Vector3(t.x, t.y, t.z);
                    Assert.That(Finite(n.x) && Finite(n.y) && Finite(n.z) && Finite(t.x) && Finite(t.y) && Finite(t.z) && Finite(t.w), Is.True);
                    Assert.That(n.magnitude, Is.EqualTo(1).Within(.001)); Assert.That(xyz.magnitude, Is.EqualTo(1).Within(.001));
                    Assert.That(Mathf.Abs(Vector3.Dot(n, xyz)), Is.LessThan(.001)); Assert.That(Mathf.Abs(t.w), Is.EqualTo(1).Within(.001));
                }
                ValidateUvTangentOrientation(mesh, normals, tangents);
                foreach (Vector3 point in mesh.vertices)
                {
                    Vector3 world = filters[0].transform.TransformPoint(point);
                    Assert.That(world.x + world.z, Is.GreaterThanOrEqualTo(.07f - .001f), "Left trimmed corner plane");
                    Assert.That(world.x - world.z, Is.LessThanOrEqualTo(31.93f + .001f), "Right trimmed corner plane");
                }
                var renderers = instance.GetComponentsInChildren<Renderer>(true); Assert.That(renderers.Length, Is.EqualTo(1));
                Assert.That(renderers[0].sharedMaterials.Length, Is.EqualTo(2)); Bounds b = renderers[0].bounds;
                Assert.That(b.min.x, Is.EqualTo(0).Within(.001)); Assert.That(b.max.x, Is.EqualTo(32).Within(.001));
                Assert.That(b.min.y, Is.EqualTo(0).Within(.001)); Assert.That(b.max.y, Is.EqualTo(4).Within(.001));
                Assert.That(b.min.z, Is.EqualTo(-.18f).Within(.001)); Assert.That(b.max.z, Is.EqualTo(.18f).Within(.001));
                Assert.That(instance.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(instance.GetComponentsInChildren<Animation>(true), Is.Empty);
                Assert.That(instance.GetComponentsInChildren<Animator>(true), Is.Empty);
                Assert.That(instance.GetComponentsInChildren<LODGroup>(true), Is.Empty);
            }
            finally { Object.DestroyImmediate(instance); }
        }
        [TestCase("basecolor")] [TestCase("normal_opengl")] [TestCase("metallic_smoothness")]
        public void OriginalBrickMapsRetainFullResolutionAndExplicitTwoDimensionalImport(string suffix)
        {
            string path = Root + "brick_" + suffix + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter; Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureShape, Is.EqualTo(TextureImporterShape.Texture2D));
            Assert.That(importer.textureType, Is.EqualTo(suffix == "normal_opengl" ? TextureImporterType.NormalMap : TextureImporterType.Default));
            Assert.That(importer.sRGBTexture, Is.EqualTo(suffix == "basecolor"));
            Assert.That(importer.convertToNormalmap, Is.False); Assert.That(importer.flipGreenChannel, Is.False);
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput)); Assert.That(importer.alphaIsTransparency, Is.False);
            Assert.That(importer.mipmapEnabled, Is.True); Assert.That(importer.maxTextureSize, Is.EqualTo(2048));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat)); Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path); Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(2048)); Assert.That(texture.height, Is.EqualTo(1024));
        }
        private static void ValidateUvTangentOrientation(Mesh mesh, Vector3[] normals, Vector4[] tangents)
        {
            Vector3[] positions = mesh.vertices; Vector2[] uv = mesh.uv;
            Assert.That(uv.Length, Is.EqualTo(mesh.vertexCount)); int front = 0, rear = 0;
            for (int slot = 0; slot < 2; slot++)
            {
                int[] indices = mesh.GetIndices(slot); int corners = 0;
                for (int face = 0; face < indices.Length; face += 3)
                {
                    int a = indices[face], b = indices[face + 1], c = indices[face + 2];
                    Vector3 e1 = positions[b] - positions[a], e2 = positions[c] - positions[a];
                    Vector3 faceNormal = Vector3.Cross(e1, e2);
                    Assert.That(Finite(faceNormal.x) && Finite(faceNormal.y) && Finite(faceNormal.z) && faceNormal.sqrMagnitude > 1e-14f, Is.True, "Degenerate geometric triangle");
                    // Original cap micro-triangles are valid below Vector3.Normalize's 1e-5
                    // magnitude cutoff; the explicit measured-area guard avoids that zero-vector fallback.
                    faceNormal /= Mathf.Sqrt(faceNormal.sqrMagnitude);
                    Vector2 d1 = uv[b] - uv[a], d2 = uv[c] - uv[a];
                    float determinant = d1.x * d2.y - d1.y * d2.x;
                    Assert.That(Finite(determinant) && Mathf.Abs(determinant) > 1e-12f, Is.True, "Degenerate PBR UV triangle");
                    Vector3 du = (e1 * d2.y - e2 * d1.y) / determinant;
                    Vector3 dv = (e2 * d1.x - e1 * d2.x) / determinant;
                    foreach (int vertex in new[] { a, b, c })
                    {
                        Vector3 n = normals[vertex]; Vector4 t = tangents[vertex]; Vector3 actual = new Vector3(t.x, t.y, t.z);
                        Assert.That(Vector3.Dot(faceNormal, n), Is.GreaterThan(.99f), "Imported flat normals must agree with triangle winding");
                        Vector3 expected = du - n * Vector3.Dot(n, du);
                        Assert.That(expected.sqrMagnitude, Is.GreaterThan(1e-12f));
                        Assert.That(Vector3.Dot(actual, expected.normalized), Is.GreaterThan(.999f), "Imported tangent must follow increasing UV0 U");
                        Vector3 bitangent = Vector3.Cross(n, actual) * t.w;
                        Vector3 projectedV = dv - n * Vector3.Dot(n, dv);
                        Assert.That(projectedV.sqrMagnitude, Is.GreaterThan(1e-12f));
                        Assert.That(Vector3.Dot(bitangent, projectedV.normalized), Is.GreaterThan(.0001f), "Imported handedness must follow UV0 V");
                        if (n.z > .99f) front++; if (n.z < -.99f) rear++; corners++;
                    }
                }
                Assert.That(corners, Is.GreaterThan(0), "Both material slots must be inspected");
            }
            Assert.That(front, Is.GreaterThan(0)); Assert.That(rear, Is.GreaterThan(0));
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
