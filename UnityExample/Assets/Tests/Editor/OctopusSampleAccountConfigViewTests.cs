using System;
using System.Threading;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OctopusSampleAccountConfigViewTests
{
    private string _key;
    private OctopusRecordingScenarioSdk _sdk;
    private SynchronizationContext _context;
    private OctopusSampleTheme _theme;

    [SetUp]
    public void SetUp()
    {
        _key = "OctopusSample.Tests.AccountConfig." + Guid.NewGuid().ToString("N");
        _context = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new InlineContext());
        _theme = OctopusSampleBranding.Theme;
        _sdk = new OctopusRecordingScenarioSdk
        {
            Profile = new OctopusExampleConfig.ExampleProfile
                { apiKey = "test-key", authToken = "not-a-real-token", userId = "test-user" }
        };
        OctopusSampleLog.Current = OctopusSampleLog.None;
        OctopusSampleFeatureToggles.Reset();
        OctopusScenarioSdk.Use(_sdk, _key);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var view in UnityEngine.Object.FindObjectsByType<OctopusSampleSettingsDetailView>(FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(view.gameObject);
        var shell = UnityEngine.Object.FindAnyObjectByType<OctopusSampleShell>();
        if (shell != null) { shell.Shutdown(); UnityEngine.Object.DestroyImmediate(shell.gameObject); }
        // Theme dispatch also detaches destroyed EditMode views (Unity skips OnDestroy there).
        OctopusSampleBranding.Theme = _theme == OctopusSampleTheme.Dark ? OctopusSampleTheme.Light : OctopusSampleTheme.Dark;
        OctopusSampleBranding.Theme = _theme;
        PlayerPrefs.DeleteKey(_key);
        PlayerPrefs.Save();
        OctopusScenarioSdk.Use(null);
        OctopusSampleFeatureToggles.Reset();
        SynchronizationContext.SetSynchronizationContext(_context);
    }

    [Test]
    public void ConfigSelectionDoesNotPersistUntilStartAndCannotBeDismissedOnFirstLaunch()
    {
        var view = OctopusSampleConfigView.Open();
        Click(view, OctopusSampleConfigView.ForcedLoginId);
        Assert.AreEqual(2, view.Selection);
        Assert.IsFalse(PlayerPrefs.HasKey(_key));
        Assert.IsEmpty(_sdk.Methods);
        Assert.IsNull(Find(view.transform, OctopusSampleConfigView.BackId));
        Assert.IsNull(Find(view.transform, OctopusSampleConfigView.AccountLinkId),
            "First launch offers a way out of Configuration before the SDK is configured.");
        view.Close();
        Assert.IsTrue(view != null);
        Click(view, OctopusSampleConfigView.StartId);
        Assert.AreEqual(2, OctopusScenarioSdk.SavedConfiguration);
        Assert.IsTrue(view == null);
    }

    [Test]
    public void ConfigAfterResetHighlightsTheLiveForcedLoginProfile()
    {
        bool? applied = null;
        OctopusScenarioSdk.ApplyConfiguration(2, (ok, message) => applied = ok);
        Assert.AreEqual(true, applied);
        string reset;
        Assert.IsTrue(OctopusScenarioSdk.ResetConfiguration(out reset));
        Assert.AreEqual(0, OctopusScenarioSdk.SavedConfiguration);

        var view = OctopusSampleConfigView.Open();
        Assert.AreEqual(2, view.Selection,
            "After a reset the screen highlighted Default over a forced-login session (#349).");
        StringAssert.StartsWith("Selected · ", Label(view, OctopusSampleConfigView.ForcedLoginId));
        _sdk.Clear();
        Click(view, OctopusSampleConfigView.StartId);
        CollectionAssert.DoesNotContain(_sdk.Methods, "SwitchCommunity",
            "Applying the highlighted profile switched communities.");
        Assert.AreEqual(2, OctopusScenarioSdk.SavedConfiguration);
    }

    [Test]
    public void FailedStartupExplainsItselfOnTheConfigurationScreen()
    {
        PlayerPrefs.SetInt(_key, 99);
        OctopusScenarioSdk.RestoreInitializedSdk();
        Assert.IsTrue(OctopusScenarioSdk.NeedsConfiguration);

        var view = OctopusSampleConfigView.Open();
        Assert.AreEqual(1, view.Selection);
        StringAssert.Contains("Startup initialization failed", view.Result);
        Assert.IsNotNull(Find(view.transform, OctopusSampleConfigView.ResultId));
    }

    [Test]
    public void SettingsOpensBothRoutesAndRevisitCanCancelWithoutChangingConfiguration()
    {
        Apply();
        var shell = OctopusSampleShell.Create();
        shell.Select(OctopusSampleTab.Settings);
        _sdk.Clear();
        Click(shell, OctopusSampleSettingsView.ConfigRowId);
        var config = UnityEngine.Object.FindAnyObjectByType<OctopusSampleConfigView>();
        Assert.IsNotNull(config);
        config.SelectProfile(2);
        Click(config, OctopusSampleConfigView.BackId);
        Assert.AreEqual(1, OctopusScenarioSdk.SavedConfiguration);
        Click(shell, OctopusSampleSettingsView.AccountRowId);
        Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<OctopusSampleAccountView>());
        Assert.IsEmpty(Changes());
    }

    [Test]
    public void BackFromConfigurationOpenedFromAccountReturnsToAccount()
    {
        Apply();
        _sdk.Clear();
        var account = OctopusSampleAccountView.Open();
        Click(account, OctopusSampleAccountView.ConfigId);
        var config = UnityEngine.Object.FindAnyObjectByType<OctopusSampleConfigView>();
        Assert.IsNotNull(config);
        Assert.IsTrue(account == null, "Account stays stacked under Configuration.");

        Click(config, OctopusSampleConfigView.BackId);

        Assert.IsTrue(config == null);
        Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<OctopusSampleAccountView>(),
            "Back from a Configuration opened from Account left the reader on Settings.");
        Assert.IsEmpty(Changes());
    }

    [Test]
    public void ApplyingConfigurationOpenedFromAccountReturnsToAccount()
    {
        Apply();
        var account = OctopusSampleAccountView.Open();
        Click(account, OctopusSampleAccountView.ConfigId);
        var config = UnityEngine.Object.FindAnyObjectByType<OctopusSampleConfigView>();
        Assert.IsNotNull(config);

        Click(config, OctopusSampleConfigView.StartId);

        Assert.IsTrue(config == null);
        Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<OctopusSampleAccountView>());
    }

    [Test]
    public void BackFromConfigurationOpenedFromSettingsDoesNotOpenAccount()
    {
        Apply();
        var config = OctopusSampleConfigView.Open();
        Click(config, OctopusSampleConfigView.BackId);
        Assert.IsTrue(config == null);
        Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<OctopusSampleAccountView>());
    }

    [Test]
    public void TheRevisitAccountLinkOpensAccount()
    {
        Apply();
        var config = OctopusSampleConfigView.Open();
        Click(config, OctopusSampleConfigView.AccountLinkId);
        Assert.IsTrue(config == null);
        Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<OctopusSampleAccountView>());
    }

    [Test]
    public void TabSwitchFromConfigurationOpenedFromAccountLandsOnTheTabAlone()
    {
        Apply();
        var shell = OctopusSampleShell.Create();
        shell.Select(OctopusSampleTab.Settings);
        _sdk.Clear();
        Click(shell, OctopusSampleSettingsView.AccountRowId);
        var account = UnityEngine.Object.FindAnyObjectByType<OctopusSampleAccountView>();
        Assert.IsNotNull(account);
        Click(account, OctopusSampleAccountView.ConfigId);
        var config = UnityEngine.Object.FindAnyObjectByType<OctopusSampleConfigView>();
        Assert.IsNotNull(config);

        shell.Select(OctopusSampleTab.Home);

        Assert.AreEqual(OctopusSampleTab.Home, shell.Selected);
        Assert.IsTrue(config == null, "Configuration stayed open over the tab just picked.");
        Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<OctopusSampleAccountView>(),
            "Closing Configuration for a tab switch reopened Account over the tab.");
        Assert.IsEmpty(Changes());
    }

    [Test]
    public void CrossScreenLinkFromUnderAConfigurationOpenedFromAccountLandsOnTheScenarioAlone()
    {
        Apply();
        var shell = OctopusSampleShell.Create();
        shell.Select(OctopusSampleTab.Settings);
        _sdk.Clear();
        var account = OctopusSampleAccountView.Open();
        Click(account, OctopusSampleAccountView.ConfigId);
        var config = UnityEngine.Object.FindAnyObjectByType<OctopusSampleConfigView>();
        Assert.IsNotNull(config);

        // What Home's and Community's Connection links run: a tab switch, then a screen over it.
        var screen = shell.OpenScenarioScreen(OctopusSampleHomeView.ConnectionScenarioId);
        try
        {
            Assert.IsNotNull(screen, "The link opened no scenario screen.");
            Assert.AreEqual(OctopusSampleHomeView.ConnectionScenarioId, screen.ScenarioId);
            Assert.AreEqual(OctopusSampleTab.Scenarios, shell.Selected);
            Assert.IsTrue(config == null, "Configuration stayed open under the linked scenario.");
            Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<OctopusSampleAccountView>(),
                "The link's tab switch reopened Account over the linked scenario.");
            Assert.AreSame(screen, UnityEngine.Object.FindAnyObjectByType<OctopusScenarioScreenView>());
        }
        finally
        {
            if (screen != null) UnityEngine.Object.DestroyImmediate(screen.gameObject);
        }
    }

    [Test]
    public void SettingsResetsAreSeparateAndConfigurationResetRequiresConfirmation()
    {
        Apply();
        var shell = OctopusSampleShell.Create();
        shell.Select(OctopusSampleTab.Settings);
        var resetCard = Find(shell.transform, OctopusSampleSettingsView.ResetCardId);
        Assert.IsNotNull(resetCard);
        Assert.IsNotNull(Find(resetCard, OctopusSampleSettingsView.ResetStateSectionId),
            "The sample-state reset left the single Reset card.");
        Assert.IsNotNull(Find(resetCard, OctopusSampleSettingsView.ResetConfigCardId),
            "The configuration reset left the single Reset card.");
        Click(shell, OctopusSampleSettingsView.ResetConfigButtonId);
        Click(shell, OctopusSampleSettingsView.ResetConfigCancelId);
        Assert.AreEqual(1, OctopusScenarioSdk.SavedConfiguration);
        OctopusSampleState.ReportUnseenNotifications(12);
        Click(shell, OctopusSampleSettingsView.ResetButtonId);
        Click(shell, OctopusSampleSettingsView.ResetConfirmId);
        Assert.AreEqual(-1, OctopusSampleState.UnseenNotifications);
        Assert.AreEqual(1, OctopusScenarioSdk.SavedConfiguration);
        OctopusSampleState.ReportUnseenNotifications(12);
        Click(shell, OctopusSampleSettingsView.ResetConfigButtonId);
        Click(shell, OctopusSampleSettingsView.ResetConfigConfirmId);
        Assert.AreEqual(12, OctopusSampleState.UnseenNotifications);
        Assert.AreEqual(0, OctopusScenarioSdk.SavedConfiguration);
        Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<OctopusSampleConfigView>());
    }

    [Test]
    public void AccountCallsSsoLoginAndLogoutButCompletionDoesNotInventAConnectedProfile()
    {
        Apply();
        _sdk.Clear();
        var account = OctopusSampleAccountView.Open();
        Assert.IsEmpty(_sdk.Methods);
        Assert.IsNull(Find(account.transform, OctopusSampleAccountView.DisconnectId));
        Click(account, OctopusSampleAccountView.ConnectId);
        CollectionAssert.AreEqual(new[] { "ConnectUser" }, _sdk.Methods);
        Assert.AreEqual("test-user", _sdk.Last.Args[0]);
        Assert.AreEqual("No connected profile reported", OctopusSampleAccountView.ConnectionText());
        Assert.IsNull(Find(account.transform, OctopusSampleAccountView.DisconnectId));
        _sdk.EmitProfile(new OctopusProfile(clientUserId: "test-user"));
        Assert.IsNotNull(Find(account.transform, OctopusSampleAccountView.DisconnectId));
        StringAssert.Contains("SSO profile connected", Label(account, OctopusSampleAccountView.StateId));
        Click(account, OctopusSampleAccountView.DisconnectId);
        Assert.AreEqual("DisconnectUser", _sdk.Last.Method);
        _sdk.EmitProfile(null);
        Assert.IsNull(Find(account.transform, OctopusSampleAccountView.DisconnectId));
        Assert.AreEqual("No connected profile reported", Label(account, OctopusSampleAccountView.StateId));
        _sdk.EmitProfile(new OctopusProfile());
        Assert.IsNull(Find(account.transform, OctopusSampleAccountView.DisconnectId));
        StringAssert.Contains("Guest", Label(account, OctopusSampleAccountView.StateId));
        _sdk.EmitProfile(new OctopusProfile(clientUserId: ""));
        Assert.IsNull(Find(account.transform, OctopusSampleAccountView.DisconnectId));
        StringAssert.Contains("Guest", Label(account, OctopusSampleAccountView.StateId));
    }

    [Test]
    public void AccountRefusesBeforeConfigurationAndWithoutSsoCredentials()
    {
        var account = OctopusSampleAccountView.Open();
        account.Login();
        account.Logout();
        Assert.IsEmpty(_sdk.Methods);
        Apply();
        _sdk.Clear();
        _sdk.Profile.authToken = null;
        account.Login();
        Assert.IsEmpty(_sdk.Methods);
        StringAssert.Contains("no SSO credentials", account.Result);
    }

    [Test]
    public void AccountSerializesOperationsAndSanitizesFailures()
    {
        Apply();
        _sdk.Clear();
        _sdk.DeferCompletions = true;
        var account = OctopusSampleAccountView.Open();
        account.Login();
        account.Logout();
        Assert.IsTrue(account.IsRunning);
        CollectionAssert.AreEqual(new[] { "ConnectUser" }, _sdk.Methods);
        _sdk.FailPending("sensitive-native-detail");
        Assert.IsFalse(account.IsRunning);
        StringAssert.DoesNotContain("sensitive-native-detail", account.Result);
        Assert.AreEqual(OctopusSampleState.Session.Failed, OctopusSampleState.ConnectionSession);
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void ThemeRebuildKeepsTheConfigSelectionAndQaControls(OctopusSampleTheme theme)
    {
        OctopusSampleBranding.Theme = theme;
        var config = OctopusSampleConfigView.Open();
        config.SelectProfile(2);
        OctopusSampleBranding.Theme = theme == OctopusSampleTheme.Light ? OctopusSampleTheme.Dark : OctopusSampleTheme.Light;
        Assert.AreEqual(2, config.Selection);
        Assert.AreEqual(SampleUi.Background, Find(config.transform, OctopusSampleConfigView.ScreenId).GetComponent<Image>().color);
        Assert.IsNotNull(Find(config.transform, OctopusSampleConfigView.StartId));
        UnityEngine.Object.DestroyImmediate(config.gameObject);
        var account = OctopusSampleAccountView.Open();
        OctopusSampleBranding.Theme = theme;
        Assert.AreEqual(SampleUi.Background, Find(account.transform, OctopusSampleAccountView.ScreenId).GetComponent<Image>().color);
        Assert.IsNotNull(Find(account.transform, OctopusSampleAccountView.ConnectId));
        Assert.IsNull(Find(account.transform, OctopusSampleAccountView.DisconnectId));
        Assert.IsEmpty(_sdk.Methods);
    }

    [TestCase("config")]
    [TestCase("account")]
    public void QaDestinationOpensTheRequestedSampleScreen(string id)
    {
        var shell = OctopusSampleShell.Create();
        Assert.IsTrue(OctopusSampleQaDestinations.Has(id));
        Assert.AreEqual("settings", OctopusSampleQaDestinations.TabOf(id));
        Assert.IsTrue(((IOctopusSampleQaNavigation)shell).OpenDestination(id));
        Assert.IsNotNull(GameObject.Find(id + "-screen"));
        Assert.IsEmpty(_sdk.Methods);
    }

    private static void Apply()
    {
        bool? result = null;
        OctopusScenarioSdk.ApplyConfiguration(1, (ok, message) => result = ok);
        Assert.AreEqual(true, result);
    }

    private static string Label(Component view, string id)
    {
        // A profile row is a button: its text sits on the child label, not on the button itself.
        return Find(view.transform, id).GetComponentInChildren<TMP_Text>().text;
    }

    private static void Click(Component view, string id)
    {
        var target = Find(view.transform, id);
        Assert.IsNotNull(target, id);
        target.GetComponent<Button>().onClick.Invoke();
    }

    private static Transform Find(Transform root, string id)
    {
        foreach (var item in root.GetComponentsInChildren<Transform>(true))
            if (item.name == id) return item;
        return null;
    }

    private sealed class InlineContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object state) { callback(state); }
    }

    /// <summary>
    /// SDK calls other than reads. Opening Configuration on a running SDK reads the effective
    /// Unified Profile flag (DebugGetCommunityConfig); a read changes nothing, and these
    /// navigation tests pin that nothing changes.
    /// </summary>
    private System.Collections.Generic.List<string> Changes()
    {
        var changes = new System.Collections.Generic.List<string>();
        foreach (var method in _sdk.Methods) if (method != "DebugGetCommunityConfig") changes.Add(method);
        return changes;
    }
}
