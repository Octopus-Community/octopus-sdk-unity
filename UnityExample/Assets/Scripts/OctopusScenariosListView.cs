using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Renders <see cref="OctopusScenarioCatalog"/> as a scrollable list, on top of whatever scene is
/// loaded, and takes itself down again on Close.
///
/// Built entirely in code, with no prefab and no scene wiring. That is deliberate: this sample is
/// scene-based, the menu's buttons live inside `MainMenu.unity`, and a list of 26 rows that has to
/// track a catalogue in another repo is the last thing that should be maintained as serialized
/// YAML. Anything that only exists in code can be diffed and reviewed.
///
/// This is the QA surface Android, Flutter and React Native already have, at its first step: the
/// rows are readable and countable, none of them is implemented yet. The Unity implementation of
/// the scenarios themselves is the next piece of work, not this one. (iOS is not owned here, and
/// the catalogue marks every iOS row unverified.)
/// </summary>
public class OctopusScenariosListView : MonoBehaviour
{
    private static readonly Color Background = new Color(0.08f, 0.08f, 0.08f, 0.97f);
    private static readonly Color RowBackground = new Color(0.16f, 0.16f, 0.16f, 1f);
    private static readonly Color TitleColor = Color.white;

    // The content opacity floor posed by pm-tools `shared/design/samples/TOKENS.md`: any text
    // that carries meaning stays at or above it, never below.
    private const float ContentAlphaFloor = 0.74f;

    // The platform slot hue, dark half — the same value the other samples give this platform.
    // TOKENS.md reserves it for identity, so it dresses the screen title and nothing else: a
    // status is a semantic element, and tinting one with the slot hue is what makes two samples
    // look like two different products.
    private static readonly Color PlatformSlot = new Color(0.471f, 0.780f, 0.482f);
    private static readonly Color Muted = new Color(1f, 1f, 1f, ContentAlphaFloor);

    // A gap to close reads as one; a permanent absence reads as settled. Not the other way round.
    private static readonly Color NotImplemented = new Color(0.98f, 0.75f, 0.35f, 1f);

    private static Font _font;

    private static Font UiFont =>
        _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

    /// <summary>Opens the list over the current scene. Safe to call twice — the second is a no-op.</summary>
    public static void Open()
    {
        if (FindAnyObjectByType<OctopusScenariosListView>() != null) return;
        new GameObject("OctopusScenariosListView").AddComponent<OctopusScenariosListView>();
    }

    /// <summary>
    /// Adds the floating entry button that opens the list, on its own canvas above the menu's.
    /// Called from the menu scene rather than serialized into it — see MainMenu.Start.
    /// </summary>
    public static void InstallEntryButton()
    {
        if (GameObject.Find("OctopusScenariosEntry") != null) return;

        var host = new GameObject("OctopusScenariosEntry");
        var canvas = host.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = host.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // 1080x1920, not MainMenu.unity's own 800x600: every size in this file is expressed in
        // that reference, and reference and sizes only mean anything together. The cost is that
        // this scales on a slightly different curve from the menu buttons beside it.
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        host.AddComponent<GraphicRaycaster>();

        var button = MakeButton("scenarios-tab", host.transform, "QA Scenarios", Open);
        button.anchorMin = button.anchorMax = button.pivot = new Vector2(1f, 0f);
        button.sizeDelta = new Vector2(320f, 96f);
        button.anchoredPosition = new Vector2(-40f, 40f);
    }

    private void Start()
    {
        // Deliberately NOT DontDestroyOnLoad: the overlay belongs to the menu scene, and loading
        // a demo scene from a row must leave that scene alone rather than draw this on top of it.
        Build();
    }

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the scene's own canvas, whatever sorting order it chose for itself.
        canvas.sortingOrder = 1000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // 1080x1920, not MainMenu.unity's own 800x600: every size in this file is expressed in
        // that reference, and reference and sizes only mean anything together. The cost is that
        // this scales on a slightly different curve from the menu buttons beside it.
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        // The scene supplies the EventSystem. One is deliberately NOT created here: the project
        // is Input-System-only (ProjectSettings activeInputHandler: 1), so the legacy
        // StandaloneInputModule a hand-rolled EventSystem would need throws every frame.
        if (EventSystem.current == null)
        {
            Debug.LogWarning("[Octopus] No EventSystem in this scene — the scenario list will " +
                             "render but not respond to taps.");
        }

        var root = Panel("Root", transform, Background);
        Stretch(root, Vector2.zero, Vector2.one);

        BuildHeader(root);
        BuildScroll(root);
    }

    private void BuildHeader(RectTransform root)
    {
        var header = Panel("Header", root, Color.clear);
        Stretch(header, new Vector2(0f, 1f), Vector2.one);
        header.sizeDelta = new Vector2(0f, 220f);
        header.anchoredPosition = new Vector2(0f, -110f);

        var title = Label("Title", header, "QA Scenarios", 46, PlatformSlot, TextAnchor.UpperLeft);
        // Left inset 70, right inset 260: clear of the Close button, and on the same left edge
        // as the subtitle under it.
        Stretch(title, new Vector2(0f, 1f), new Vector2(1f, 1f));
        title.sizeDelta = new Vector2(-330f, 60f);
        title.anchoredPosition = new Vector2(-95f, -50f);

        var notApplicable = 0;
        var pending = 0;
        foreach (var s in OctopusScenarioCatalog.All)
        {
            if (s.Status == ScenarioStatus.NotApplicable) notApplicable++;
            else pending++;
        }

        var subtitle = Label(
            "Subtitle", header,
            OctopusScenarioCatalog.All.Count + " scenarios in the shared catalogue · " + pending +
            " not implemented on Unity yet · " + notApplicable + " not applicable",
            26, Muted, TextAnchor.UpperLeft);
        Stretch(subtitle, new Vector2(0f, 1f), new Vector2(1f, 1f));
        subtitle.sizeDelta = new Vector2(-330f, 70f);
        subtitle.anchoredPosition = new Vector2(-95f, -125f);

        var close = MakeButton("Close", header, "Close", () => Destroy(gameObject));
        close.anchorMin = close.anchorMax = new Vector2(1f, 1f);
        close.pivot = new Vector2(1f, 1f);
        close.sizeDelta = new Vector2(180f, 80f);
        close.anchoredPosition = new Vector2(-40f, -30f);
    }

    private void BuildScroll(RectTransform root)
    {
        var viewport = Panel("Viewport", root, Color.clear);
        Stretch(viewport, Vector2.zero, Vector2.one);
        viewport.offsetMax = new Vector2(-40f, -220f);
        viewport.offsetMin = new Vector2(40f, 40f);
        viewport.gameObject.AddComponent<RectMask2D>();
        // Panel() derives raycastTarget from alpha, which is right everywhere but here: uGUI
        // walks UP from the graphic it hit, so a drag starting in the gap between two rows would
        // hit Root — the ScrollRect's parent, not a descendant — and scroll nothing.
        viewport.GetComponent<Image>().raycastTarget = true;

        var content = Panel("Content", viewport, Color.clear);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = Vector2.zero;

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 16f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 40f;

        foreach (var scenario in OctopusScenarioCatalog.All) BuildRow(content, scenario);
    }

    private void BuildRow(RectTransform parent, OctopusScenario scenario)
    {
        // Named with the id the QA pipeline taps, so the row is addressable the day a Unity
        // driver exists — Android, Flutter and RN already carry this id on their card.
        var row = Panel(scenario.CardTestId, parent, RowBackground);
        var rowLayout = row.gameObject.AddComponent<VerticalLayoutGroup>();
        rowLayout.padding = new RectOffset(24, 24, 20, 20);
        rowLayout.spacing = 8f;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childControlWidth = true;
        rowLayout.childForceExpandWidth = true;
        // No ContentSizeFitter here on purpose: Content's VerticalLayoutGroup has
        // childControlHeight, so it already sizes this row from the row's own layout group —
        // and Unity warns about a layout-group child that fits its own content.

        FlexibleLabel(row, scenario.Title, 32, TitleColor);
        FlexibleLabel(row, scenario.Capability, 22, Muted);

        if (scenario.Status == ScenarioStatus.NotApplicable)
        {
            // Not a gap to re-ask about: the concept has no Unity counterpart at all. Stated with
            // its reason so nobody re-opens it as an oversight.
            FlexibleLabel(row, "Not applicable on Unity — " + scenario.NotApplicableReason,
                          22, Muted);
        }
        else
        {
            FlexibleLabel(row, "Not implemented", 24, NotImplemented);
            if (scenario.DemoScene != null)
            {
                // An existing scene touching the same capability, offered as a shortcut. It is
                // explicitly NOT the scenario: it ships none of the catalogue's preset ids.
                var scene = scenario.DemoScene;
                FlexibleLabel(row, "Related demo scene (not the scenario): " + scene, 20, Muted);
                var open = MakeButton(scenario.Id + "-open", row, "Open " + scene,
                                  () => SceneManager.LoadScene(scene));
                open.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;
            }
        }
    }

    // --- small uGUI builders ------------------------------------------------------------------

    private static RectTransform Panel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = color.a > 0f;
        return rect;
    }

    private static RectTransform Label(string name, Transform parent, string text, int size,
                                       Color color, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        var label = go.GetComponent<Text>();
        label.font = UiFont;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = anchor;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        return rect;
    }

    private static void FlexibleLabel(Transform parent, string text, int size, Color color)
    {
        var rect = Label("Line", parent, text, size, color, TextAnchor.UpperLeft);
        // The row's ContentSizeFitter needs a real preferred height per line. Only flexibleWidth
        // is set here, deliberately: leaving preferredHeight unset lets Text report the height its
        // own wrapped content needs, instead of pinning every line to a guessed constant.
        rect.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
    }

    private static RectTransform MakeButton(string name, Transform parent, string text,
                                            UnityEngine.Events.UnityAction onClick)
    {
        var rect = Panel(name, parent, new Color(0.008f, 0.298f, 0.357f, 1f));
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(onClick);

        var label = Label("Label", rect, text, 26, Color.white, TextAnchor.MiddleCenter);
        Stretch(label, Vector2.zero, Vector2.one);
        return rect;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
