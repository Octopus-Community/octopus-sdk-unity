using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The Sign-in and Notifications switches: their wording, placement, what a flip writes
/// to the console, and what it changes downstream.
///
/// The switch itself is a `UnityEngine.UI` type this assembly cannot name — it overrides its
/// references down to `nunit.framework.dll` — so what is on screen is asserted through
/// `GameObject.name` and sibling order, and the behaviour through
/// <see cref="OctopusSampleFeatureToggles"/>, which is pure static C#. That split is the reason the
/// toggle's strings and its "locked" note live there rather than inside the view.
/// </summary>
public class OctopusSampleFeatureTogglesTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        OctopusSamplePushRegistration.Reset();
        OctopusSampleFeatureToggles.Reset();
        OctopusSampleState.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var host in _spawned)
        {
            if (host != null) Object.DestroyImmediate(host);
        }
        _spawned.Clear();
        OctopusSamplePushRegistration.Reset();
        OctopusSampleFeatureToggles.Reset();
        OctopusSampleState.Reset();
        OctopusSampleLog.Current = OctopusSampleLog.None;
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
    }

    [Test]
    public void TheSwitchIsOffByDefaultAndCarriesAndroidsOwnWording()
    {
        // Verbatim from the Android sample's `FeatureToggle.ForceLogin`, id included: three samples
        // describing one switch in three wordings is how a tester ends up trusting none of them,
        // and a QA step that taps `scenarios-toggle-force-login` has to find it on both platforms.
        Assert.IsFalse(OctopusSampleFeatureToggles.ForceLogin, "The switch does not start off.");
        Assert.AreEqual("scenarios-toggle-force-login", OctopusSampleFeatureToggles.ForceLoginId);
        Assert.AreEqual("Force login", OctopusSampleFeatureToggles.ForceLoginLabel);
        Assert.AreEqual("Visitors must sign in before they can read or post.",
                        OctopusSampleFeatureToggles.ForceLoginEffect(true));
        Assert.AreEqual("Visitors can browse the community before signing in.",
                        OctopusSampleFeatureToggles.ForceLoginEffect(false));
    }

    [Test]
    public void AFlipWritesTheContractsHeadlineOnceAndNamesTheNewPosition()
    {
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;

        Assert.IsTrue(OctopusSampleFeatureToggles.SetForceLogin(true), "The flip was refused.");
        Assert.IsFalse(OctopusSampleFeatureToggles.SetForceLogin(true),
            "Setting the position it already holds counts as a flip, so a repaint would log one.");

        Assert.AreEqual(1, probe.StateChanges.Count,
            "The console shows " + probe.StateChanges.Count + " entries for one flip.");
        Assert.AreEqual(OctopusSampleFeatureToggles.ToggledHeadline, probe.StateChanges[0].headline);
        StringAssert.Contains("Force login → on", probe.StateChanges[0].detail);
        StringAssert.Contains(OctopusSampleFeatureToggles.ForceLoginEffect(true),
                              probe.StateChanges[0].detail);
    }

    [Test]
    public void AFlipIsAStateChangeAndNeverAnSdkCall()
    {
        // The console labels the two differently, and the Explorer counts only API calls: a toggle
        // that logged itself as a call would inflate both.
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;

        OctopusSampleFeatureToggles.SetForceLogin(true);

        Assert.IsEmpty(probe.Methods, "Flipping the switch logged an SDK call: " +
                                      string.Join(", ", probe.Methods.ToArray()));
    }

    [Test]
    public void TheSwitchSitsDirectlyUnderTheSignInHeadAndNowhereElse()
    {
        // The design contract's rule — "a feature toggle lives in the section header, and nowhere
        // else" — plus Android's own layout, which draws the toggle row as the head's next sibling
        // rather than inside a row whose tap collapses the section.
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);

        var row = Find(shell.transform, OctopusScenariosListView.ForceLoginRowId);
        var head = Find(shell.transform,
                        OctopusScenarioSections.HeaderIdOf(ScenarioSection.SignIn));
        Assert.IsNotNull(row, "The Sign-in section head carries no `Force login` row.");
        Assert.IsNotNull(Find(row, OctopusSampleFeatureToggles.ForceLoginId),
            "The row carries no switch, so nothing can flip it.");
        Assert.AreSame(head.parent, row.parent, "The row is not a sibling of the head.");
        Assert.AreEqual(head.GetSiblingIndex() + 1, row.GetSiblingIndex(),
            "The row is not the head's next sibling, so it reads as belonging to another section.");
        Assert.AreEqual(1, Count(shell.transform, OctopusSampleFeatureToggles.ForceLoginId),
            "The switch is drawn more than once, so a tap is ambiguous.");
    }

    [Test]
    public void CollapsingSignInKeepsItsSwitchOnScreen()
    {
        // Iso-Android: the toggle row sits outside the collapsible content. A switch that a collapse
        // hides is a switch the tester cannot find again without knowing it was there.
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var view = shell.GetComponentInChildren<OctopusScenariosListView>();

        view.ToggleSection(ScenarioSection.SignIn);

        Assert.IsFalse(view.IsSectionOpen(ScenarioSection.SignIn));
        Assert.IsNotNull(Find(shell.transform, OctopusSampleFeatureToggles.ForceLoginId),
            "Collapsing Sign-in took its `Force login` switch with it.");
    }

    [Test]
    public void TheConnectionPresetsNameTheProfileTheSwitchPicks()
    {
        // What the switch actually decides, as the tester sees it before tapping Run: which of the
        // config asset's two profiles — two API keys, two demo communities — the next
        // initialisation uses. Android's switch picks between two named keys the same way.
        var pilot = OctopusScenarioPilots.Create("connection");
        var preset = pilot.Presets[0];

        preset.Fill(pilot.Fields);
        Assert.AreEqual("Default (OctopusExampleConfig)", pilot.Fields.Get("profile"));

        OctopusSampleFeatureToggles.SetForceLogin(true);
        preset.Fill(pilot.Fields);
        Assert.AreEqual("Forced login (OctopusExampleConfig)", pilot.Fields.Get("profile"),
            "The preset still names the profile the tester moved away from.");
    }

    [Test]
    public void OnceTheSdkIsUpTheSwitchSaysWhyItStoppedDeciding()
    {
        // This sample initialises once per process, so a flip after that changes nothing until a
        // restart. The header says so; the alternative — a live-looking switch with no effect — is
        // the defect the contract names.
        Assert.AreEqual(string.Empty, OctopusSampleFeatureToggles.ForceLoginLockedNote(),
            "The switch claims to be locked before anything initialised it.");

        OctopusSampleState.ReportInitialized("SSO");

        StringAssert.Contains("Restart", OctopusSampleFeatureToggles.ForceLoginLockedNote(),
            "The switch is dead and the header does not say why.");
    }

    [Test]
    public void AFlipIsRefusedOnceTheSdkIsUp()
    {
        // The backstop under the drawn-dead switch. A header built before a scenario initialised
        // the SDK is still on screen behind it; a flip accepted through that stale control would
        // move the profile the presets name away from the one the running SDK actually came up
        // with, and the two would disagree for the rest of the process.
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;
        OctopusSampleState.ReportInitialized("SSO");

        Assert.IsFalse(OctopusSampleFeatureToggles.SetForceLogin(true),
            "The switch accepted a flip after the SDK came up.");
        Assert.IsFalse(OctopusSampleFeatureToggles.ForceLogin, "The refused flip moved it anyway.");
        Assert.IsEmpty(probe.StateChanges,
            "The console announced a config rebuild that did not happen.");
    }

    [Test]
    public void InitialisingFromAScenarioLocksTheSwitchAlreadyOnScreen()
    {
        // The list is drawn once and then sits behind the scenario screen that initialises the SDK.
        // Nothing repaints it on the way back, so it listens: without that, the tester returns to a
        // switch that looks live and is not.
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        Assert.IsNull(Find(shell.transform, OctopusScenariosListView.ForceLoginLockedNoteId),
            "The switch claims to be locked before anything initialised the SDK.");

        OctopusSampleState.ReportInitialized("SSO");

        Assert.IsNotNull(Find(shell.transform, OctopusScenariosListView.ForceLoginLockedNoteId),
            "A scenario brought the SDK up and the switch behind it still looks live.");
    }

    [Test]
    public void PushRegistrationDefaultsOnWithAndroidsIdAndBothEffectSentences()
    {
        Assert.IsTrue(OctopusSampleFeatureToggles.PushRegistration);
        Assert.AreEqual("scenarios-toggle-push-registration", OctopusSampleFeatureToggles.PushRegistrationId);
        Assert.AreEqual("Push registration", OctopusSampleFeatureToggles.PushRegistrationLabel);
        Assert.AreEqual("New device tokens go to Octopus, so this device can be notified.",
            OctopusSampleFeatureToggles.PushRegistrationEffect(true));
        Assert.AreEqual("Device tokens stay in the app; already-registered ones live until a reset.",
            OctopusSampleFeatureToggles.PushRegistrationEffect(false));
    }

    [Test]
    public void EachPushFlipLogsOneConfigRebuildAndNoApiCall()
    {
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;
        Assert.IsTrue(OctopusSampleFeatureToggles.SetPushRegistration(false));
        Assert.IsFalse(OctopusSampleFeatureToggles.SetPushRegistration(false));
        Assert.IsTrue(OctopusSampleFeatureToggles.SetPushRegistration(true));
        Assert.AreEqual(3, probe.StateChanges.Count);
        Assert.AreEqual("feature toggled → SDK config rebuilt", probe.StateChanges[0].headline);
        Assert.AreEqual("feature toggled → SDK config rebuilt", probe.StateChanges[1].headline);
        Assert.AreEqual("Push registration → off\n" + OctopusSampleFeatureToggles.PushRegistrationEffect(false),
            probe.StateChanges[0].detail);
        Assert.AreEqual("Push registration → on\n" + OctopusSampleFeatureToggles.PushRegistrationEffect(true),
            probe.StateChanges[1].detail);
        Assert.AreEqual("Push registration: device token is not available yet.", probe.StateChanges[2].headline);
        Assert.IsEmpty(probe.Methods);
    }

    [Test]
    public void PushSwitchSitsImmediatelyAfterNotificationsHeadBeforeTheScenarioCards()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var head = Find(shell.transform, "scenarios-section-notifications");
        var row = Find(shell.transform, "scenarios-toggle-push-registration-row");
        var card = Find(shell.transform, "scenarios-pushNotifications-card");
        Assert.IsNotNull(head);
        Assert.IsNotNull(row);
        Assert.IsNotNull(card, "pushNotifications is built: its card must be listed");
        Assert.AreSame(head.parent, row.parent);
        Assert.AreEqual(head.GetSiblingIndex() + 1, row.GetSiblingIndex());
        Assert.AreEqual(1, Count(shell.transform, "scenarios-toggle-push-registration"));
        AssertPushCopy(row, OctopusSampleFeatureToggles.PushRegistrationEffect(true));
    }

    [Test]
    public void PushToggleSurvivesCollapseAndCanBeFoundBySearch()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var view = shell.GetComponentInChildren<OctopusScenariosListView>();
        view.ToggleSection(ScenarioSection.Notifications);
        Assert.IsNotNull(Find(shell.transform, OctopusSampleFeatureToggles.PushRegistrationId));
        view.SetQuery("  PUSH REGISTRATION  ");
        Assert.IsNotNull(Find(shell.transform, OctopusSampleFeatureToggles.PushRegistrationId));
        Assert.IsNull(Find(shell.transform, "scenarios-connection-card"));
        view.SetQuery("locale");
        Assert.IsNull(Find(shell.transform, OctopusSampleFeatureToggles.PushRegistrationId));
        view.SetQuery("");
        Assert.IsNotNull(Find(shell.transform, OctopusSampleFeatureToggles.PushRegistrationId));
    }

    [Test]
    public void ClickingPushPersistsAcrossTabsThemeAndShellRecreationLikeForceLogin()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var button = Find(shell.transform, OctopusSampleFeatureToggles.PushRegistrationId)
            .GetComponent("UnityEngine.UI.Button");
        Assert.IsNotNull(button);
        var click = button.GetType().GetProperty("onClick").GetValue(button, null);
        click.GetType().GetMethod("Invoke").Invoke(click, null);
        Assert.IsFalse(OctopusSampleFeatureToggles.PushRegistration);
        Assert.IsFalse(OctopusSampleFeatureToggles.ForceLogin, "The push switch moved Force login.");
        AssertPushCopy(Find(shell.transform, OctopusScenariosListView.PushRegistrationRowId),
            OctopusSampleFeatureToggles.PushRegistrationEffect(false));

        OctopusSampleFeatureToggles.SetForceLogin(true);
        shell.Select(OctopusSampleTab.Home);
        shell.Select(OctopusSampleTab.Scenarios);
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        Object.DestroyImmediate(shell.gameObject);
        shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        Assert.IsTrue(OctopusSampleFeatureToggles.ForceLogin);
        Assert.IsFalse(OctopusSampleFeatureToggles.PushRegistration);
        AssertPushCopy(Find(shell.transform, OctopusScenariosListView.PushRegistrationRowId),
            OctopusSampleFeatureToggles.PushRegistrationEffect(false));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InitializingKeepsPushInteractableInEitherPositionAndLocksOnlyForceLogin(bool enabled)
    {
        OctopusSampleFeatureToggles.SetPushRegistration(enabled);
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        Assert.IsNull(Find(shell.transform, OctopusScenariosListView.PushRegistrationLockedNoteId));
        OctopusSampleState.ReportInitialized("SSO");
        Assert.IsNull(Find(shell.transform, OctopusScenariosListView.PushRegistrationLockedNoteId));
        Assert.IsNotNull(Find(shell.transform, OctopusScenariosListView.ForceLoginLockedNoteId));
        var forceLogin = Find(shell.transform, OctopusSampleFeatureToggles.ForceLoginId)
            .GetComponent("UnityEngine.UI.Button");
        Assert.AreEqual(false, forceLogin.GetType().GetProperty("interactable").GetValue(forceLogin, null));
        var button = Find(shell.transform, OctopusSampleFeatureToggles.PushRegistrationId)
            .GetComponent("UnityEngine.UI.Button");
        Assert.AreEqual(true, button.GetType().GetProperty("interactable").GetValue(button, null));
        var click = button.GetType().GetProperty("onClick").GetValue(button, null);
        click.GetType().GetMethod("Invoke").Invoke(click, null);
        Assert.AreEqual(!enabled, OctopusSampleFeatureToggles.PushRegistration);
        AssertPushCopy(Find(shell.transform, OctopusScenariosListView.PushRegistrationRowId),
            OctopusSampleFeatureToggles.PushRegistrationEffect(!enabled));
        Assert.IsTrue(OctopusSampleFeatureToggles.SetPushRegistration(enabled));
        Assert.AreEqual(enabled, OctopusSampleFeatureToggles.PushRegistration);
    }

    [Test]
    public void EnablingPushAfterInitializationForwardsOnlyTheLatestCachedTokenOnce()
    {
        OctopusSampleFeatureToggles.SetPushRegistration(false);
        OctopusSampleState.ReportInitialized("SSO");
        var sent = new List<string>();
        OctopusSamplePushRegistration.Register("test-old-token", sent.Add);
        OctopusSamplePushRegistration.Register("test-current-token", sent.Add);
        Assert.IsEmpty(sent);
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;

        Assert.IsTrue(OctopusSampleFeatureToggles.SetPushRegistration(true));
        Assert.IsFalse(OctopusSampleFeatureToggles.SetPushRegistration(true));
        OctopusSampleState.ReportUnseenNotifications(3);

        CollectionAssert.AreEqual(new[] { "test-current-token" }, sent);
        CollectionAssert.AreEqual(new[] { "OctopusSDK.RegisterNotificationsToken" }, probe.Methods);
        CollectionAssert.AreEqual(new string[] { null }, probe.ApiDetails);
        Assert.AreEqual(1, probe.StateChanges.Count);
        Assert.AreEqual("Push registration → on\n" + OctopusSampleFeatureToggles.PushRegistrationEffect(true),
            probe.StateChanges[0].detail);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EnablingPushWithoutACachedTokenReportsWaitingAndMakesNoApiCall(bool initialized)
    {
        OctopusSampleFeatureToggles.SetPushRegistration(false);
        if (initialized) OctopusSampleState.ReportInitialized("SSO");
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;

        Assert.IsTrue(OctopusSampleFeatureToggles.SetPushRegistration(true));
        Assert.IsFalse(OctopusSampleFeatureToggles.SetPushRegistration(true));

        Assert.IsEmpty(probe.Methods);
        Assert.AreEqual(2, probe.StateChanges.Count);
        Assert.AreEqual("Push registration: device token is not available yet.", probe.StateChanges[1].headline);
        Assert.IsNull(probe.StateChanges[1].detail);
    }

    [Test]
    public void DisablingPushNeitherForwardsNorUnregistersAndReenablingReplaysTheToken()
    {
        OctopusSampleState.ReportInitialized("SSO");
        var sent = new List<string>();
        OctopusSamplePushRegistration.Register("test-device-token", sent.Add);
        sent.Clear();
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;

        Assert.IsTrue(OctopusSampleFeatureToggles.SetPushRegistration(false));
        OctopusSampleState.ReportUnseenNotifications(3);

        Assert.IsEmpty(sent);
        Assert.IsEmpty(probe.Methods);
        Assert.IsTrue(OctopusSampleFeatureToggles.SetPushRegistration(true));
        CollectionAssert.AreEqual(new[] { "test-device-token" }, sent);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ATokenReceivedBeforeInitializationWaitsForBothInitializationAndEnablement(bool enabled)
    {
        OctopusSampleFeatureToggles.SetPushRegistration(enabled);
        var sent = new List<string>();
        Assert.IsFalse(OctopusSamplePushRegistration.Register("test-device-token", sent.Add));
        Assert.IsEmpty(sent);

        OctopusSampleState.ReportInitialized("SSO");
        Assert.AreEqual(enabled ? 1 : 0, sent.Count);
        OctopusSampleFeatureToggles.SetPushRegistration(true);
        OctopusSampleState.ReportUnseenNotifications(3);

        CollectionAssert.AreEqual(new[] { "test-device-token" }, sent);
    }

    [Test]
    public void EnablingPushBeforeInitializationDefersTheCachedToken()
    {
        OctopusSampleFeatureToggles.SetPushRegistration(false);
        var sent = new List<string>();
        OctopusSamplePushRegistration.Register("test-device-token", sent.Add);
        OctopusSampleFeatureToggles.SetPushRegistration(true);
        Assert.IsEmpty(sent);

        OctopusSampleState.ReportInitialized("SSO");

        CollectionAssert.AreEqual(new[] { "test-device-token" }, sent);
    }

    [TestCase(null)]
    [TestCase("")]
    public void EmptyTokensDoNotReplaceTheCachedToken(string token)
    {
        OctopusSampleFeatureToggles.SetPushRegistration(false);
        OctopusSampleState.ReportInitialized("SSO");
        var sent = new List<string>();
        OctopusSamplePushRegistration.Register("test-device-token", sent.Add);
        Assert.IsFalse(OctopusSamplePushRegistration.Register(token, sent.Add));

        OctopusSampleFeatureToggles.SetPushRegistration(true);

        CollectionAssert.AreEqual(new[] { "test-device-token" }, sent);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TokenForwardingHonoursPushAfterInitializationAndLogsWithoutTokens(bool enabled)
    {
        OctopusSampleFeatureToggles.SetPushRegistration(enabled);
        OctopusSampleState.ReportInitialized("OctopusAuth");
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;
        var sent = new List<string>();
        Assert.AreEqual(enabled, OctopusSamplePushRegistration.Register("test-device-token", sent.Add));
        Assert.AreEqual(enabled, OctopusSamplePushRegistration.Register("test-refreshed-token", sent.Add));
        if (enabled)
        {
            CollectionAssert.AreEqual(new[] { "test-device-token", "test-refreshed-token" }, sent);
            CollectionAssert.AreEqual(new[] { "OctopusSDK.RegisterNotificationsToken",
                "OctopusSDK.RegisterNotificationsToken" }, probe.Methods);
            CollectionAssert.AreEqual(new string[] { null, null }, probe.ApiDetails);
            Assert.IsEmpty(probe.StateChanges);
        }
        else
        {
            Assert.IsEmpty(sent);
            Assert.IsEmpty(probe.Methods);
            Assert.AreEqual(2, probe.StateChanges.Count);
            foreach (var entry in probe.StateChanges)
            {
                Assert.AreEqual("Push registration skipped by the toggle", entry.headline);
                Assert.AreEqual(OctopusSampleFeatureToggles.PushRegistrationEffect(false), entry.detail);
            }
        }
    }

    [Test]
    public void ResetRestoresBothToggleDefaults()
    {
        OctopusSampleFeatureToggles.SetForceLogin(true);
        OctopusSampleFeatureToggles.SetPushRegistration(false);
        OctopusSampleFeatureToggles.Reset();
        Assert.IsFalse(OctopusSampleFeatureToggles.ForceLogin);
        Assert.IsTrue(OctopusSampleFeatureToggles.PushRegistration);
    }

    // The test assembly does not reference uGUI: inspect real rendered Text components by name.
    private static void AssertPushCopy(Transform row, string effect)
    {
        Assert.IsNotNull(row);
        var texts = new List<string>();
        foreach (var child in row.GetComponentsInChildren<Transform>(true))
        {
            var label = child.GetComponent("TMPro.TMP_Text");
            if (label != null) texts.Add((string)label.GetType().GetProperty("text").GetValue(label, null));
        }
        CollectionAssert.Contains(texts, "Push registration");
        CollectionAssert.Contains(texts, effect);
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

    private static int Count(Transform root, string name)
    {
        var found = root.gameObject.name == name ? 1 : 0;
        for (var i = 0; i < root.childCount; i++) found += Count(root.GetChild(i), name);
        return found;
    }

    private sealed class ProbeLog : IOctopusSampleLog
    {
        public readonly List<string> Methods = new List<string>();
        public readonly List<string> ApiDetails = new List<string>();

        public readonly List<(string headline, string detail)> StateChanges =
            new List<(string, string)>();

        public void LogApiCall(string method, string detail = null)
        {
            Methods.Add(method);
            ApiDetails.Add(detail);
        }

        public void LogStateChange(string headline, string detail = null)
        {
            StateChanges.Add((headline, detail));
        }
    }
}
