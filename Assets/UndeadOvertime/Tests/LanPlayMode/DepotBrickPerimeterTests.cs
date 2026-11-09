using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class DepotBrickPerimeterTests
    {
        private static readonly string[] WallNames = { "North wall", "South wall", "West wall", "East wall" };
        private static readonly Vector3[] Positions = {
            new Vector3(-16, 0, 15.93f), new Vector3(16, 0, -15.93f),
            new Vector3(-15.93f, 0, -16), new Vector3(15.93f, 0, 16) };
        private static readonly float[] Yaws = { 0, 180, 270, 90 };
        private static readonly string[] Maps = { "_BaseMap", "_BumpMap", "_MetallicGlossMap" };
        private GameObject arena, survivors;
        private DepotBrickPerimeter perimeter;
        private Material temporaryMaterial;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            arena = new GameObject("Depot brick perimeter tests");
            for (int i = 0; i < 4; i++)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = WallNames[i];
                wall.transform.SetParent(arena.transform, false);
                wall.transform.localPosition = i < 2 ? new Vector3(0, 2, i == 0 ? 16 : -16) : new Vector3(i == 2 ? -16 : 16, 2, 0);
                wall.transform.localScale = i < 2 ? new Vector3(32, 4, .5f) : new Vector3(.5f, 4, 32);
                wall.GetComponent<Renderer>().enabled = i != 3;
            }
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (perimeter != null) perimeter.Dispose(); perimeter = null;
            if (arena != null) Object.Destroy(arena);
            if (survivors != null) Object.Destroy(survivors);
            if (temporaryMaterial != null) Object.Destroy(temporaryMaterial);
            yield return null; yield return null;
        }

        private static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Depot brick rendering and ownership checks require an actual graphics device.");
        }
        private static Material Concrete()
        {
            // A read only: headless guards can use the same serialized material without a shader lookup.
            Material material = Resources.Load<Material>(DepotFloorPresentation.ResourceRoot + "concrete_material");
            Assert.That(material, Is.Not.Null); return material;
        }
        private DepotBrickPerimeter Create()
        { RequireGraphics(); perimeter = DepotBrickPerimeter.Create(arena.transform, Concrete()); return perimeter; }
        private Transform Wall(int index) => arena.transform.Find(WallNames[index]);
        private WallSnapshot[] SnapshotWalls() => Enumerable.Range(0, 4).Select(i => new WallSnapshot(Wall(i))).ToArray();
        private static int OwnedMaterialCount() => Resources.FindObjectsOfTypeAll<Material>().Count(m => m.name == "Owned depot perimeter brick PBR");
        private static Material Brick(DepotBrickPerimeter owner) => owner.Root.GetComponentsInChildren<Renderer>(true)[0].sharedMaterials[0];

        [Test]
        public void FourCanonicalStripsHaveThreeFullBoundsNonCullingLodsInsideOriginalEnvelopes()
        {
            var owner = Create();
            Assert.That(owner.Root.parent, Is.SameAs(arena.transform)); Assert.That(owner.Root.childCount, Is.EqualTo(4));
            Assert.That(owner.Root.localPosition, Is.EqualTo(Vector3.zero)); Assert.That(owner.Root.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(owner.Root.localScale, Is.EqualTo(Vector3.one));
            Assert.That(owner.Root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(owner.Root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(owner.Root.GetComponentsInChildren<Animation>(true), Is.Empty);
            Assert.That(owner.Root.GetComponentsInChildren<Animator>(true), Is.Empty);
            Assert.That(owner.Root.GetComponentsInChildren<MonoBehaviour>(true), Is.EqualTo(new MonoBehaviour[] { owner }));
            Assert.That(owner.Root.GetComponentsInChildren<LODGroup>(true).Length, Is.EqualTo(4));
            Assert.That(owner.Root.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(12));
            Assert.That(arena.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(4));
            Material brick = Brick(owner), concrete = Concrete();
            for (int wall = 0; wall < 4; wall++)
            {
                Transform strip = owner.Root.Find("Brick " + WallNames[wall]);
                Assert.That(strip, Is.Not.Null); Assert.That(strip.localPosition, Is.EqualTo(Positions[wall]));
                Assert.That(Quaternion.Angle(strip.localRotation, Quaternion.Euler(0, Yaws[wall], 0)), Is.LessThan(.001f));
                Assert.That(strip.localScale, Is.EqualTo(Vector3.one)); Assert.That(strip.childCount, Is.EqualTo(3));
                var group = strip.GetComponent<LODGroup>(); LOD[] lods = group.GetLODs();
                Assert.That(group.fadeMode, Is.EqualTo(LODFadeMode.None)); Assert.That(group.animateCrossFading, Is.False);
                Assert.That(lods.Length, Is.EqualTo(3)); AssertVector(group.localReferencePoint, new Vector3(16, 2, 0));
                Assert.That(group.size, Is.EqualTo(32).Within(.001f));
                var envelope = new Bounds(Wall(wall).localPosition, Wall(wall).localScale);
                Vector3 center = wall < 2 ? new Vector3(0, 2, wall == 0 ? 15.93f : -15.93f) : new Vector3(wall == 2 ? -15.93f : 15.93f, 2, 0);
                var expected = new Bounds(center, wall < 2 ? new Vector3(32, 4, .36f) : new Vector3(.36f, 4, 32));
                for (int lod = 0; lod < 3; lod++)
                {
                    Assert.That(lods[lod].screenRelativeTransitionHeight, Is.EqualTo(lod == 0 ? .25f : lod == 1 ? .08f : 0));
                    Assert.That(lods[lod].fadeTransitionWidth, Is.Zero); Assert.That(lods[lod].renderers.Length, Is.EqualTo(1));
                    Renderer renderer = lods[lod].renderers[0];
                    Assert.That(renderer, Is.InstanceOf<MeshRenderer>()); Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(2));
                    Assert.That(renderer.sharedMaterials[0], Is.SameAs(brick)); Assert.That(renderer.sharedMaterials[1], Is.SameAs(concrete));
                    AssertVector(renderer.bounds.min, expected.min); AssertVector(renderer.bounds.max, expected.max);
                    AssertInside(renderer.bounds, envelope);
                    // The foundation reaches the existing inner face, with no new collision envelope.
                    float innerFace = wall == 0 ? renderer.bounds.min.z : wall == 1 ? -renderer.bounds.max.z :
                        wall == 2 ? -renderer.bounds.max.x : renderer.bounds.min.x;
                    Assert.That(innerFace, Is.EqualTo(15.75f).Within(.001f));
                }
            }
        }

        [Test]
        public void AllInstancesShareImportedMeshesMapsAndOneOpaqueBrickCloneWithConcreteSlotUntouched()
        {
            Material concrete = Concrete(); var before = new MaterialSnapshot(concrete); int owned = OwnedMaterialCount();
            var owner = Create(); Material brick = Brick(owner);
            Assert.That(OwnedMaterialCount(), Is.EqualTo(owned + 1)); Assert.That(brick, Is.Not.SameAs(concrete));
            string[] resourceNames = { "brick_basecolor", "brick_normal_opengl", "brick_metallic_smoothness" };
            for (int i = 0; i < Maps.Length; i++)
            {
                var texture = Resources.Load<Texture2D>(DepotBrickPerimeter.ResourceRoot + resourceNames[i]);
                Assert.That(texture, Is.Not.Null); Assert.That(texture.width, Is.EqualTo(2048)); Assert.That(texture.height, Is.EqualTo(1024));
                Assert.That(brick.GetTexture(Maps[i]), Is.SameAs(texture));
                Assert.That(brick.GetTextureScale(Maps[i]), Is.EqualTo(Vector2.one)); Assert.That(brick.GetTextureOffset(Maps[i]), Is.EqualTo(Vector2.zero));
            }
            foreach (string property in new[] { "_BumpScale", "_Metallic", "_Smoothness", "_WorkflowMode", "_ZWrite", "_SrcBlend" })
                Assert.That(brick.GetFloat(property), Is.EqualTo(1), property);
            foreach (string property in new[] { "_SmoothnessTextureChannel", "_Surface", "_AlphaClip", "_Blend", "_DstBlend" })
                Assert.That(brick.GetFloat(property), Is.Zero, property);
            Assert.That(brick.GetColor("_BaseColor"), Is.EqualTo(Color.white)); Assert.That(brick.GetColor("_EmissionColor"), Is.EqualTo(Color.black));
            Assert.That(brick.GetTexture("_OcclusionMap"), Is.Null); Assert.That(brick.GetTexture("_EmissionMap"), Is.Null);
            Assert.That(brick.GetTag("RenderType", false), Is.EqualTo("Opaque")); Assert.That(brick.renderQueue, Is.EqualTo((int)RenderQueue.Geometry));
            Assert.That(brick.IsKeywordEnabled("_NORMALMAP"), Is.True); Assert.That(brick.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
            foreach (string keyword in new[] { "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", "_ALPHATEST_ON", "_ALPHAPREMULTIPLY_ON",
                "_ALPHAMODULATE_ON", "_EMISSION", "_SPECULAR_SETUP", "_OCCLUSIONMAP", "_SURFACE_TYPE_TRANSPARENT", "_DETAIL_MULX2", "_DETAIL_SCALED" })
                Assert.That(brick.IsKeywordEnabled(keyword), Is.False, keyword);
            Assert.That(brick.enableInstancing, Is.EqualTo(concrete.enableInstancing));
            for (int lod = 0; lod < 3; lod++)
            {
                GameObject resource = Resources.Load<GameObject>(DepotBrickPerimeter.ResourceRoot + "UO_DepotFacade32m_LOD" + lod);
                Mesh imported = resource.GetComponentInChildren<MeshFilter>(true).sharedMesh;
                foreach (LODGroup group in owner.Root.GetComponentsInChildren<LODGroup>(true))
                {
                    Renderer renderer = group.GetLODs()[lod].renderers[0];
                    Assert.That(renderer.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(imported));
                    Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { brick, concrete }));
                }
            }
            before.AssertUnchanged(concrete);
        }

        [Test]
        public void OriginalObjectsTransformsCollidersMeshesAndMaterialsRemainIntactAcrossDisposal()
        {
            Wall(2).gameObject.SetActive(false); WallSnapshot[] before = SnapshotWalls();
            Mesh sourceMesh = Wall(0).GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = sourceMesh.vertices, normals = sourceMesh.normals; Vector2[] uv = sourceMesh.uv; int[] triangles = sourceMesh.triangles;
            var owner = Create();
            foreach (var snapshot in before) snapshot.AssertUnchanged(false);
            owner.Dispose();
            foreach (var snapshot in before) snapshot.AssertUnchanged(snapshot.Enabled);
            Assert.That(sourceMesh.vertices, Is.EqualTo(vertices)); Assert.That(sourceMesh.normals, Is.EqualTo(normals));
            Assert.That(sourceMesh.uv, Is.EqualTo(uv)); Assert.That(sourceMesh.triangles, Is.EqualTo(triangles));
        }

        [Test]
        public void InactiveAndHiddenSourcesMirrorActivityVisibilityLayerAndShadowFlags()
        {
            for (int i = 0; i < 4; i++)
            {
                var source = Wall(i); var renderer = source.GetComponent<Renderer>();
                source.gameObject.layer = 8 + i; source.gameObject.SetActive(i % 2 != 0);
                renderer.enabled = i < 2; renderer.forceRenderingOff = i == 1;
                renderer.shadowCastingMode = i % 2 == 0 ? ShadowCastingMode.Off : ShadowCastingMode.TwoSided;
                renderer.receiveShadows = i % 2 != 0;
            }
            var before = SnapshotWalls(); var owner = Create(); Assert.That(owner.Root.gameObject.activeInHierarchy, Is.True);
            for (int i = 0; i < 4; i++)
            {
                Transform strip = owner.Root.Find("Brick " + WallNames[i]); var source = Wall(i); var original = source.GetComponent<Renderer>();
                Assert.That(strip.gameObject.activeSelf, Is.EqualTo(source.gameObject.activeSelf));
                foreach (Transform child in strip.GetComponentsInChildren<Transform>(true)) Assert.That(child.gameObject.layer, Is.EqualTo(source.gameObject.layer));
                foreach (Renderer renderer in strip.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.That(renderer.enabled, Is.EqualTo(before[i].Enabled)); Assert.That(renderer.forceRenderingOff, Is.EqualTo(original.forceRenderingOff));
                    Assert.That(renderer.shadowCastingMode, Is.EqualTo(original.shadowCastingMode)); Assert.That(renderer.receiveShadows, Is.EqualTo(original.receiveShadows));
                }
            }
            owner.Dispose(); foreach (var snapshot in before) snapshot.AssertUnchanged(snapshot.Enabled);
        }

        [UnityTest]
        public IEnumerator RepeatedSameFrameDisposeImmediatelyHidesCopiesAndReleasesOnlyOwnedMaterials()
        {
            RequireGraphics(); int baseline = OwnedMaterialCount(); Material concrete = Concrete(); var materialBefore = new MaterialSnapshot(concrete);
            Wall(2).gameObject.SetActive(false); var before = SnapshotWalls();
            var retired = new List<Material>(); var sharedMeshes = new List<Mesh>(); var sharedTextures = new List<Texture>();
            for (int cycle = 0; cycle < 4; cycle++)
            {
                var owner = Create(); Transform visual = owner.Root; Material brick = Brick(owner);
                Assert.That(retired.Contains(brick), Is.False); retired.Add(brick);
                sharedMeshes.AddRange(visual.GetComponentsInChildren<MeshFilter>(true).Select(x => x.sharedMesh));
                sharedTextures.AddRange(Maps.Select(brick.GetTexture));
                owner.Dispose(); owner.Dispose();
                Assert.That(owner.Root, Is.Null); Assert.That(visual.gameObject.activeSelf, Is.False);
                Assert.That(arena.GetComponentsInChildren<DepotBrickPerimeter>(), Is.Empty);
                foreach (var snapshot in before) snapshot.AssertUnchanged(snapshot.Enabled);
            }
            yield return null; yield return null;
            Assert.That(arena.transform.childCount, Is.EqualTo(4)); Assert.That(OwnedMaterialCount(), Is.EqualTo(baseline));
            foreach (Material material in retired) Assert.That(material == null, Is.True);
            foreach (Mesh mesh in sharedMeshes) Assert.That(mesh == null, Is.False);
            foreach (Texture texture in sharedTextures) Assert.That(texture == null, Is.False);
            materialBefore.AssertUnchanged(concrete);
        }

        [UnityTest]
        public IEnumerator DestroyingArenaRestoresSurvivingOriginalsAndReleasesInactiveStripOwnership()
        {
            RequireGraphics(); int baseline = OwnedMaterialCount();
            for (int i = 0; i < 4; i++) Wall(i).gameObject.SetActive(false);
            var before = SnapshotWalls(); Material concrete = Concrete(); var materialBefore = new MaterialSnapshot(concrete);
            var owner = Create(); Material brick = Brick(owner); Texture[] textures = Maps.Select(brick.GetTexture).ToArray();
            Mesh[] meshes = owner.Root.GetComponentsInChildren<MeshFilter>(true).Select(x => x.sharedMesh).ToArray();
            survivors = new GameObject("Surviving original walls");
            foreach (var snapshot in before) snapshot.Source.SetParent(survivors.transform, false);
            Object.Destroy(arena); yield return null; yield return null;
            Assert.That(owner == null, Is.True); Assert.That(brick == null, Is.True); Assert.That(OwnedMaterialCount(), Is.EqualTo(baseline));
            foreach (var snapshot in before) snapshot.AssertUnchanged(snapshot.Enabled);
            foreach (Mesh mesh in meshes) Assert.That(mesh == null, Is.False);
            foreach (Texture texture in textures) Assert.That(texture == null, Is.False);
            materialBefore.AssertUnchanged(concrete);
        }

        [UnityTest]
        public IEnumerator DestroyingOwnerComponentAlsoRemovesVisualRootAndRestoresSources()
        {
            var before = SnapshotWalls(); var owner = Create(); GameObject visual = owner.Root.gameObject; Material brick = Brick(owner);
            Object.Destroy(owner); yield return null; yield return null;
            Assert.That(owner == null, Is.True); Assert.That(visual == null, Is.True); Assert.That(brick == null, Is.True);
            foreach (var snapshot in before) snapshot.AssertUnchanged(snapshot.Enabled);
            Assert.That(arena.transform.childCount, Is.EqualTo(4));
        }

        [Test]
        public void NullOrInactiveParentAndNullMaterialAreRejectedBeforeAllocation()
        {
            Material concrete = Concrete(); int baseline = OwnedMaterialCount();
            Assert.Throws<ArgumentNullException>(() => DepotBrickPerimeter.Create(null, concrete));
            Assert.Throws<ArgumentNullException>(() => DepotBrickPerimeter.Create(arena.transform, null));
            arena.SetActive(false);
            Assert.Throws<InvalidOperationException>(() => DepotBrickPerimeter.Create(arena.transform, concrete));
            var child = new GameObject("Active child of inactive arena"); child.transform.SetParent(arena.transform, false);
            Assert.Throws<InvalidOperationException>(() => DepotBrickPerimeter.Create(child.transform, concrete));
            Assert.That(child.transform.childCount, Is.Zero); Assert.That(arena.transform.childCount, Is.EqualTo(5));
            Assert.That(arena.GetComponentsInChildren<DepotBrickPerimeter>(true), Is.Empty); Assert.That(OwnedMaterialCount(), Is.EqualTo(baseline));
        }

        [Test]
        public void WorldTransformedReflectedCollapsedAndInheritedParentsAreRejectedBeforeAllocation()
        {
            Material concrete = Concrete(); int baseline = OwnedMaterialCount();
            arena.transform.position = Vector3.right; Reject(arena.transform); arena.transform.position = Vector3.zero;
            arena.transform.rotation = Quaternion.Euler(0, 20, 0); Reject(arena.transform); arena.transform.rotation = Quaternion.identity;
            foreach (Vector3 scale in new[] { new Vector3(2, 1, 1), new Vector3(-1, 1, 1), new Vector3(0, 1, 1) })
            { arena.transform.localScale = scale; Reject(arena.transform); }
            arena.transform.localScale = Vector3.one;
            var child = new GameObject("Locally identity arena"); child.transform.SetParent(arena.transform, false);
            arena.transform.position = Vector3.forward; Reject(child.transform); Assert.That(child.transform.childCount, Is.Zero);
            void Reject(Transform target)
            {
                Assert.Throws<ArgumentException>(() => DepotBrickPerimeter.Create(target, concrete));
                Assert.That(arena.GetComponentsInChildren<DepotBrickPerimeter>(true), Is.Empty); Assert.That(OwnedMaterialCount(), Is.EqualTo(baseline));
            }
        }

        [Test]
        public void IdentityGuardRejectsEveryNonFiniteMatrixEntryAndShearWithoutSettingInvalidUnityTransforms()
        {
            MethodInfo guard = typeof(DepotBrickPerimeter).GetMethod("FiniteIdentityMatrix", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(guard, Is.Not.Null); Assert.That((bool)guard.Invoke(null, new object[] { Matrix4x4.identity }), Is.True);
            // Unity can reject NaN transform assignments itself. Exercise the exact production guard with pure matrix data instead.
            for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++)
                foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    Matrix4x4 matrix = Matrix4x4.identity; matrix[row, column] = value;
                    Assert.That((bool)guard.Invoke(null, new object[] { matrix }), Is.False);
                }
            Matrix4x4 shear = Matrix4x4.identity; shear[0, 1] = .01f;
            Assert.That((bool)guard.Invoke(null, new object[] { shear }), Is.False);
        }

        [Test]
        public void GraphicsDeviceContractExercisesCurrentPlatformWithoutChangingDeviceSettings()
        {
            Material concrete = Concrete(); int baseline = OwnedMaterialCount(); var before = SnapshotWalls();
            // A rendered run covers the rendered branch, not headless execution. Neither branch is deliberately skipped here.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Throws<InvalidOperationException>(() => DepotBrickPerimeter.Create(arena.transform, concrete));
                Assert.That(arena.transform.childCount, Is.EqualTo(4)); Assert.That(OwnedMaterialCount(), Is.EqualTo(baseline));
                foreach (var snapshot in before) snapshot.AssertUnchanged(snapshot.Enabled);
            }
            else
            {
                perimeter = DepotBrickPerimeter.Create(arena.transform, concrete);
                Assert.That(perimeter.Root.childCount, Is.EqualTo(4)); Assert.That(OwnedMaterialCount(), Is.EqualTo(baseline + 1));
                Assert.That(perimeter.Root.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(12));
            }
        }

        [UnityTest]
        public IEnumerator MissingFourthWallRollsBackEarlierCompletedStripsWithoutActiveDuplicates()
        {
            RequireGraphics(); int baseline = OwnedMaterialCount(); var before = SnapshotWalls(); Wall(3).name = "Missing east wall";
            Assert.Throws<InvalidOperationException>(() => DepotBrickPerimeter.Create(arena.transform, Concrete()));
            AssertNoActiveReplacement(); foreach (var snapshot in before) snapshot.AssertUnchanged(snapshot.Enabled);
            yield return null; yield return null;
            Assert.That(arena.transform.childCount, Is.EqualTo(4)); Assert.That(OwnedMaterialCount(), Is.EqualTo(baseline));
        }

        [UnityTest]
        public IEnumerator LateFourthWallEnvelopeFailureRollsBackHiddenAndInactiveOriginalsExactly()
        {
            RequireGraphics(); int baseline = OwnedMaterialCount(); Wall(1).gameObject.SetActive(false);
            Wall(3).GetComponent<BoxCollider>().center = Vector3.right; var before = SnapshotWalls();
            Assert.Throws<ArgumentException>(() => DepotBrickPerimeter.Create(arena.transform, Concrete()));
            AssertNoActiveReplacement(); foreach (var snapshot in before) snapshot.AssertUnchanged(snapshot.Enabled);
            yield return null; yield return null;
            Assert.That(arena.transform.childCount, Is.EqualTo(4)); Assert.That(OwnedMaterialCount(), Is.EqualTo(baseline));
            Assert.That(arena.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator ChangedSourcePoseScaleColliderSizeOrMissingMeshNeverGetsSilentlyFitted()
        {
            RequireGraphics(); int baseline = OwnedMaterialCount(); Transform source = Wall(3);
            Vector3 position = source.localPosition, scale = source.localScale; var collider = source.GetComponent<BoxCollider>();
            var filter = source.GetComponent<MeshFilter>(); Mesh shared = filter.sharedMesh;
            for (int trial = 0; trial < 5; trial++)
            {
                if (trial == 0) source.localPosition += Vector3.right * .01f;
                if (trial == 1) source.localRotation = Quaternion.Euler(0, 1, 0);
                if (trial == 2) source.localScale = new Vector3(.51f, 4, 32);
                if (trial == 3) collider.size = new Vector3(1, 1, .9f);
                if (trial == 4) filter.sharedMesh = null;
                if (trial < 4) Assert.Throws<ArgumentException>(() => DepotBrickPerimeter.Create(arena.transform, Concrete()));
                else Assert.Throws<InvalidOperationException>(() => DepotBrickPerimeter.Create(arena.transform, Concrete()));
                AssertNoActiveReplacement();
                for (int wall = 0; wall < 4; wall++) Assert.That(Wall(wall).GetComponent<Renderer>().enabled, Is.EqualTo(wall != 3));
                source.localPosition = position; source.localRotation = Quaternion.identity; source.localScale = scale;
                collider.size = Vector3.one; filter.sharedMesh = shared;
                yield return null; yield return null;
                Assert.That(arena.transform.childCount, Is.EqualTo(4)); Assert.That(OwnedMaterialCount(), Is.EqualTo(baseline));
            }
        }

        [Test]
        public void ASecondLiveOwnerIsRejectedWithoutHidingOrRecapturingOriginalVisibility()
        {
            var before = SnapshotWalls(); var owner = Create(); Material brick = Brick(owner); int baseline = OwnedMaterialCount();
            Assert.Throws<InvalidOperationException>(() => DepotBrickPerimeter.Create(arena.transform, Concrete()));
            Assert.That(arena.GetComponentsInChildren<DepotBrickPerimeter>(true), Is.EqualTo(new[] { owner }));
            Assert.That(OwnedMaterialCount(), Is.EqualTo(baseline)); Assert.That(Brick(owner), Is.SameAs(brick));
            owner.Dispose(); foreach (var snapshot in before) snapshot.AssertUnchanged(snapshot.Enabled);
        }

        [Test]
        public void MissingAnchoredPbrKeywordIsRejectedBeforeOwnedAllocationWithoutChangingTemplate()
        {
            RequireGraphics(); Material concrete = Concrete(); var before = new MaterialSnapshot(concrete);
            temporaryMaterial = new Material(concrete); temporaryMaterial.DisableKeyword("_NORMALMAP"); int baseline = OwnedMaterialCount();
            Assert.Throws<InvalidOperationException>(() => DepotBrickPerimeter.Create(arena.transform, temporaryMaterial));
            Assert.That(arena.transform.childCount, Is.EqualTo(4)); Assert.That(OwnedMaterialCount(), Is.EqualTo(baseline));
            Assert.That(temporaryMaterial.IsKeywordEnabled("_NORMALMAP"), Is.False); before.AssertUnchanged(concrete);
        }

        private void AssertNoActiveReplacement()
        {
            Assert.That(arena.GetComponentsInChildren<DepotBrickPerimeter>(), Is.Empty);
            foreach (var owner in arena.GetComponentsInChildren<DepotBrickPerimeter>(true))
            { Assert.That(owner.gameObject.activeSelf, Is.False); Assert.That(owner.Root, Is.Null); }
        }
        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(.001f)); Assert.That(actual.y, Is.EqualTo(expected.y).Within(.001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(.001f));
        }
        private static void AssertInside(Bounds actual, Bounds envelope)
        {
            Assert.That(actual.min.x, Is.GreaterThanOrEqualTo(envelope.min.x - .001f)); Assert.That(actual.max.x, Is.LessThanOrEqualTo(envelope.max.x + .001f));
            Assert.That(actual.min.y, Is.GreaterThanOrEqualTo(envelope.min.y - .001f)); Assert.That(actual.max.y, Is.LessThanOrEqualTo(envelope.max.y + .001f));
            Assert.That(actual.min.z, Is.GreaterThanOrEqualTo(envelope.min.z - .001f)); Assert.That(actual.max.z, Is.LessThanOrEqualTo(envelope.max.z + .001f));
        }

        private sealed class WallSnapshot
        {
            public readonly Transform Source;
            public readonly bool Enabled;
            private readonly Mesh mesh;
            private readonly Material[] materials;
            private readonly Renderer renderer;
            private readonly BoxCollider collider;
            private readonly Vector3 position, scale, center, size;
            private readonly Quaternion rotation;
            private readonly bool active, colliderEnabled, trigger, receiveShadows, forceOff;
            private readonly int layer;
            private readonly ShadowCastingMode shadows;
            public WallSnapshot(Transform source)
            {
                Source = source; renderer = source.GetComponent<Renderer>(); collider = source.GetComponent<BoxCollider>();
                mesh = source.GetComponent<MeshFilter>().sharedMesh; materials = renderer.sharedMaterials; Enabled = renderer.enabled;
                position = source.localPosition; scale = source.localScale; rotation = source.localRotation;
                center = collider.center; size = collider.size; colliderEnabled = collider.enabled; trigger = collider.isTrigger;
                active = source.gameObject.activeSelf; layer = source.gameObject.layer; shadows = renderer.shadowCastingMode;
                receiveShadows = renderer.receiveShadows; forceOff = renderer.forceRenderingOff;
            }
            public void AssertUnchanged(bool expectedEnabled)
            {
                Assert.That(Source == null, Is.False); Assert.That(Source.GetComponent<Renderer>(), Is.SameAs(renderer));
                Assert.That(Source.GetComponent<BoxCollider>(), Is.SameAs(collider)); Assert.That(Source.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                Assert.That(renderer.sharedMaterials, Is.EqualTo(materials)); Assert.That(renderer.enabled, Is.EqualTo(expectedEnabled));
                Assert.That(Source.localPosition, Is.EqualTo(position)); Assert.That(Source.localRotation, Is.EqualTo(rotation)); Assert.That(Source.localScale, Is.EqualTo(scale));
                Assert.That(collider.center, Is.EqualTo(center)); Assert.That(collider.size, Is.EqualTo(size)); Assert.That(collider.enabled, Is.EqualTo(colliderEnabled));
                Assert.That(collider.isTrigger, Is.EqualTo(trigger)); Assert.That(Source.gameObject.activeSelf, Is.EqualTo(active)); Assert.That(Source.gameObject.layer, Is.EqualTo(layer));
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(shadows)); Assert.That(renderer.receiveShadows, Is.EqualTo(receiveShadows));
                Assert.That(renderer.forceRenderingOff, Is.EqualTo(forceOff));
            }
        }

        private sealed class MaterialSnapshot
        {
            private static readonly string[] Floats = { "_Metallic", "_Smoothness", "_BumpScale", "_SmoothnessTextureChannel", "_WorkflowMode", "_Surface", "_AlphaClip" };
            private readonly Shader shader;
            private readonly string name, tag;
            private readonly string[] keywords;
            private readonly Texture[] textures;
            private readonly Vector2[] scales, offsets;
            private readonly float[] values;
            private readonly Color color;
            private readonly int queue;
            private readonly bool instancing, doubleSided;
            private readonly MaterialGlobalIlluminationFlags gi;
            public MaterialSnapshot(Material material)
            {
                shader = material.shader; name = material.name; tag = material.GetTag("RenderType", false); keywords = material.shaderKeywords;
                textures = Maps.Select(material.GetTexture).ToArray(); scales = Maps.Select(material.GetTextureScale).ToArray(); offsets = Maps.Select(material.GetTextureOffset).ToArray();
                values = Floats.Select(material.GetFloat).ToArray(); color = material.GetColor("_BaseColor"); queue = material.renderQueue;
                instancing = material.enableInstancing; doubleSided = material.doubleSidedGI; gi = material.globalIlluminationFlags;
            }
            public void AssertUnchanged(Material material)
            {
                Assert.That(material == null, Is.False); Assert.That(material.shader, Is.SameAs(shader)); Assert.That(material.name, Is.EqualTo(name));
                Assert.That(material.GetTag("RenderType", false), Is.EqualTo(tag)); Assert.That(material.shaderKeywords, Is.EqualTo(keywords));
                Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(color)); Assert.That(material.renderQueue, Is.EqualTo(queue));
                Assert.That(material.enableInstancing, Is.EqualTo(instancing)); Assert.That(material.doubleSidedGI, Is.EqualTo(doubleSided));
                Assert.That(material.globalIlluminationFlags, Is.EqualTo(gi));
                for (int i = 0; i < Maps.Length; i++)
                {
                    Assert.That(textures[i] == null, Is.False); Assert.That(material.GetTexture(Maps[i]), Is.SameAs(textures[i]));
                    Assert.That(material.GetTextureScale(Maps[i]), Is.EqualTo(scales[i])); Assert.That(material.GetTextureOffset(Maps[i]), Is.EqualTo(offsets[i]));
                }
                for (int i = 0; i < Floats.Length; i++) Assert.That(material.GetFloat(Floats[i]), Is.EqualTo(values[i]));
            }
        }
    }
}
