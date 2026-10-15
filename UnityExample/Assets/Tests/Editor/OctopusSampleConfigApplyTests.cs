using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public class OctopusSampleConfigApplyTests
{
    private string _key;
    private OctopusRecordingScenarioSdk _sdk;
    private SynchronizationContext _context;
    private OctopusSampleTheme _theme;

    [SetUp]
    public void SetUp()
    {
        _key = "OctopusSample.Tests.U2." + Guid.NewGuid().ToString("N");
        _context = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new InlineContext());
        _theme = OctopusSampleBranding.Theme;
        _sdk = new OctopusRecordingScenarioSdk
        {
            Profile = Profile("test-default-key"), AlternateProfile = Profile("test-forced-key"),
            ManagedProfile = Profile("test-all-key"), SomeManagedProfile = Profile("test-some-key"),
            DemoServerHost = "demo-injected.example.test"
        };
        OctopusSampleFeatureToggles.Reset();
        OctopusScenarioSdk.Use(_sdk, _key);
        OctopusSampleLog.Current = OctopusSampleLog.None;
    }

    [TearDown]
    public void TearDown()
    {
        var view = UnityEngine.Object.FindAnyObjectByType<OctopusSampleConfigView>();
        if (view != null) UnityEngine.Object.DestroyImmediate(view.gameObject);
        foreach (var suffix in new[] { "", ".CustomServer", ".Host", ".User", ".UserSelected", ".Navy", ".Appearance" })
            PlayerPrefs.DeleteKey(_key + suffix);
        PlayerPrefs.Save();
        OctopusScenarioSdk.Use(null);
        OctopusSampleFeatureToggles.Reset();
        OctopusSampleBranding.Theme = _theme;
        SynchronizationContext.SetSynchronizationContext(_context);
    }

    [Test]
    public void DraftAndCancelLeaveSessionBuildProfileAndPreferencesAlone()
    {
        Apply(OctopusScenarioSdk.ConfigurationDraft());
        _sdk.Clear();
        var view = OctopusSampleConfigView.Open();
        view.Draft.Community = 2;
        view.Draft.CustomServer = true;
        view.Draft.CustomHost = "custom.example.test";
        view.Draft.UserId = "qa-tester-ios";
        view.Draft.Appearance = 1;
        view.Draft.NavyTheme = false;
        Assert.AreEqual("test-user", _sdk.Profile.userId);
        Assert.AreEqual(1, PlayerPrefs.GetInt(_key));
        Assert.AreEqual("demo-injected.example.test", OctopusScenarioSdk.RunningServerHost);
        Assert.IsEmpty(_sdk.Methods);
        view.Close();
        Assert.AreEqual(1, OctopusScenarioSdk.ConfigurationDraft().Community);
        Assert.AreEqual("test-user", OctopusScenarioSdk.ConfigurationDraft().UserId);
        Assert.IsEmpty(_sdk.Methods);
    }

    [Test]
    public void ApplyReportsChangedAndUnchangedWithoutClosingTheScreen()
    {
        Apply(OctopusScenarioSdk.ConfigurationDraft());
        var view = OctopusSampleConfigView.Open();
        view.SelectProfile(2);
        view.Apply();
        Assert.IsTrue(view != null);
        Assert.AreEqual(OctopusScenarioSdk.AppliedMessage, view.Result);
        Assert.AreEqual(2, PlayerPrefs.GetInt(_key));
        _sdk.Clear();
        view.Apply();
        Assert.AreEqual(OctopusScenarioSdk.UnchangedMessage, view.Result);
        Assert.IsEmpty(_sdk.Methods);
    }

    [Test]
    public void FailedApplyKeepsDraftAndSanitizesNativeError()
    {
        Apply(OctopusScenarioSdk.ConfigurationDraft());
        _sdk.CallbackError = "credential-shaped-native-detail";
        var view = OctopusSampleConfigView.Open();
        view.SelectProfile(2);
        view.Apply();
        Assert.IsTrue(view != null);
        Assert.AreEqual(2, view.Selection);
        Assert.IsFalse(view.IsApplying);
        StringAssert.Contains("could not be applied", view.Result);
        StringAssert.DoesNotContain(_sdk.CallbackError, view.Result);
        Assert.AreEqual(1, PlayerPrefs.GetInt(_key));
    }

    [Test]
    public void RetryAfterSwitchErrorResynchronizesWithoutASecondInitialize()
    {
        Apply(OctopusScenarioSdk.ConfigurationDraft());
        _sdk.CallbackError = "synthetic-error";
        var draft = OctopusScenarioSdk.ConfigurationDraft();
        draft.Community = 2;
        OctopusScenarioSdk.ApplyConfiguration(draft, (ok, message) => Assert.IsFalse(ok));
        _sdk.CallbackError = null;
        _sdk.Clear();
        Apply(draft);
        CollectionAssert.Contains(_sdk.Methods, "SwitchCommunity");
        CollectionAssert.DoesNotContain(_sdk.Methods, "Initialize");
        Assert.AreEqual(2, OctopusScenarioSdk.LiveSelection);
    }

    [Test]
    public void DemoAlwaysResolvesBuildHostAndDeletesSavedCustomHost()
    {
        var draft = OctopusScenarioSdk.ConfigurationDraft();
        draft.CustomServer = true;
        draft.CustomHost = "old.example.test";
        Apply(draft);
        Assert.AreEqual("old.example.test", OctopusScenarioSdk.RunningServerHost);
        draft = OctopusScenarioSdk.ConfigurationDraft();
        draft.CustomServer = false;
        Apply(draft);
        Assert.AreEqual("demo-injected.example.test", OctopusScenarioSdk.RunningServerHost);
        Assert.IsFalse(PlayerPrefs.HasKey(_key + ".Host"));
        _sdk.DemoServerHost = "new-build.example.test";
        OctopusScenarioSdk.Use(_sdk, _key);
        OctopusScenarioSdk.RestoreInitializedSdk();
        Assert.AreEqual("new-build.example.test", OctopusScenarioSdk.RunningServerHost);
    }

    [TestCase("")]
    [TestCase("https://example.test")]
    [TestCase("example.test:443")]
    [TestCase("example.test/path")]
    public void InvalidCustomHostRefusesBeforeCallingSdk(string host)
    {
        var draft = OctopusScenarioSdk.ConfigurationDraft();
        draft.CustomServer = true;
        draft.CustomHost = host;
        bool? result = null;
        OctopusScenarioSdk.ApplyConfiguration(draft, (ok, message) => result = ok);
        Assert.AreEqual(false, result);
        Assert.IsEmpty(_sdk.Methods);
        Assert.IsFalse(PlayerPrefs.HasKey(_key));
    }

    [Test]
    public void KeysAreMaskedInLabelsAndNeverPersistedEvenForCustomCommunity()
    {
        var view = OctopusSampleConfigView.Open();
        view.ToggleSection(1);
        view.SelectProfile(5);
        const string key = "synthetic-u2-custom-key";
        view.Draft.CustomApiKey = key;
        view.ToggleSection(1);
        view.ToggleSection(1);
        var input = view.GetComponentsInChildren<TMP_InputField>().Single();
        Assert.AreEqual(TMP_InputField.ContentType.Password, input.contentType);
        input.ForceLabelUpdate();
        foreach (var text in view.GetComponentsInChildren<TMP_Text>())
            StringAssert.DoesNotContain(key, text.text);
        StringAssert.DoesNotContain(key, view.Draft.ToString());
        foreach (var shortKey in new[] { "a", "abc", key })
            StringAssert.DoesNotContain(shortKey, OctopusSampleConfigurationDraft.MaskApiKey(shortKey));
        view.Apply();
        Assert.AreEqual(5, PlayerPrefs.GetInt(_key));
        foreach (var suffix in new[] { "", ".Host", ".User", ".CustomApiKey", ".ApiKey" })
            StringAssert.DoesNotContain(key, PlayerPrefs.GetString(_key + suffix, ""));
        OctopusScenarioSdk.Use(_sdk, _key);
        OctopusScenarioSdk.RestoreInitializedSdk();
        Assert.IsFalse(OctopusSampleState.IsInitialized);
        StringAssert.Contains("No API key", OctopusScenarioSdk.StartupFailureReason);
    }

    [TestCase(3, 3)]
    [TestCase(4, 1)]
    public void ManagedPresetsReachExistingSsoMode(int selection, int fieldCount)
    {
        var draft = OctopusScenarioSdk.ConfigurationDraft();
        draft.Community = selection;
        Apply(draft);
        var init = _sdk.Calls.First(c => c.Method == "Initialize");
        Assert.AreEqual(fieldCount, ((ConnectionMode)init.Args[1]).AppManagedFields.Count);
        Assert.IsTrue(OctopusSampleFeatureToggles.ForceLogin);
        Assert.AreEqual(selection, OctopusScenarioSdk.LiveSelection);
    }

    [Test]
    public void SelectingAUserCallsConnectOnlyOnApplyAndDoesNotMutateBundledIdentity()
    {
        Apply(OctopusScenarioSdk.ConfigurationDraft());
        _sdk.Clear();
        var draft = OctopusScenarioSdk.ConfigurationDraft();
        draft.UserId = "qa-tester-ios";
        Assert.IsEmpty(_sdk.Methods);
        Apply(draft);
        var call = _sdk.Calls.First(c => c.Method == "ConnectUser");
        Assert.AreEqual("qa-tester-ios", call.Args[0]);
        Assert.IsNull(call.Args[1], "Do not carry another user's nickname into the new identity.");
        Assert.AreEqual("test-user", _sdk.Profile.userId);
        Assert.AreEqual("qa-tester-ios", OctopusScenarioSdk.ConfigurationDraft().UserId);
    }

    [Test]
    public void ThemeDraftDoesNotApplyUntilSubmitAndSdkDefaultUnsetsEverySlot()
    {
        Apply(OctopusScenarioSdk.ConfigurationDraft());
        _sdk.Clear();
        var draft = OctopusScenarioSdk.ConfigurationDraft();
        draft.NavyTheme = false;
        draft.Appearance = 0;
        Assert.IsEmpty(_sdk.Methods);
        Apply(draft);
        var colors = (OctopusColorScheme)_sdk.Calls.First(c => c.Method == "ApplyTheme").Args[0];
        Assert.AreEqual(0, OctopusSDK.ColorToInt(colors.Primary));
        Assert.AreEqual(0, OctopusSDK.ColorToInt(colors.Background));
        Assert.AreEqual(0, _sdk.Calls.Last(c => c.Method == "SetColorSchemeType").Args[0]);
        _sdk.Clear();
        draft.NavyTheme = true;
        Apply(draft);
        Assert.AreEqual(OctopusSampleBranding.SdkLightPrimaryMain,
            ((OctopusColorScheme)_sdk.Calls.First(c => c.Method == "ApplyTheme").Args[0]).Primary);
    }

    [Test]
    public void TimeoutReleasesApplyAndLateCallbackCannotOverwriteRetry()
    {
        Apply(OctopusScenarioSdk.ConfigurationDraft());
        var now = DateTime.UtcNow;
        OctopusScenarioSdk.UtcNow = () => now;
        _sdk.DeferCompletions = true;
        var view = OctopusSampleConfigView.Open();
        view.SelectProfile(2);
        view.Apply();
        var late = _sdk.PendingCompleted;
        Assert.IsTrue(view.IsApplying);
        view.Close();
        Assert.IsTrue(view != null);
        now += OctopusScenarioSdk.OperationTimeout;
        view.CheckApplyTimeout();
        Assert.IsFalse(view.IsApplying);
        StringAssert.Contains("retry", view.Result);
        _sdk.Clear();
        string refused;
        Assert.IsNull(OctopusScenarioSdk.EnsurePilotInitialized(out refused));
        StringAssert.Contains("still pending", refused);
        Assert.IsFalse(OctopusScenarioSdk.TryBeginOperation("Lifecycle.Stop", out refused));
        Assert.IsEmpty(_sdk.Methods);
        view.SelectProfile(1);
        _sdk.DeferCompletions = false;
        view.Apply();
        StringAssert.Contains("still pending", view.Result);
        Assert.AreEqual(1, PlayerPrefs.GetInt(_key));
        late();
        Assert.AreEqual(1, PlayerPrefs.GetInt(_key));
        view.Apply();
        Assert.AreEqual(1, PlayerPrefs.GetInt(_key));
        Assert.AreEqual(1, OctopusScenarioSdk.LiveSelection);
    }

    [Test]
    public void RetryAfterUserConnectionErrorRestoresSessionWithSwitchCommunity()
    {
        Apply(OctopusScenarioSdk.ConfigurationDraft());
        _sdk.DeferCompletions = true;
        var draft = OctopusScenarioSdk.ConfigurationDraft();
        draft.UserId = "qa-tester-ios";
        bool? success = null;
        OctopusScenarioSdk.ApplyConfiguration(draft, (ok, message) => success = ok);
        _sdk.FailPending("synthetic-connection-error");
        Assert.AreEqual(false, success);
        _sdk.DeferCompletions = false;
        _sdk.Clear();
        Apply(draft);
        CollectionAssert.Contains(_sdk.Methods, "SwitchCommunity");
        CollectionAssert.Contains(_sdk.Methods, "ConnectUser");
        CollectionAssert.DoesNotContain(_sdk.Methods, "Initialize");
    }

    [Test]
    public void SwitchingPresetAndUserBackToBuildIdentityReconnectsThatUser()
    {
        Apply(OctopusScenarioSdk.ConfigurationDraft());
        var draft = OctopusScenarioSdk.ConfigurationDraft();
        draft.UserId = "qa-tester-ios";
        Apply(draft);
        _sdk.EmitProfile(new OctopusProfile(clientUserId: "qa-tester-ios"));
        _sdk.Clear();
        draft = OctopusScenarioSdk.ConfigurationDraft();
        draft.Community = 2;
        draft.UserId = "test-user";
        Apply(draft);
        CollectionAssert.Contains(_sdk.Methods, "SwitchCommunity");
        Assert.AreEqual("test-user", _sdk.Calls.First(c => c.Method == "ConnectUser").Args[0]);
    }

    [Test]
    public void SwitchingCommunityReconnectsTheAlreadySelectedUser()
    {
        Apply(OctopusScenarioSdk.ConfigurationDraft());
        var draft = OctopusScenarioSdk.ConfigurationDraft();
        draft.UserId = "qa-tester-ios";
        Apply(draft);
        _sdk.Clear();
        draft = OctopusScenarioSdk.ConfigurationDraft();
        draft.Community = 2;
        Apply(draft);
        Assert.AreEqual("qa-tester-ios", _sdk.Calls.First(c => c.Method == "ConnectUser").Args[0]);
    }

    [Test]
    public void ApplyingSystemAfterSettingsDarkRestoresActualNativePolicy()
    {
        var draft = OctopusScenarioSdk.ConfigurationDraft();
        draft.Appearance = 0;
        Apply(draft);
        var view = OctopusSampleConfigView.Open();
        Assert.AreEqual(0, view.Draft.Appearance);
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
        Assert.AreEqual(2, OctopusScenarioSdk.ConfigurationDraft().Appearance);
        _sdk.Clear();
        view.Apply();
        Assert.AreEqual(OctopusScenarioSdk.AppliedMessage, view.Result);
        Assert.AreEqual(0, _sdk.Calls.Last(c => c.Method == "SetColorSchemeType").Args[0]);
    }

    private static OctopusExampleConfig.ExampleProfile Profile(string key)
    {
        return new OctopusExampleConfig.ExampleProfile { apiKey = key, userId = "test-user", signingSecret = "test-signing-secret" };
    }

    [TestCase(1, OctopusSampleTheme.Light)]
    [TestCase(2, OctopusSampleTheme.Dark)]
    public void ExplicitNativeAppearanceMapsToTheCanvasEnum(int appearance, OctopusSampleTheme theme)
    {
        var draft = OctopusScenarioSdk.ConfigurationDraft();
        draft.Appearance = appearance;
        Apply(draft);
        Assert.AreEqual(theme, OctopusSampleBranding.Theme);
        Assert.AreEqual(appearance, _sdk.Calls.Last(c => c.Method == "SetColorSchemeType").Args[0]);
    }

    private static void Apply(OctopusSampleConfigurationDraft draft)
    {
        bool? result = null;
        OctopusScenarioSdk.ApplyConfiguration(draft, (ok, message) => result = ok);
        Assert.AreEqual(true, result);
    }

    private sealed class InlineContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object state) { callback(state); }
    }
}
