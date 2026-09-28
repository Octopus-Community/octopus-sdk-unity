using UnityEngine;
using UnityEngine.UI;

public class OctopusSampleConfigView : OctopusSampleSettingsDetailView
{
    public const string ScreenId = "config-screen";
    public const string BackId = "config-back";
    public const string ProfilesId = "config-profiles-card";
    public const string DefaultId = "config-default-profile";
    public const string ForcedLoginId = "config-forced-login-profile";
    public const string StartId = "config-start-button";
    public const string ResultId = "config-result";
    public const string AccountLinkId = "config-account-link";

    public int Selection { get; private set; }
    public string Result { get; private set; }
    public bool IsApplying { get; private set; }
    private bool _revisit;
    private System.DateTime _startedAt;
    private System.Action _returnTo;

    public static OctopusSampleConfigView Open()
    {
        return Open(null);
    }

    /// <summary>
    /// Opens the screen, or returns the one already open. <paramref name="returnTo"/> reopens the
    /// screen this one was reached from once it closes — by Back or by a successful Apply — so a
    /// reader who came from Account lands on Account again, where the next step (signing in again
    /// after a community switch) is. Null closes onto whatever is underneath: the tab.
    /// </summary>
    public static OctopusSampleConfigView Open(System.Action returnTo)
    {
        var existing = FindAnyObjectByType<OctopusSampleConfigView>();
        if (existing != null) return existing;
        var view = new GameObject("OctopusSampleConfigView").AddComponent<OctopusSampleConfigView>();
        view._returnTo = returnTo;
        view._revisit = !OctopusScenarioSdk.NeedsConfiguration;
        view.Selection = InitialSelection();
        // A failed startup replay opens this screen over Home: say why, instead of a bare picker.
        if (!OctopusSampleState.IsInitialized) view.Result = OctopusScenarioSdk.StartupFailureReason;
        view.InitializeView();
        OctopusSampleQaLaunch.Log("screen=config");
        OctopusSampleQaLaunch.ConfigShown(view.Apply);
        return view;
    }

    /// <summary>
    /// The profile highlighted when the screen opens. While the SDK runs, it is the live profile —
    /// not the saved one, which a reset has just cleared: highlighting Default over a forced-login
    /// session would make Apply switch communities while claiming to keep the choice (#349). The
    /// live profile is the one the SDK was started or switched onto
    /// (<see cref="OctopusScenarioSdk.LiveSelection"/>), not the Force login switch, which the
    /// Lifecycle community switch leaves alone (#387). Before that, the saved choice, else Default.
    /// </summary>
    public static int InitialSelection()
    {
        if (OctopusSampleState.IsInitialized)
        {
            var live = OctopusScenarioSdk.LiveSelection;
            if (live != 0) return live;
            return OctopusSampleFeatureToggles.ForceLogin ? 2 : 1;
        }
        var saved = OctopusScenarioSdk.SavedConfiguration;
        return saved == 0 ? 1 : saved;
    }

    public void SelectProfile(int selection)
    {
        if (IsApplying || (selection != 1 && selection != 2)) return;
        Selection = selection;
        Result = null;
        Refresh();
    }

    public void Apply()
    {
        if (IsApplying) return;
        IsApplying = true;
        _startedAt = OctopusScenarioSdk.UtcNow();
        Result = "Applying configuration…";
        Refresh();
        OctopusScenarioSdk.ApplyConfiguration(Selection, (success, message) =>
        {
            if (this == null) return;
            IsApplying = false;
            Result = message;
            if (success)
            {
                OctopusSampleQaLaunch.ConfigStarted();
                Leave();
            }
            else Refresh();
        });
    }

    protected override void LateUpdate()
    {
        if (IsApplying && OctopusScenarioSdk.UtcNow() - _startedAt >= OctopusScenarioSdk.OperationTimeout)
        {
            IsApplying = false;
            Result = "No response yet. You can retry applying the configuration.";
            Refresh();
        }
        base.LateUpdate();
    }

    /// <summary>Whether Back may leave: never mid-apply, never before a profile is in place.</summary>
    public bool CanLeave
    {
        get { return !IsApplying && !OctopusScenarioSdk.NeedsConfiguration; }
    }

    public override void Close()
    {
        if (CanLeave) Leave();
    }

    /// <summary>
    /// A tab switch closes onto the tab, never back to the screen this one was reached from:
    /// reopening Account there would cover the tab just picked. Refused on the same terms as Back.
    /// </summary>
    protected override void Dismiss()
    {
        if (!CanLeave) return;
        _returnTo = null;
        base.Close();
    }

    /// <summary>Closes onto Account, whatever this screen was opened from. Wired to <see cref="AccountLinkId"/>.</summary>
    public void OpenAccount()
    {
        if (!CanLeave) return;
        _returnTo = null;
        base.Close();
        OctopusSampleAccountView.Open();
    }

    private void Leave()
    {
        var returnTo = _returnTo;
        _returnTo = null;
        base.Close();
        if (returnTo != null) returnTo();
    }

    protected override void Build()
    {
        var content = Content(ScreenId, "Configuration", BackId,
            _revisit && !IsApplying && !OctopusScenarioSdk.NeedsConfiguration);
        var card = SampleUi.Card(ProfilesId, content);
        SampleUi.FlexibleLabel(card, "Choose a sample profile", SampleUi.TextTitle, SampleUi.TitleColor);
        SampleUi.FlexibleLabel(card,
            "Use one of this build's configured profiles. Authentication uses SSO; credentials stay in the local build configuration.",
            SampleUi.TextBody, SampleUi.Muted);
        Choice(card, DefaultId, 1, "Default", OctopusSampleFeatureToggles.ForceLoginEffectOff);
        Choice(card, ForcedLoginId, 2, "Forced login", OctopusSampleFeatureToggles.ForceLoginEffectOn);
        SampleUi.FlexibleLabel(card, "Changing communities disconnects the current session. Sign in again from Account.",
            SampleUi.TextCaption, SampleUi.Muted);
        // Only once the screen can be left: before the first profile is in place there is no
        // session to sign back into, and Back is withheld for the same reason.
        if (_revisit && CanLeave)
            SampleUi.Button(AccountLinkId, card, "Open Account", SampleUiButtonVariant.Tertiary, OpenAccount);
        if (!string.IsNullOrEmpty(Result))
            SampleUi.FlexibleLabel(content, Result, SampleUi.TextBody, SampleUi.Attention).gameObject.name = ResultId;
        var button = SampleUi.Button(StartId, content, IsApplying ? "Applying…" : OctopusSampleState.IsInitialized ? "Apply" : "Start SDK",
            SampleUiButtonVariant.Primary, Apply);
        button.GetComponent<Button>().interactable = !IsApplying;
    }

    private void Choice(RectTransform card, string id, int selection, string title, string description)
    {
        var button = SampleUi.Button(id, card, (Selection == selection ? "Selected · " : "") + title,
            Selection == selection ? SampleUiButtonVariant.Secondary : SampleUiButtonVariant.Tertiary,
            () => SelectProfile(selection));
        button.GetComponent<Button>().interactable = !IsApplying;
        SampleUi.FlexibleLabel(card, description, SampleUi.TextCaption, SampleUi.Muted);
    }
}
