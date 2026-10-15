using System;
using System.Threading.Tasks;
using UnityEngine;

public static partial class OctopusScenarioSdk
{
    private static OctopusSampleConfigurationDraft _liveConfiguration;
    private static bool _configurationMayChangeSession;
    private static bool _configurationNativePending;
    private static int _configurationNativeToken;
    private static bool _configurationRequiresSwitch;
    public const string AppliedMessage = "Applied · configuration changed";
    public const string UnchangedMessage = "Nothing to apply · the configuration is unchanged";

    public static int SavedConfiguration
    {
        get
        {
            var value = _startupPreferenceKey == null ? 0 : PlayerPrefs.GetInt(_startupPreferenceKey, 0);
            return value >= 1 && value <= 5 ? value : 0;
        }
    }

    public static bool NeedsConfiguration
    {
        get { return SavedConfiguration == 0 || !OctopusSampleState.IsInitialized; }
    }

    public static OctopusSampleConfigurationDraft ConfigurationDraft()
    {
        var draft = _liveConfiguration == null ? LoadConfiguration() : _liveConfiguration.Copy();
        if (OctopusSampleState.IsInitialized && _liveSelection != 0) draft.Community = _liveSelection;
        // Appearance is also editable in Settings; never hydrate an obsolete explicit choice.
        if (OctopusSampleState.IsInitialized)
        {
            draft.Appearance = OctopusSampleNativeTheme.CurrentAppearance;
            draft.NavyTheme = OctopusSampleNativeTheme.NavyTheme;
        }
        else if (!draft.Appearance.HasValue) draft.Appearance = OctopusSampleNativeTheme.SchemeTypeFor(OctopusSampleBranding.Theme);
        if (_activeProfile != null) draft.UserId = _activeProfile.userId;
        if (draft.UserId == null) draft.UserId = _sdk.Profile == null ? null : _sdk.Profile.userId;
        return draft;
    }

    private static OctopusSampleConfigurationDraft LoadConfiguration()
    {
        var draft = new OctopusSampleConfigurationDraft { Community = SavedConfiguration == 0 ? 1 : SavedConfiguration };
        if (_startupPreferenceKey == null) return draft;
        draft.CustomServer = PlayerPrefs.GetInt(_startupPreferenceKey + ".CustomServer", 0) == 1;
        draft.CustomHost = draft.CustomServer ? PlayerPrefs.GetString(_startupPreferenceKey + ".Host", "") : "";
        draft.UserId = PlayerPrefs.GetString(_startupPreferenceKey + ".User", "");
        if (draft.UserId.Length == 0) draft.UserId = null;
        draft.UserSelected = PlayerPrefs.GetInt(_startupPreferenceKey + ".UserSelected", 0) == 1;
        draft.NavyTheme = PlayerPrefs.GetInt(_startupPreferenceKey + ".Navy", 1) == 1;
        var appearance = PlayerPrefs.GetInt(_startupPreferenceKey + ".Appearance", -1);
        draft.Appearance = appearance >= 0 && appearance <= 2 ? (int?)appearance : null;
        return draft;
    }

    private static bool SaveConfiguration(OctopusSampleConfigurationDraft draft)
    {
        if (_startupPreferenceKey == null) return true;
        try
        {
            // No key, static token or signing secret is ever persisted, including Custom.
            PlayerPrefs.SetInt(_startupPreferenceKey, draft.Community);
            PlayerPrefs.SetInt(_startupPreferenceKey + ".CustomServer", draft.CustomServer ? 1 : 0);
            if (draft.CustomServer) PlayerPrefs.SetString(_startupPreferenceKey + ".Host", draft.CustomHost.Trim());
            else PlayerPrefs.DeleteKey(_startupPreferenceKey + ".Host");
            PlayerPrefs.SetString(_startupPreferenceKey + ".User", draft.UserId ?? "");
            PlayerPrefs.SetInt(_startupPreferenceKey + ".UserSelected", draft.UserSelected ? 1 : 0);
            PlayerPrefs.SetInt(_startupPreferenceKey + ".Navy", draft.NavyTheme ? 1 : 0);
            PlayerPrefs.SetInt(_startupPreferenceKey + ".Appearance", draft.Appearance ?? -1);
            PlayerPrefs.Save();
            return true;
        }
        catch (Exception) { return false; }
    }

    private static void ClearSavedConfiguration()
    {
        if (_startupPreferenceKey == null) return;
        foreach (var suffix in new[] { "", ".CustomServer", ".Host", ".User", ".UserSelected", ".Navy", ".Appearance" })
            PlayerPrefs.DeleteKey(_startupPreferenceKey + suffix);
    }

    public static void SetAppearance(bool navy, int appearance)
    {
        OctopusSampleNativeTheme.Configure(navy, appearance);
        if (OctopusSampleState.IsInitialized) OctopusSampleNativeTheme.Apply(_sdk);
        if (_liveConfiguration != null)
        {
            _liveConfiguration.NavyTheme = navy;
            _liveConfiguration.Appearance = appearance;
        }
        if (_startupPreferenceKey != null)
        {
            PlayerPrefs.SetInt(_startupPreferenceKey + ".Navy", navy ? 1 : 0);
            PlayerPrefs.SetInt(_startupPreferenceKey + ".Appearance", appearance);
            PlayerPrefs.Save();
        }
    }

    // Stop first: clearing the initialization record while a native session still exists would
    // make the next Apply initialize a second channel. Errors leave the saved setup intact.
    public static void ResetLocalSetup(Action<bool, string> completed)
    {
        string busy;
        if (!TryBeginOperation("Settings.Reset", out busy)) { completed(false, busy); return; }
        var token = OperationToken;
        Action finish = () =>
        {
            if (token != OperationToken) return;
            LifecycleStopped();
            EndOperation(token);
            string message;
            if (!ResetConfiguration(out message)) { completed(false, message); return; }
            OctopusSampleLanguage.Clear();
            OctopusSampleHostIntegration.Clear();
            OctopusSampleFeatureToggles.Reset();
            OctopusSampleUnifiedProfile.Reset();
            OctopusSampleNativeTheme.Configure(true, 0);
            PlayerPrefs.DeleteKey("OctopusSample.Theme");
            PlayerPrefs.Save();
            _configurationRequiresSwitch = false;
            _configurationMayChangeSession = false;
            StartupFailureReason = null;
            completed(true, "Local demo setup cleared. Nothing was deleted on the server.");
        };
        Action<string> fail = error =>
        {
            if (token != OperationToken) return;
            EndOperation(token);
            completed(false, "Could not stop the SDK. Saved configuration was kept; try again.");
        };
        try
        {
            if (OctopusSampleState.IsInitialized || _configurationRequiresSwitch)
                _sdk.Stop(finish, fail);
            else finish();
        }
        catch (Exception) { fail(null); }
    }

    public static bool ResetConfiguration(out string message)
    {
        if (OperationInFlight != null)
        {
            message = "Wait for the current SDK operation before resetting configuration.";
            return false;
        }
        try
        {
            ClearSavedConfiguration();
            PlayerPrefs.Save();
            _operationToken++;
            message = "Configuration cleared. The running session is unchanged.";
            OctopusSampleQaLaunch.Log("config=reset");
            return true;
        }
        catch (Exception) { message = "Configuration could not be cleared. Try again."; return false; }
    }

    public static void ApplyConfiguration(int selection, Action<bool, string> completed)
    {
        var draft = ConfigurationDraft();
        draft.Community = selection;
        ApplyConfiguration(draft, completed);
    }

    public static string ConfigurationKey(OctopusSampleConfigurationDraft draft)
    {
        if (draft.Community == 5) return draft.CustomApiKey;
        var profile = _sdk.ConfigurationProfile(draft.Community);
        if (OctopusSampleState.IsInitialized && draft.Community == _liveSelection && _activeProfile != null)
            profile = _activeProfile;
        return profile == null ? null : profile.apiKey;
    }

    public static void ApplyConfiguration(OctopusSampleConfigurationDraft edited, Action<bool, string> completed)
    {
        // Freeze at submit: even callers outside the UI cannot alter an in-flight request.
        var draft = edited.Copy();
        if (_configurationNativePending)
        { completed(false, "The previous native operation is still pending. Retry after it completes, or restart the sample."); return; }
        var current = ConfigurationDraft();
        if (draft.Community < 1 || draft.Community > 5)
        { completed(false, "Choose a configuration profile."); return; }
        if (draft.CustomServer && !OctopusSampleConfigurationDraft.ValidCustomHost(draft.CustomHost))
        { completed(false, "Enter a host without a scheme, port or path, or choose Demo."); return; }
        if (draft.Appearance.HasValue && (draft.Appearance < 0 || draft.Appearance > 2))
        { completed(false, "Choose System, Light or Dark."); return; }
        var key = ConfigurationKey(draft);
        if (string.IsNullOrWhiteSpace(key))
        { completed(false, "No API key configured for this preset. Inject a demo key in the local build, or enter a custom key."); return; }
        var source = _sdk.ConfigurationProfile(draft.Community == 5 ? 1 : draft.Community);
        if (OctopusSampleState.IsInitialized && draft.Community == _liveSelection && _activeProfile != null) source = _activeProfile;
        if (source == null) source = new OctopusExampleConfig.ExampleProfile();
        var profile = CopyConfigurationProfile(source);
        profile.apiKey = key.Trim();
        profile.apiServerHost = draft.ResolveHost(_sdk.DemoServerHost);
        var userChanged = draft.UserId != null && draft.UserId != current.UserId;
        var identityChanged = draft.UserId != null && draft.UserId != source.userId;
        var nativeChange = _configurationRequiresSwitch || !OctopusSampleState.IsInitialized || !IsInPilotMode || _activeProfile == null ||
            _activeProfile.apiKey != profile.apiKey || RunningServerHost != profile.apiServerHost ||
            current.Community != draft.Community;
        var reportedUser = _sdk.CurrentProfile == null ? null : _sdk.CurrentProfile.ClientUserId;
        var connectUser = draft.UserId != null && (userChanged || (draft.UserSelected && !current.UserSelected) ||
            (nativeChange && (draft.UserSelected || !string.IsNullOrEmpty(reportedUser))));
        if (connectUser && (identityChanged && string.IsNullOrWhiteSpace(source.signingSecret)))
        { completed(false, "Changing the demo user needs the local demo SSO signing secret. The current session is unchanged."); return; }
        string credentials;
        if (connectUser && !OctopusSampleTokenProvider.CanConnect(profile, out credentials))
        { completed(false, credentials); return; }
        if (connectUser) draft.UserSelected = true;
        if (draft.UserId != null) profile.userId = draft.UserId;
        if (identityChanged)
        {
            profile.authToken = null;
            profile.nickname = null;
            profile.bio = null;
            profile.picture = null;
            profile.entitlements = new string[0];
        }
        string busy;
        if (!TryBeginOperation("Configuration.Apply", out busy)) { completed(false, busy); return; }
        var token = OperationToken;
        var finished = false;
        var changed = nativeChange || userChanged || connectUser || current.NavyTheme != draft.NavyTheme ||
            current.Appearance != draft.Appearance || current.CustomServer != draft.CustomServer;
        _configurationMayChangeSession = false;
        Action<bool, string> finish = (ok, message) =>
        {
            if (finished || token != OperationToken) return;
            finished = true;
            _configurationMayChangeSession = false;
            EndOperation(token);
            completed(ok, message);
        };
        Action fail = () =>
        {
            if (finished || token != OperationToken) return;
            if (_configurationMayChangeSession)
            {
                _configurationRequiresSwitch = true;
                LifecycleStopped();
            }
            finish(false, "Configuration could not be applied. Check the local demo setup and retry.");
        };
        Action commit = () =>
        {
            if (finished || token != OperationToken) return;
            try
            {
                if (changed)
                {
                    SetActiveProfile(profile);
                    _runningServerHost = profile.apiServerHost;
                    _liveSelection = draft.Community;
                    _bridgeTokenProvider = new OctopusSampleTokenProvider(profile);
                    _liveConfiguration = draft.Copy();
                    OctopusSampleFeatureToggles.ConfigurationApplied(draft.ForceLogin);
                    OctopusSampleNativeTheme.Configure(draft.NavyTheme, draft.Appearance);
                    OctopusSampleNativeTheme.Apply(_sdk);
                    _configurationRequiresSwitch = false;
                }
                if (!SaveConfiguration(draft))
                { finish(false, "Configuration applied but could not be saved. Try Apply again."); return; }
                StartupFailureReason = null;
                _startupFailed = false;
                finish(true, changed ? AppliedMessage : UnchangedMessage);
            }
            catch (Exception) { fail(); }
        };
        Action connectOrCommit = () =>
        {
            if (finished || token != OperationToken) return;
            // A changed SSO id is applied, never merely relabelled. No user is invented on success.
            if (!connectUser) { commit(); return; }
            _configurationMayChangeSession = true;
            _configurationNativePending = true;
            _configurationNativeToken = token;
            ConnectConfigurationUser(profile, token, commit, fail);
        };
        try
        {
            if (!OctopusSampleState.IsInitialized && !_configurationRequiresSwitch)
            {
                _configurationMayChangeSession = true;
                SetActiveProfile(profile);
                string reason;
                if (EnsureInitialized(ConnectionMode.SSO(draft.ManagedFields), PilotModeLabel, out reason) == null)
                { fail(); return; }
                _configurationRequiresSwitch = true;
                connectOrCommit();
            }
            else if (nativeChange)
            {
                _configurationMayChangeSession = true;
                _configurationRequiresSwitch = true;
                ClearCommunityObservations();
                OctopusSampleLog.Current.LogApiCall("OctopusSDK.SwitchCommunity", "mode=SSO, server=" + (draft.CustomServer ? "Custom" : "Demo"));
                _configurationNativePending = true;
                _configurationNativeToken = token;
                _sdk.SwitchCommunity(profile.apiKey, ConnectionMode.SSO(draft.ManagedFields), profile.apiServerHost, () =>
                {
                    ReleaseConfigurationNativeRequest(token);
                    if (finished || token != OperationToken) return;
                    try { CommunitySwitched(profile, draft.Community); connectOrCommit(); }
                    catch (Exception) { fail(); }
                }, error => { ReleaseConfigurationNativeRequest(token); fail(); });
            }
            else connectOrCommit();
        }
        catch (Exception) { ReleaseConfigurationNativeRequest(token); fail(); }
    }

    private static async void ConnectConfigurationUser(OctopusExampleConfig.ExampleProfile profile, int token, Action done, Action fail)
    {
        try
        {
            var provider = new OctopusSampleTokenProvider(profile);
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.ConnectUser", "configuration user");
            await _sdk.ConnectUser(profile.userId, profile.nickname, profile.bio, profile.picture,
                () => Task.FromResult(provider.GetToken(profile.userId, profile.entitlements)));
            ReleaseConfigurationNativeRequest(token);
            if (token == OperationToken) done();
        }
        catch (Exception) { ReleaseConfigurationNativeRequest(token); if (token == OperationToken) fail(); }
    }

    private static OctopusExampleConfig.ExampleProfile CopyConfigurationProfile(OctopusExampleConfig.ExampleProfile source)
    {
        return new OctopusExampleConfig.ExampleProfile
        {
            apiKey = source.apiKey, authToken = source.authToken, signingSecret = source.signingSecret,
            apiServerHost = source.apiServerHost, userId = source.userId, nickname = source.nickname,
            bio = source.bio, picture = source.picture, entitlements = (string[])(source.entitlements ?? new string[0]).Clone()
        };
    }

    private static void ReleaseConfigurationNativeRequest(int token)
    {
        if (token == _configurationNativeToken) _configurationNativePending = false;
    }

    public static void TimeoutConfigurationApply(int token)
    {
        if (token != OperationToken || _operationInFlight != "Configuration.Apply") return;
        _operationToken++;
        _operationInFlight = null;
        if (_configurationMayChangeSession)
        {
            _configurationRequiresSwitch = true;
            LifecycleStopped();
        }
        _configurationMayChangeSession = false;
    }
}
