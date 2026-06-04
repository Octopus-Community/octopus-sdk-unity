#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Builds the OpenScreen example scene (the prefilled-post test UI), wires its
// buttons, registers it in the build settings, and adds an entry to the MainMenu
// scene. Run once from "Octopus > Examples > Build OpenScreen Example Scene".
//
// Re-running is safe: it rebuilds the scene from scratch and skips the
// build-settings / MainMenu entries if they already exist.
public static class OpenScreenExampleSceneBuilder
{
    const string SceneDir = "Assets/OpenScreenExample";
    const string ScenePath = SceneDir + "/OpenScreenExample.unity";
    const string MainMenuPath = "Assets/MainMenu/MainMenu.unity";
    const string MenuLabel = "OpenScreen Example";
    const string MainMenuMethod = "OpenOpenScreenExample";

    static Font UIFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    [MenuItem("Octopus/Examples/Build OpenScreen Example Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Canvas (screen-space overlay, scales with a portrait reference resolution).
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        // EventSystem with the New Input System UI module (project uses it exclusively).
        var esGo = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        var module = esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        module.AssignDefaultActions(); // AddComponent does not run Reset(), so wire the default UI actions ourselves.
#else
        esGo.AddComponent<StandaloneInputModule>();
#endif

        // The example controller that the buttons drive.
        var controller = new GameObject("OpenScreenExample", typeof(OpenScreenExample)).GetComponent<OpenScreenExample>();

        CreateText(canvasGo.transform, "Title", "Open Screen Example", new Vector2(0, 640), 48, FontStyle.Bold);

        CreateButton(canvasGo.transform, "OpenCreatePostButton", "Open Create Post (prefilled)",
            new Vector2(0, 90), controller.OnOpenCreatePostClicked);
        CreateButton(canvasGo.transform, "OpenPostButton", "Open Post",
            new Vector2(0, -90), controller.OnOpenPostClicked);

        if (!AssetDatabase.IsValidFolder(SceneDir))
            AssetDatabase.CreateFolder("Assets", "OpenScreenExample");

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings();
        AddMainMenuButton();

        Debug.Log("OpenScreen example scene built at " + ScenePath +
                  ". Open MainMenu and press Play, then tap \"" + MenuLabel + "\".");
    }

    static void CreateText(Transform parent, string name, string content, Vector2 pos, int size, FontStyle style)
    {
        var go = DefaultControls.CreateText(new DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900, 120);
        rt.anchoredPosition = pos;

        var text = go.GetComponent<Text>();
        text.text = content;
        text.font = UIFont;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
    }

    static void CreateButton(Transform parent, string name, string label, Vector2 pos, UnityAction onClick)
    {
        var go = DefaultControls.CreateButton(new DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(680, 130);
        rt.anchoredPosition = pos;

        var text = go.GetComponentInChildren<Text>(true);
        text.text = label;
        text.font = UIFont;
        text.fontSize = 32;

        UnityEventTools.AddPersistentListener(go.GetComponent<Button>().onClick, onClick);
    }

    static void AddToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == ScenePath))
            return;
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // Adds a MainMenu button by cloning an existing menu button (so it inherits the
    // layout and styling) and rewiring it to MainMenu.OpenOpenScreenExample.
    static void AddMainMenuButton()
    {
        var menuScene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);

        MainMenu mainMenu = null;
        var menuButtons = new List<Button>();
        foreach (var root in menuScene.GetRootGameObjects())
        {
            if (mainMenu == null)
                mainMenu = root.GetComponentInChildren<MainMenu>(true);
            menuButtons.AddRange(root.GetComponentsInChildren<Button>(true));
        }

        if (mainMenu == null)
        {
            Debug.LogWarning("MainMenu component not found in " + MainMenuPath +
                             ". Add a button calling " + MainMenuMethod + "() manually.");
            return;
        }

        // Only buttons that already drive the MainMenu component are valid templates.
        var templates = new List<Button>();
        foreach (var b in menuButtons)
        {
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
            {
                if (b.onClick.GetPersistentTarget(i) != mainMenu)
                    continue;
                if (b.onClick.GetPersistentMethodName(i) == MainMenuMethod)
                {
                    Debug.Log("MainMenu already has an OpenScreen entry — skipping.");
                    EditorSceneManager.SaveScene(menuScene);
                    return;
                }
                templates.Add(b);
                break;
            }
        }

        if (templates.Count == 0)
        {
            Debug.LogWarning("No MainMenu buttons found to clone. Add a button calling " +
                             MainMenuMethod + "() manually.");
            return;
        }

        // Bottom-most button = lowest anchored Y; clone it and place one row below.
        templates.Sort((a, b) => RectY(a).CompareTo(RectY(b)));
        var template = templates[0];
        float newY = RectY(template) - RowGap(templates);

        var clone = Object.Instantiate(template.gameObject, template.transform.parent);
        clone.name = "OpenScreenExample";
        var cloneRt = (RectTransform)clone.transform;
        var templateRt = (RectTransform)template.transform;
        cloneRt.anchoredPosition = new Vector2(templateRt.anchoredPosition.x, newY);

        var cloneText = clone.GetComponentInChildren<Text>(true);
        if (cloneText != null)
            cloneText.text = MenuLabel;

        var cloneButton = clone.GetComponent<Button>();
        while (cloneButton.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(cloneButton.onClick, 0);
        UnityEventTools.AddPersistentListener(cloneButton.onClick, mainMenu.OpenOpenScreenExample);

        EditorSceneManager.MarkSceneDirty(menuScene);
        EditorSceneManager.SaveScene(menuScene);
        Debug.Log("Added \"" + MenuLabel + "\" button to MainMenu.");
    }

    static float RectY(Button b) => ((RectTransform)b.transform).anchoredPosition.y;

    // Spacing between rows: gap between the two lowest buttons, or a default if only one.
    static float RowGap(List<Button> sortedAscByY)
    {
        if (sortedAscByY.Count >= 2)
            return Mathf.Abs(RectY(sortedAscByY[1]) - RectY(sortedAscByY[0]));
        return 90f;
    }
}
#endif
