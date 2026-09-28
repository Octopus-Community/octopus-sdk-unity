using TMPro;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// The Scenarios tab's grammar: what is listed, how it is grouped, and what a query does to it.
///
/// Everything the screen decides lives in <see cref="OctopusScenarioSections"/> as data, which is
/// what lets these tests assert it at all — the EditMode assembly overrides its references down to
/// `nunit.framework.dll`, so no `UnityEngine.UI` type can be named here. What is on screen is
/// asserted through `GameObject.name` and the view's own public API.
/// </summary>
public class OctopusScenariosListViewTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var host in _spawned)
        {
            if (host != null) Object.DestroyImmediate(host);
        }
        _spawned.Clear();
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void SectionHeaderHasVisiblePressedFeedback(OctopusSampleTheme theme)
    {
        var previous = OctopusSampleBranding.Theme;
        GameObject events = null;
        try
        {
            OctopusSampleBranding.Theme = theme;
            if (EventSystem.current == null) events = new GameObject("Events", typeof(EventSystem));
            var shell = Create();
            shell.Select(OctopusSampleTab.Scenarios);
            var head = Find(shell.transform, OctopusScenarioSections.HeaderIdOf(ScenarioSection.SignIn));
            var button = head.GetComponent<SampleUiButton>();
            Assert.IsNotNull(button);
            var image = head.GetComponent<Image>();
            var normal = image.color;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            button.OnPointerEnter(pointer);
            button.OnPointerDown(pointer);
            Assert.AreNotEqual(normal, image.color);
            Assert.AreEqual(1f, image.color.a);
            button.OnPointerUp(pointer);
        }
        finally
        {
            if (events != null) Object.DestroyImmediate(events);
            OctopusSampleBranding.Theme = previous;
        }
    }

    [Test]
    public void EveryCatalogueScenarioSitsInExactlyOneSection()
    {
        // The gate behind OctopusScenarioSections.SectionOf's throw: a scenario added to the
        // catalogue and forgotten in the section map fails here rather than on a device.
        var placed = new List<string>();
        foreach (var section in OctopusScenarioSections.All)
        {
            foreach (var scenario in OctopusScenarioCatalog.All)
            {
                if (OctopusScenarioSections.SectionOf(scenario.Id) == section) placed.Add(scenario.Id);
            }
        }

        Assert.AreEqual(OctopusScenarioCatalog.All.Count, placed.Count);
        Assert.AreEqual(placed.Count, placed.Distinct().Count(), "A scenario is in two sections.");
    }

    [Test]
    public void TheSixSectionTitlesAreTheOnesEveryOtherSampleUses()
    {
        // Verbatim from the Android sample, which is this port's reference. Flutter and React
        // Native head the first section "SSO & user"; the other five match all round.
        var titles = OctopusScenarioSections.All.Select(OctopusScenarioSections.TitleOf).ToArray();

        Assert.AreEqual(
            new[]
            {
                "Sign-in & user", "Presentation modes", "Community & groups", "Notifications",
                "Theme & language", "Host callbacks & events",
            },
            titles);
    }

    [Test]
    public void OnlyAScenarioWithAScreenIsListed()
    {
        // TOKENS §1, struck on 2026-08-26: "A scenario that is not implemented at all is not
        // listed. Filtering it out is the whole point: the list is what the sample can demonstrate
        // right now." The four not-applicable scenarios fall out of the same rule.
        var listed = OctopusScenarioSections.Filter("")
            .SelectMany(g => g.Scenarios)
            .Select(s => s.Id)
            .ToList();

        Assert.IsNotEmpty(listed, "Nothing is listed at all, so the tab is empty.");
        foreach (var id in listed)
        {
            Assert.IsTrue(OctopusScenarioPilots.Has(id),
                "Scenario '" + id + "' is listed but no pilot drives it: the card opens an empty " +
                "screen.");
        }

        foreach (var scenario in OctopusScenarioCatalog.All)
        {
            if (OctopusScenarioPilots.Has(scenario.Id)) continue;
            Assert.IsFalse(listed.Contains(scenario.Id),
                "Scenario '" + scenario.Id + "' has no pilot and is listed anyway.");
        }
    }

    [Test]
    public void SectionsComeOutInDisplayOrderAndOnlyNotificationsCanHaveJustAToggle()
    {
        var sections = OctopusScenarioSections.Filter("").Select(g => g.Section).ToList();

        var expected = OctopusScenarioSections.All.Where(sections.Contains).ToList();
        Assert.AreEqual(expected, sections, "Sections are not in the canonical display order.");
        foreach (var group in OctopusScenarioSections.Filter(""))
        {
            if (group.Section == ScenarioSection.Notifications) continue;
            Assert.IsNotEmpty(group.Scenarios,
                "Section '" + group.Title + "' has neither a card nor the push toggle.");
        }
    }

    [Test]
    public void SearchMatchesTitleCapabilityAndIdCaseInsensitively()
    {
        var connection = OctopusScenarioCatalog.All.First(s => s.Id == "connection");

        Assert.IsTrue(OctopusScenarioSections.Matches(connection, "CONNECTION"), "Id, upper case.");
        Assert.IsTrue(OctopusScenarioSections.Matches(connection, "connect"), "Title, partial.");
        Assert.IsTrue(OctopusScenarioSections.Matches(connection, "disconnectUser"),
            "Capability line, which is where the SDK symbol a tester searches for lives.");
        Assert.IsTrue(OctopusScenarioSections.Matches(connection, "   "),
            "A whitespace-only query is not a query.");
        Assert.IsFalse(OctopusScenarioSections.Matches(connection, "reactions"));

        // Not the section title: a query that hit a head would return cards that do not contain it.
        Assert.IsFalse(OctopusScenarioSections.Matches(connection, "Sign-in"));
    }

    [Test]
    public void AQueryThatMatchesNothingLeavesNoSectionAndSaysSo()
    {
        Assert.IsEmpty(OctopusScenarioSections.Filter("zzzz-no-such-scenario"));
        Assert.AreEqual("No scenario matches \"zzzz\".",
                        OctopusScenariosListView.EmptyStateText("  zzzz  "));
    }

    [Test]
    public void TheTabRendersTheSearchFieldAndTheSectionHeads()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);

        Assert.IsNotNull(Find(shell.transform, OctopusScenariosListView.SearchInputId),
            "The search field is gone, and with it the id every other sample's QA script taps.");
        foreach (var group in OctopusScenarioSections.Filter(""))
        {
            Assert.IsNotNull(Find(shell.transform, group.HeaderId),
                "No section head named '" + group.HeaderId + "'.");
        }
    }

    [Test]
    public void InitialScreenMakesPresentationVisibleAndCollapsesWithItsSection()
    {
        var presentation = OctopusScenarioSections.Filter("")
            .Single(group => group.Section == ScenarioSection.Presentation);
        CollectionAssert.AreEqual(new[] { "initialScreen" }, presentation.Scenarios.Select(row => row.Id));
        Assert.IsFalse(OctopusScenarioSections.Filter("")
            .Single(group => group.Section == ScenarioSection.Community).Scenarios.Any(row => row.Id == "initialScreen"));
        var search = OctopusScenarioSections.Filter("initialScreen");
        Assert.AreEqual(1, search.Count);
        Assert.AreEqual(ScenarioSection.Presentation, search[0].Section);

        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var view = shell.GetComponentInChildren<OctopusScenariosListView>();
        Assert.IsNotNull(Find(shell.transform, presentation.HeaderId));
        Assert.IsNotNull(Find(shell.transform, "scenarios-initialScreen-card"));
        view.ToggleSection(ScenarioSection.Presentation);
        Assert.IsNotNull(Find(shell.transform, presentation.HeaderId));
        Assert.IsNull(Find(shell.transform, "scenarios-initialScreen-card"));
        view.ToggleSection(ScenarioSection.Presentation);
        Assert.IsNotNull(Find(shell.transform, "scenarios-initialScreen-card"));
    }

    [Test]
    public void EveryCardIdentifierNamesTheButtonThatOpensItsScenario()
    {
        var sdk = new OctopusRecordingScenarioSdk();
        OctopusScenarioSdk.Use(sdk);
        try
        {
            var shell = Create();
            shell.Select(OctopusSampleTab.Scenarios);

            foreach (var scenario in OctopusScenarioSections.Filter("").SelectMany(g => g.Scenarios))
            {
                var matches = shell.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == scenario.CardTestId).ToArray();
                Assert.AreEqual(1, matches.Length, "The card identifier must be unique: " + scenario.Id);
                // The test assembly has no uGUI reference; inspect the actual component and invoke
                // its click event rather than opening the screen through its static API.
                var button = matches[0].GetComponent("UnityEngine.UI.Button");
                Assert.IsNotNull(button, "The identifier is on a container, not a button: " + scenario.Id);
                var click = button.GetType().GetProperty("onClick").GetValue(button, null);
                click.GetType().GetMethod("Invoke").Invoke(click, null);

                var screen = Object.FindAnyObjectByType<OctopusScenarioScreenView>();
                Assert.IsNotNull(screen, "Tapping the named button opened no screen: " + scenario.Id);
                _spawned.Add(screen.gameObject);
                Assert.AreEqual(scenario.Id, screen.ScenarioId);
                Object.DestroyImmediate(screen.gameObject);
            }

            Assert.IsEmpty(sdk.ScenarioMethods, "Opening a scenario must not call the SDK.");
        }
        finally
        {
            OctopusScenarioSdk.Use(null);
        }
    }

    [Test]
    public void SearchingHidesTheCardsThatDoNotMatchAndThenGivesThemBack()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var view = shell.GetComponentInChildren<OctopusScenariosListView>();
        Assert.IsNotNull(view, "The Scenarios tab built no list view.");

        view.SetQuery("locale");
        Assert.IsNotNull(Find(shell.transform, "scenarios-locale-card"));
        Assert.IsNull(Find(shell.transform, "scenarios-connection-card"),
            "A card that does not match the query is still on screen.");

        view.SetQuery("");
        Assert.IsNotNull(Find(shell.transform, "scenarios-connection-card"),
            "Clearing the query did not bring the cards back.");
    }

    [Test]
    public void AQueryThatMatchesNothingRendersTheEmptyState()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var view = shell.GetComponentInChildren<OctopusScenariosListView>();

        view.SetQuery("zzzz-no-such-scenario");

        Assert.IsNotNull(Find(shell.transform, OctopusScenariosListView.EmptyStateId),
            "Nothing matched and the tab shows nothing at all — indistinguishable from a screen " +
            "that failed to build.");
        Assert.IsNull(Find(shell.transform, "scenarios-connection-card"));
    }

    [Test]
    public void ShowingASectionClearsTheQueryReopensItAndScrollsToIt()
    {
        // What a link into the list ("Feature: …", "Scenarios › Notifications") lands on: the named
        // section, visible, whatever the reader left collapsed or typed the last time.
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var list = shell.GetComponentInChildren<OctopusScenariosListView>();
        list.ToggleSection(ScenarioSection.Theme);
        list.SetQuery("zzz-no-match");
        shell.Select(OctopusSampleTab.Home);

        shell.ShowScenarioSection(ScenarioSection.Theme);

        Assert.AreEqual(OctopusSampleTab.Scenarios, shell.Selected);
        list = shell.GetComponentInChildren<OctopusScenariosListView>();
        Assert.AreEqual("", list.Query);
        Assert.IsTrue(list.IsSectionOpen(ScenarioSection.Theme));
        Assert.AreEqual(ScenarioSection.Theme, list.FocusedSection);
        Assert.IsTrue(list.ScrollTo(ScenarioSection.Theme), "The section's card is not on screen.");
    }

    [Test]
    public void APlainTabSwitchKeepsTheListAsTheReaderLeftIt()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var list = shell.GetComponentInChildren<OctopusScenariosListView>();
        list.ToggleSection(ScenarioSection.Theme);
        shell.Select(OctopusSampleTab.Home);

        shell.Select(OctopusSampleTab.Scenarios);

        list = shell.GetComponentInChildren<OctopusScenariosListView>();
        Assert.IsFalse(list.IsSectionOpen(ScenarioSection.Theme),
            "Only a link reopens a section; a tab tap must not.");
        Assert.IsNull(list.FocusedSection);
    }

    [Test]
    public void CollapsingASectionHidesItsCardsButKeepsItsHead()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var view = shell.GetComponentInChildren<OctopusScenariosListView>();
        var section = OctopusScenarioSections.SectionOf("connection");

        Assert.IsTrue(view.IsSectionOpen(section), "Sections start open.");
        view.ToggleSection(section);

        Assert.IsFalse(view.IsSectionOpen(section));
        Assert.IsNotNull(Find(shell.transform, OctopusScenarioSections.HeaderIdOf(section)),
            "Collapsing took the head with it, so nothing can open the section again.");
        Assert.IsNull(Find(shell.transform, "scenarios-connection-card"));

        view.ToggleSection(section);
        Assert.IsNotNull(Find(shell.transform, "scenarios-connection-card"));
    }

    [Test]
    public void ASearchOpensACollapsedSectionWithoutForgettingItWasCollapsed()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var view = shell.GetComponentInChildren<OctopusScenariosListView>();
        var section = OctopusScenarioSections.SectionOf("connection");
        view.ToggleSection(section);

        view.SetQuery("connection");
        Assert.IsTrue(view.IsSectionOpen(section),
            "A card hidden by a collapse the reader set ten taps ago reads as 'no result'.");
        Assert.IsNotNull(Find(shell.transform, "scenarios-connection-card"));

        view.SetQuery("");
        Assert.IsFalse(view.IsSectionOpen(section), "The collapse was forgotten by the search.");
    }

    [Test]
    public void OpeningTheScenariosTabMakesNoSdkCall()
    {
        // Same rule as the Home dashboard: a tab switch that mutates anything makes every later QA
        // step ambiguous. The list reads a static catalogue and nothing else.
        var sdk = new OctopusRecordingScenarioSdk();
        OctopusScenarioSdk.Use(sdk);
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;
        try
        {
            var shell = Create();
            shell.Select(OctopusSampleTab.Scenarios);
            shell.GetComponentInChildren<OctopusScenariosListView>().SetQuery("connection");

            Assert.IsEmpty(sdk.ScenarioMethods, "Opening Scenarios reached the SDK: " +
                                        string.Join(", ", sdk.ScenarioMethods));
            Assert.IsEmpty(probe.Methods, "Opening Scenarios logged an SDK call: " +
                                          string.Join(", ", probe.Methods));
        }
        finally
        {
            OctopusScenarioSdk.Use(null);
            OctopusSampleLog.Current = OctopusSampleLog.None;
        }
    }

    [Test]
    public void CollapsedSectionKeepsItsToggleInsideTheSameFrame()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        shell.GetComponentInChildren<OctopusScenariosListView>().ToggleSection(ScenarioSection.SignIn);
        var head = Find(shell.transform, OctopusScenarioSections.HeaderIdOf(ScenarioSection.SignIn));
        var toggle = Find(shell.transform, OctopusScenariosListView.ForceLoginRowId);
        Assert.AreSame(head.parent, toggle.parent);
        Assert.IsTrue(toggle.gameObject.activeInHierarchy);
        Assert.IsNull(Find(shell.transform, "scenarios-connection-card"));
        Assert.IsFalse(toggle.IsChildOf(head), "A toggle miss must not collapse the section.");
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void SearchIsVisibleBeforeFocusAndItsPaddingReceivesPointerClicks(OctopusSampleTheme theme)
    {
        OctopusSampleBranding.Theme = theme;
        var eventHost = new GameObject("SearchEventSystem", typeof(EventSystem));
        _spawned.Add(eventHost);
        // Use a fixed world-space canvas: EditMode has no dependable Game view viewport or
        // native render depth. This is the production list builder with deterministic geometry.
        var host = new GameObject("SearchCanvas", typeof(RectTransform));
        _spawned.Add(host);
        var canvas = SampleUi.OverlayCanvas(host, 0);
        canvas.renderMode = RenderMode.WorldSpace;
        var hostRect = (RectTransform)host.transform;
        hostRect.sizeDelta = new Vector2(1080f, 1920f);
        OctopusScenariosListView.BuildInto(hostRect);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(hostRect);
        var field = Find(host.transform, OctopusScenariosListView.SearchInputId).GetComponent<TMP_InputField>();
        Assert.AreEqual(SampleUi.TitleColor, field.textComponent.color);
        Assert.AreEqual(1f, field.textComponent.color.a);
        var border = field.transform.Find("Stroke").GetComponent<Image>();
        Assert.AreEqual(OctopusSampleBranding.Palette.ControlBorder, border.color);
        Assert.IsTrue(field.GetComponent<Image>().raycastTarget);
        Assert.IsNotNull(field.placeholder);
        Assert.AreEqual("Search scenarios", ((TMP_Text)field.placeholder).text);
        Assert.IsTrue(field.placeholder.enabled);
        Assert.IsFalse(field.isFocused);
        var rect = (RectTransform)field.transform;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        // Four points within the padding, outside the text's inset rectangle.
        foreach (var corner in corners)
        {
            var point = Vector3.Lerp(corner, rect.TransformPoint(rect.rect.center), 0.02f);
            var pointer = new PointerEventData(eventHost.GetComponent<EventSystem>())
            {
                position = RectTransformUtility.WorldToScreenPoint(null, point)
            };
            // GraphicRaycaster discards depth == -1 before testing geometry. In headless
            // EditMode there is no rendered native canvas depth, even after ForceUpdateCanvases.
            // Test the actual rectangle, graphic filters (including the viewport mask), and
            // uGUI's handler resolution at each padding point without requiring a render frame.
            Assert.IsTrue(RectTransformUtility.RectangleContainsScreenPoint(rect, pointer.position, null));
            Assert.IsFalse(RectTransformUtility.RectangleContainsScreenPoint(
                field.textComponent.rectTransform, pointer.position, null),
                "The probe must be in the field's padding, outside its text rectangle.");
            Assert.IsTrue(field.GetComponent<Image>().Raycast(pointer.position, null),
                "The field or a parent mask rejects a point inside its padding.");
            Assert.AreSame(field.gameObject,
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(field.gameObject));
            Assert.AreSame(field.gameObject,
                ExecuteEvents.ExecuteHierarchy(field.gameObject, pointer, ExecuteEvents.pointerClickHandler),
                "The padding target must dispatch the pointer click to the InputField.");
        }
    }

    [Test]
    public void EveryCardPanelIsNamedAfterItsScenario()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);

        var expected = OctopusScenarioSections.Filter("").SelectMany(g => g.Scenarios)
            .Select(scenario => "scenarios-" + scenario.Id + "-card-panel").ToArray();
        var actual = shell.GetComponentsInChildren<Transform>(true)
            .Select(t => t.name).Where(name => name.EndsWith("-card-panel")).ToArray();

        CollectionAssert.AreEquivalent(expected, actual);
        Assert.AreEqual(actual.Length, actual.Distinct().Count(),
            "Two cards share a GameObject.name; a hierarchy dump cannot tell them apart.");
        Assert.IsEmpty(shell.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name == "Card").ToArray(),
            "A card fell back to the shared 'Card' name.");
    }

    /// <summary>
    /// The card prints the product wording, not the catalogue's. Written against the rendered text
    /// rather than against the catalogue fields, because the regression #129 describes is a card
    /// still showing "Locale" and a capability line while the data sitting behind it is correct.
    /// </summary>
    [Test]
    public void EveryCardShowsItsProductTitleSubtitleAndApiSymbol()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);

        foreach (var scenario in OctopusScenarioSections.Filter("").SelectMany(g => g.Scenarios))
        {
            var card = Find(shell.transform, scenario.CardTestId);
            Assert.IsNotNull(card, "No card for " + scenario.Id);
            var lines = card.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text).ToArray();

            CollectionAssert.Contains(lines, scenario.ProductTitle,
                "The card for " + scenario.Id + " does not show its product title.");
            CollectionAssert.Contains(lines, scenario.Subtitle,
                "The card for " + scenario.Id + " does not show its subtitle.");
            CollectionAssert.DoesNotContain(lines, scenario.Capability,
                "The card for " + scenario.Id + " still shows the catalogue capability line.");

            var chip = Find(card, scenario.CardTestId + "-api");
            Assert.IsNotNull(chip, "No API chip on the card for " + scenario.Id);
            Assert.AreEqual(scenario.ApiSymbol,
                chip.GetComponentInChildren<TMP_Text>(true).text);
        }
    }

    /// <summary>
    /// The chip is sized by its content, not by the card. Nothing inside it may ask a parent layout
    /// group for flexible width: a single `FlexibleLabel` in there is enough to make the pill
    /// absorb the whole row and read as a banner, and no text assertion would notice.
    /// </summary>
    [Test]
    public void TheApiChipAsksForNoFlexibleWidth()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);

        foreach (var scenario in OctopusScenarioSections.Filter("").SelectMany(g => g.Scenarios))
        {
            var chip = Find(Find(shell.transform, scenario.CardTestId), scenario.CardTestId + "-api");
            Assert.IsNotNull(chip, "No API chip on the card for " + scenario.Id);

            foreach (var element in chip.GetComponentsInChildren<LayoutElement>(true))
            {
                Assert.LessOrEqual(element.flexibleWidth, 0f,
                    "'" + element.gameObject.name + "' inside the API chip of " + scenario.Id +
                    " asks for flexible width, which stretches the pill across the card.");
            }
        }
    }

    [TestCase(1080f)]
    [TestCase(1178f)]
    public void ApiChipRectsHugTheirLabelsAfterLayout(float canvasWidth)
    {
        var host = new GameObject("ApiChipCanvas", typeof(RectTransform));
        _spawned.Add(host);
        var canvas = SampleUi.OverlayCanvas(host, 0);
        canvas.renderMode = RenderMode.WorldSpace;
        var hostRect = (RectTransform)host.transform;
        hostRect.sizeDelta = new Vector2(canvasWidth, 2340f);
        OctopusScenariosListView.BuildInto(hostRect);

        // Exercise the actual nested groups, not just explicit LayoutElement components:
        // a layout group can report flexible width even when no LayoutElement requests it.
        for (var pass = 0; pass < 3; pass++)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(hostRect);
        }

        var symbols = new[] { "ConnectUser", "FetchCommunityData", "DebugOverrideProfileFieldsLock" };
        var widths = new List<float>();
        var expectedWidths = new List<float>();
        foreach (var symbol in symbols)
        {
            var scenario = OctopusScenarioCatalog.All.Single(s => s.ApiSymbol == symbol);
            var chip = (RectTransform)Find(host.transform, scenario.CardTestId + "-api");
            Assert.IsNotNull(chip, "No API chip for " + symbol);
            var label = chip.GetComponentInChildren<TMP_Text>();
            var stack = chip.GetComponent<VerticalLayoutGroup>();
            widths.Add(chip.rect.width);
            expectedWidths.Add(label.preferredWidth + stack.padding.horizontal);
            TestContext.WriteLine("{0}: canvas={1}, chip={2:F2}, text+padding={3:F2}, flexible={4}",
                symbol, canvasWidth, chip.rect.width, expectedWidths.Last(),
                LayoutUtility.GetFlexibleWidth(chip));
        }

        for (var i = 0; i < symbols.Length; i++)
            Assert.That(widths[i], Is.EqualTo(expectedWidths[i]).Within(1f),
                symbols[i] + " must hug its label after the real layout rebuild.");
        Assert.That(widths[0], Is.LessThan(widths[1]));
        Assert.That(widths[1], Is.LessThan(widths[2]));
    }

    /// <summary>
    /// The chip is not decoration: an integrator who knows one method name can find the scenario
    /// that calls it. Android matches the API name for the same reason.
    /// </summary>
    [Test]
    public void SearchingForAnApiSymbolFindsItsCard()
    {
        // "SetLightColorScheme" appears in no other field of the theme row — its capability line
        // reads "custom OctopusTheme (colors, fonts, logo)" — so a hit here can only come from the
        // chip. Most symbols also occur in the capability line and would prove nothing.
        var hits = OctopusScenarioSections.Filter("SetLightColorScheme")
            .SelectMany(g => g.Scenarios).Select(s => s.Id).ToArray();

        CollectionAssert.AreEqual(new[] { "theme" }, hits);
    }

    private OctopusSampleShell Create()
    {
        var shell = OctopusSampleShell.Create();
        _spawned.Add(shell.gameObject);
        return shell;
    }

    private static Transform Find(Transform root, string name)
    {
        if (root.gameObject.name == name) return root;
        for (var i = 0; i < root.childCount; i++)
        {
            var found = Find(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private sealed class ProbeLog : IOctopusSampleLog
    {
        public readonly List<string> Methods = new List<string>();

        public void LogApiCall(string method, string detail = null)
        {
            Methods.Add(method);
        }

        public readonly List<(string headline, string detail)> StateChanges =
            new List<(string, string)>();

        public void LogStateChange(string headline, string detail = null)
        {
            StateChanges.Add((headline, detail));
        }
    }
}
