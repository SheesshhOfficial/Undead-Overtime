using System;
using System.IO;
using NUnit.Framework;
using UndeadOvertime.LanPrototype.Editor;
using UnityEditor.Build;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class PrivateContentBuildGuardTests
    {
        private string root;
        private string assets;
        private string privateRoot;

        [SetUp]
        public void CreateSyntheticDirectory()
        {
            root = Path.Combine(Path.GetTempPath(), "UndeadOvertimePrivateGuardTests-" + Guid.NewGuid().ToString("N"));
            assets = Path.Combine(root, "Assets");
            privateRoot = Path.Combine(assets, PrivateContentBuildGuard.PrivateFolderName);
            Directory.CreateDirectory(assets);
        }

        [TearDown]
        public void RemoveSyntheticDirectory()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void GuardParticipatesInNormalPlayerBuildPreprocessing()
        {
            var guard = new PrivateContentBuildGuard();
            Assert.That(guard, Is.InstanceOf<IPreprocessBuildWithReport>());
            Assert.That(guard.callbackOrder, Is.EqualTo(int.MinValue));
        }

        [Test]
        public void CleanPublicAssetsAreAllowed()
        {
            Directory.CreateDirectory(Path.Combine(assets, "UndeadOvertime"));
            File.WriteAllText(Path.Combine(assets, "public-marker.txt"), "synthetic");
            Assert.DoesNotThrow(() => PrivateContentBuildGuard.ValidateAssetsDirectoryForPlayerBuild(assets));
        }

        [Test]
        public void EmptyPrivateDirectoryAndItsSiblingMetaAreAllowed()
        {
            Directory.CreateDirectory(privateRoot);
            File.WriteAllText(privateRoot + ".meta", "synthetic folder metadata");
            Assert.DoesNotThrow(() => PrivateContentBuildGuard.ValidateAssetsDirectoryForPlayerBuild(assets));
        }

        [TestCase("synthetic.txt")]
        [TestCase("Resources/synthetic.txt")]
        [TestCase("StreamingAssets/synthetic.txt")]
        [TestCase("Editor/synthetic.txt")]
        [TestCase("synthetic.txt.meta")]
        public void AnyPrivateContentBlocksPlayerBuildWithoutModifyingIt(string relative)
        {
            string path = Path.Combine(privateRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "synthetic marker only");
            var failure = Assert.Throws<BuildFailedException>(() => PrivateContentBuildGuard.ValidateAssetsDirectoryForPlayerBuild(assets));
            Assert.That(failure.Message, Does.Contain("Player build blocked"));
            Assert.That(File.ReadAllText(path), Is.EqualTo("synthetic marker only"));
        }

        [Test]
        public void NestedEmptyDirectoryAlsoBlocksPlayerBuild()
        {
            Directory.CreateDirectory(Path.Combine(privateRoot, "EmptyTrial"));
            Assert.Throws<BuildFailedException>(() => PrivateContentBuildGuard.ValidateAssetsDirectoryForPlayerBuild(assets));
        }

        [Test]
        public void CaseVariantPrivateDirectoryCannotEvadeGuard()
        {
            string variant = Path.Combine(assets, "undeadOvertimePRIVATE");
            Directory.CreateDirectory(variant);
            File.WriteAllText(Path.Combine(variant, "synthetic.txt"), "synthetic");
            Assert.Throws<BuildFailedException>(() => PrivateContentBuildGuard.ValidateAssetsDirectoryForPlayerBuild(assets));
        }

        [Test]
        public void FileOccupyingPrivateRootBlocksPlayerBuild()
        {
            File.WriteAllText(privateRoot, "synthetic");
            Assert.Throws<BuildFailedException>(() => PrivateContentBuildGuard.ValidateAssetsDirectoryForPlayerBuild(assets));
        }

        [Test]
        public void MissingAssetsDirectoryFailsClosed()
        {
            Assert.Throws<BuildFailedException>(() => PrivateContentBuildGuard.ValidateAssetsDirectoryForPlayerBuild(Path.Combine(root, "missing")));
        }
    }
}
