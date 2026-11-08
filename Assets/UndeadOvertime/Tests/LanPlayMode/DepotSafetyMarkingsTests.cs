using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class DepotSafetyMarkingsTests
    {
        private GameObject root;
        private DepotMissionPresentation layout;
        [UnitySetUp] public IEnumerator Setup() { root = new GameObject("Depot paint tests"); yield return null; }
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            layout?.Dispose(); layout = null;
            if (root != null) Object.Destroy(root);
            yield return null; yield return null;
        }
        private static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Paint rendering and ownership checks require an actual graphics device.");
        }
        private static Material SharedPaint()
        {
            // Reuse an existing resource to verify caller ownership. Do not create, recolor or save a material in tests.
            Material material = Resources.Load<Material>(DepotFloorPresentation.ResourceRoot + "floor_material");
            Assert.That(material, Is.Not.Null); return material;
        }
        private DepotSafetyMarkings Create()
        { RequireGraphics(); return DepotSafetyMarkings.Create(root.transform, SharedPaint()); }
        private static Mesh MeshOf(DepotSafetyMarkings owner) => owner.GetComponent<MeshFilter>().sharedMesh;
        private static int OwnedMeshCount() => Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name == "Owned depot safety paint");

        [Test]
        public void OneCombinedMeshHasFiniteUpwardTrianglesUvsAndNoPhysics()
        {
            var owner = Create(); Mesh mesh = MeshOf(owner);
            Assert.That(owner.transform.parent, Is.SameAs(root.transform)); Assert.That(owner.transform.childCount, Is.Zero);
            Assert.That(owner.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(owner.transform.localRotation, Is.EqualTo(Quaternion.identity)); Assert.That(owner.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(owner.GetComponentsInChildren<MeshFilter>(true).Length, Is.EqualTo(1));
            Assert.That(owner.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(1));
            Assert.That(owner.GetComponentsInChildren<MonoBehaviour>(true), Is.EqualTo(new MonoBehaviour[] { owner }));
            Assert.That(owner.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(owner.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            var renderer = owner.GetComponent<MeshRenderer>();
            Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(1));
            Assert.That(renderer.sharedMaterial, Is.SameAs(SharedPaint()));
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off)); Assert.That(renderer.receiveShadows, Is.True);
            Assert.That(mesh.subMeshCount, Is.EqualTo(1)); Assert.That(mesh.vertexCount, Is.EqualTo(164));
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            Vector2[] uv = mesh.uv; Vector4[] tangents = mesh.tangents; int[] indices = mesh.triangles;
            Assert.That(indices.Length / 3, Is.EqualTo(82)); Assert.That(indices.Length / 3, Is.LessThan(200));
            Assert.That(DepotSafetyMarkings.QuadCount, Is.EqualTo(41)); Assert.That(DepotSafetyMarkings.TriangleCount, Is.EqualTo(82));
            Assert.That(normals.Length, Is.EqualTo(vertices.Length)); Assert.That(uv.Length, Is.EqualTo(vertices.Length));
            Assert.That(tangents.Length, Is.EqualTo(vertices.Length));
            for (int i = 0; i < vertices.Length; i++)
            {
                AssertFinite(vertices[i].x); AssertFinite(vertices[i].y); AssertFinite(vertices[i].z);
                Assert.That(vertices[i].y, Is.EqualTo(.012f)); Assert.That(normals[i], Is.EqualTo(Vector3.up));
                Assert.That(uv[i], Is.EqualTo(new Vector2(vertices[i].x, vertices[i].z)));
                AssertFinite(tangents[i].x); AssertFinite(tangents[i].y); AssertFinite(tangents[i].z); AssertFinite(tangents[i].w);
                Assert.That(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z).sqrMagnitude, Is.EqualTo(1).Within(.0001f));
                Assert.That(tangents[i].y, Is.EqualTo(0).Within(.0001f)); Assert.That(Mathf.Abs(tangents[i].w), Is.EqualTo(1));
            }
            for (int i = 0; i < indices.Length; i += 3)
            {
                for (int j = 0; j < 3; j++) Assert.That(indices[i + j], Is.InRange(0, vertices.Length - 1));
                Vector3 face = Vector3.Cross(vertices[indices[i + 1]] - vertices[indices[i]], vertices[indices[i + 2]] - vertices[indices[i]]);
                Assert.That(face.y, Is.GreaterThan(0)); Assert.That(face.x, Is.Zero); Assert.That(face.z, Is.Zero);
            }
            Assert.That(mesh.bounds.min.x, Is.EqualTo(-13)); Assert.That(mesh.bounds.max.x, Is.EqualTo(13));
            Assert.That(mesh.bounds.min.z, Is.EqualTo(-13)); Assert.That(mesh.bounds.max.z, Is.EqualTo(11));
            Assert.That(mesh.bounds.size.y, Is.Zero);
        }

        [Test]
        public void FourBayOutlinesStayInsideTheirContractsAndOutsideTheCentralRoute()
        {
            Mesh mesh = MeshOf(Create()); Rect[] quads = QuadRects(mesh);
            var bays = new[] { Rect.MinMaxRect(-13, 1, -5, 5), Rect.MinMaxRect(5, 1, 13, 5),
                Rect.MinMaxRect(-13, 7, -5, 11), Rect.MinMaxRect(5, 7, 13, 11) };
            for (int bay = 0; bay < bays.Length; bay++)
            {
                Rect b = bays[bay]; int first = bay * 4;
                AssertRect(quads[first], b.xMin, b.xMax, b.yMin, b.yMin + .1f);
                AssertRect(quads[first + 1], b.xMin, b.xMax, b.yMax - .1f, b.yMax);
                AssertRect(quads[first + 2], b.xMin, b.xMin + .1f, b.yMin + .1f, b.yMax - .1f);
                AssertRect(quads[first + 3], b.xMax - .1f, b.xMax, b.yMin + .1f, b.yMax - .1f);
                Rect interior = Rect.MinMaxRect(b.xMin + .1f, b.yMin + .1f, b.xMax - .1f, b.yMax - .1f);
                for (int i = first; i < first + 4; i++)
                {
                    Assert.That(OverlapsArea(quads[i], interior), Is.False);
                    Assert.That(OverlapsArea(quads[i], Rect.MinMaxRect(-2, -16, 2, 16)), Is.False);
                }
            }
        }

        [Test]
        public void CabinetApproachIsAnOpenNorthUOutsideTheCabinetProxy()
        {
            Rect[] quads = QuadRects(MeshOf(Create()));
            AssertRect(quads[16], -10.15f, -7.85f, -6.8f, -6.7f);
            AssertRect(quads[17], -10.15f, -10.05f, -6.7f, -5.25f);
            AssertRect(quads[18], -7.95f, -7.85f, -6.7f, -5.25f);
            var proxy = new Bounds(DepotConsolePresentation.CollisionCenter, DepotConsolePresentation.CollisionSize);
            Rect cabinet = Rect.MinMaxRect(proxy.min.x, proxy.min.z, proxy.max.x, proxy.max.z);
            Rect approachInterior = Rect.MinMaxRect(-10.05f, -6.7f, -7.95f, -5.25f);
            for (int i = 16; i < 19; i++)
            {
                Assert.That(OverlapsArea(quads[i], cabinet), Is.False);
                Assert.That(OverlapsArea(quads[i], approachInterior), Is.False);
            }
        }

        [Test]
        public void LaneDashesKeepGateGapAndStopBeforeExtractionWithoutDirectionalGeometry()
        {
            Rect[] quads = QuadRects(MeshOf(Create()));
            int index = 19;
            for (int z = -13; z < 8; z += 2)
            {
                float end = z == -3 ? -2.35f : z + 1;
                AssertRect(quads[index++], -1.7f, -1.6f, z, end);
                AssertRect(quads[index++], 1.6f, 1.7f, z, end);
            }
            Assert.That(index, Is.EqualTo(41));
            for (int i = 19; i < quads.Length; i++)
                Assert.That(OverlapsArea(quads[i], Rect.MinMaxRect(-1.6f, -13, 1.6f, 14)), Is.False);
        }

        [Test]
        public void PaintHasNoOverlappingCoplanarStripsAndLeavesExtractionAndGateClear()
        {
            Rect[] quads = QuadRects(MeshOf(Create()));
            Rect extraction = Rect.MinMaxRect(-2, 10, 2, 14), gate = Rect.MinMaxRect(-2, -2.35f, 2, -1.65f);
            for (int i = 0; i < quads.Length; i++)
            {
                Assert.That(OverlapsArea(quads[i], extraction), Is.False, "Extraction must have no safety paint.");
                Assert.That(OverlapsArea(quads[i], gate), Is.False, "Closed gate clearance must have no safety paint.");
                for (int j = i + 1; j < quads.Length; j++)
                    Assert.That(OverlapsArea(quads[i], quads[j]), Is.False, "Coplanar overlap between strips " + i + " and " + j);
            }
        }

        [Test]
        public void NullMaterialAndNullOrInactiveParentsAreRejectedBeforeOwnership()
        {
            Material paint = SharedPaint(); int meshes = OwnedMeshCount();
            Assert.Throws<ArgumentNullException>(() => DepotSafetyMarkings.Create(null, paint));
            Assert.Throws<ArgumentNullException>(() => DepotSafetyMarkings.Create(root.transform, null));
            root.SetActive(false);
            Assert.Throws<InvalidOperationException>(() => DepotSafetyMarkings.Create(root.transform, paint));
            var activeChild = new GameObject("Active child with inactive ancestor"); activeChild.transform.SetParent(root.transform, false);
            Assert.That(activeChild.activeSelf, Is.True);
            Assert.Throws<InvalidOperationException>(() => DepotSafetyMarkings.Create(activeChild.transform, paint));
            Assert.That(activeChild.transform.childCount, Is.Zero); Assert.That(root.transform.childCount, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<DepotSafetyMarkings>(true), Is.Empty);
            Assert.That(OwnedMeshCount(), Is.EqualTo(meshes)); Assert.That(paint == null, Is.False);
        }

        [Test]
        public void TransformedAndInheritedTransformedParentsAreRejectedBeforeAllocation()
        {
            Material paint = SharedPaint(); int meshes = OwnedMeshCount();
            root.transform.position = new Vector3(1, 0, 0); Reject(); root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.Euler(0, 30, 0); Reject(); root.transform.rotation = Quaternion.identity;
            foreach (Vector3 scale in new[] { new Vector3(2, 1, 1), new Vector3(-1, 1, 1), new Vector3(0, 1, 1) })
            { root.transform.localScale = scale; Reject(); }
            root.transform.localScale = Vector3.one;
            var child = new GameObject("Locally identity child"); child.transform.SetParent(root.transform, false);
            root.transform.position = Vector3.forward;
            Assert.Throws<ArgumentException>(() => DepotSafetyMarkings.Create(child.transform, paint));
            Assert.That(child.transform.childCount, Is.Zero); Assert.That(OwnedMeshCount(), Is.EqualTo(meshes));
            void Reject()
            {
                Assert.Throws<ArgumentException>(() => DepotSafetyMarkings.Create(root.transform, paint));
                Assert.That(root.transform.childCount, Is.Zero);
                Assert.That(root.GetComponentsInChildren<DepotSafetyMarkings>(true), Is.Empty);
                Assert.That(OwnedMeshCount(), Is.EqualTo(meshes));
            }
        }

        [Test]
        public void ColliderOnlyMissionNeverConstructsPaint()
        {
            int meshes = OwnedMeshCount(); layout = new DepotMissionPresentation(root.transform, 5, 2, false);
            Assert.That(layout.Root.GetComponentsInChildren<DepotSafetyMarkings>(true), Is.Empty);
            Assert.That(layout.Root.GetComponentsInChildren<MeshFilter>(true), Is.Empty);
            Assert.That(layout.Root.GetComponentsInChildren<Renderer>(true), Is.Empty);
            Assert.That(OwnedMeshCount(), Is.EqualTo(meshes));
        }

        [Test]
        public void GraphicsDeviceContractPreventsHeadlessConstruction()
        {
            Material paint = SharedPaint(); int meshes = OwnedMeshCount();
            // Only the current device branch is exercised by a run; a rendered run is not headless execution evidence.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Throws<InvalidOperationException>(() => DepotSafetyMarkings.Create(root.transform, paint));
                Assert.That(root.transform.childCount, Is.Zero); Assert.That(OwnedMeshCount(), Is.EqualTo(meshes));
            }
            else
            {
                var owner = DepotSafetyMarkings.Create(root.transform, paint);
                Assert.That(root.transform.childCount, Is.EqualTo(1)); Assert.That(OwnedMeshCount(), Is.EqualTo(meshes + 1));
                Assert.That(owner.GetComponentsInChildren<MeshFilter>(true).Length, Is.EqualTo(1));
                Assert.That(owner.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(1));
                Assert.That(owner.GetComponent<Renderer>().sharedMaterial, Is.SameAs(paint));
            }
        }

        [UnityTest]
        public IEnumerator CallerMaterialAndTexturesStayUnchangedThroughParentDisposal()
        {
            RequireGraphics(); Material paint = SharedPaint();
            Shader shader = paint.shader; string name = paint.name; string[] keywords = paint.shaderKeywords;
            Color color = paint.GetColor("_BaseColor"); float smoothness = paint.GetFloat("_Smoothness"), metallic = paint.GetFloat("_Metallic");
            int queue = paint.renderQueue; bool instancing = paint.enableInstancing, doubleSided = paint.doubleSidedGI;
            MaterialGlobalIlluminationFlags gi = paint.globalIlluminationFlags;
            string[] properties = { "_BaseMap", "_BumpMap", "_MetallicGlossMap" };
            Texture[] textures = properties.Select(paint.GetTexture).ToArray();
            Vector2[] scales = properties.Select(paint.GetTextureScale).ToArray(), offsets = properties.Select(paint.GetTextureOffset).ToArray();
            var owner = DepotSafetyMarkings.Create(root.transform, paint); Mesh owned = MeshOf(owner);
            Assert.That(owner.GetComponent<Renderer>().sharedMaterial, Is.SameAs(paint));
            Object.Destroy(root); yield return null; yield return null;
            Assert.That(root == null, Is.True); Assert.That(owner == null, Is.True); Assert.That(owned == null, Is.True);
            Assert.That(paint == null, Is.False); Assert.That(paint.shader, Is.SameAs(shader)); Assert.That(paint.name, Is.EqualTo(name));
            Assert.That(paint.shaderKeywords, Is.EqualTo(keywords)); Assert.That(paint.GetColor("_BaseColor"), Is.EqualTo(color));
            Assert.That(paint.GetFloat("_Smoothness"), Is.EqualTo(smoothness)); Assert.That(paint.GetFloat("_Metallic"), Is.EqualTo(metallic));
            Assert.That(paint.renderQueue, Is.EqualTo(queue)); Assert.That(paint.enableInstancing, Is.EqualTo(instancing));
            Assert.That(paint.doubleSidedGI, Is.EqualTo(doubleSided)); Assert.That(paint.globalIlluminationFlags, Is.EqualTo(gi));
            for (int i = 0; i < properties.Length; i++)
            {
                Assert.That(textures[i] == null, Is.False); Assert.That(paint.GetTexture(properties[i]), Is.SameAs(textures[i]));
                Assert.That(paint.GetTextureScale(properties[i]), Is.EqualTo(scales[i]));
                Assert.That(paint.GetTextureOffset(properties[i]), Is.EqualTo(offsets[i]));
            }
        }

        [UnityTest]
        public IEnumerator DisablingKeepsMeshAliveUntilOwnerComponentIsDestroyed()
        {
            var owner = Create(); Mesh mesh = MeshOf(owner); Material paint = owner.GetComponent<Renderer>().sharedMaterial;
            GameObject visual = owner.gameObject; MeshFilter filter = owner.GetComponent<MeshFilter>();
            root.SetActive(false); yield return null;
            Assert.That(mesh == null, Is.False); Assert.That(filter.sharedMesh, Is.SameAs(mesh));
            root.SetActive(true); yield return null; Assert.That(filter.sharedMesh, Is.SameAs(mesh));
            Object.Destroy(owner); yield return null; yield return null;
            Assert.That(owner == null, Is.True); Assert.That(mesh == null, Is.True); Assert.That(visual == null, Is.False);
            Assert.That(filter.sharedMesh == null, Is.True); Assert.That(paint == null, Is.False);
            Assert.That(visual.GetComponent<Renderer>().sharedMaterial, Is.SameAs(paint));
        }

        [UnityTest]
        public IEnumerator RepeatedSameFrameCreationAndCleanupLeavesNoOwnedMeshesOrChildren()
        {
            RequireGraphics(); Material paint = SharedPaint(); int baseline = OwnedMeshCount();
            var retired = new List<Mesh>();
            for (int cycle = 0; cycle < 4; cycle++)
            {
                var owner = DepotSafetyMarkings.Create(root.transform, paint); Mesh mesh = MeshOf(owner);
                Assert.That(retired.Contains(mesh), Is.False); retired.Add(mesh);
                Assert.That(root.GetComponentsInChildren<DepotSafetyMarkings>().Length, Is.EqualTo(1));
                owner.gameObject.SetActive(false); Object.Destroy(owner.gameObject);
                Assert.That(root.GetComponentsInChildren<DepotSafetyMarkings>(), Is.Empty);
                // Deferred Unity destruction must not release a previously active owner's mesh just on deactivation.
                Assert.That(mesh == null, Is.False);
            }
            yield return null; yield return null;
            Assert.That(root.transform.childCount, Is.Zero); Assert.That(OwnedMeshCount(), Is.EqualTo(baseline));
            foreach (Mesh mesh in retired) Assert.That(mesh == null, Is.True);
            Assert.That(paint == null, Is.False);
        }

        private static void AssertFinite(float value) => Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False);
        private static Rect[] QuadRects(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices; int[] triangles = mesh.triangles;
            Assert.That(vertices.Length, Is.EqualTo(164)); Assert.That(triangles.Length, Is.EqualTo(246));
            var result = new Rect[vertices.Length / 4];
            for (int q = 0; q < result.Length; q++)
            {
                int v = q * 4; Vector3 a = vertices[v], b = vertices[v + 1], c = vertices[v + 2], d = vertices[v + 3];
                Assert.That(a.x, Is.LessThan(c.x)); Assert.That(a.z, Is.LessThan(c.z));
                Assert.That(b, Is.EqualTo(new Vector3(a.x, a.y, c.z)));
                Assert.That(d, Is.EqualTo(new Vector3(c.x, a.y, a.z)));
                Assert.That(triangles.Skip(q * 6).Take(6), Is.EqualTo(new[] { v, v + 1, v + 2, v, v + 2, v + 3 }));
                result[q] = Rect.MinMaxRect(a.x, a.z, c.x, c.z);
            }
            return result;
        }
        private static bool OverlapsArea(Rect a, Rect b) =>
            Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > .00001f &&
            Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > .00001f;
        private static void AssertRect(Rect actual, float minX, float maxX, float minZ, float maxZ)
        {
            Assert.That(actual.xMin, Is.EqualTo(minX).Within(.00001f)); Assert.That(actual.xMax, Is.EqualTo(maxX).Within(.00001f));
            Assert.That(actual.yMin, Is.EqualTo(minZ).Within(.00001f)); Assert.That(actual.yMax, Is.EqualTo(maxZ).Within(.00001f));
        }
    }
}
