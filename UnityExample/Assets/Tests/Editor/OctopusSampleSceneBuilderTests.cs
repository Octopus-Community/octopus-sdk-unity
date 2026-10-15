using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class OctopusSampleSceneBuilderTests
{
    [Test]
    public void GeneratedEntrySceneContainsOnlyTheThreeCodeBuiltHosts()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var builder = Type.GetType("OctopusSampleSceneBuilder, Assembly-CSharp-Editor");
            Assert.IsNotNull(builder);
            builder.GetMethod("Build").Invoke(null, null);
            Assert.AreEqual("Assets/OctopusSample/OctopusSample.unity", EditorBuildSettings.scenes[0].path);
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            Assert.AreEqual(3, roots.Length);
            CollectionAssert.AreEquivalent(new[] { "Main Camera", "EventSystem", "OctopusSampleShell" },
                Array.ConvertAll(roots, item => item.name));
        }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
