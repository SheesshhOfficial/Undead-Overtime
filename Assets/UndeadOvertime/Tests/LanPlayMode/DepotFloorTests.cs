using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class DepotFloorTests
    {
        private GameObject root, cameraObject;
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (root != null) Object.Destroy(root);
            if (cameraObject != null) Object.Destroy(cameraObject);
            yield return null; yield return null;
        }
        [UnityTest]
        public IEnumerator OriginalTilesCoverArenaWithSharedPbrMaterialsAndThreeLods()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Rendered import check requires an actual graphics device.");
            root = new GameObject("Original floor test"); var floor = DepotFloorPresentation.Create(root.transform); yield return null;
            Assert.That(floor.TileCount, Is.EqualTo(64)); var groups = floor.GetComponentsInChildren<LODGroup>(); Assert.That(groups.Length, Is.EqualTo(64));
            Assert.That(floor.GetComponentsInChildren<Collider>(true), Is.Empty);
            Material top = null, side = null; Bounds total = default; bool first = true;
            foreach (var group in groups)
            {
                LOD[] lods = group.GetLODs(); Assert.That(lods.Length, Is.EqualTo(3));
                Assert.That(lods[0].screenRelativeTransitionHeight, Is.EqualTo(.25f));
                Assert.That(lods[1].screenRelativeTransitionHeight, Is.EqualTo(.08f));
                Assert.That(lods[2].screenRelativeTransitionHeight, Is.Zero, "Distant floor must not cull into holes.");
                foreach (var lod in lods)
                {
                    Assert.That(lod.renderers.Length, Is.EqualTo(1)); Renderer renderer = lod.renderers[0];
                    if (top == null) { top = renderer.sharedMaterials[0]; side = renderer.sharedMaterials[1]; }
                    Assert.That(renderer.sharedMaterials[0], Is.SameAs(top)); Assert.That(renderer.sharedMaterials[1], Is.SameAs(side));
                }
                if (first) { total = lods[0].renderers[0].bounds; first = false; } else total.Encapsulate(lods[0].renderers[0].bounds);
            }
            Assert.That(total.min.x, Is.EqualTo(-16).Within(.001)); Assert.That(total.max.x, Is.EqualTo(16).Within(.001));
            Assert.That(total.min.z, Is.EqualTo(-16).Within(.001)); Assert.That(total.max.z, Is.EqualTo(16).Within(.001));
            Assert.That(total.max.y, Is.EqualTo(0).Within(.001));
            foreach (Material material in new[] { top, side })
            {
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null); Assert.That(material.GetTexture("_BumpMap"), Is.Not.Null);
                Assert.That(material.GetTexture("_MetallicGlossMap"), Is.Not.Null); Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True);
                Assert.That(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
            }
        }
        [UnityTest]
        public IEnumerator RenderedFloorKeepsContinuousSupportWithoutTileColliderSeams()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Rendered range check requires an actual graphics device.");
            root = new GameObject("Original floor range test"); cameraObject = new GameObject("Floor test camera");
            var range = root.AddComponent<LanRange>(); range.Build(cameraObject.AddComponent<Camera>()); yield return null;
            Assert.That(root.GetComponentsInChildren<DepotFloorPresentation>().Length, Is.EqualTo(1));
            Transform ground = root.transform.Find("LAN range generated geometry/Ground"); Assert.That(ground, Is.Not.Null);
            Assert.That(ground.GetComponent<Renderer>().enabled, Is.False); Assert.That(ground.GetComponent<BoxCollider>().enabled, Is.True);
            Physics.SyncTransforms();
            foreach (float x in new[] { -15.9f, -12f, -8f, -4f, 0f, 4f, 8f, 12f, 15.9f })
                foreach (float z in new[] { -15.9f, -12f, -8f, -4f, 0f, 4f, 8f, 12f, 15.9f })
                {
                    Assert.That(Physics.Raycast(new Vector3(x, .2f, z), Vector3.down, out RaycastHit hit, 1), Is.True);
                    Assert.That(hit.collider, Is.SameAs(ground.GetComponent<BoxCollider>())); Assert.That(hit.point.y, Is.EqualTo(0).Within(.001));
                }
        }
        [UnityTest]
        public IEnumerator HeadlessRangeDoesNotLoadOrConstructFloorPresentation()
        {
            root = new GameObject("Headless floor range test"); cameraObject = new GameObject("Headless floor camera");
            var range = root.AddComponent<LanRange>(); range.Build(cameraObject.AddComponent<Camera>(), false); yield return null;
            Assert.That(root.GetComponentsInChildren<DepotFloorPresentation>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Renderer>(true), Is.Empty);
            Assert.That(root.transform.Find("LAN range generated geometry/Ground").GetComponent<BoxCollider>(), Is.Not.Null);
        }
    }
}
