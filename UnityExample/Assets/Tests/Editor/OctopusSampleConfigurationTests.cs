using System;
using NUnit.Framework;
using UnityEngine;

public class OctopusSampleConfigurationTests
{
    private string _key;
    private OctopusRecordingScenarioSdk _sdk;

    [SetUp]
    public void SetUp()
    {
        _key = "OctopusSample.Tests.Configuration." + Guid.NewGuid().ToString("N");
        _sdk = new OctopusRecordingScenarioSdk
        {
            Profile = new OctopusExampleConfig.ExampleProfile { apiKey = "test-default-key", authToken = "not-a-real-token", userId = "test-user" },
            AlternateProfile = new OctopusExampleConfig.ExampleProfile { apiKey = "test-alternate-key", authToken = "not-a-real-token", userId = "test-user" }
        };
        OctopusSampleLog.Current = OctopusSampleLog.None;
        OctopusSampleFeatureToggles.Reset();
        OctopusScenarioSdk.Use(_sdk, _key);
    }

    [TearDown]
    public void TearDown()
    {
        var config = UnityEngine.Object.FindAnyObjectByType<OctopusSampleConfigView>();
        if (config != null) UnityEngine.Object.DestroyImmediate(config.gameObject);
        var shell = UnityEngine.Object.FindAnyObjectByType<OctopusSampleShell>();
        if (shell != null) { shell.Shutdown(); UnityEngine.Object.DestroyImmediate(shell.gameObject); }
        PlayerPrefs.DeleteKey(_key);
        PlayerPrefs.Save();
        OctopusScenarioSdk.Use(null);
        OctopusSampleFeatureToggles.Reset();
        OctopusSampleLog.Current = OctopusSampleLog.None;
    }

    [Test]
    public void FreshLaunchShowsConfigurationWithoutInitializing()
    {
        var shell = OctopusSampleShell.Create();
        shell.RestoreConfiguration();
        Assert.IsTrue(OctopusScenarioSdk.NeedsConfiguration);
        Assert.AreEqual(0, OctopusScenarioSdk.SavedConfiguration);
        Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<OctopusSampleConfigView>());
        Assert.IsEmpty(_sdk.Methods);
    }

    [TestCase(1)]
    [TestCase(2)]
    public void ApplyingPersistsTheStartupConventionAndReplaySkipsOnboarding(int choice)
    {
        Apply(choice);
        Assert.AreEqual(choice, PlayerPrefs.GetInt(_key, 0));
        Assert.AreEqual(choice, OctopusScenarioSdk.SavedConfiguration);
        Assert.IsFalse(OctopusScenarioSdk.NeedsConfiguration);
        OctopusScenarioSdk.Use(_sdk, _key);
        OctopusSampleFeatureToggles.Reset();
        _sdk.Clear();
        var shell = OctopusSampleShell.Create();
        shell.RestoreConfiguration();
        Assert.IsFalse(OctopusScenarioSdk.NeedsConfiguration);
        Assert.AreEqual(choice == 2, OctopusSampleFeatureToggles.ForceLogin);
        CollectionAssert.Contains(_sdk.Methods, "Initialize");
        Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<OctopusSampleConfigView>());
        _sdk.Clear();
        shell.RestoreConfiguration();
        Assert.IsEmpty(_sdk.Methods, "Startup replay must not initialize twice.");
    }

    [TestCase(-1)]
    [TestCase(3)]
    public void InvalidStoredSelectionReturnsToConfiguration(int value)
    {
        PlayerPrefs.SetInt(_key, value);
        OctopusSampleShell.Create().RestoreConfiguration();
        Assert.IsTrue(OctopusScenarioSdk.NeedsConfiguration);
        Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<OctopusSampleConfigView>());
        Assert.IsEmpty(_sdk.Methods);
    }

    [Test]
    public void MissingBuildProfileDoesNotPersistOrInitialize()
    {
        _sdk.Profile = null;
        bool? success = null;
        OctopusScenarioSdk.ApplyConfiguration(1, (ok, message) => success = ok);
        Assert.AreEqual(false, success);
        Assert.IsFalse(PlayerPrefs.HasKey(_key));
        Assert.IsTrue(OctopusScenarioSdk.NeedsConfiguration);
        Assert.IsEmpty(_sdk.Methods);
    }

    [Test]
    public void ConfigurationResetKeepsObservationsAndInitializationAndDoesNotCallSdk()
    {
        Apply(1);
        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "recorded result");
        OctopusSampleState.ReportUnseenNotifications(7);
        OctopusSampleState.ReportCommunityAccess(true);
        OctopusSampleState.ReportLocaleOverride("fr");
        _sdk.Clear();
        string message;
        Assert.IsTrue(OctopusScenarioSdk.ResetConfiguration(out message));
        Assert.IsFalse(PlayerPrefs.HasKey(_key));
        Assert.IsTrue(OctopusScenarioSdk.NeedsConfiguration);
        Assert.IsTrue(OctopusSampleState.IsInitialized);
        Assert.AreEqual(OctopusSampleState.Session.ConnectCompleted, OctopusSampleState.ConnectionSession);
        Assert.AreEqual("recorded result", OctopusSampleState.SessionDetail);
        Assert.AreEqual(7, OctopusSampleState.UnseenNotifications);
        Assert.IsTrue(OctopusSampleState.HasCommunityAccess);
        Assert.AreEqual("fr", OctopusSampleState.LocaleOverride);
        Assert.IsEmpty(_sdk.Methods);
        Apply(1);
        Assert.IsEmpty(_sdk.Methods, "Reapplying the current profile must reuse initialization.");
    }

    [Test]
    public void ObservationResetKeepsPersistedConfiguration()
    {
        Apply(2);
        OctopusSampleState.ReportUnseenNotifications(7);
        OctopusSampleState.ResetObservations();
        Assert.AreEqual(2, OctopusScenarioSdk.SavedConfiguration);
        Assert.IsFalse(OctopusScenarioSdk.NeedsConfiguration);
        Assert.AreEqual(-1, OctopusSampleState.UnseenNotifications);
    }

    [Test]
    public void ChangeConfigurationSwitchesWithoutASecondInitializeAndPersistsOnlyOnSuccess()
    {
        Apply(1);
        _sdk.Clear();
        _sdk.DeferCompletions = true;
        bool? success = null;
        OctopusScenarioSdk.ApplyConfiguration(2, (ok, message) => success = ok);
        Assert.IsNull(success);
        Assert.AreEqual(1, OctopusScenarioSdk.SavedConfiguration);
        CollectionAssert.Contains(_sdk.Methods, "SwitchCommunity");
        CollectionAssert.DoesNotContain(_sdk.Methods, "Initialize");
        _sdk.PendingCompleted();
        Assert.AreEqual(true, success);
        Assert.AreEqual(2, OctopusScenarioSdk.SavedConfiguration);
        Assert.IsTrue(OctopusSampleFeatureToggles.ForceLogin);
    }

    [Test]
    public void FailedSwitchDoesNotSaveTheRequestedChoiceOrExposeNativeError()
    {
        Apply(1);
        _sdk.CallbackError = "sensitive-native-detail";
        string result = null;
        OctopusScenarioSdk.ApplyConfiguration(2, (ok, message) => { Assert.IsFalse(ok); result = message; });
        Assert.AreEqual(1, OctopusScenarioSdk.SavedConfiguration);
        Assert.IsTrue(OctopusScenarioSdk.NeedsConfiguration);
        StringAssert.DoesNotContain(_sdk.CallbackError, result);
    }

    [Test]
    public void ResetRefusesAnInFlightConfigurationSoItCannotBeSavedAgainByALateCallback()
    {
        Apply(1);
        _sdk.DeferCompletions = true;
        OctopusScenarioSdk.ApplyConfiguration(2, (ok, message) => { });
        string reason;
        Assert.IsFalse(OctopusScenarioSdk.ResetConfiguration(out reason));
        Assert.AreEqual(1, OctopusScenarioSdk.SavedConfiguration);
        _sdk.PendingCompleted();
        Assert.IsTrue(OctopusScenarioSdk.ResetConfiguration(out reason));
        Assert.AreEqual(0, OctopusScenarioSdk.SavedConfiguration);
    }

    [Test]
    public void LateTimedOutSwitchCannotRestoreAResetConfiguration()
    {
        Apply(1);
        var clock = DateTime.UtcNow;
        OctopusScenarioSdk.UtcNow = () => clock;
        _sdk.DeferCompletions = true;
        OctopusScenarioSdk.ApplyConfiguration(2, (ok, message) => { });
        clock += OctopusScenarioSdk.OperationTimeout;
        string reason;
        Assert.IsTrue(OctopusScenarioSdk.ResetConfiguration(out reason));
        _sdk.PendingCompleted();
        Assert.AreEqual(0, OctopusScenarioSdk.SavedConfiguration);
        Assert.IsFalse(OctopusSampleFeatureToggles.ForceLogin);
    }

    [TestCase(1)]
    [TestCase(2)]
    public void LifecycleSwitchLeavesConfigurationOnTheLiveProfile(int started)
    {
        Apply(started);
        var lifecycle = new LifecycleScenario();
        lifecycle.Presets[0].Fill(lifecycle.Fields);
        lifecycle.Execute(() => lifecycle.Presets[0].Run(lifecycle.Fields));
        CollectionAssert.Contains(_sdk.Methods, "SwitchCommunity");

        var live = 3 - started;
        Assert.AreEqual(live, OctopusScenarioSdk.LiveSelection);
        Assert.AreEqual(live, OctopusSampleConfigView.InitialSelection(),
            "Configuration pre-selected the Force login switch, not the profile the switch landed on (#387).");

        // Applying the pre-selection keeps the live community instead of switching back.
        _sdk.Clear();
        Apply(live);
        CollectionAssert.DoesNotContain(_sdk.Methods, "SwitchCommunity");
    }

    [Test]
    public void StopForgetsTheLiveProfile()
    {
        Apply(2);
        Assert.AreEqual(2, OctopusScenarioSdk.LiveSelection);
        OctopusScenarioSdk.LifecycleStopped();
        Assert.AreEqual(0, OctopusScenarioSdk.LiveSelection);
    }

    private static void Apply(int selection)
    {
        bool? result = null;
        OctopusScenarioSdk.ApplyConfiguration(selection, (ok, message) => result = ok);
        Assert.AreEqual(true, result);
    }
}
