using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UndeadOvertime.Prototype.Tests
{
    public sealed class PrototypeSceneTests
    {
        [TestCase("Assets/UndeadOvertime/Scenes/PrototypeRange.unity", false)]
        [TestCase("Assets/UndeadOvertime/Scenes/SurvivalYard.unity", true)]
        public void DedicatedSceneHasOneBootstrapAndAssignedCamera(string scenePath, bool survival)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Not.Null);
            Scene scene = EditorSceneManager.OpenPreviewScene(scenePath);
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
                Assert.That(found.IsSurvivalMode, Is.EqualTo(survival));
                Assert.That(found.IsBuilt, Is.False, "Opening the scene must not generate or edit objects in Edit Mode.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}

