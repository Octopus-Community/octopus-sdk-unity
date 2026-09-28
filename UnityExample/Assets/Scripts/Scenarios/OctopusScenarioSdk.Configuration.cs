using System;
using UnityEngine;

public static partial class OctopusScenarioSdk
{
    // Same key and values as startup replay: no credentials are stored in PlayerPrefs.
    public static int SavedConfiguration
    {
        get
        {
            if (_startupPreferenceKey == null) return 0;
            var value = PlayerPrefs.GetInt(_startupPreferenceKey, 0);
            return value == 1 || value == 2 ? value : 0;
        }
    }

    public static bool NeedsConfiguration
    {
        get { return SavedConfiguration == 0 || !OctopusSampleState.IsInitialized; }
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
            if (_startupPreferenceKey != null)
            {
                PlayerPrefs.DeleteKey(_startupPreferenceKey);
                PlayerPrefs.Save();
            }
            // Invalidate callbacks from an operation whose slot has already timed out.
            _operationToken++;
            // This is only a local preference reset. Keep the real SDK lifecycle and every
            // observation intact; applying the next choice uses SwitchCommunity if needed.
            message = "Configuration cleared. Choose a profile to continue.";
            OctopusSampleQaLaunch.Log("config=reset");
            return true;
        }
        catch (Exception)
        {
            message = "Configuration could not be cleared. Try again.";
            return false;
        }
    }

    public static void ApplyConfiguration(int selection, Action<bool, string> completed)
    {
        if (selection != 1 && selection != 2)
        {
            completed(false, "Choose a configuration profile.");
            return;
        }
        string busy;
        if (!TryBeginOperation("Configuration.Apply", out busy))
        {
            completed(false, busy);
            return;
        }
        var token = OperationToken;
        var finished = false;
        Action<bool, string> finish = (ok, message) =>
        {
            // A late/duplicate native callback must not overwrite a newer selection.
            if (finished || token != OperationToken) return;
            finished = true;
            EndOperation(token);
            completed(ok, message);
        };
        var forceLogin = selection == 2;
        var wasForceLogin = OctopusSampleFeatureToggles.ForceLogin;
        var switching = false;
        try
        {
            if (!OctopusSampleState.IsInitialized)
            {
                OctopusSampleFeatureToggles.SetForceLogin(forceLogin);
                string reason;
                if (EnsurePilotInitialized(out reason) == null)
                {
                    OctopusSampleFeatureToggles.SetForceLogin(wasForceLogin);
                    finish(false, reason);
                    return;
                }
            }
            else
            {
                var target = forceLogin == wasForceLogin ? _sdk.Profile : _sdk.AlternateProfile;
                if (target == null || string.IsNullOrWhiteSpace(target.apiKey))
                {
                    finish(false, "This profile is unavailable in this build. Configure the local sample asset and rebuild.");
                    return;
                }
                string reason;
                var active = UsableProfile(out reason);
                if (!IsInPilotMode || active == null || active.apiKey != target.apiKey)
                {
                    switching = true;
                    Action failed = () =>
                    {
                        if (finished || token != OperationToken) return;
                        // The bridge can fail after tearing down the previous community.
                        LifecycleStopped();
                        finish(false, "Configuration change failed. Choose a profile to retry.");
                    };
                    ClearCommunityObservations();
                    var switchHost = ServerHostFor(target);
                    OctopusSampleLog.Current.LogApiCall("OctopusSDK.SwitchCommunity", "mode=SSO, host=" + switchHost);
                    _sdk.SwitchCommunity(target.apiKey, PilotMode(), switchHost, () =>
                    {
                        if (finished || token != OperationToken) return;
                        try
                        {
                            OctopusSampleFeatureToggles.ConfigurationApplied(forceLogin);
                            CommunitySwitched(target, selection);
                            var saved = RememberInitializedConfig();
                            finish(saved, saved ? null : "Configuration applied but could not be saved. Try Apply again.");
                        }
                        catch (Exception) { failed(); }
                    }, error => failed());
                    return;
                }
                // Profiles can share a community but supply different demo SSO identities.
                _activeProfile = target;
                _liveSelection = selection;
                _bridgeTokenProvider = new OctopusSampleTokenProvider(target);
                OctopusSampleFeatureToggles.ConfigurationApplied(forceLogin);
            }
            var remembered = RememberInitializedConfig();
            finish(remembered, remembered ? null : "Configuration applied but could not be saved. Try Apply again.");
        }
        catch (Exception)
        {
            if (switching) LifecycleStopped();
            if (!OctopusSampleState.IsInitialized)
                OctopusSampleFeatureToggles.SetForceLogin(wasForceLogin);
            finish(false, "Configuration could not be applied. Check the local sample setup and try again.");
        }
    }
}
