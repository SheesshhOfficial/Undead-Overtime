using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UndeadOvertime.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class DepotArchitectureTests
    {
        private GameObject root, cameraObject;
        private DepotMissionPresentation layout;
        private LanRange range;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            root = new GameObject("Depot architecture test"); yield return null;
        }
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (range != null)
            {
                range.StopSession();
                for (int i = 0; i < 120 && range.IsBusy; i++) yield return null;
            }
            layout?.Dispose(); Object.Destroy(root);
            if (cameraObject != null) Object.Destroy(cameraObject);
            yield return null; yield return null;
        }
        private static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Rendered architecture requires an actual graphics device.");
        }
        private GameObject Wall()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform, false);
            wall.transform.localScale = new Vector3(14, 3, .4f); return wall;
        }
        private static CoopMissionSnapshot OpenState() => new CoopMissionSnapshot(5, 2, CoopMissionPhase.DefendDepot, true, true, 0);
        private DepotMissionPresentation Rendered()
        {
            RequireGraphics(); Material concrete = DepotFloorPresentation.LoadConcreteMaterial();
            Material other = Resources.Load<Material>(DepotFloorPresentation.ResourceRoot + "floor_material");
            return new DepotMissionPresentation(root.transform, 5, 2, true, other, other, other, concrete);
        }
        [Test]
        public void MeterUvsPreserveGeometryAndSharedCubeWithCorrectFaceDensity()
        {
            RequireGraphics(); var wall = Wall(); var filter = wall.GetComponent<MeshFilter>(); Mesh source = filter.sharedMesh;
            Vector3[] positions = source.vertices, normals = source.normals; Vector2[] sourceUv = source.uv;
            Bounds bounds = source.bounds; var collider = wall.GetComponent<BoxCollider>(); Vector3 size = collider.size;
            Material concrete = DepotFloorPresentation.LoadConcreteMaterial();
            Vector2 scale = concrete.GetTextureScale("_BaseMap"), offset = concrete.GetTextureOffset("_BaseMap");
            DepotWallSurface.Apply(wall, concrete); Mesh copy = filter.sharedMesh;
            Assert.That(copy, Is.Not.SameAs(source)); Assert.That(copy.vertices, Is.EqualTo(positions));
            Assert.That(copy.normals, Is.EqualTo(normals)); Assert.That(copy.bounds, Is.EqualTo(bounds));
            Assert.That(source.uv, Is.EqualTo(sourceUv)); Assert.That(collider.size, Is.EqualTo(size));
            Assert.That(collider.enabled, Is.True); Assert.That(wall.GetComponent<Renderer>().sharedMaterial, Is.SameAs(concrete));
            Assert.That(concrete.GetTextureScale("_BaseMap"), Is.EqualTo(scale)); Assert.That(concrete.GetTextureOffset("_BaseMap"), Is.EqualTo(offset));
            Vector2[] uv = copy.uv;
            foreach (Vector3 face in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                int[] indices = Enumerable.Range(0, normals.Length).Where(i => Vector3.Dot(normals[i], face) > .999f).ToArray();
                Assert.That(indices.Length, Is.EqualTo(4));
                float width = indices.Max(i => uv[i].x) - indices.Min(i => uv[i].x);
                float height = indices.Max(i => uv[i].y) - indices.Min(i => uv[i].y);
                Assert.That(width, Is.EqualTo(Mathf.Abs(face.x) > .5f ? .4f : 14).Within(.0001f));
                Assert.That(height, Is.EqualTo(Mathf.Abs(face.y) > .5f ? .4f : 3).Within(.0001f));
            }
            Assert.That(copy.tangents.Length, Is.EqualTo(positions.Length));
            for (int i = 0; i < copy.tangents.Length; i++)
            {
                Vector4 t = copy.tangents[i]; Vector3 tangent = new Vector3(t.x, t.y, t.z);
                Assert.That(tangent.sqrMagnitude, Is.EqualTo(1).Within(.001)); Assert.That(Mathf.Abs(t.w), Is.EqualTo(1));
                Assert.That(Vector3.Dot(normals[i], tangent), Is.EqualTo(0).Within(.001));
            }
        }
        [Test]
        public void InvalidOrRepeatedWallSetupCannotReplaceOriginalMeshes()
        {
            RequireGraphics(); Material concrete = DepotFloorPresentation.LoadConcreteMaterial(); var wall = Wall();
            Mesh source = wall.GetComponent<MeshFilter>().sharedMesh;
            Assert.Throws<ArgumentNullException>(() => DepotWallSurface.Apply(null, concrete));
            Assert.Throws<ArgumentNullException>(() => DepotWallSurface.Apply(wall, null));
            wall.transform.localScale = new Vector3(0, 3, .4f);
            Assert.Throws<ArgumentException>(() => DepotWallSurface.Apply(wall, concrete));
            Assert.That(wall.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(source));
            Assert.That(wall.GetComponent<DepotWallSurface>(), Is.Null);
            wall.transform.localScale = new Vector3(14, 3, .4f); wall.SetActive(false);
            Assert.Throws<InvalidOperationException>(() => DepotWallSurface.Apply(wall, concrete));
            Assert.That(wall.GetComponent<DepotWallSurface>(), Is.Null); wall.SetActive(true);
            DepotWallSurface.Apply(wall, concrete);
            Mesh copy = wall.GetComponent<MeshFilter>().sharedMesh;
            Assert.Throws<InvalidOperationException>(() => DepotWallSurface.Apply(wall, concrete));
            Assert.That(wall.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(copy));
        }
        [UnityTest]
        public IEnumerator SurfaceOwnerDestroysOnlyItsMeshCopy()
        {
            RequireGraphics(); var wall = Wall(); Mesh source = wall.GetComponent<MeshFilter>().sharedMesh;
            Material concrete = DepotFloorPresentation.LoadConcreteMaterial(); DepotWallSurface.Apply(wall, concrete);
            Mesh copy = wall.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(wall); yield return null; yield return null;
            Assert.That(copy == null, Is.True); Assert.That(source == null, Is.False); Assert.That(concrete == null, Is.False);
        }
        [Test]
        public void RenderedPartitionsAndShutterPreserveCollidersAndOpenState()
        {
            layout = Rendered(); Material concrete = DepotFloorPresentation.LoadConcreteMaterial();
            Assert.That(layout.ArchitecturalDetailsEnabled, Is.True); Assert.That(layout.Root.childCount, Is.EqualTo(7));
            Assert.That(layout.Root.GetComponentsInChildren<DepotWallSurface>().Length, Is.EqualTo(2));
            Assert.That(layout.Root.GetComponentsInChildren<DepotSafetyMarkings>().Length, Is.EqualTo(1));
            foreach (string name in new[] { "Mission west partition", "Mission east partition" })
            {
                var wall = layout.Root.Find(name); Assert.That(wall.GetComponent<Renderer>().sharedMaterial, Is.SameAs(concrete));
                Assert.That(wall.localScale, Is.EqualTo(new Vector3(14, 3, .4f)));
                Assert.That(wall.GetComponent<BoxCollider>().size, Is.EqualTo(Vector3.one));
                Assert.That(wall.position.y, Is.EqualTo(1.5f)); Assert.That(wall.position.z, Is.EqualTo(-2));
            }
            var gate = layout.Root.Find("Mission route gate"); Assert.That(gate.childCount, Is.EqualTo(20));
            Assert.That(gate.GetComponent<Renderer>().sharedMaterial, Is.Not.SameAs(concrete));
            Assert.That(layout.Root.GetComponentsInChildren<Collider>(true).Count(c => c.enabled), Is.EqualTo(4));
            foreach (Transform detail in gate)
            {
                Assert.That(detail.GetComponent<Collider>(), Is.Null);
                Assert.That(detail.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(gate.GetComponent<MeshFilter>().sharedMesh));
                if (detail.name.StartsWith("Gate shutter rib", StringComparison.Ordinal))
                {
                    Bounds rib = detail.GetComponent<Renderer>().bounds;
                    Assert.That(rib.min.x, Is.GreaterThan(-1.85f)); Assert.That(rib.max.x, Is.LessThan(1.85f));
                }
                Assert.That(detail.GetComponent<Renderer>().bounds.min.z, Is.GreaterThanOrEqualTo(-2.221f));
                Assert.That(detail.GetComponent<Renderer>().bounds.max.z, Is.LessThanOrEqualTo(-1.779f));
            }
            Vector3 start = new Vector3(0, 1.6f, -2.7f);
            Assert.That(LanRange.ResolvePlanarMovement(start, Vector3.forward * .2f).z, Is.LessThan(-2.55f));
            Assert.That(layout.HasPowerLine(new AimVector(-9, 1.6, -6.8)), Is.True);
            Assert.That(layout.ContainsExtraction(DepotMissionPresentation.ExtractionCenter), Is.True);
            Assert.That(layout.TryApply(OpenState()), Is.True); Assert.That(gate.gameObject.activeInHierarchy, Is.False);
            foreach (Transform detail in gate) Assert.That(detail.gameObject.activeInHierarchy, Is.False);
            Assert.That(LanRange.ResolvePlanarMovement(start, Vector3.forward * .2f).z, Is.EqualTo(-2.5f).Within(.001f));
        }
        [UnityTest]
        public IEnumerator DisposedPresentationReleasesWallCopiesButNotSharedResources()
        {
            layout = Rendered(); Material concrete = DepotFloorPresentation.LoadConcreteMaterial();
            Mesh[] copies = layout.Root.GetComponentsInChildren<DepotWallSurface>().Select(w => w.GetComponent<MeshFilter>().sharedMesh).ToArray();
            var savedRoot = layout.Root; layout.Dispose(); Assert.That(savedRoot.gameObject.activeSelf, Is.False);
            yield return null; yield return null;
            Assert.That(savedRoot == null, Is.True); foreach (Mesh copy in copies) Assert.That(copy == null, Is.True);
            Assert.That(concrete == null, Is.False); Assert.That(concrete.GetTexture("_BaseMap"), Is.Not.Null);
        }
        [Test]
        public void ColliderOnlyConstructionStillCreatesNoSurfaceCopiesOrGateRelief()
        {
            layout = new DepotMissionPresentation(root.transform, 5, 2, false);
            Assert.That(layout.ArchitecturalDetailsEnabled, Is.False); Assert.That(layout.Root.childCount, Is.EqualTo(5));
            Assert.That(layout.Root.GetComponentsInChildren<DepotWallSurface>(true), Is.Empty);
            Assert.That(layout.Root.GetComponentsInChildren<DepotSafetyMarkings>(true), Is.Empty);
            Assert.That(layout.Root.GetComponentsInChildren<MeshFilter>(true), Is.Empty);
            Assert.That(layout.Root.Find("Mission route gate").childCount, Is.Zero);
            Assert.That(layout.Root.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(4));
        }
        [UnityTest]
        public IEnumerator ActualRenderedMissionWiresSharedConcreteWithoutChangingLobbyRange()
        {
            RequireGraphics(); cameraObject = new GameObject("Architecture integration camera");
            range = root.AddComponent<LanRange>(); range.Build(cameraObject.AddComponent<Camera>());
            Assert.That(root.GetComponentsInChildren<DepotWallSurface>(), Is.Empty);
            Assert.That(range.SetMissionMode(true), Is.True); ushort port;
            using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0))) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            Assert.That(range.StartHosting("127.0.0.1", port), Is.True);
            for (int i = 0; i < 120 && !range.IsConnected; i++) yield return null;
            Assert.That(range.IsConnected, Is.True); Assert.That(range.BeginRound(), Is.True);
            Assert.That(root.GetComponentsInChildren<DepotWallSurface>().Length, Is.EqualTo(6));
            Assert.That(root.GetComponentsInChildren<DepotSafetyMarkings>().Length, Is.EqualTo(1));
            Assert.That(range.CurrentMission.Mission.Phase, Is.EqualTo(CoopMissionPhase.RestorePower));
            Assert.That(range.ReturnToLobby(), Is.True); Assert.That(root.GetComponentsInChildren<DepotWallSurface>(), Is.Empty);
            yield return null; yield return null;
            Assert.That(root.GetComponentsInChildren<DepotWallSurface>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<DepotSafetyMarkings>(true), Is.Empty);
            foreach (string name in new[] { "North wall", "South wall", "West wall", "East wall" })
                Assert.That(root.transform.Find("LAN range generated geometry/" + name).GetComponent<Renderer>().enabled, Is.True);
            Assert.That(range.BeginRound(), Is.True); Assert.That(root.GetComponentsInChildren<DepotWallSurface>().Length, Is.EqualTo(6));
            Assert.That(range.MissionGateOpen, Is.False);
        }
    }
}
