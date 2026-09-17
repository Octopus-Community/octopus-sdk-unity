using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.TestTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Proves the three parts of the SDK_STANDARDS §5.2 contract that are observable without a device:
/// the QA handles exist under the exact names the shared catalogue publishes, the screen is usable
/// the moment it opens, and opening it mutates nothing.
///
/// Controls are located by GameObject.name, then their real uGUI events are invoked so the tests
/// cover the same listeners as a user's taps and edits. SDK calls use a recorder, never a network.
///
/// These are EditMode tests, so `Start` never runs; <see cref="OctopusScenarioScreenView.Open"/>
/// builds the screen itself, which is why it can be tested at all.
/// </summary>
public class OctopusScenarioScreenViewTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private OctopusRecordingScenarioSdk _sdk;
    private OctopusSampleTheme _previousTheme;

    [SetUp]
    public void SetUp()
    {
        _previousTheme = OctopusSampleBranding.Theme;
        OctopusSampleState.Reset();
        OctopusSampleFeatureToggles.Reset();
        _sdk = new OctopusRecordingScenarioSdk
        {
            Profile = new OctopusExampleConfig.ExampleProfile { apiKey = "test-key", authToken = "not-a-real-token" }
        };
        OctopusScenarioSdk.Use(_sdk);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var host in _spawned)
        {
            if (host != null) Object.DestroyImmediate(host);
        }
        _spawned.Clear();
        OctopusSampleLog.Current = OctopusSampleLog.None;
        OctopusScenarioSdk.Use(null);
        OctopusSampleState.Reset();
        OctopusSampleFeatureToggles.Reset();
        OctopusSampleBranding.Theme = _previousTheme;
    }

    [Test]
    public void DebugEntryStaysAboveOverlaysWhileScenarioIsRunningAndAfterDismissal()
    {
        var owner = new GameObject("DebugEntryOwner");
        _spawned.Add(owner);
        SampleUi.OverlayCanvas(owner, 900);
        var opened = 0;
        var entry = SampleUi.Button("debug-open-button", owner.transform, "Debug", () => opened++);
        SampleUi.RegisterDebugEntry(entry);
        try
        {
            var pilot = new DeferredCustomPilot();
            var view = OpenFor(pilot);
            Tap(view, "groups-customize");
            Tap(view, "groups-run");
            Assert.IsTrue(pilot.IsRunning);
            Assert.AreEqual("Header", entry.parent.name);
            var canvas = entry.GetComponent<Canvas>();
            Assert.IsTrue(canvas.overrideSorting);
            Assert.Greater(canvas.sortingOrder, view.GetComponent<Canvas>().sortingOrder);
            Assert.Greater(canvas.sortingOrder, 1200, "Also clear the profile overlay.");
            Assert.IsNotNull(entry.GetComponent<GraphicRaycaster>());
            entry.GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(1, opened);
            Assert.IsTrue(pilot.IsRunning);
            pilot.Complete();
            Assert.AreEqual("Completed default", Result(view).text);
            view.Dismiss();
            Assert.IsTrue(entry.gameObject.activeInHierarchy);
            Assert.AreSame(owner.transform, entry.parent);
            Assert.AreSame(canvas, entry.GetComponent<Canvas>());
            Assert.Greater(canvas.sortingOrder, 1200);
            Assert.AreEqual(1, entry.GetComponents<GraphicRaycaster>().Length);
        }
        finally
        {
            SampleUiDebugEntryHost.ReleaseAll();
            SampleUi.RegisterDebugEntry(null);
        }
    }

    [Test]
    public void CustomizedEntitlementsPresetKeepsTheNotSentCaveat()
    {
        var pilot = new ConnectionScenario();
        var view = OpenFor(pilot);
        Tap(view, pilot.Presets[3].TestId);
        Tap(view, "connection-customize");
        _sdk.Clear();
        Tap(view, "connection-run");
        CollectionAssert.AreEqual(new[] { "ConnectUser" }, _sdk.ScenarioMethods);
        StringAssert.Contains("premium", Result(view).text);
        StringAssert.Contains("moderator", Result(view).text);
        StringAssert.Contains("were NOT sent", Result(view).text);
    }

    [Test]
    public void ScenarioChromeBleedsOutsideTheSafeAreaAndUsesTheSharedHeaderHeight()
    {
        var view = OpenFor(new ConnectionScenario());
        var safe = Find(view.transform, "scenario-safe-area");
        foreach (var name in new[] { "TopBleed", "BottomBleed" })
        {
            var bleed = Find(view.transform, name);
            Assert.AreSame(safe.parent, bleed.parent);
            Assert.AreEqual(name == "TopBleed" ? OctopusSampleBranding.Palette.Chrome
                : OctopusSampleBranding.Palette.Surface, bleed.GetComponent<Image>().color);
        }
        Assert.AreEqual(OctopusSampleBranding.AppBarUnits,
            ((RectTransform)Find(view.transform, "Header")).sizeDelta.y);
    }

    [Test]
    public void ConnectionScreenExposesTheCatalogPresetIds()
    {
        AssertHandles("connection",
                      new[]
                      {
                          "qa-preset-connection-1",
                          "qa-preset-connection-2",
                          "qa-preset-connection-3",
                          "qa-preset-connection-4",
                          "qa-preset-connection-5",
                      },
                      "connection-result");
    }

    [Test]
    public void CustomEventsScreenExposesTheCatalogPresetIds()
    {
        AssertHandles("customEvents",
                      new[] { "qa-preset-customEvents-1", "qa-preset-customEvents-2" },
                      "customEvents-result");
    }

    [Test]
    public void LocaleScreenExposesTheCatalogPresetIds()
    {
        AssertHandles("locale",
                      new[] { "qa-preset-locale-1", "qa-preset-locale-2", "qa-preset-locale-3" },
                      "locale-result");
    }

    [Test]
    public void OpeningAScreenMakesNoSdkCall()
    {
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;

        foreach (var id in OctopusScenarioPilots.Ids)
        {
            Open(id);
            TearDownSpawned();
        }

        Assert.IsEmpty(probe.Methods,
            "Opening a scenario screen reached the SDK: " + string.Join(", ", probe.Methods) +
            " — §5.2 asks for no mutation without a user action.");
    }

    [Test]
    public void AScreenIsUsableOnEntryWithEveryFieldPreFilled()
    {
        foreach (var id in OctopusScenarioPilots.Ids)
        {
            var pilot = OctopusScenarioPilots.Create(id);
            OpenFor(pilot);

            foreach (var field in pilot.Fields.All)
            {
                Assert.IsNotEmpty(field.Value,
                    "Scenario '" + id + "' opens with field '" + field.Key + "' empty.");
            }
            TearDownSpawned();
        }
    }

    [Test]
    public void EveryPresetHasAnInputForEveryFieldItFills()
    {
        foreach (var id in OctopusScenarioPilots.Ids)
        {
            var pilot = OctopusScenarioPilots.Create(id);
            var view = OpenFor(pilot);

            foreach (var field in pilot.Fields.All)
            {
                var name = id + "-field-" + field.Key;
                Assert.IsNotNull(Find(view.gameObject.transform, name),
                    "Scenario '" + id + "' has no input named '" + name + "'.");
            }
            TearDownSpawned();
        }
    }

    [Test]
    public void QaPresetUsesTheButtonPathAndLogsTheDisplayedResult()
    {
        LogAssert.Expect(LogType.Log, "[OctopusQA] scenario=customEvents state=opened");
        LogAssert.Expect(LogType.Log, "[OctopusQA] scenario=customEvents result=Ready — no SDK call made yet. Tap a preset to run one.");
        var view = Open("customEvents");
        LogAssert.Expect(LogType.Log, "[OctopusQA] scenario=customEvents preset=1 state=running");
        LogAssert.Expect(LogType.Log, "[OctopusQA] scenario=customEvents result=Running…");
        LogAssert.Expect(LogType.Log, "[OctopusQA] scenario=customEvents result=Tracked 'qa_sample_event' with 0 properties (mode: SSO).");
        Assert.IsTrue(((IOctopusSampleQaScenario)view).RunPreset(1));
        CollectionAssert.AreEqual(new[] { "Initialize", "Track" }, _sdk.ScenarioMethods);
        Assert.AreEqual("Tracked 'qa_sample_event' with 0 properties (mode: SSO).", Result(view).text);
        _sdk.Clear();
        Assert.IsFalse(((IOctopusSampleQaScenario)view).RunPreset(999));
        Assert.IsEmpty(_sdk.Calls);
    }

    [Test]
    public void QaPresetNumberAlsoAddressesNamedCatalogActions()
    {
        var pilot = new DeferredCustomPilot("qa-preset-profileFieldsLock-clear");
        var view = OpenFor(pilot);
        Assert.IsTrue(((IOctopusSampleQaScenario)view).RunPreset(1));
        Assert.AreEqual(1, pilot.PresetRunCount);
        Assert.IsFalse(((IOctopusSampleQaScenario)view).RunPreset(0));
        Assert.IsFalse(((IOctopusSampleQaScenario)view).RunPreset(2));
        Assert.AreEqual(1, pilot.PresetRunCount);
    }

    [Test]
    public void QaNavigationUsesTheShellListAndRegisteredPilot()
    {
        var shell = OctopusSampleShell.Create();
        _spawned.Add(shell.gameObject);
        try
        {
            var options = OctopusSampleQaLaunchOptions.Parse(new Dictionary<string, string>
            {
                { "qaScenario", "customEvents" }, { "qaPreset", "1" }
            });
            var request = new OctopusSampleQaRequest(options);
            Assert.IsFalse(request.Apply(shell).MoveNext(), "EditMode builds the tab synchronously.");
            var view = Object.FindAnyObjectByType<OctopusScenarioScreenView>();
            if (view != null) _spawned.Add(view.gameObject);
            Assert.IsNotNull(view);
            Assert.AreEqual(OctopusSampleTab.Scenarios, shell.Selected);
            Assert.AreEqual("customEvents", view.ScenarioId);
            CollectionAssert.AreEqual(new[] { "Initialize", "Track" }, _sdk.ScenarioMethods);
            _sdk.Clear();
            Assert.IsFalse(request.Apply(shell).MoveNext());
            Assert.IsEmpty(_sdk.Calls);
        }
        finally { shell.Shutdown(); }
    }

    [Test]
    public void QaResultLogsStaleTextOnceAndDoesNotRepeatItForThemeRebuilds()
    {
        var view = Open("customEvents");
        ((IOctopusSampleQaScenario)view).RunPreset(1);
        var results = new List<string>();
        Application.LogCallback capture = (message, stack, type) =>
        {
            if (message.StartsWith("[OctopusQA] scenario=customEvents result=")) results.Add(message);
        };
        Application.logMessageReceived += capture;
        try
        {
            Tap(view, "customEvents-customize");
            Field(view, "customEvents-field-eventName").text = "edited_event";
            OctopusSampleBranding.Theme = OctopusSampleBranding.Theme == OctopusSampleTheme.Light
                ? OctopusSampleTheme.Dark : OctopusSampleTheme.Light;
            CollectionAssert.AreEqual(new[]
            {
                "[OctopusQA] scenario=customEvents result=Not run yet — values changed Run these values to refresh the result."
            }, results);
        }
        finally { Application.logMessageReceived -= capture; }
    }

    [Test]
    public void ASecondOpenReturnsTheScreenAlreadyOpen()
    {
        var first = Open("locale");
        var second = OctopusScenarioScreenView.Open(OctopusScenarioPilots.Create("connection"));

        Assert.AreSame(first, second,
            "A second Open built a new screen on top of the open one.");
    }

    [TestCase("locale")]
    [TestCase("customEvents")]
    public void CustomizeUnlocksAllFieldsAndExposesCustomRun(string id)
    {
        var view = Open(id);
        var inputs = view.GetComponentsInChildren<TMP_InputField>();
        Assert.IsNotEmpty(inputs);
        foreach (var input in inputs)
        {
            Assert.IsTrue(input.readOnly);
            Assert.IsFalse(input.interactable, "Locked fields must not open a mobile keyboard.");
        }
        var run = Find(view.transform, id + "-run");
        Assert.IsNotNull(run);
        Assert.IsFalse(run.gameObject.activeSelf);

        Tap(view, id + "-customize");

        foreach (var input in inputs)
        {
            Assert.IsFalse(input.readOnly);
            Assert.IsTrue(input.interactable);
        }
        Assert.IsTrue(run.gameObject.activeSelf);
        Assert.IsEmpty(_sdk.Calls, "Unlocking must not run the scenario.");
    }

    [Test]
    public void CustomLocaleRunUsesEditedCodeWithoutRefillingPreset()
    {
        var view = Open("locale");
        Tap(view, "locale-customize");
        Field(view, "locale-field-locale").text = "es";
        Tap(view, "locale-run");

        CollectionAssert.AreEqual(new[] { "Initialize", "OverrideDefaultLocale" }, _sdk.ScenarioMethods);
        Assert.AreEqual("es", _sdk.Last.Args[0]);
        Assert.AreEqual("es", Field(view, "locale-field-locale").text);
        StringAssert.Contains("'es'", Result(view).text);
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void CustomRunStaysInParametersAndUsesEditsAfterRebuild(OctopusSampleTheme theme)
    {
        OctopusSampleBranding.Theme = theme;
        var view = Open("locale");
        Tap(view, "locale-customize");
        Field(view, "locale-field-locale").text = "es";
        OctopusSampleBranding.Theme = theme == OctopusSampleTheme.Light
            ? OctopusSampleTheme.Dark : OctopusSampleTheme.Light;

        var parameters = Find(view.transform, "ParametersSection");
        var run = Find(view.transform, "locale-run");
        var result = Find(view.transform, "locale-result");
        Assert.AreEqual(parameters, run.parent);
        Assert.AreEqual(parameters.childCount - 1, run.GetSiblingIndex());
        Assert.AreEqual(parameters.parent, result.parent);
        Assert.Less(parameters.GetSiblingIndex(), result.GetSiblingIndex());
        Assert.AreEqual(1, CountNamed(view.transform, "locale-run"));
        Assert.IsTrue(run.gameObject.activeInHierarchy);
        Assert.IsEmpty(_sdk.ScenarioMethods, "Rebuilding must not run the edited parameters.");

        Tap(view, "locale-run");

        CollectionAssert.AreEqual(new[] { "Initialize", "OverrideDefaultLocale" }, _sdk.ScenarioMethods);
        Assert.AreEqual("es", _sdk.Last.Args[0]);
        StringAssert.Contains("'es'", Result(view).text);
        Tap(view, "locale-customize");
        Assert.IsFalse(run.gameObject.activeSelf, "Reset hides Run inside Parameters.");
    }

    [Test]
    public void CustomEventRunUsesEditedNameAndEveryProperty()
    {
        var view = Open("customEvents");
        Tap(view, "customEvents-customize");
        Field(view, "customEvents-field-eventName").text = "edited_event";
        Field(view, "customEvents-field-properties").text = "level=7; source=custom";
        Tap(view, "customEvents-run");

        CollectionAssert.AreEqual(new[] { "Initialize", "Track" }, _sdk.ScenarioMethods);
        Assert.AreEqual("edited_event", _sdk.Last.Args[0]);
        CollectionAssert.AreEquivalent(new Dictionary<string, string>
        {
            { "level", "7" }, { "source", "custom" }
        }, (IDictionary<string, string>)_sdk.Last.Args[1]);
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void EditsReplaceOldResultWithMutedStaleLineUntilValuesMatchOrRun(OctopusSampleTheme theme)
    {
        OctopusSampleBranding.Theme = theme;
        var view = Open("locale");
        Tap(view, "qa-preset-locale-1");
        var original = Result(view).text;
        Tap(view, "locale-customize");
        Assert.AreEqual(original, Result(view).text, "Unlocking alone is not an edit.");
        var field = Field(view, "locale-field-locale");
        field.text = "es";

        StringAssert.StartsWith("Not run yet — values changed\nRun these values", Result(view).text);
        Assert.AreEqual(OctopusSampleBranding.Palette.Muted, Result(view).color);
        field.text = "fr";
        Assert.AreEqual(original, Result(view).text, "Staleness compares values, not edit counts.");
        Assert.IsFalse(field.readOnly, "Typing a preset value must not leave Customize.");
        field.text = "es";
        Tap(view, "locale-run");
        StringAssert.Contains("'es'", Result(view).text);
        Assert.AreEqual(SampleUi.TitleColor, Result(view).color);
    }

    [Test]
    public void EditingBeforeAnyRunKeepsTheReadyResultAndMakesNoCall()
    {
        var view = Open("locale");
        var ready = Result(view).text;
        Tap(view, "locale-customize");
        Field(view, "locale-field-locale").text = "es";
        Assert.AreEqual(ready, Result(view).text);
        Assert.IsEmpty(_sdk.Calls);
    }

    [Test]
    public void ResetRestoresLastSelectedPresetAndLocksWithoutCallingSdk()
    {
        var view = Open("locale");
        Tap(view, "qa-preset-locale-2");
        Tap(view, "locale-customize");
        Field(view, "locale-field-locale").text = "fr";
        Field(view, "locale-field-locale").text = "es";
        Tap(view, "locale-run");
        _sdk.Clear();

        Tap(view, "locale-customize"); // Now labelled Reset to preset.

        Assert.AreEqual("en", Field(view, "locale-field-locale").text);
        Assert.IsTrue(Field(view, "locale-field-locale").readOnly);
        Assert.IsFalse(Field(view, "locale-field-locale").interactable);
        Assert.IsFalse(Find(view.transform, "locale-run").gameObject.activeSelf);
        StringAssert.StartsWith("Not run yet — values changed\nRun these values", Result(view).text);
        Assert.IsEmpty(_sdk.Calls);
    }

    [Test]
    public void PresetAfterCustomRunRefillsEveryFieldLocksAndClearsStaleness()
    {
        var view = Open("customEvents");
        Tap(view, "customEvents-customize");
        Field(view, "customEvents-field-eventName").text = "edited_event";
        Field(view, "customEvents-field-properties").text = "level=7";
        Tap(view, "customEvents-run");
        Field(view, "customEvents-field-eventName").text = "another_event";

        Tap(view, "qa-preset-customEvents-1");

        Assert.AreEqual("qa_sample_event", _sdk.Last.Args[0]);
        Assert.IsEmpty((IDictionary<string, string>)_sdk.Last.Args[1]);
        Assert.AreEqual("qa_sample_event", Field(view, "customEvents-field-eventName").text);
        Assert.AreEqual("(none)", Field(view, "customEvents-field-properties").text);
        foreach (var input in view.GetComponentsInChildren<TMP_InputField>()) Assert.IsTrue(input.readOnly);
        Assert.IsFalse(Find(view.transform, "customEvents-run").gameObject.activeSelf);
        StringAssert.Contains("Tracked 'qa_sample_event'", Result(view).text);
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void ConnectionCustomizeOnlyOffersTheConsumedAction(OctopusSampleTheme theme)
    {
        OctopusSampleBranding.Theme = theme;
        var view = Open("connection");
        Tap(view, "connection-customize");
        var parameters = Find(view.transform, "ParametersSection");
        Assert.IsTrue(Field(view, "connection-field-action").transform.IsChildOf(parameters));
        Assert.IsFalse(Field(view, "connection-field-action").readOnly);
        foreach (var key in new[] { "profile", "entitlements" })
        {
            var input = Field(view, "connection-field-" + key);
            Assert.IsTrue(input.readOnly);
            Assert.IsFalse(input.interactable);
            Assert.IsFalse(input.transform.IsChildOf(parameters), "D2: informative context is outside Customize.");
            StringAssert.Contains("Read only", input.transform.parent.GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual(OctopusSampleBranding.Palette.DisabledSurface, input.targetGraphic.color);
            Assert.AreEqual(OctopusSampleBranding.Palette.DisabledInk, input.textComponent.color);
        }
        Assert.IsEmpty(_sdk.Calls);
    }

    [Test]
    public void AnyOptedInPilotRunsItsOwnCustomActionWithoutRefilling()
    {
        var pilot = new DeferredCustomPilot();
        var view = OpenFor(pilot);
        Tap(view, "groups-customize");
        Field(view, "groups-field-value").text = "edited";
        Tap(view, "groups-run");

        Assert.AreEqual("edited", pilot.Submitted);
        Assert.AreEqual(1, pilot.FillCount, "Only opening the screen should have filled a preset.");
        Assert.AreEqual(0, pilot.PresetRunCount, "Custom Run must use the pilot's custom action.");
    }

    [Test]
    public void LateResultCannotClearEditsMadeAfterTheRunStarted()
    {
        var pilot = new DeferredCustomPilot();
        var view = OpenFor(pilot);
        Tap(view, "groups-customize");
        Tap(view, "groups-run");
        Field(view, "groups-field-value").text = "edited while running";
        pilot.Complete();

        StringAssert.StartsWith("Not run yet — values changed\nRun these values", Result(view).text);
        Field(view, "groups-field-value").text = "default";
        Assert.AreEqual("Completed default", Result(view).text);
        Field(view, "groups-field-value").text = "next run";
        Tap(view, "groups-run");
        Assert.IsTrue(Find(view.transform, "scenario-result-skeleton").gameObject.activeSelf,
            "Run must clear staleness at call start, not at completion.");
    }

    [TestCase("connection")]
    [TestCase("locale")]
    [TestCase("customEvents")]
    public void ScreenKeepsCurrentCatalogHandlesAndZoneOrder(string id)
    {
        var view = Open(id);
        OctopusScenario row = null;
        foreach (var entry in OctopusScenarioCatalog.All)
            if (entry.Id == id) row = entry;
        Assert.IsNotNull(row);
        var actual = new List<string>();
        foreach (var button in view.GetComponentsInChildren<Button>(true))
            if (button.name.StartsWith("qa-preset-")) actual.Add(button.name);
        CollectionAssert.AreEqual(row.PresetTestIds, actual);
        Assert.AreEqual(1, CountNamed(view.transform, row.ResultTestId));

        var header = Find(view.transform, "Header");
        var parameters = Find(view.transform, "ParametersSection");
        var content = parameters.parent;
        Assert.Less(header.GetSiblingIndex(), content.GetComponentInParent<ScrollRect>().transform.GetSiblingIndex());
        var feature = Find(view.transform, "scenario-feature-state");
        if (id == "connection")
        {
            Assert.AreEqual(content, feature.parent);
            Assert.Less(feature.GetSiblingIndex(), parameters.GetSiblingIndex());
        }
        else Assert.IsNull(feature, "A section without a switch must not invent a feature state.");
        var guidance = Find(view.transform, "scenario-you-will-see");
        var presets = Find(view.transform, "PresetsSection");
        var result = Find(view.transform, row.ResultTestId);
        Assert.Less(parameters.GetSiblingIndex(), guidance.GetSiblingIndex());
        Assert.Less(guidance.GetSiblingIndex(), presets.GetSiblingIndex());
        Assert.Less(presets.GetSiblingIndex(), result.GetSiblingIndex());
        Assert.AreEqual("You will see: " + row.YouWillSee, guidance.GetComponent<TMP_Text>().text);
        Tap(view, id + "-customize");
        var run = Find(view.transform, id + "-run");
        Assert.AreEqual(parameters, run.parent, "#174: Run belongs inside Parameters.");
        Assert.AreEqual(parameters.childCount - 1, run.GetSiblingIndex());
        Assert.IsTrue(run.gameObject.activeSelf);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FeatureLineReadsStateAndReturnsToScenariosWithoutToggling(bool enabled)
    {
        OctopusSampleFeatureToggles.SetForceLogin(enabled);
        var shell = OctopusSampleShell.Create();
        _spawned.Add(shell.gameObject);
        shell.Select(OctopusSampleTab.Home);
        var view = Open("connection");
        var feature = Find(view.transform, "scenario-feature-state");
        Assert.AreEqual("Feature: Force login · " + (enabled ? "On" : "Off"),
                        feature.GetComponentInChildren<TMP_Text>().text);
        Assert.IsNull(feature.GetComponent<Toggle>());
        Assert.GreaterOrEqual(feature.GetComponent<LayoutElement>().minHeight,
                              OctopusSampleBranding.MinTouchUnits);
        Tap(view, "scenario-feature-state");
        Assert.IsTrue(view == null, "The detail overlay must close to reveal Scenarios.");
        Assert.AreEqual(OctopusSampleTab.Scenarios, shell.Selected);
        Assert.AreEqual(enabled, OctopusSampleFeatureToggles.ForceLogin);
        Assert.IsEmpty(_sdk.Calls);
    }

    [Test]
    public void MissingCatalogGuidanceUsesNeutralFallback()
    {
        var view = OpenFor(new DeferredCustomPilot());
        Assert.AreEqual("You will see: the outcome of the selected action in the result area.",
            Find(view.transform, "scenario-you-will-see").GetComponent<TMP_Text>().text);
    }

    [UnityTest]
    public IEnumerator RunningConnectionReturnsToOriginalCompletionResult()
    {
        yield return CheckConnectionTransition(false);
    }

    [UnityTest]
    public IEnumerator RunningConnectionReturnsToOriginalFailureResult()
    {
        yield return CheckConnectionTransition(true);
    }

    private IEnumerator CheckConnectionTransition(bool fail)
    {
        _sdk.DeferCompletions = true;
        var pilot = OctopusScenarioPilots.Create("connection");
        var view = OpenFor(pilot);
        var panel = Find(view.transform, pilot.ResultTestId);
        var skeleton = Find(view.transform, "scenario-result-skeleton");
        Assert.IsTrue(panel.gameObject.activeSelf);
        Assert.IsFalse(skeleton.gameObject.activeSelf);
        Tap(view, "qa-preset-connection-1");
        Assert.IsTrue(pilot.IsRunning);
        Assert.IsFalse(panel.gameObject.activeSelf);
        Assert.IsTrue(skeleton.gameObject.activeSelf);
        Assert.AreEqual("Running…", skeleton.GetComponentInChildren<TMP_Text>().text);
        Tap(view, "qa-preset-connection-5");
        Assert.AreEqual("ConnectUser", _sdk.Last.Method, "A second tap must not replace an in-flight run.");
        if (fail) _sdk.FailPending("test failure");
        else _sdk.CompletePending();
        for (var frame = 0; frame < 60 && pilot.IsRunning; frame++) yield return null;
        Assert.IsFalse(pilot.IsRunning);
        Assert.IsTrue(panel.gameObject.activeSelf);
        Assert.IsFalse(skeleton.gameObject.activeSelf);
        Assert.AreEqual(pilot.Result, Result(view).text);
        StringAssert.StartsWith(fail ? "ConnectUser failed: test failure" : "ConnectUser call completed",
                                Result(view).text);
    }

    [Test]
    public void SynchronousFailureRemovesRunningPlaceholder()
    {
        var pilot = new DeferredCustomPilot();
        var view = OpenFor(pilot);
        pilot.Execute(() => { throw new System.InvalidOperationException("test failure"); });
        Assert.IsFalse(pilot.IsRunning);
        Assert.IsFalse(Find(view.transform, "scenario-result-skeleton").gameObject.activeSelf);
        Assert.AreEqual("Run failed: test failure", Result(view).text);
    }

    [TestCase("connect", "ConnectUser")]
    [TestCase("disconnect", "DisconnectUser")]
    public void CustomConnectionRunConsumesActionOnce(string action, string method)
    {
        var view = Open("connection");
        Tap(view, "connection-customize");
        Field(view, "connection-field-action").text = action;
        Tap(view, "connection-run");
        CollectionAssert.AreEqual(new[] { "Initialize", method }, _sdk.ScenarioMethods);
        StringAssert.Contains("call completed", Result(view).text);
    }

    [Test]
    public void InvalidConnectionActionDoesNotEvenInitialize()
    {
        var view = Open("connection");
        Tap(view, "connection-customize");
        Field(view, "connection-field-action").text = "invalid";
        Tap(view, "connection-run");
        Assert.IsEmpty(_sdk.Calls);
        StringAssert.Contains("No call made", Result(view).text);
    }

    [TestCase("connection")]
    [TestCase("locale")]
    [TestCase("customEvents")]
    public void EveryPresetTapFullyRefillsAndCallsExactlyOneAction(string id)
    {
        var pilot = OctopusScenarioPilots.Create(id);
        var view = OpenFor(pilot);
        // Warm the one-time initialization separately from the action count.
        Tap(view, pilot.Presets[0].TestId);
        foreach (var preset in pilot.Presets)
        {
            Tap(view, id + "-customize");
            foreach (var input in view.GetComponentsInChildren<TMP_InputField>())
                if (input.interactable) input.text = "previous custom value";
            _sdk.Clear();
            Tap(view, preset.TestId);
            Assert.AreEqual(1, _sdk.Calls.Count, preset.TestId);
            var expected = OctopusScenarioPilots.Create(id);
            preset.Fill(expected.Fields);
            foreach (var field in expected.Fields.All)
            {
                Assert.AreEqual(field.Value, pilot.Fields.Get(field.Key), preset.TestId);
                Assert.AreEqual(field.Value, Field(view, id + "-field-" + field.Key).text);
            }
        }
    }

    [TestCase("connection")]
    [TestCase("locale")]
    [TestCase("customEvents")]
    public void ThemeChangePreservesResultEditsAndSelectedPresetWithoutCalls(string id)
    {
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        var pilot = OctopusScenarioPilots.Create(id);
        var view = OpenFor(pilot);
        Tap(view, pilot.Presets[1].TestId);
        var result = Result(view).text;
        _sdk.Clear();
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
        Assert.AreEqual(result, Result(view).text);
        Assert.AreEqual(1, _sdk.ThemeCalls.Count);
        Assert.AreEqual(SampleUi.TitleColor, Result(view).color);
        Tap(view, id + "-customize");
        TMP_InputField editable = null;
        foreach (var input in view.GetComponentsInChildren<TMP_InputField>())
            if (input.interactable) { editable = input; break; }
        var inputName = editable.name;
        editable.text = "edited value";
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        Assert.AreEqual("edited value", Field(view, inputName).text);
        Assert.IsTrue(Field(view, inputName).interactable);
        StringAssert.StartsWith("Not run yet — values changed\nRun these values", Result(view).text);
        Assert.IsTrue(Find(view.transform, id + "-run").gameObject.activeSelf);
        Tap(view, id + "-customize");
        Assert.AreEqual(result, Result(view).text, "Reset must still use preset 2.");
        // A theme change legitimately re-sends the native appearance (#134); it must not re-run
        // any scenario step.
        Assert.IsEmpty(_sdk.ScenarioMethods);
        Assert.AreEqual(2, _sdk.ThemeCalls.Count);
    }

    [Test]
    public void ThemeChangePreservesRunningAndAcceptsTheLateResult()
    {
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        var pilot = new DeferredCustomPilot();
        var view = OpenFor(pilot);
        Tap(view, "groups-customize");
        Tap(view, "groups-run");
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
        Assert.IsTrue(Find(view.transform, "scenario-result-skeleton").gameObject.activeSelf);
        Assert.IsFalse(Find(view.transform, "groups-run").GetComponent<Button>().interactable);
        pilot.Complete();
        Assert.AreEqual("Completed default", Result(view).text);
        Assert.IsTrue(Find(view.transform, "groups-run").GetComponent<Button>().interactable);
        Assert.AreEqual(1, pilot.FillCount);
    }

    [Test]
    public void ThemeChangePreservesAnErrorAndDoesNotDuplicateTheCanvas()
    {
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        var pilot = new DeferredCustomPilot();
        var view = OpenFor(pilot);
        pilot.Execute(() => { throw new System.InvalidOperationException("test failure"); });
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
        Assert.AreEqual("Run failed: test failure", Result(view).text);
        Assert.IsFalse(Find(view.transform, "scenario-result-skeleton").gameObject.activeSelf);
        Assert.AreEqual(1, view.GetComponents<Canvas>().Length);
        Assert.AreEqual(1, CountNamed(view.transform, "Root"));
        Assert.AreEqual(1, pilot.FillCount);
        Assert.IsEmpty(_sdk.Calls);
    }

    [Test]
    public void ThemeChangePreservesTheScrollPosition()
    {
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        var view = Open("connection");
        Canvas.ForceUpdateCanvases();
        var scroll = view.GetComponentInChildren<ScrollRect>();
        scroll.verticalNormalizedPosition = 0.35f;
        var position = scroll.verticalNormalizedPosition;
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
        Assert.AreEqual(position, view.GetComponentInChildren<ScrollRect>().verticalNormalizedPosition, 0.01f);
        Assert.IsEmpty(_sdk.Calls);
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void FieldsAreVisibleBeforeFocusAndTheOverlayUsesTheSafeArea(OctopusSampleTheme theme)
    {
        OctopusSampleBranding.Theme = theme;
        var view = Open("locale");
        var safe = (RectTransform)Find(view.transform, "scenario-safe-area");
        Assert.IsTrue(Find(view.transform, "Header").IsChildOf(safe));
        Assert.IsTrue(Find(view.transform, "ParametersSection").IsChildOf(safe));
        if (Screen.width > 0 && Screen.height > 0)
        {
            Assert.AreEqual(new Vector2(Screen.safeArea.xMin / Screen.width,
                Screen.safeArea.yMin / Screen.height), safe.anchorMin);
            Assert.AreEqual(new Vector2(Screen.safeArea.xMax / Screen.width,
                Screen.safeArea.yMax / Screen.height), safe.anchorMax);
        }
        Tap(view, "locale-customize");
        var input = Field(view, "locale-field-locale");
        Assert.IsFalse(input.isFocused);
        Assert.IsTrue(input.targetGraphic.raycastTarget);
        Assert.AreEqual(OctopusSampleBranding.Palette.Title, input.textComponent.color);
        Assert.AreEqual(OctopusSampleBranding.Palette.ControlBorder,
            Find(input.transform, "Stroke").GetComponent<Image>().color);
        Assert.AreNotEqual(input.targetGraphic.color, input.textComponent.color);
    }

    [Test]
    public void ConnectionExplainsEntitlementsBeforeAnyCallAndSeparatesApiSymbols()
    {
        var view = Open("connection");
        var notice = Find(view.transform, "scenario-parameter-notice");
        StringAssert.Contains("signing secret adds the requested entitlements", notice.GetComponentInChildren<TMP_Text>().text);
        StringAssert.Contains("Connect a sample user", Find(view.transform, "Capability").GetComponent<TMP_Text>().text);
        foreach (var symbol in new[] { "ConnectUser", "DisconnectUser" })
            Assert.IsNotNull(Find(view.transform, "scenario-api-" + symbol));
        Assert.IsEmpty(_sdk.Calls);
    }

    private static int CountNamed(Transform root, string name)
    {
        var count = root.name == name ? 1 : 0;
        foreach (Transform child in root) count += CountNamed(child, name);
        return count;
    }

    // A local test pilot proves the view has no locale/customEvents dispatch table and models
    // a result arriving after the reader has edited again. It never reaches an SDK.
    private sealed class DeferredCustomPilot : OctopusScenarioPilot
    {
        private readonly OctopusScenarioFields _fields = new OctopusScenarioFields(
            new OctopusScenarioField("value", "Value"));
        private readonly List<OctopusScenarioPreset> _presets;
        public string Submitted;
        public int FillCount;
        public int PresetRunCount;

        public DeferredCustomPilot(string testId = "qa-preset-groups-1") : base("groups")
        {
            _presets = new List<OctopusScenarioPreset>
            {
                new OctopusScenarioPreset(testId, PresetLabel(1, "Default"), fields =>
                {
                    FillCount++;
                    fields.Set("value", "default");
                }, fields => PresetRunCount++)
            };
        }

        public override OctopusScenarioFields Fields { get { return _fields; } }
        public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return _presets; } }
        public override bool CanCustomize { get { return true; } }

        public override void RunCustom()
        {
            Submitted = Fields.Get("value");
            ReportRunning("Running " + Submitted);
        }

        public void Complete()
        {
            Report("Completed " + Submitted);
        }
    }

    private static void Tap(OctopusScenarioScreenView view, string id)
    {
        var node = Find(view.transform, id);
        Assert.IsNotNull(node, "Missing control " + id);
        Assert.IsTrue(node.gameObject.activeInHierarchy, "Hidden control " + id);
        node.GetComponent<Button>().onClick.Invoke();
    }

    private static TMP_InputField Field(OctopusScenarioScreenView view, string id)
    {
        return Find(view.transform, id).GetComponent<TMP_InputField>();
    }

    private static TMP_Text Result(OctopusScenarioScreenView view)
    {
        return Find(view.transform, view.ScenarioId + "-result").GetComponentInChildren<TMP_Text>();
    }

    private void AssertHandles(string scenarioId, string[] presetTestIds, string resultTestId)
    {
        var view = Open(scenarioId);
        var root = view.gameObject.transform;

        foreach (var presetTestId in presetTestIds)
        {
            Assert.IsNotNull(Find(root, presetTestId),
                "No GameObject named '" + presetTestId + "' on the '" + scenarioId +
                "' screen — the QA pipeline addresses presets by that name.");
        }

        Assert.IsNotNull(Find(root, resultTestId),
            "No result panel named '" + resultTestId + "' on the '" + scenarioId + "' screen.");
    }

    private OctopusScenarioScreenView Open(string scenarioId)
    {
        return OpenFor(OctopusScenarioPilots.Create(scenarioId));
    }

    [Test]
    public void HeaderBackArrowKeepsItsQaNameAndTouchTarget()
    {
        var view = OpenFor(new ConnectionScenario());
        var back = (RectTransform)Find(view.transform, "Back");
        var header = (RectTransform)Find(view.transform, "Header");
        LayoutRebuilder.ForceRebuildLayoutImmediate(header);
        Assert.IsNotNull(back.GetComponent<Button>());
        Assert.IsNull(back.GetComponentInChildren<TMP_Text>());
        Assert.AreEqual(OctopusSampleBranding.MinTouchUnits, back.rect.width);
        Assert.AreEqual(OctopusSampleBranding.MinTouchUnits, back.rect.height);
        Assert.AreEqual(Vector2.one * OctopusSampleBranding.Dp(OctopusSampleBranding.MinInteractiveIconDp),
            ((RectTransform)back.Find("Icon")).sizeDelta);
        var title = (RectTransform)Find(header, "Title");
        var titleBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(header, title);
        var backBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(header, back);
        Assert.GreaterOrEqual(titleBounds.min.x, backBounds.max.x);
    }

    private OctopusScenarioScreenView OpenFor(OctopusScenarioPilot pilot)
    {
        var view = OctopusScenarioScreenView.Open(pilot);
        _spawned.Add(view.gameObject);
        return view;
    }

    /// <summary>Destroys what this test has opened so far, mid-test — Open is a no-op while one
    /// screen is still alive.</summary>
    private void TearDownSpawned()
    {
        foreach (var host in _spawned)
        {
            if (host != null) Object.DestroyImmediate(host);
        }
        _spawned.Clear();
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
