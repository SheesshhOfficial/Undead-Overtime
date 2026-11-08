using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UndeadOvertime.Core;
using Object = UnityEngine.Object;
namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class DepotConsoleTests
    {
        private GameObject root;
        private DepotMissionPresentation layout;
        [UnitySetUp] public IEnumerator Setup() { root = new GameObject("Original cabinet tests"); yield return null; }
        [UnityTearDown] public IEnumerator Teardown() { layout?.Dispose(); Object.Destroy(root); yield return null; yield return null; }
        private static void RequireGraphics()
        { if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Cabinet render checks need an actual graphics device."); }
        [Test]
        public void RenderedCabinetUsesOriginalAtlasAndThreeNonCullingLodsInsideTightProxy()
        {
            RequireGraphics(); Material template = DepotFloorPresentation.LoadConcreteMaterial();
            string[] originalKeywords = template.shaderKeywords;
            Texture oldColor = template.GetTexture("_BaseMap"), oldNormal = template.GetTexture("_BumpMap"), oldPacked = template.GetTexture("_MetallicGlossMap");
            var cabinet = DepotConsolePresentation.Create(root.transform, template);
            Assert.That(cabinet.transform.localPosition, Is.EqualTo(DepotConsolePresentation.Origin));
            Assert.That(cabinet.transform.localScale, Is.EqualTo(Vector3.one * .65f));
            Assert.That(cabinet.GetComponentsInChildren<Collider>(true), Is.Empty);
            LOD[] lods = cabinet.GetComponent<LODGroup>().GetLODs(); Assert.That(lods.Length, Is.EqualTo(3));
            Assert.That(lods[0].screenRelativeTransitionHeight, Is.EqualTo(.18f)); Assert.That(lods[1].screenRelativeTransitionHeight, Is.EqualTo(.06f));
            Assert.That(lods[2].screenRelativeTransitionHeight, Is.Zero);
            Material owned = lods[0].renderers[0].sharedMaterial; Assert.That(owned, Is.Not.SameAs(template));
            var proxy = new Bounds(DepotConsolePresentation.CollisionCenter, DepotConsolePresentation.CollisionSize);
            foreach (LOD lod in lods)
            {
                Assert.That(lod.renderers.Length, Is.EqualTo(1)); Assert.That(lod.renderers[0].sharedMaterial, Is.SameAs(owned));
                Bounds b = lod.renderers[0].bounds;
                Assert.That(b.min.x, Is.GreaterThanOrEqualTo(proxy.min.x - .001f)); Assert.That(b.max.x, Is.LessThanOrEqualTo(proxy.max.x + .001f));
                Assert.That(b.min.z, Is.GreaterThanOrEqualTo(proxy.min.z - .001f)); Assert.That(b.max.z, Is.LessThanOrEqualTo(proxy.max.z + .001f));
                Assert.That(b.min.y, Is.EqualTo(0).Within(.001)); Assert.That(b.max.y, Is.LessThanOrEqualTo(proxy.max.y + .001f));
            }
            Assert.That(owned.GetTexture("_BaseMap"), Is.SameAs(Resources.Load<Texture2D>(DepotConsolePresentation.ResourceRoot + "cabinet_basecolor")));
            Assert.That(owned.GetTexture("_BumpMap"), Is.SameAs(Resources.Load<Texture2D>(DepotConsolePresentation.ResourceRoot + "cabinet_normal_opengl")));
            Assert.That(owned.GetTexture("_MetallicGlossMap"), Is.SameAs(Resources.Load<Texture2D>(DepotConsolePresentation.ResourceRoot + "cabinet_metallic_smoothness")));
            Assert.That(owned.IsKeywordEnabled("_NORMALMAP") && owned.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
            Assert.That(owned.GetFloat("_Smoothness"), Is.EqualTo(1)); Assert.That(owned.GetFloat("_Metallic"), Is.EqualTo(1));
            Assert.That(owned.GetFloat("_SmoothnessTextureChannel"), Is.Zero);
            Assert.That(template.shaderKeywords, Is.EqualTo(originalKeywords));
            Assert.That(template.GetTexture("_BaseMap"), Is.SameAs(oldColor)); Assert.That(template.GetTexture("_BumpMap"), Is.SameAs(oldNormal));
            Assert.That(template.GetTexture("_MetallicGlossMap"), Is.SameAs(oldPacked));
        }
        [Test]
        public void InactiveParentIsRejectedBeforeCreatingOwnedObjects()
        {
            RequireGraphics(); Material template = DepotFloorPresentation.LoadConcreteMaterial(); root.SetActive(false);
            Assert.Throws<System.InvalidOperationException>(() => DepotConsolePresentation.Create(root.transform, template));
            Assert.That(root.transform.childCount, Is.Zero); Assert.That(root.GetComponentsInChildren<DepotConsolePresentation>(true), Is.Empty);
            Assert.That(template == null, Is.False);
        }
        [UnityTest]
        public IEnumerator CabinetDisposalDestroysOnlyOwnedMaterialNotImportedMeshesOrTextures()
        {
            RequireGraphics(); Material template = DepotFloorPresentation.LoadConcreteMaterial();
            var cabinet = DepotConsolePresentation.Create(root.transform, template);
            Material owned = cabinet.GetComponent<LODGroup>().GetLODs()[0].renderers[0].sharedMaterial;
            Mesh shared = cabinet.GetComponentsInChildren<MeshFilter>()[0].sharedMesh; Texture color = owned.GetTexture("_BaseMap");
            Object.Destroy(cabinet.gameObject); yield return null; yield return null;
            Assert.That(owned == null, Is.True); Assert.That(template == null, Is.False); Assert.That(shared == null, Is.False); Assert.That(color == null, Is.False);
        }
        [Test]
        public void HeadlessProxyMatchesMeasuredCabinetAndKeepsPowerAccessWithoutAssetLoad()
        {
            layout = new DepotMissionPresentation(root.transform, 5, 2, false);
            var collider = layout.Root.Find("Mission power console").GetComponent<BoxCollider>(); Physics.SyncTransforms();
            Assert.That((collider.bounds.center - DepotConsolePresentation.CollisionCenter).sqrMagnitude, Is.LessThan(.00000001f));
            Assert.That((collider.bounds.size - DepotConsolePresentation.CollisionSize).sqrMagnitude, Is.LessThan(.000001));
            Assert.That(collider.bounds.max.y, Is.LessThan(DepotMissionPresentation.PowerControl.Y));
            Assert.That(layout.Root.GetComponentsInChildren<DepotConsolePresentation>(true), Is.Empty);
            Assert.That(layout.Root.GetComponentsInChildren<Renderer>(true), Is.Empty);
            Assert.That(layout.HasPowerLine(new AimVector(-9, 1.6, -6.8)), Is.True);
            // Tight proxy no longer has the old 23 cm lateral padding. Walking still stops against the actual box.
            Vector3 outside = LanRange.ResolvePlanarMovement(new Vector3(-9.9f, 1.6f, -6), Vector3.forward * .2f);
            Assert.That(outside.z, Is.EqualTo(-5.8f).Within(.001));
            Vector3 blocked = LanRange.ResolvePlanarMovement(new Vector3(-9, 1.6f, -6.3f), Vector3.forward * .2f);
            Assert.That(blocked.z, Is.LessThan(-6.16f));
        }
        [Test]
        public void RenderedMissionHidesPlaceholderAndUsesSamePhysicalProxy()
        {
            RequireGraphics(); Material template = DepotFloorPresentation.LoadConcreteMaterial();
            layout = new DepotMissionPresentation(root.transform, 5, 2, true, template, template, template, template);
            var proxy = layout.Root.Find("Mission power console"); Assert.That(proxy.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(proxy.GetComponent<BoxCollider>().enabled, Is.True); Assert.That(proxy.localScale, Is.EqualTo(DepotConsolePresentation.CollisionSize));
            Assert.That(layout.Root.GetComponentsInChildren<DepotConsolePresentation>().Length, Is.EqualTo(1));
            Assert.That(layout.HasPowerLine(new AimVector(-9, 1.6, -6.8)), Is.True);
        }
    }
}
