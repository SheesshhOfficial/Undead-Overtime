using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class DepotPerimeterTests
    {
        private GameObject root;
        private DepotPerimeterPresentation perimeter;
        private readonly string[] names = { "North wall", "South wall", "West wall", "East wall" };
        [UnitySetUp]
        public IEnumerator Setup()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Perimeter checks require an actual graphics device.");
            root = new GameObject("Perimeter test owner");
            for (int i = 0; i < 4; i++)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = names[i]; wall.transform.SetParent(root.transform, false);
                wall.transform.localPosition = i < 2 ? new Vector3(0, 2, i == 0 ? 16 : -16) : new Vector3(i == 2 ? -16 : 16, 2, 0);
                wall.transform.localScale = i < 2 ? new Vector3(32, 4, .5f) : new Vector3(.5f, 4, 32);
                wall.GetComponent<Renderer>().enabled = i != 3;
            }
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            perimeter?.Dispose(); if (root != null) Object.Destroy(root); yield return null; yield return null;
        }
        [Test]
        public void ConcreteCopiesCoincideWithExistingWallsWithoutColliderOrSharedMeshChanges()
        {
            Mesh source = root.transform.Find(names[0]).GetComponent<MeshFilter>().sharedMesh;
            Material concrete = DepotFloorPresentation.LoadConcreteMaterial();
            perimeter = new DepotPerimeterPresentation(root.transform, concrete);
            Assert.That(perimeter.Root.childCount, Is.EqualTo(4));
            Assert.That(perimeter.Root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(4));
            for (int i = 0; i < names.Length; i++)
            {
                Transform original = root.transform.Find(names[i]), visual = perimeter.Root.Find("Concrete " + names[i]);
                Assert.That(original.GetComponent<Renderer>().enabled, Is.False);
                Assert.That(original.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(source));
                Assert.That(original.GetComponent<BoxCollider>().enabled, Is.True);
                Assert.That(original.GetComponent<BoxCollider>().size, Is.EqualTo(Vector3.one));
                Assert.That(visual.localPosition, Is.EqualTo(original.localPosition));
                Assert.That(visual.localRotation, Is.EqualTo(original.localRotation));
                Assert.That(visual.localScale, Is.EqualTo(original.localScale));
                Assert.That(visual.GetComponent<MeshFilter>().sharedMesh.vertices, Is.EqualTo(source.vertices));
                Assert.That(visual.GetComponent<Renderer>().sharedMaterial, Is.SameAs(concrete));
                Assert.That(visual.GetComponent<Renderer>().enabled, Is.EqualTo(i != 3));
            }
        }
        [UnityTest]
        public IEnumerator RepeatedSameFrameDisposalRestoresVisibilityAndReleasesOnlyOwnedCopies()
        {
            Mesh source = root.transform.Find(names[0]).GetComponent<MeshFilter>().sharedMesh;
            Material concrete = DepotFloorPresentation.LoadConcreteMaterial();
            root.transform.Find(names[3]).gameObject.SetActive(false);
            var retired = new System.Collections.Generic.List<Mesh>();
            for (int cycle = 0; cycle < 4; cycle++)
            {
                perimeter = new DepotPerimeterPresentation(root.transform, concrete);
                retired.AddRange(perimeter.Root.GetComponentsInChildren<DepotWallSurface>(true).Select(x => x.GetComponent<MeshFilter>().sharedMesh));
                Assert.That(root.GetComponentsInChildren<DepotWallSurface>().Length, Is.EqualTo(3));
                Transform old = perimeter.Root; perimeter.Dispose(); perimeter.Dispose();
                Assert.That(old.gameObject.activeSelf, Is.False); Assert.That(perimeter.Root, Is.Null);
                Assert.That(root.GetComponentsInChildren<DepotWallSurface>(), Is.Empty);
                for (int i = 0; i < names.Length; i++) Assert.That(root.transform.Find(names[i]).GetComponent<Renderer>().enabled, Is.EqualTo(i != 3));
            }
            yield return null; yield return null;
            Assert.That(root.GetComponentsInChildren<DepotWallSurface>(true), Is.Empty);
            foreach (Mesh mesh in retired) Assert.That(mesh == null, Is.True);
            Assert.That(source == null, Is.False); Assert.That(concrete == null, Is.False);
        }
        [UnityTest]
        public IEnumerator LateConstructionFailureRestoresAllOriginalVisibilityWithoutActiveDuplicates()
        {
            Material concrete = DepotFloorPresentation.LoadConcreteMaterial();
            root.transform.Find(names[3]).localScale = new Vector3(0, 4, 32);
            Assert.Throws<ArgumentException>(() => new DepotPerimeterPresentation(root.transform, concrete));
            Assert.That(root.GetComponentsInChildren<DepotWallSurface>(), Is.Empty);
            for (int i = 0; i < names.Length; i++) Assert.That(root.transform.Find(names[i]).GetComponent<Renderer>().enabled, Is.EqualTo(i != 3));
            Assert.That(root.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(4));
            yield return null; yield return null;
            Assert.That(root.GetComponentsInChildren<DepotWallSurface>(true), Is.Empty);
            Assert.That(root.transform.childCount, Is.EqualTo(4));
        }
    }
}
