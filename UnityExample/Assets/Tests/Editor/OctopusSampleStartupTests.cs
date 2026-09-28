using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Covers <see cref="OctopusScenarioSdk.RestoreInitializedSdk"/>: the auto-init-on-launch path
/// that replays the last successfully validated scenario configuration, without a tap, on every
/// launch after the first. Lives alongside the sample it tests (not in the published package) so
/// it can call the sample's internal startup surface directly instead of through reflection.
/// </summary>
public class OctopusSampleStartupTests
{
    private const string PreferenceKey = "OctopusSample.Tests.ValidatedStartupConfig";
    private OctopusRecordingScenarioSdk _sdk;
    private IOctopusSampleLog _previousLog;
    private ProbeLog _log;

    [SetUp]
    public void SetUp()
    {
        _previousLog = OctopusSampleLog.Current;
        _log = new ProbeLog();
        OctopusSampleLog.Current = _log;
        PlayerPrefs.DeleteKey(PreferenceKey);
        OctopusSampleFeatureToggles.Reset();
        NewLaunch();
    }

    [TearDown]
    public void TearDown()
    {
        OctopusScenarioSdk.Use(null, null);
        OctopusSampleFeatureToggles.Reset();
        PlayerPrefs.DeleteKey(PreferenceKey);
        PlayerPrefs.Save();
        OctopusSampleLog.Current = _previousLog;
    }

    [Test]
    public void FirstLaunchDoesNotInitializeOrPersistAnything()
    {
        Restore();
        Assert.AreEqual(0, _sdk.Methods.Count);
        Assert.IsFalse(OctopusSampleState.IsInitialized);
        Assert.AreEqual(0, Saved);
    }

    [TestCase(false, 1)]
    [TestCase(true, 2)]
    public void SuccessfulTapRoundTripsTheSelectedProfileOnNextLaunch(bool forced, int saved)
    {
        OctopusSampleFeatureToggles.SetForceLogin(forced);
        TapLocalePreset();
        Assert.IsTrue(OctopusSampleState.IsInitialized);
        Assert.AreEqual(saved, Saved);

        NewLaunch();
        Restore();
        Assert.IsTrue(OctopusSampleState.IsInitialized);
        Assert.AreEqual(forced, OctopusSampleFeatureToggles.ForceLogin);
        CollectionAssert.AreEqual(new[] { "Initialize" }, _sdk.ScenarioMethods);
        Assert.AreEqual("READY", OctopusSampleHomeView.SdkStatusLabel());
    }

    [Test]
    public void StartupTwiceAndThenAPresetDoesNotInitializeAgain()
    {
        Initialize();
        NewLaunch();
        Restore();
        Restore();
        TapLocalePreset();
        CollectionAssert.AreEqual(new[] { "Initialize", "OverrideDefaultLocale" }, _sdk.ScenarioMethods);
    }

    [Test]
    public void InvalidSavedSelectionReportsFailureWithoutCallingTheSdk()
    {
        PlayerPrefs.SetInt(PreferenceKey, 99);
        Assert.DoesNotThrow(Restore);
        Assert.AreEqual(0, _sdk.Methods.Count);
        StringAssert.Contains("Saved configuration is invalid", OctopusSampleState.SessionDetail);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void InvalidConfigDoesNotOptInAndStartupRefusalIsVisible(string key)
    {
        if (key == null) _sdk.Profile = null;
        else _sdk.Profile.apiKey = key;
        Assert.IsNull(Initialize());
        Assert.AreEqual(0, Saved);
        PlayerPrefs.SetInt(PreferenceKey, 1);
        Assert.DoesNotThrow(Restore);
        Assert.IsFalse(OctopusSampleState.IsInitialized);
        Assert.AreEqual(0, _sdk.Methods.Count);
        StringAssert.Contains("Startup initialization failed", OctopusSampleHomeView.ConnectionDetail());
    }

    [Test]
    public void FailedStartupCanBeRetriedByAPreset()
    {
        PlayerPrefs.SetInt(PreferenceKey, 1);
        _sdk.Profile = null;
        Restore();
        _sdk.Profile = NewProfile();
        Assert.IsNotNull(Initialize());
        Assert.IsTrue(OctopusSampleState.IsInitialized);
        CollectionAssert.AreEqual(new[] { "Initialize" }, _sdk.ScenarioMethods);
        Assert.IsNull(OctopusSampleState.SessionDetail);
    }

    [TestCase(99)]
    [TestCase(-1)]
    public void InvalidSavedSelectionIsForgottenSoTheNextLaunchIsCold(int value)
    {
        PlayerPrefs.SetInt(PreferenceKey, value);
        Restore();
        Assert.AreEqual(0, Saved, "A bad saved selection survived the failed restore (#342).");

        NewLaunch();
        Restore();
        Assert.AreEqual(0, _sdk.Methods.Count);
        Assert.AreEqual(OctopusSampleState.Session.None, OctopusSampleState.ConnectionSession,
            "The second launch failed again on the same saved value.");
    }

    [TestCase(1)]
    [TestCase(2)]
    public void RefusedRestoreIsForgottenAndLeavesForceLoginAtItsDefault(int saved)
    {
        PlayerPrefs.SetInt(PreferenceKey, saved);
        _sdk.Profile = null;
        Restore();
        Assert.IsFalse(OctopusSampleState.IsInitialized);
        Assert.AreEqual(0, Saved);
        Assert.IsFalse(OctopusSampleFeatureToggles.ForceLogin,
            "A failed restore left the switch on the profile that could not start.");
        Assert.IsTrue(OctopusScenarioSdk.NeedsConfiguration);
    }

    [Test]
    public void ThrowingRestoreIsForgotten()
    {
        PlayerPrefs.SetInt(PreferenceKey, 2);
        _sdk.InitializeThrows = true;
        Assert.DoesNotThrow(Restore);
        Assert.AreEqual(0, Saved);
        Assert.IsFalse(OctopusSampleFeatureToggles.ForceLogin);
        StringAssert.Contains("Startup initialization failed", OctopusSampleState.SessionDetail);
    }

    [Test]
    public void AThrowAfterASuccessfulInitKeepsTheSavedProfile()
    {
        PlayerPrefs.SetInt(PreferenceKey, 2);
        _sdk.ApplyThemeThrows = true;
        Assert.DoesNotThrow(Restore);
        Assert.IsTrue(OctopusSampleState.IsInitialized);
        Assert.AreEqual(2, Saved, "A setup step after a successful init forgot the saved profile (#387).");
        Assert.IsTrue(OctopusSampleFeatureToggles.ForceLogin);
        Assert.AreNotEqual(OctopusSampleState.Session.StartupFailed, OctopusSampleState.ConnectionSession);
        Assert.IsNull(OctopusScenarioSdk.StartupFailureReason);
    }

    [Test]
    public void StartupFailureIsNotDescribedAsAFailedConnectionCall()
    {
        PlayerPrefs.SetInt(PreferenceKey, 99);
        Restore();
        Assert.AreEqual(OctopusSampleState.Session.StartupFailed, OctopusSampleState.ConnectionSession);
        Assert.AreEqual("STARTUP FAILED", OctopusSampleHomeView.ConnectionStatusLabel());
        var detail = OctopusSampleHomeView.ConnectionDetail();
        StringAssert.Contains("no connection call was made", detail);
        StringAssert.DoesNotContain("the call did not complete", detail);
        StringAssert.Contains("Startup initialization failed", OctopusScenarioSdk.StartupFailureReason);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RestoringAProfileLogsNoToggleFlip(bool forced)
    {
        OctopusSampleFeatureToggles.SetForceLogin(forced);
        TapLocalePreset();
        NewLaunch();
        _log.StateChanges.Clear();

        Restore();
        Assert.IsTrue(OctopusSampleState.IsInitialized);
        Assert.AreEqual(forced, OctopusSampleFeatureToggles.ForceLogin);
        CollectionAssert.DoesNotContain(_log.StateHeadlines, OctopusSampleFeatureToggles.ToggledHeadline,
            "The startup replay logged a toggle flip nobody made (#342).");
    }

    [Test]
    public void SuccessfulRetryClearsTheStartupFailureReason()
    {
        PlayerPrefs.SetInt(PreferenceKey, 1);
        _sdk.Profile = null;
        Restore();
        Assert.IsNotNull(OctopusScenarioSdk.StartupFailureReason);
        _sdk.Profile = NewProfile();
        Assert.IsNotNull(Initialize());
        Assert.IsNull(OctopusScenarioSdk.StartupFailureReason);
        Assert.AreEqual(OctopusSampleState.Session.None, OctopusSampleState.ConnectionSession);
    }

    private void NewLaunch()
    {
        OctopusSampleFeatureToggles.Reset();
        _sdk = new OctopusRecordingScenarioSdk { Profile = NewProfile() };
        OctopusScenarioSdk.Use(_sdk, PreferenceKey);
    }

    private static OctopusExampleConfig.ExampleProfile NewProfile()
    {
        return new OctopusExampleConfig.ExampleProfile { apiKey = "not-a-real-key" };
    }

    private static int Saved { get { return PlayerPrefs.GetInt(PreferenceKey, 0); } }

    private static void Restore() { OctopusScenarioSdk.RestoreInitializedSdk(); }

    private static OctopusExampleConfig.ExampleProfile Initialize()
    {
        string reason;
        return OctopusScenarioSdk.EnsurePilotInitialized(out reason);
    }

    private static void TapLocalePreset()
    {
        var pilot = new LocaleScenario();
        var preset = pilot.Presets[0];
        preset.Fill(pilot.Fields);
        preset.Run(pilot.Fields);
    }

    private sealed class ProbeLog : IOctopusSampleLog
    {
        public readonly List<string> StateHeadlines = new List<string>();
        public List<string> StateChanges { get { return StateHeadlines; } }

        public void LogApiCall(string method, string detail = null) { }

        public void LogStateChange(string headline, string detail = null)
        {
            StateHeadlines.Add(headline);
        }
    }
}
