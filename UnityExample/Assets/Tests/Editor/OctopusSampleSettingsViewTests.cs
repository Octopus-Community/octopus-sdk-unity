using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OctopusSampleSettingsViewTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    [SetUp] public void SetUp() { OctopusSampleSettingsRows.Clear(); OctopusScenarioSdk.Use(new OctopusRecordingScenarioSdk()); }
    [TearDown] public void TearDown()
    {
        foreach (var host in _spawned) if (host != null) Object.DestroyImmediate(host);
        _spawned.Clear();
        foreach (var view in Object.FindObjectsByType<OctopusSampleSettingsDetailView>(FindObjectsSortMode.None)) view.Close();
        var about = Object.FindAnyObjectByType<OctopusSampleAboutView>(); if (about != null) about.Close();
        var appearance = Object.FindAnyObjectByType<OctopusSampleAppearanceView>(); if (appearance != null) appearance.Close();
        var tools = Object.FindAnyObjectByType<OctopusSampleDeveloperToolsView>(); if (tools != null) tools.Close();
        OctopusSampleSettingsRows.Clear(); OctopusScenarioSdk.Use(null); OctopusSampleLog.Current = OctopusSampleLog.None;
    }
    private OctopusSampleShell Shell()
    {
        var shell = OctopusSampleShell.Create(); _spawned.Add(shell.gameObject); shell.Select(OctopusSampleTab.Settings); return shell;
    }
    private OctopusSampleSettingsView View() { return Shell().GetComponentInChildren<OctopusSampleSettingsView>(); }
    private static Transform Find(Transform root, string id) { return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == id); }
    [Test] public void AboutShowsBuildPinsLicencesAndLegacyEntry()
    {
        var view = OctopusSampleAboutView.Open();
        foreach (var id in new[] { "Build", "Native build pins", "Octopus SDK", "Firebase / EDM4U", "Liberation Sans font", OctopusSampleShell.LegacyScenesId })
            Assert.IsNotNull(Find(view.transform, id), id);
        Assert.IsNotEmpty(OctopusSampleAboutView.BuildText());
        Assert.IsNull(Find(view.transform, "sample-update-status"));
    }
    [Test] public void RootOffersAndroidGroupsAndACommunitySummary()
    {
        var shell = Shell();
        foreach (var id in new[] { "settings-community-block", "settings-sdk-status", OctopusSampleSettingsView.ScreenId,
            OctopusSampleSettingsView.AccountRowId, OctopusSampleSettingsView.AppearanceRowId, OctopusSampleSettingsView.LanguageRowId,
            OctopusSampleSettingsView.ConfigRowId, "settings-devtools-row", OctopusSampleSettingsView.AboutRowId,
            OctopusSampleSettingsView.ResetButtonId, OctopusSampleSettingsView.VersionLabelId }) Assert.IsNotNull(Find(shell.transform, id), id);
        Assert.IsNull(Find(shell.transform, OctopusSampleShell.LegacyScenesId));
        StringAssert.Contains("sample record", OctopusSampleSettingsView.StatusText());
        Assert.IsFalse(shell.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("Legacy demo scenes")));
    }
    [Test] public void LanguageRowOpensThePickerWithoutALocaleScenarioDetour()
    {
        var shell = Shell(); Find(shell.transform, OctopusSampleSettingsView.LanguageRowId).GetComponent<Button>().onClick.Invoke();
        Assert.IsNotNull(Object.FindAnyObjectByType<OctopusSampleLanguageView>());
        Assert.IsNull(Object.FindAnyObjectByType<OctopusScenarioScreenView>());
    }
    [Test] public void SupportRegistryUpdatesAndAboutStaysLast()
    {
        var shell = Shell(); var view = shell.GetComponentInChildren<OctopusSampleSettingsView>();
        OctopusSampleSettingsRows.Register("test-support", "Test", "Support", () => {});
        CollectionAssert.Contains(view.SupportRowIds, "test-support");
        Assert.AreEqual(OctopusSampleSettingsView.AboutRowId, view.SupportRowIds.Last());
        Assert.AreEqual(1, view.RowsEventCount);
        shell.Select(OctopusSampleTab.Scenarios);
        OctopusSampleSettingsRows.Register("test-other", "Other", "Support", () => {});
        Assert.AreEqual(1, view.RowsEventCount);
    }
    [Test] public void ResetAsksFirstAndCancelKeepsTheSession()
    {
        var shell = Shell(); var view = shell.GetComponentInChildren<OctopusSampleSettingsView>();
        view.ConfirmReset(); Assert.IsNull(GameObject.Find("settings-reset-dialog"));
        view.RequestReset(); Assert.IsTrue(view.IsConfirmingReset);
        var dialog = GameObject.Find("settings-reset-dialog"); Assert.IsNotNull(dialog);
        StringAssert.Contains("Nothing is deleted on the server", OctopusSampleResetDialog.Wording);
        Find(dialog.transform, OctopusSampleSettingsView.ResetCancelId).GetComponent<Button>().onClick.Invoke();
        Assert.IsFalse(view.IsConfirmingReset); Assert.IsNull(GameObject.Find("settings-reset-dialog"));
    }
    [Test] public void SettingsSummaryRefreshesAfterStop()
    {
        var shell = Shell(); OctopusSampleState.ReportInitialized("SSO");
        StringAssert.Contains("Initialized", Find(shell.transform, "settings-sdk-status").GetComponent<TMP_Text>().text);
        OctopusScenarioSdk.LifecycleStopped();
        StringAssert.Contains("Not initialized", Find(shell.transform, "settings-sdk-status").GetComponent<TMP_Text>().text);
    }
    [Test] public void ResetDialogFitsItsContentsAndOnlyExteriorClicksCancel()
    {
        var previousScale = OctopusSampleTextScale.Provider;
        try
        {
            OctopusSampleTextScale.Provider = () => 1f;
            var view = View(); view.RequestReset();
            var dialog = GameObject.Find("settings-reset-dialog");
            var card = (RectTransform)Find(dialog.transform, "settings-reset-confirmation");
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            Canvas.ForceUpdateCanvases();
            Assert.Greater(card.rect.height, OctopusSampleBranding.MinTouchUnits * 2f);
            foreach (var child in card.GetComponentsInChildren<RectTransform>())
            {
                if (child.GetComponent<TMP_Text>() == null && child.GetComponent<Button>() == null) continue;
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(card, child);
                Assert.GreaterOrEqual(bounds.min.y, card.rect.yMin - 0.5f, child.name);
                Assert.LessOrEqual(bounds.max.y, card.rect.yMax + 0.5f, child.name);
            }
            var text = card.GetComponentsInChildren<TMP_Text>().First(t => t.text == OctopusSampleResetDialog.Wording);
            var click = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
            Assert.IsNull(ExecuteEvents.ExecuteHierarchy(text.gameObject, click, ExecuteEvents.pointerClickHandler));
            Assert.IsTrue(view.IsConfirmingReset);
            Assert.IsNotNull(dialog);
            ExecuteEvents.Execute(Find(dialog.transform, "Scrim").gameObject, click, ExecuteEvents.pointerClickHandler);
            Assert.IsFalse(view.IsConfirmingReset);
            Assert.IsTrue(dialog == null);
        }
        finally { OctopusSampleTextScale.Provider = previousScale; }
    }
    [Test]
    public void AboutIsTheLastSupportRowWhenNothingElseRegistered()
    {
        var view = View();

        CollectionAssert.AreEqual(new[] { "settings-devtools-row", OctopusSampleSettingsView.AboutRowId },
                                  view.SupportRowIds,
                                  "The built-in Developer tools route precedes About when no recorder is registered.");
        Find(view.transform, "settings-devtools-row").GetComponent<Button>().onClick.Invoke();
        var tools = Object.FindAnyObjectByType<OctopusSampleDeveloperToolsView>();
        Assert.IsNotNull(tools);
        Assert.AreEqual("developer-tools-screen", tools.CurrentScreen);
        Find(tools.transform, "devtools-debug-info-row").GetComponent<Button>().onClick.Invoke();
        Assert.IsNotNull(Find(tools.transform, "debug-info-native-build-pins"));
    }

    [Test]
    public void AboutOpensOverTheTabAndClosesAgain()
    {
        Shell();
        Assert.IsFalse(OctopusSampleAboutView.IsOpen, "An About screen was already open.");

        var about = OctopusSampleAboutView.Open();

        Assert.IsTrue(OctopusSampleAboutView.IsOpen, "About did not open.");
        foreach (var id in new[]
                 {
                     OctopusSampleAboutView.ScreenId,
                     OctopusSampleAboutView.VersionsCardId,
                     OctopusSampleAboutView.LicencesCardId,
                     OctopusSampleAboutView.BackId,
                 })
        {
            Assert.IsNotNull(Find(about.transform, id),
                "Nothing on the About screen is named '" + id + "'.");
        }

        about.Close();
        Assert.IsFalse(OctopusSampleAboutView.IsOpen, "Back left the About screen open.");
    }

    [Test]
    public void OpeningAboutTwiceOpensOneScreen()
    {
        Shell();

        var first = OctopusSampleAboutView.Open();
        var second = OctopusSampleAboutView.Open();

        Assert.AreSame(first, second, "A second Open stacked a duplicate About screen.");
    }

    [Test]
    public void AboutCarriesTheUnityAttributionAndItsNotice()
    {
        Shell();

        var about = OctopusSampleAboutView.Open();

        Assert.IsNotNull(Find(about.transform, OctopusSampleAboutView.AttributionCardId),
            "The About screen carries no attribution card.");
        Assert.AreEqual("Made with Unity", OctopusSampleAboutView.MadeWithUnityText(),
            "The engine attribution is not the wording Unity's guidelines allow.");
    }

    [Test]
    public void TheNonAffiliationNoticeIsUnitysOwnWording()
    {
        // Verbatim from Unity's trademark guidelines, with this application's name substituted in
        // where their template leaves a slot. Reworded, it stops being the notice they require.
        var notice = OctopusSampleAboutView.NonAffiliationNoticeText();

        StringAssert.StartsWith(Application.productName, notice);
        StringAssert.Contains(
            "is not sponsored by or affiliated with Unity Technologies or its affiliates.", notice);
        StringAssert.Contains(
            "Unity is a trademark or registered trademark of Unity Technologies or its affiliates " +
            "in the U.S. and elsewhere.", notice);
    }

    [TestCase("file:///tmp/design.html")]
    public void UnavailableDesignLinksAreAbsent(string url)
    {
        Assert.IsEmpty(OctopusSampleAboutView.ResolveDesignReferenceUrl(url));
    }

    [Test]
    public void DesignLinksRequireAnInternalBuildEvenWhenConfigured()
    {
        const string url = "https://example.invalid/design";
        Assert.AreEqual(url, OctopusSampleAboutView.ResolveDesignReferenceUrl(" " + url + " "));
#if !OCTOPUS_INTERNAL
        Assert.IsEmpty(OctopusSampleAboutView.DesignReferenceUrl());
        Assert.IsNull(Find(OctopusSampleAboutView.Open().transform, OctopusSampleAboutView.DesignReferenceRowId));
#endif
    }

    [Test]
    public void AboutHasPublicLinksAndTextAttribution()
    {
        var about = OctopusSampleAboutView.Open();
        Assert.IsNotNull(Find(about.transform, OctopusSampleAboutView.RepositoryRowId).GetComponent<Button>());
        Assert.IsNotNull(Find(about.transform, OctopusSampleAboutView.LicenceLinkId).GetComponent<Button>());
        var attribution = Find(about.transform, OctopusSampleAboutView.AttributionCardId);
        StringAssert.Contains(OctopusSampleAboutView.NonAffiliationNoticeText(),
            string.Join(" ", System.Array.ConvertAll(attribution.GetComponentsInChildren<TMP_Text>(), t => t.text)));
        Assert.IsNull(Find(attribution, "Badge"));
    }

    [Test]
    public void TheSdkVersionComesFromThePackageRatherThanTheSample()
    {
        // The value has to be the package's own constant, not `Application.version` under another
        // label: the two agreeing by accident on one machine is exactly how this row would start
        // lying. Pinning the source is what the pin guard cannot check from outside.
        Assert.AreEqual(OctopusSDK.Version, OctopusSampleAboutView.SdkVersionText());
        Assert.IsNotEmpty(OctopusSDK.Version, "The package's runtime version constant is empty.");
    }

    [Test]
    public void AboutStatesTheSdkVersionBesideTheOthers()
    {
        // `BuildValueRow` names each row's GameObject after its label, which is the only handle
        // this assembly has: it cannot reference `UnityEngine.UI`, so the rendered string is out
        // of reach here and is what the visual QA pass reads instead. What is checked mechanically
        // is that the row is built at all, and — in the test above — that the value it is built
        // from is the package's constant rather than the sample's own version under a new label.
        Shell();

        var about = OctopusSampleAboutView.Open();
        var card = Find(about.transform, OctopusSampleAboutView.VersionsCardId);

        Assert.IsNotNull(card, "About shows no versions card.");
        Assert.IsNotNull(Find(card, "SDK version"), "About shows no `SDK version` row.");
        Assert.IsNotNull(Find(card, "Sample version"), "About lost its `Sample version` row.");
        Assert.IsNotNull(Find(card, "Unity version"), "About lost its `Unity version` row.");
    }

    [Test]
    public void ArrivingOnTheTabCallsNothingAndChangesNothing()
    {
        OctopusSampleState.Reset();
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;

        Shell();

        Assert.IsEmpty(probe.Methods,
            "Building the Settings tab reached the SDK: " + string.Join(", ", probe.Methods.ToArray()));
        Assert.IsFalse(OctopusSampleState.IsInitialized,
            "Building the Settings tab changed the sample's recorded state.");
    }

    [Test]
    public void RegisteringTheSameIdTwiceReplacesTheRowRatherThanDoublingIt()
    {
        // In the editor a domain reload re-runs every [RuntimeInitializeOnLoadMethod] against a
        // registry that is not always empty. Two identical 'Developer tools' rows is the shape of
        // bug nobody reads a stack trace for.
        var view = View();
        OctopusSampleSettingsRows.Register("settings-devtools-row", "Developer tools", "first", () => { });
        OctopusSampleSettingsRows.Register("settings-devtools-row", "Developer tools", "second", () => { });

        CollectionAssert.AreEqual(
            new[] { "settings-devtools-row", OctopusSampleSettingsView.AboutRowId },
            view.SupportRowIds,
            "The registry appended a duplicate instead of replacing the row under that id.");
    }

    [Test]
    public void ARowWithNoActionStillRenders()
    {
        var view = View();
        OctopusSampleSettingsRows.Register("settings-note-row", "A fact", "with no affordance", null);

        CollectionAssert.Contains(view.SupportRowIds, "settings-note-row",
            "A row registered without an action was dropped rather than drawn as a plain statement.");
    }

    private sealed class ProbeLog : IOctopusSampleLog
    {
        public readonly List<string> Methods = new List<string>();
        public void LogApiCall(string method, string detail = null) { Methods.Add(method); }
        public void LogStateChange(string headline, string detail = null) { }
    }
}
