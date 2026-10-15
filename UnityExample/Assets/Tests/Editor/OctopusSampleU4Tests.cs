using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OctopusSampleU4Tests
{
    private string _key, _localeKey, _hostPrefix;
    private OctopusRecordingScenarioSdk _sdk;
    private OctopusSampleTheme _theme;
    [SetUp] public void SetUp()
    {
        _key = "OctopusSample.Tests.U4." + Guid.NewGuid().ToString("N");
        _localeKey = OctopusSampleLanguage.PreferenceKey; OctopusSampleLanguage.PreferenceKey = _key + ".Locale";
        _hostPrefix = OctopusSampleHostIntegration.Prefix; OctopusSampleHostIntegration.Prefix = _key + ".Integration.";
        _theme = OctopusSampleBranding.Theme;
        OctopusSampleSystemAppearance.Reader = () => OctopusSampleTheme.Light;
        _sdk = new OctopusRecordingScenarioSdk { Profile = new OctopusExampleConfig.ExampleProfile { apiKey = "test-key", userId = "test-user" },
            AlternateProfile = new OctopusExampleConfig.ExampleProfile { apiKey = "test-alt", userId = "test-user" } };
        OctopusScenarioSdk.Use(_sdk, _key);
    }
    [TearDown] public void TearDown()
    {
        foreach (var view in UnityEngine.Object.FindObjectsByType<OctopusSampleSettingsDetailView>(FindObjectsSortMode.None)) view.Close();
        var appearance = UnityEngine.Object.FindAnyObjectByType<OctopusSampleAppearanceView>(); if (appearance != null) appearance.Close();
        var callback = UnityEngine.Object.FindAnyObjectByType<OctopusSampleHostCallbackView>(); if (callback != null) callback.Close();
        OctopusSampleHostIntegration.Clear(); OctopusSampleLanguage.Clear();
        OctopusSampleLanguage.PreferenceKey = _localeKey; OctopusSampleHostIntegration.Prefix = _hostPrefix;
        foreach (var suffix in new[] { "", ".CustomServer", ".Host", ".User", ".UserSelected", ".Navy", ".Appearance" }) PlayerPrefs.DeleteKey(_key + suffix);
        OctopusSampleSystemAppearance.Reader = null; OctopusScenarioSdk.Use(null); OctopusSampleBranding.Theme = _theme;
    }
    private void Start() { OctopusScenarioSdk.ApplyConfiguration(1, (ok, message) => Assert.IsTrue(ok, message)); }
    private static Transform Find(Component root, string id) { return root.GetComponentsInChildren<Transform>(true).Single(t => t.name == id); }
    [Test] public void FailedStartupForgetsEverySavedNonsecretChoice()
    {
        PlayerPrefs.SetInt(_key, 99);
        foreach (var suffix in new[] { ".CustomServer", ".Host", ".User", ".UserSelected", ".Navy", ".Appearance" }) PlayerPrefs.SetString(_key + suffix, "stale");
        OctopusScenarioSdk.RestoreInitializedSdk();
        foreach (var suffix in new[] { "", ".CustomServer", ".Host", ".User", ".UserSelected", ".Navy", ".Appearance" }) Assert.IsFalse(PlayerPrefs.HasKey(_key + suffix), suffix);
    }
    [Test] public void CommunitySwitchReplacesTheLiveHostAndUserAndStopDropsItsDraft()
    {
        Start();
        OctopusScenarioSdk.CommunitySwitched(new OctopusExampleConfig.ExampleProfile { apiKey = "test-alt", apiServerHost = "demo.example.invalid", userId = "alt-user" }, 2);
        var draft = OctopusScenarioSdk.ConfigurationDraft();
        Assert.AreEqual(2, draft.Community); Assert.AreEqual("alt-user", draft.UserId); Assert.IsTrue(draft.CustomServer);
        Assert.AreEqual("demo.example.invalid", draft.CustomHost);
        OctopusScenarioSdk.LifecycleStopped();
        Assert.AreEqual(0, OctopusScenarioSdk.LiveSelection); Assert.IsNull(OctopusScenarioSdk.RunningServerHost);
        Assert.AreNotEqual("alt-user", OctopusScenarioSdk.ConfigurationDraft().UserId);
    }
    [Test] public void AppearanceSystemFollowsOsChangesWithoutForcingNativeLightOrDark()
    {
        Start();
        OctopusScenarioSdk.SetAppearance(true, 0);
        Assert.AreEqual(OctopusSampleTheme.Light, OctopusSampleBranding.Theme);
        OctopusSampleSystemAppearance.Reader = () => OctopusSampleTheme.Dark;
        OctopusSampleNativeTheme.RefreshSystemAppearance();
        Assert.AreEqual(OctopusSampleTheme.Dark, OctopusSampleBranding.Theme);
        Assert.AreEqual(0, OctopusSampleNativeTheme.CurrentAppearance);
        Assert.AreEqual(0, _sdk.ThemeCalls.Last().Args[0]);
        Assert.AreEqual(0, PlayerPrefs.GetInt(_key + ".Appearance"));
    }
    [Test] public void AppearanceScreenHasThreeChoicesAndBothPresets()
    {
        var view = OctopusSampleAppearanceView.Open();
        foreach (var id in new[] { "settings-appearance-system", "settings-appearance-light", "settings-appearance-dark", "appearance-theme-navy", "appearance-theme-default" }) Assert.IsNotNull(Find(view, id));
        Find(view, "appearance-theme-default").GetComponent<Button>().onClick.Invoke();
        Assert.IsFalse(OctopusSampleNativeTheme.NavyTheme);
    }
    [Test] public void LanguageOffersAndroidLanguagesAndAppliesThroughExistingFacade()
    {
        Start(); var view = OctopusSampleLanguageView.Open();
        CollectionAssert.AreEqual(new[] { "", "en", "fr", "de", "it", "pl", "pt", "es", "sv", "tr" }, OctopusSampleLanguage.Codes);
        foreach (var code in OctopusSampleLanguage.Codes) Assert.IsNotNull(Find(view, "language-" + (code == "" ? "system" : code)));
        view.Select("de"); Assert.AreEqual("de", OctopusSampleState.LocaleOverride); Assert.AreEqual("de", OctopusSampleLanguage.Selected);
        Assert.AreEqual("OverrideDefaultLocale", _sdk.Calls.Last().Method); Assert.AreEqual("de", _sdk.Calls.Last().Args[0]);
        view.Select(""); Assert.IsNull(OctopusSampleState.LocaleOverride); Assert.IsNotNull(_sdk.Calls.Last().Args[0]);
    }
    [Test] public void ConfigurationIntegrationIsAClickableRouteAndKeepsTheUnsavedDraft()
    {
        var config = OctopusSampleConfigView.Open(); config.SelectProfile(2);
        Find(config, OctopusSampleConfigView.IntegrationId).GetComponent<Button>().onClick.Invoke();
        var view = UnityEngine.Object.FindAnyObjectByType<OctopusSampleHostIntegrationView>(); Assert.IsNotNull(view);
        view.Close(); Assert.AreEqual(2, config.Draft.Community);
    }
    [Test] public void HostScreenExposesSupportedCallbacksAndMarksProfileEditUnavailable()
    {
        var view = OctopusSampleHostIntegrationView.Open();
        foreach (var id in new[] { "profile", "profile-edit", "login", "client-object", "group-access-denied" }) Assert.IsNotNull(Find(view, "host-integration-" + id + "-switch"));
        Assert.IsFalse(Find(view, "host-integration-profile-edit-switch").GetComponent<Button>().interactable);
        Assert.IsFalse(Find(view, "host-integration-login-switch").GetComponent<Button>().interactable);
        Find(view, "host-integration-client-object-switch").GetComponent<Button>().onClick.Invoke();
        Assert.IsFalse(OctopusSampleHostIntegration.ClientObject);
    }
    [Test] public void UrlPolicyHasAnOctopusFallbackAndUsesCachedDecisions()
    {
        Start(); OctopusSampleHostIntegration.SetUrlMode(0);
        Assert.AreEqual(UrlOpeningStrategy.HandledByOctopus, OctopusSampleHostIntegration.DecideUrl(OctopusSampleHostIntegration.UrlMode, "https://example.invalid"));
        OctopusSampleHostIntegration.SetUrlMode(1);
        Assert.AreEqual(UrlOpeningStrategy.HandledByApp, OctopusSampleHostIntegration.DecideUrl(OctopusSampleHostIntegration.UrlMode, "https://example.invalid"));
        OctopusSampleHostIntegration.SetUrlMode(2); Assert.IsNull(OctopusSDK.NavigateToUrlHandler);
    }
    [Test] public void ResetStopsThenClearsLocalSetupAndAnErrorKeepsIt()
    {
        Start(); _sdk.CallbackError = "refused"; bool ok = true;
        OctopusScenarioSdk.ResetLocalSetup((success, message) => ok = success);
        Assert.IsFalse(ok); Assert.AreEqual(1, OctopusScenarioSdk.SavedConfiguration); Assert.IsTrue(OctopusSampleState.IsInitialized);
        _sdk.CallbackError = null; OctopusScenarioSdk.ResetLocalSetup((success, message) => ok = success);
        Assert.IsTrue(ok); Assert.AreEqual(0, OctopusScenarioSdk.SavedConfiguration); Assert.IsFalse(OctopusSampleState.IsInitialized);
        CollectionAssert.Contains(_sdk.Methods, "Stop");
    }
    [Test] public void DebugInfoContainsPinsSessionConfigurationAndHostFactsWithoutCredentials()
    {
        var rows = OctopusSampleDiagnostics.Facts();
        foreach (var id in new[] { "debug-info-native-build-pins", "debug-info-initialized", "debug-info-last-connection-call", "debug-info-host", "debug-info-build", "debug-info-url-route" }) Assert.IsTrue(rows.Any(r => r.Id == id));
        Assert.IsFalse(rows.Any(r => r.Value != null && r.Value.Contains("test-key")));
    }
}
