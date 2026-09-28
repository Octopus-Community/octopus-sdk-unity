using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class OctopusSampleAccountView : OctopusSampleSettingsDetailView
{
    public const string ScreenId = "account-screen";
    public const string BackId = "account-back";
    public const string StateId = "account-state";
    public const string ModeId = "account-mode";
    public const string ConnectId = "account-connect-button";
    public const string DisconnectId = "account-disconnect-button";
    public const string ConfigId = "account-config-button";
    public const string ResultId = "account-result";
    public string Result { get; private set; }
    public bool IsRunning { get; private set; }
    private IOctopusScenarioSdk _sdk;

    public static OctopusSampleAccountView Open()
    {
        var existing = FindAnyObjectByType<OctopusSampleAccountView>();
        if (existing != null) return existing;
        var view = new GameObject("OctopusSampleAccountView").AddComponent<OctopusSampleAccountView>();
        view._sdk = OctopusScenarioSdk.Current;
        view._sdk.ProfileChanged += view.ProfileChanged;
        OctopusSampleState.Changed += view.Refresh;
        view.InitializeView();
        OctopusSampleQaLaunch.Log("screen=account");
        return view;
    }

    public static string ConnectionText()
    {
        if (!OctopusSampleState.IsInitialized) return "SDK not initialized";
        var profile = OctopusScenarioSdk.Current.CurrentProfile;
        if (profile == null) return "No connected profile reported";
        if (IsGuest(profile)) return "Guest / no SSO identity reported";
        return "SSO profile connected · " + profile.ClientUserId;
    }

    private static bool IsGuest(OctopusProfile profile)
    {
        return profile == null || string.IsNullOrEmpty(profile.ClientUserId);
    }

    private void ProfileChanged(OctopusProfile profile) { Refresh(); }

    protected override void Unsubscribe()
    {
        base.Unsubscribe();
        if (_sdk != null) _sdk.ProfileChanged -= ProfileChanged;
        OctopusSampleState.Changed -= Refresh;
    }

    public void Login() { RunConnection(true); }
    public void Logout() { RunConnection(false); }

    private async void RunConnection(bool connect)
    {
        if (IsRunning) return;
        if (OctopusScenarioSdk.NeedsConfiguration || !OctopusScenarioSdk.IsInPilotMode)
        {
            Result = "Choose an SSO configuration before signing in or out.";
            Refresh();
            return;
        }
        string reason;
        var profile = OctopusScenarioSdk.UsableProfile(out reason);
        if (connect && (profile == null || !OctopusSampleTokenProvider.CanConnect(profile, out reason)))
        {
            Result = reason;
            Refresh();
            return;
        }
        string busy;
        if (!OctopusScenarioSdk.TryBeginOperation(connect ? "ConnectUser" : "DisconnectUser", out busy))
        {
            Result = busy;
            Refresh();
            return;
        }
        var token = OctopusScenarioSdk.OperationToken;
        IsRunning = true;
        Result = connect ? "Signing in…" : "Signing out…";
        Refresh();
        try
        {
            Task call;
            if (connect)
            {
                var provider = new OctopusSampleTokenProvider(profile);
                OctopusSampleLog.Current.LogApiCall("OctopusSDK.ConnectUser");
                call = _sdk.ConnectUser(profile.userId, profile.nickname, profile.bio, profile.picture,
                    () => Task.FromResult(provider.GetToken(profile.userId, new string[0])));
            }
            else
            {
                OctopusSampleLog.Current.LogApiCall("OctopusSDK.DisconnectUser");
                call = _sdk.DisconnectUser();
            }
            if (await Task.WhenAny(call, Task.Delay(OctopusScenarioSdk.OperationTimeout)) != call)
            {
                // Observe a possible late fault without updating this screen or a newer session.
                ObserveLateCall(call);
                Result = "No response yet. Check the reported profile before trying again.";
                return;
            }
            await call;
            if (token != OctopusScenarioSdk.OperationToken) return;
            Result = connect ? "Sign-in call completed. The profile above shows the SDK's reported state."
                : "Sign-out call completed. The profile above shows the SDK's reported state.";
            OctopusSampleState.ReportSession(connect ? OctopusSampleState.Session.ConnectCompleted
                : OctopusSampleState.Session.Disconnected, Result);
        }
        catch (Exception)
        {
            if (token != OctopusScenarioSdk.OperationToken) return;
            Result = "The account operation failed. Check the sample configuration and try again.";
            OctopusSampleState.ReportSession(OctopusSampleState.Session.Failed, Result);
        }
        finally
        {
            OctopusScenarioSdk.EndOperation(token);
            if (this != null) { IsRunning = false; Refresh(); }
        }
    }

    private static async void ObserveLateCall(Task call)
    {
        try { await call; } catch (Exception) { }
    }

    protected override void Build()
    {
        var content = Content(ScreenId, "Account", BackId, true);
        var card = SampleUi.Card("account-identity-card", content);
        SampleUi.FlexibleLabel(card, ConnectionText(), SampleUi.TextTitle, SampleUi.TitleColor).gameObject.name = StateId;
        SampleUi.FlexibleLabel(card, "Authentication · " + (OctopusSampleState.ModeLabel ?? "SSO (not initialized)"),
            SampleUi.TextBody, SampleUi.Muted).gameObject.name = ModeId;
        SampleUi.FlexibleLabel(card, "Sign in with this build's configured demo SSO identity.", SampleUi.TextBody, SampleUi.Muted);
        var ready = !OctopusScenarioSdk.NeedsConfiguration && OctopusScenarioSdk.IsInPilotMode && !IsRunning;
        SampleUi.Button(ConnectId, card, "Sign in (SSO)", SampleUiButtonVariant.Primary, Login)
            .GetComponent<Button>().interactable = ready;
        if (!IsGuest(OctopusScenarioSdk.Current.CurrentProfile))
        {
            SampleUi.Button(DisconnectId, card, "Sign out", SampleUiButtonVariant.Destructive, Logout)
                .GetComponent<Button>().interactable = ready;
        }
        SampleUi.Button(ConfigId, content, "Change configuration", SampleUiButtonVariant.Secondary, () =>
        {
            Close();
            // Back from Configuration returns here, not to the tab under both screens.
            OctopusSampleConfigView.Open(() => OctopusSampleAccountView.Open());
        }).GetComponent<Button>().interactable = !IsRunning;
        if (!string.IsNullOrEmpty(Result))
            SampleUi.FlexibleLabel(content, Result, SampleUi.TextBody, SampleUi.Muted).gameObject.name = ResultId;
    }
}
