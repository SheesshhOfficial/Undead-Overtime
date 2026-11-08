using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UndeadOvertime.Prototype.Tests
{
    public sealed class PrototypeSceneTests
    {
        private const string ScenePath = "Assets/UndeadOvertime/Scenes/PrototypeRange.unity";

        [Test]
        public void DedicatedSceneHasOneBootstrapAndAssignedCamera()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null);
            Scene scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                PrototypeRange found = null;
                int count = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (PrototypeRange range in root.GetComponentsInChildren<PrototypeRange>(true))
                    {
                        found = range;
                        count++;
                    }
                    foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject), Is.Zero,
                            "Missing script on " + node.name);
                }
                Assert.That(count, Is.EqualTo(1));
                Assert.That(found.WorldCamera, Is.Not.Null);
                Assert.That(found.IsBuilt, Is.False, "Opening the scene must not generate or edit objects in Edit Mode.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
