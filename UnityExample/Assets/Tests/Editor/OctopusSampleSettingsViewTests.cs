using TMPro;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Settings tab's contract: a support surface that states no SDK fact Home already states,
/// an open Support group that code outside `Scripts/` fills, and a reset that asks first.
///
/// Assertions go through <c>GameObject.name</c> and the view's own readers, because the EditMode
/// assembly overrides its references down to `nunit.framework.dll` and can name no
/// `UnityEngine.UI` type. It also cannot name anything under `Assets/Debug/` — a different
/// assembly, and one the public mirror does not ship — so the `Developer tools` row is tested here
/// as "whatever registers appears", with its real registration covered on the `Debug/` side.
/// </summary>
public class OctopusSampleSettingsViewTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        OctopusSampleState.Reset();
        // A registry is static and survives a test: without this, the row a previous test
        // registered is still on the Support group when the next one counts it.
        OctopusSampleSettingsRows.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var host in _spawned)
        {
            if (host != null) Object.DestroyImmediate(host);
        }
        _spawned.Clear();

        var about = Object.FindAnyObjectByType<OctopusSampleAboutView>();
        if (about != null) Object.DestroyImmediate(about.gameObject);

        var appearance = Object.FindAnyObjectByType<OctopusSampleAppearanceView>();
        if (appearance != null) appearance.Close();

        OctopusSampleSettingsRows.Clear();
        OctopusScenarioSdk.Use(null);
        OctopusSampleState.Reset();
        OctopusSampleLog.Current = OctopusSampleLog.None;
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
    }

    [Test]
    public void TheTabRendersItsGroupsAndItsFooter()
    {
        var shell = Shell();

        foreach (var id in new[]
                 {
                     OctopusSampleSettingsView.ScreenId,
                     OctopusSampleSettingsView.IntroCardId,
                     OctopusSampleSettingsView.SupportCardId,
                     OctopusSampleSettingsView.AboutRowId,
                     OctopusSampleSettingsView.ResetCardId,
                     OctopusSampleSettingsView.ResetButtonId,
                     OctopusSampleSettingsView.VersionLabelId,
                     OctopusSampleSettingsView.AppearanceRowId,
                     OctopusSampleSettingsView.LanguageRowId,
                 })
        {
            Assert.IsNotNull(Find(shell.transform, id),
                "Nothing in the Settings tab is named '" + id + "' — the QA pipeline addresses " +
                "it by that name.");
        }
    }

    [Test]
    public void TheRouteToTheLegacyScenesSurvivesTheTabBeingBuilt()
    {
        // The Settings tab is the only way back to the four legacy scenes the Scenarios tab does
        // not list (issue #100). Replacing the placeholder with a real screen is exactly the moment
        // that route is easiest to drop without noticing.
        var shell = Shell();

        Assert.IsNotNull(Find(shell.transform, OctopusSampleShell.LegacyScenesId),
            "The 'Legacy demo scenes' route is gone from the Settings tab; four scenes are now " +
            "unreachable in the player while their scripts still compile.");
    }

    [Test]
    public void ArrivingOnTheTabCallsNothingAndChangesNothing()
    {
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;

        Shell();

        Assert.IsEmpty(probe.Methods,
            "Building the Settings tab reached the SDK: " + string.Join(", ", probe.Methods.ToArray()));
        Assert.IsFalse(OctopusSampleState.IsInitialized,
            "Building the Settings tab changed the sample's recorded state.");
    }

    [Test]
    public void AboutIsTheLastSupportRowWhenNothingElseRegistered()
    {
        var view = View();

        CollectionAssert.AreEqual(new[] { OctopusSampleSettingsView.AboutRowId },
                                  view.SupportRowIds,
                                  "With an empty registry the Support group should hold About alone.");
    }

    [Test]
    public void ARegisteredRowLandsAboveAboutWithoutTheScreenNamingIt()
    {
        // This is the whole point of the seam: `Assets/Debug/` is export-ignore'd from the public
        // mirror, so the Settings screen may not name a type from it. The row has to arrive from
        // the other side, and it has to arrive on a screen that is already built.
        var view = View();
        OctopusSampleSettingsRows.Register("settings-devtools-row", "Developer tools", "Debug console", () => { });

        CollectionAssert.AreEqual(
            new[] { "settings-devtools-row", OctopusSampleSettingsView.AboutRowId },
            view.SupportRowIds,
            "A row registered after the screen was built did not reach it, or displaced About.");
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

    [Test]
    public void TheResetAsksBeforeItClears()
    {
        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "ok");
        var view = View();

        view.RequestReset();

        Assert.IsTrue(view.IsConfirmingReset, "The reset did not arm.");
        Assert.AreEqual(OctopusSampleState.Session.ConnectCompleted,
            OctopusSampleState.ConnectionSession,
            "Asking for a reset already cleared the state; the confirm step decides nothing.");
        Assert.IsNotNull(Find(view.transform, OctopusSampleSettingsView.ResetConfirmId),
            "The armed reset draws no '" + OctopusSampleSettingsView.ResetConfirmId + "' control.");
        Assert.IsNotNull(Find(view.transform, OctopusSampleSettingsView.ResetCancelId),
            "The armed reset offers no way out.");
    }

    [Test]
    public void CancellingLeavesTheStateAlone()
    {
        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "ok");
        var view = View();

        view.RequestReset();
        view.CancelReset();

        Assert.IsFalse(view.IsConfirmingReset, "The reset stayed armed after being cancelled.");
        StringAssert.Contains("cancelled", view.ResetResult);
        Assert.AreEqual(OctopusSampleState.Session.ConnectCompleted,
            OctopusSampleState.ConnectionSession, "Cancelling a reset cleared the state anyway.");
        Assert.IsNotNull(Find(view.transform, OctopusSampleSettingsView.ResetButtonId),
            "Cancelling left no way to ask again.");
    }

    [Test]
    public void ConfirmingClearsTheSdksAnswersAndMakesNoCall()
    {
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;
        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "ok");
        OctopusSampleState.ReportUnseenNotifications(4);
        var view = View();

        view.RequestReset();
        view.ConfirmReset();

        Assert.IsFalse(view.IsConfirmingReset, "The reset stayed armed after being carried out.");
        StringAssert.Contains("Sample state cleared", view.ResetResult);
        Assert.IsNotNull(Find(view.transform, OctopusSampleSettingsView.ResetResultId));
        Assert.AreEqual(OctopusSampleState.Session.None, OctopusSampleState.ConnectionSession,
            "The reset left the last connection call behind.");
        Assert.Less(OctopusSampleState.UnseenNotifications, 0,
            "The reset left a published unseen count rather than the not-yet-published sentinel.");
        // The guarantee behind the name: nothing leaves the device, so nothing is deleted on the
        // server and nothing is torn down.
        Assert.IsEmpty(probe.Methods,
            "Resetting the sample state reached the SDK: " + string.Join(", ", probe.Methods.ToArray()));
    }

    [Test]
    public void ConfirmingKeepsWhatTheSdkIsStillConfiguredWith()
    {
        // The reset clears answers, not configuration. Initialisation and a locale override are
        // facts about the SDK that this button cannot undo, and forgetting them would only make the
        // Home dashboard disagree with the app the reader is looking at.
        OctopusSampleState.ReportInitialized("SSO");
        OctopusSampleState.ReportLocaleOverride("fr");
        var view = View();

        view.RequestReset();
        view.ConfirmReset();

        Assert.IsTrue(OctopusSampleState.IsInitialized,
            "The reset cleared the initialisation flag, which is the guard against a second Initialize.");
        Assert.AreEqual("SSO", OctopusSampleState.ModeLabel, "The reset forgot the mode it was initialised in.");
        Assert.AreEqual("fr", OctopusSampleState.LocaleOverride,
            "The reset forgot a locale override the SDK is still applying.");
    }

    [Test]
    public void AResetBetweenTwoVisitsToTheCommunityDoesNotInitializeTwice()
    {
        // The sequence the review found: open Community, reset, open Community. With the button
        // wired to OctopusSampleState.Reset it cleared the initialisation guard, and the second
        // visit called Initialize again — a second channel and a second set of collectors on the
        // native side, from a control that claims to touch nothing but the sample's own record.
        var sdk = new OctopusRecordingScenarioSdk
        {
            Profile = new OctopusExampleConfig.ExampleProfile { apiKey = "test-key" }
        };
        OctopusScenarioSdk.Use(sdk);

        var shell = Shell();
        shell.Select(OctopusSampleTab.Community);
        shell.GetComponentInChildren<OctopusSampleCommunityView>().OpenCommunity();
        Assert.AreEqual(1, CountOf(sdk, "Initialize"), "The first visit did not initialise once.");

        shell.Select(OctopusSampleTab.Settings);
        var settings = shell.GetComponentInChildren<OctopusSampleSettingsView>();
        settings.RequestReset();
        settings.ConfirmReset();

        shell.Select(OctopusSampleTab.Community);
        shell.GetComponentInChildren<OctopusSampleCommunityView>().OpenCommunity();

        Assert.AreEqual(1, CountOf(sdk, "Initialize"),
            "The SDK was initialised again after a reset: " + string.Join(", ", sdk.ScenarioMethods));
    }

    private static int CountOf(OctopusRecordingScenarioSdk sdk, string method)
    {
        var count = 0;
        foreach (var name in sdk.ScenarioMethods)
        {
            if (name == method) count++;
        }
        return count;
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

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not a URL")]
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

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void AppearanceOpensFromSettingsAndChangesOnlyTheSampleTheme(OctopusSampleTheme theme)
    {
        OctopusSampleBranding.Theme = theme;
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;
        var shell = Shell();
        Find(shell.transform, OctopusSampleSettingsView.AppearanceRowId).GetComponent<Button>().onClick.Invoke();
        var appearance = Object.FindAnyObjectByType<OctopusSampleAppearanceView>();
        Assert.IsNotNull(appearance);
        Assert.AreEqual(theme, OctopusSampleBranding.Theme, "Opening the route changed the theme.");
        Assert.AreSame(appearance, OctopusSampleAppearanceView.Open());
        Find(appearance.transform, OctopusSampleAppearanceView.ShellThemeToggleId).GetComponent<Button>().onClick.Invoke();
        Assert.AreNotEqual(theme, OctopusSampleBranding.Theme);
        Assert.IsTrue(OctopusSampleAppearanceView.IsOpen, "A theme rebuild closed Appearance.");
        Find(appearance.transform, OctopusSampleAppearanceView.BackId).GetComponent<Button>().onClick.Invoke();
        Assert.IsFalse(OctopusSampleAppearanceView.IsOpen);
        Assert.AreEqual(OctopusSampleTab.Settings, shell.Selected);
        Assert.IsEmpty(probe.Methods);
    }

    [TestCase(OctopusSampleTheme.Light, OctopusSampleAppearanceView.LightId)]
    [TestCase(OctopusSampleTheme.Dark, OctopusSampleAppearanceView.DarkId)]
    public void AppearanceChoiceSurvivesNavigationAndReopening(OctopusSampleTheme theme, string id)
    {
        var sdk = new OctopusRecordingScenarioSdk();
        OctopusScenarioSdk.Use(sdk);
        OctopusSampleBranding.Theme = theme == OctopusSampleTheme.Light
            ? OctopusSampleTheme.Dark : OctopusSampleTheme.Light;
        var shell = Shell();
        Find(shell.transform, OctopusSampleSettingsView.AppearanceRowId).GetComponent<Button>().onClick.Invoke();
        var appearance = Object.FindAnyObjectByType<OctopusSampleAppearanceView>();
        Assert.IsNotNull(Find(appearance.transform, OctopusSampleAppearanceView.ThemeToggleId));
        Find(appearance.transform, id).GetComponent<Button>().onClick.Invoke();
        Assert.AreEqual(theme, OctopusSampleBranding.Theme);
        // Selecting the already selected value must not flip it back.
        Find(appearance.transform, id).GetComponent<Button>().onClick.Invoke();
        Assert.AreEqual(theme, OctopusSampleBranding.Theme);
        appearance.Close();
        shell.Select(OctopusSampleTab.Home);
        shell.Select(OctopusSampleTab.Settings);
        Assert.AreEqual(theme.ToString(),
            Find(shell.transform, "settings-appearance-value").GetComponent<TMP_Text>().text);
        Find(shell.transform, OctopusSampleSettingsView.AppearanceRowId).GetComponent<Button>().onClick.Invoke();
        appearance = Object.FindAnyObjectByType<OctopusSampleAppearanceView>();
        Assert.AreEqual(theme, OctopusSampleBranding.Theme);
        Assert.IsFalse(System.Array.Exists(appearance.GetComponentsInChildren<TMP_Text>(), t => t.text == "System"));
        Assert.IsEmpty(sdk.ScenarioMethods);
    }

    [Test]
    public void PreferenceRowsShowCurrentValuesWithoutInlineEditors()
    {
        OctopusScenarioSdk.Use(new OctopusRecordingScenarioSdk());
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
        var host = new GameObject("Preferences test", typeof(RectTransform));
        _spawned.Add(host);
        var view = OctopusSampleSettingsView.BuildInto((RectTransform)host.transform);
        var appearance = Find(view.transform, OctopusSampleSettingsView.AppearanceRowId);
        var language = Find(view.transform, OctopusSampleSettingsView.LanguageRowId);
        Assert.AreEqual("Dark", Find(appearance, "settings-appearance-value").GetComponent<TMP_Text>().text);
        Assert.AreEqual("System default (no override)",
            Find(language, "settings-language-value").GetComponent<TMP_Text>().text);
        Assert.IsEmpty(language.GetComponentsInChildren<Selectable>(true));
        Assert.IsEmpty(appearance.GetComponentsInChildren<TMP_InputField>(true));
        Assert.IsEmpty(appearance.GetComponentsInChildren<Toggle>(true));

        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        OctopusSampleState.ReportLocaleOverride("en");
        Assert.AreEqual("Light", Find(appearance, "settings-appearance-value").GetComponent<TMP_Text>().text);
        Assert.AreEqual("en", Find(language, "settings-language-value").GetComponent<TMP_Text>().text);
        OctopusSampleState.ReportLocaleOverride(null);
        Assert.AreEqual("System default (no override)",
            Find(language, "settings-language-value").GetComponent<TMP_Text>().text);
    }

    [Test]
    public void LanguageIsReadOnlyAndFollowsTheRecordedOverride()
    {
        var view = View();
        var row = Find(view.transform, OctopusSampleSettingsView.LanguageRowId);
        Assert.IsNull(row.GetComponent<Button>());
        StringAssert.Contains("System default", OctopusSampleSettingsView.LanguageText());
        OctopusSampleState.ReportLocaleOverride("fr");
        Assert.AreEqual("fr", OctopusSampleSettingsView.LanguageText());
        Assert.IsTrue(System.Array.Exists(row.GetComponentsInChildren<TMP_Text>(), t => t.text == "fr"));
    }

    [Test]
    public void ConfirmWithoutARequestCannotResetAndResetKeepsAppearance()
    {
        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "ok");
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        var view = View();
        view.ConfirmReset();
        Assert.AreEqual(OctopusSampleState.Session.ConnectCompleted, OctopusSampleState.ConnectionSession);
        view.RequestReset();
        view.ConfirmReset();
        Assert.AreEqual(OctopusSampleTheme.Light, OctopusSampleBranding.Theme);
        Assert.IsNotNull(Find(Find(view.transform, OctopusSampleSettingsView.ResetCardId), "Stroke"));
        view.RequestReset();
        Assert.IsEmpty(view.ResetResult);
    }

    [Test]
    public void TheFooterNamesTheBuildAndTheSdk()
    {
        // It used to assert the opposite: nothing in `UnityPackage/` published a version at
        // runtime, so an SDK number here could only be a hardcoded string going stale at the next
        // bump. #116 added `OctopusSDK.Version`, kept equal to `package.json` by
        // `ci/native-pins/verify-native-pins.sh`, so the footer can now name all three.
        var text = OctopusSampleSettingsView.VersionLabelText();

        StringAssert.Contains("Sample ", text);
        StringAssert.Contains("SDK ", text);
        StringAssert.Contains("Unity ", text);
        StringAssert.Contains(Application.unityVersion, text);
        StringAssert.Contains(OctopusSDK.Version, text);
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
    public void LeavingTheTabStopsItListening()
    {
        var shell = Shell();
        var view = shell.GetComponentInChildren<OctopusSampleSettingsView>();
        OctopusSampleSettingsRows.Register("settings-probe-row", "Probe", null, () => { });
        var whileOnScreen = view.RowsEventCount;
        Assert.Greater(whileOnScreen, 0, "The view never heard the registry at all.");

        // Edit mode delivers neither OnDisable nor OnDestroy to a plain MonoBehaviour, so the view
        // is still subscribed the moment it is destroyed; the guarantee is that it drops itself on
        // the first event that reaches it afterwards, which is the path a scene unload takes too.
        shell.Select(OctopusSampleTab.Home);
        OctopusSampleSettingsRows.Register("settings-probe-row-2", "Probe", null, () => { });
        var afterUnhooking = view.RowsEventCount;
        OctopusSampleSettingsRows.Register("settings-probe-row-3", "Probe", null, () => { });

        Assert.AreEqual(afterUnhooking, view.RowsEventCount,
            "The destroyed Settings view is still on OctopusSampleSettingsRows.Changed.");
    }

    private OctopusSampleShell Shell()
    {
        var shell = OctopusSampleShell.Create();
        _spawned.Add(shell.gameObject);
        shell.Select(OctopusSampleTab.Settings);
        return shell;
    }

    private OctopusSampleSettingsView View()
    {
        var view = Shell().GetComponentInChildren<OctopusSampleSettingsView>();
        Assert.IsNotNull(view, "The Settings tab built no view.");
        return view;
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
