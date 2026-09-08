using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Renders <see cref="OctopusScenarioCatalog"/> as a scrollable list, on top of whatever scene is
/// loaded, and takes itself down again on Close.
///
/// Built entirely in code, with no prefab and no scene wiring — through the shared
/// <see cref="SampleUi"/> builders, which this screen's own styles became. That is deliberate:
/// this sample is scene-based, the menu's buttons live inside `MainMenu.unity`, and a list of 27
/// rows that has to track a catalogue in another repo is the last thing that should be maintained
/// as serialized YAML. Anything that only exists in code can be diffed and reviewed.
///
/// A row whose scenario this sample drives (see <see cref="OctopusScenarioPilots"/>) opens the
/// generic <see cref="OctopusScenarioScreenView"/>. Every other row keeps the behaviour it had:
/// "Not implemented", plus a shortcut to a related demo scene where one exists. Two counts are
/// therefore shown side by side and mean different things — the catalogue's scenarios, and the
/// ones Unity has actually built. (iOS is not owned here, and the catalogue marks every iOS row
/// unverified.)
/// </summary>
public class OctopusScenariosListView : MonoBehaviour
{
    /// <summary>Below <see cref="OctopusScenarioScreenView"/>'s 1100, which opens over it.</summary>
    private const int SortingOrder = 1000;

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
        // 900: under the list this button opens, and over the menu scene's own canvas. The cost of
        // SampleUi's 1080x1920 reference, shared by every code-built screen here, is that this
        // scales on a slightly different curve from MainMenu.unity's own 800x600 buttons beside it.
        SampleUi.OverlayCanvas(host, 900);

        var button = SampleUi.Button("scenarios-tab", host.transform, "QA Scenarios", Open);
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
        SampleUi.OverlayCanvas(gameObject, SortingOrder);

        // The scene supplies the EventSystem. One is deliberately NOT created here: the project
        // is Input-System-only (ProjectSettings activeInputHandler: 1), so the legacy
        // StandaloneInputModule a hand-rolled EventSystem would need throws every frame.
        if (EventSystem.current == null)
        {
            Debug.LogWarning("[Octopus] No EventSystem in this scene — the scenario list will " +
                             "render but not respond to taps.");
        }

        var root = SampleUi.Panel("Root", transform, SampleUi.Background);
        SampleUi.Stretch(root, Vector2.zero, Vector2.one);

        BuildHeader(root);

        var content = SampleUi.VerticalScroll(root, new RectOffset(40, 40, 220, 40));
        foreach (var scenario in OctopusScenarioCatalog.All) BuildRow(content, scenario);
    }

    private void BuildHeader(RectTransform root)
    {
        var header = SampleUi.Panel("Header", root, Color.clear);
        SampleUi.Stretch(header, new Vector2(0f, 1f), Vector2.one);
        header.sizeDelta = new Vector2(0f, 220f);
        header.anchoredPosition = new Vector2(0f, -110f);

        // Left inset 70, right inset 260: clear of the Close button, and on the same left edge
        // as the subtitle under it.
        var title = SampleUi.Label("Title", header, "QA Scenarios", 46, SampleUi.PlatformSlot,
                                   TextAnchor.UpperLeft);
        SampleUi.Stretch(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f));
        title.rectTransform.sizeDelta = new Vector2(-330f, 60f);
        title.rectTransform.anchoredPosition = new Vector2(-95f, -50f);

        var notApplicable = 0;
        var driven = 0;
        var pending = 0;
        foreach (var s in OctopusScenarioCatalog.All)
        {
            if (s.Status == ScenarioStatus.NotApplicable) notApplicable++;
            else if (OctopusScenarioPilots.Has(s.Id)) driven++;
            else pending++;
        }

        var subtitle = SampleUi.Label(
            "Subtitle", header,
            OctopusScenarioCatalog.All.Count + " scenarios in the shared catalogue · " + driven +
            " with a scenario screen · " + pending + " not implemented on Unity yet · " +
            notApplicable + " not applicable",
            26, SampleUi.Muted, TextAnchor.UpperLeft);
        SampleUi.Stretch(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f));
        subtitle.rectTransform.sizeDelta = new Vector2(-330f, 70f);
        subtitle.rectTransform.anchoredPosition = new Vector2(-95f, -125f);

        var close = SampleUi.Button("Close", header, "Close", () => Destroy(gameObject));
        close.anchorMin = close.anchorMax = new Vector2(1f, 1f);
        close.pivot = new Vector2(1f, 1f);
        close.sizeDelta = new Vector2(180f, 80f);
        close.anchoredPosition = new Vector2(-40f, -30f);
    }

    private void BuildRow(RectTransform parent, OctopusScenario scenario)
    {
        // Named with the id the QA pipeline taps, so the row is addressable the day a Unity
        // driver exists — Android, Flutter and RN already carry this id on their card.
        var row = SampleUi.Panel(scenario.CardTestId, parent, SampleUi.RowBackground);
        // No ContentSizeFitter on a row: the content stack already controls its height, and Unity
        // warns about a layout-group child that fits its own content — hence selfSizing: false.
        SampleUi.VerticalStack(row, 8f, new RectOffset(24, 24, 20, 20), false);

        SampleUi.FlexibleLabel(row, scenario.Title, 32, SampleUi.TitleColor);
        SampleUi.FlexibleLabel(row, scenario.Capability, 22, SampleUi.Muted);

        if (scenario.Status == ScenarioStatus.NotApplicable)
        {
            // Not a gap to re-ask about: the concept has no Unity counterpart at all. Stated with
            // its reason so nobody re-opens it as an oversight.
            SampleUi.FlexibleLabel(row, "Not applicable on Unity — " + scenario.NotApplicableReason,
                                   22, SampleUi.Muted);
            return;
        }

        if (OctopusScenarioPilots.Has(scenario.Id))
        {
            // The screen exists here; the pm-tools catalogue still lists Unity as `pending` for
            // every scenario, and flipping that is a pm-tools change, not this sample's to claim.
            SampleUi.FlexibleLabel(row, "Scenario screen available (the shared catalogue still " +
                                        "lists Unity as pending)", 24, SampleUi.PlatformSlot);
            var id = scenario.Id;
            var open = SampleUi.Button(scenario.Id + "-open", row, "Open scenario",
                                       () => OctopusScenarioScreenView.Open(
                                           OctopusScenarioPilots.Create(id)));
            open.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;
            return;
        }

        SampleUi.FlexibleLabel(row, "Not implemented", 24, SampleUi.Attention);
        if (scenario.DemoScene != null)
        {
            // An existing scene touching the same capability, offered as a shortcut. It is
            // explicitly NOT the scenario: it ships none of the catalogue's preset ids.
            var scene = scenario.DemoScene;
            SampleUi.FlexibleLabel(row, "Related demo scene (not the scenario): " + scene, 20,
                                   SampleUi.Muted);
            var open = SampleUi.Button(scenario.Id + "-open", row, "Open " + scene,
                                       () => SceneManager.LoadScene(scene));
            open.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;
        }
    }
}
