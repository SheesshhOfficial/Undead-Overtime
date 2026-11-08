using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class LanSceneTests
    {
        [Test]
        public void SceneContainsOneAssignedBootstrapAndDoesNotRunInEditMode()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/UndeadOvertime/Scenes/LanRange.unity");
            try
            {
                int count = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject), Is.Zero);
                    foreach (LanRange range in root.GetComponentsInChildren<LanRange>(true))
                    { count++; Assert.That(range.WorldCamera, Is.Not.Null); Assert.That(range.IsBuilt, Is.False); }
                }
                Assert.That(count, Is.EqualTo(1));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
