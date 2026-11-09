using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class InfectedDamageTintTests
    {
        private GameObject first, second;
        private Material material;
        private int colorId;
        [SetUp]
        public void Setup()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Requires graphics-enabled Unity; headless cannot prove renderer property-block behavior.");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null);
            material = new Material(shader);
            colorId = Shader.PropertyToID(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color");
            material.SetColor(colorId, new Color(.35f, .55f, .15f, .8f));
            first = GameObject.CreatePrimitive(PrimitiveType.Capsule); second = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            first.GetComponent<Renderer>().sharedMaterial = second.GetComponent<Renderer>().sharedMaterial = material;
        }
        [TearDown]
        public void Teardown()
        { if (first != null) Object.DestroyImmediate(first); if (second != null) Object.DestroyImmediate(second); if (material != null) Object.DestroyImmediate(material); }
        [Test]
        public void TintIsPerRendererAndNeverMutatesSharedMaterialPhysicsOrPose()
        {
            Renderer a = first.GetComponent<Renderer>(), b = second.GetComponent<Renderer>();
            var helper = new InfectedDamageTint(a); var block = new MaterialPropertyBlock();
            Color before = material.GetColor(colorId); Vector3 position = first.transform.position, scale = first.transform.localScale;
            Quaternion rotation = first.transform.rotation; Collider collider = first.GetComponent<Collider>();
            helper.Apply(1); a.GetPropertyBlock(block);
            Assert.That(block.GetColor(colorId).r, Is.GreaterThan(before.r)); Assert.That(block.GetColor(colorId).a, Is.EqualTo(before.a));
            b.GetPropertyBlock(block); Assert.That(block.isEmpty, Is.True);
            Assert.That(material.GetColor(colorId), Is.EqualTo(before)); Assert.That(a.sharedMaterial, Is.SameAs(material));
            Assert.That(first.transform.position, Is.EqualTo(position)); Assert.That(first.transform.localScale, Is.EqualTo(scale));
            Assert.That(first.transform.rotation, Is.EqualTo(rotation)); Assert.That(first.GetComponent<Collider>(), Is.SameAs(collider));
            Assert.That(collider.enabled && first.activeSelf, Is.True);
            helper.Reset(); a.GetPropertyBlock(block); Assert.That(block.isEmpty, Is.True);
        }
        [Test]
        public void OriginalOverridesAndUnrelatedPropertiesSurvivePulseAndReset()
        {
            Renderer target = first.GetComponent<Renderer>(); var block = new MaterialPropertyBlock();
            Color initial = new Color(.2f, .3f, .4f, .6f); int sentinel = Shader.PropertyToID("_TestSentinel");
            block.SetColor(colorId, initial); block.SetFloat(sentinel, 42); target.SetPropertyBlock(block);
            var helper = new InfectedDamageTint(target); helper.Apply(1); target.GetPropertyBlock(block);
            Assert.That(block.GetFloat(sentinel), Is.EqualTo(42)); Assert.That(block.GetColor(colorId).a, Is.EqualTo(initial.a));
            helper.Reset(); target.GetPropertyBlock(block);
            Assert.That(block.GetColor(colorId), Is.EqualTo(initial)); Assert.That(block.GetFloat(sentinel), Is.EqualTo(42));
            helper.Apply(.5); helper.Reset(); target.GetPropertyBlock(block); Assert.That(block.GetColor(colorId), Is.EqualTo(initial));
        }
        [Test]
        public void InvalidAndNegativePulseRestoreBaselineAndDestroyedRendererIsSafe()
        {
            Renderer target = first.GetComponent<Renderer>(); var helper = new InfectedDamageTint(target);
            var block = new MaterialPropertyBlock();
            foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d })
            { helper.Apply(1); helper.Apply(invalid); target.GetPropertyBlock(block); Assert.That(block.isEmpty, Is.True); }
            helper.Apply(double.MaxValue); target.GetPropertyBlock(block);
            Assert.That(float.IsNaN(block.GetColor(colorId).r) || float.IsInfinity(block.GetColor(colorId).r), Is.False);
            Object.DestroyImmediate(first); Assert.DoesNotThrow(() => helper.Reset());
        }
    }
}
