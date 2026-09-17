#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Builds `Assets/OctopusSample/OctopusSample.unity` — the sample's entry scene, hosting
/// <see cref="OctopusSampleShell"/> — and registers it first in the build settings so the app
/// launches into the shell.
///
/// The scene holds three objects and nothing else: a camera, an EventSystem, and the shell
/// component that builds every pixel above it in code. That is the whole point of generating it
/// rather than hand-editing it in the Editor — the reviewable artefact is this file, not 400 lines
/// of serialized YAML, and the scene can be regenerated from scratch at any time. Same precedent as
/// <c>OpenScreenExampleSceneBuilder</c>, which built the OpenScreen example the same way.
///
/// Re-running is safe and idempotent: the scene is rebuilt from scratch, and the build-settings
/// entry is moved to the front rather than duplicated.
///
/// Batchmode, for CI or a scripted regeneration:
/// <code>
/// Unity -batchmode -quit -projectPath UnityExample \
///       -executeMethod OctopusSampleSceneBuilder.Build -logFile -
/// </code>
/// </summary>
public static class OctopusSampleSceneBuilder
{
    private const string SceneDir = "Assets/OctopusSample";
    private const string ScenePath = SceneDir + "/OctopusSample.unity";

    [MenuItem("Octopus/Sample/Build Sample Shell Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // EmptyScene, not DefaultGameObjects: the default set brings a directional light this scene
        // has nothing to light, and a reviewer then has to work out whether it matters.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // A screen-space-overlay canvas needs no camera to render, but a scene with none logs
        // "No cameras rendering" every frame. Solid black so nothing shows through the shell.
        var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.tag = "MainCamera";

        // The EventSystem belongs to the scene, not to the screens: the project is
        // Input-System-only (ProjectSettings activeInputHandler: 1), so the legacy
        // StandaloneInputModule that a hand-rolled EventSystem would fall back to throws every
        // frame. Every code-built screen in this sample documents the same dependency.
        var events = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        var module = events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        // AddComponent does not run Reset(), so the default UI actions are wired by hand.
        module.AssignDefaultActions();
#else
        events.AddComponent<StandaloneInputModule>();
#endif

        new GameObject("OctopusSampleShell", typeof(OctopusSampleShell));

        if (!AssetDatabase.IsValidFolder(SceneDir))
        {
            AssetDatabase.CreateFolder("Assets", "OctopusSample");
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        PutFirstInBuildSettings();

        Debug.Log("Sample shell scene built at " + ScenePath + " and registered as scene 0.");
    }

    /// <summary>
    /// Makes the shell scene the first entry, moving it rather than adding a duplicate. Everything
    /// else keeps its order: the eight legacy `*Example` scenes stay reachable from the scenario
    /// rows until the PR that rebuilds each of them deletes it (issue #100).
    /// </summary>
    private static void PutFirstInBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
